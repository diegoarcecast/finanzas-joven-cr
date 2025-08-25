using Asp.Versioning;
using Asp.Versioning.Builder;
using finanzas.api.Contracts;
using finanzas.api.Domain;
using finanzas.api.Infra;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Linq;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ----------------------- Swagger + Authorize -----------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "finanzas.api", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Introduce SOLO el JWT (sin 'Bearer ').",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
});

builder.Services.AddHealthChecks();

builder.Services
    .AddApiVersioning(opt =>
    {
        opt.DefaultApiVersion = new ApiVersion(1, 0);
        opt.AssumeDefaultVersionWhenUnspecified = true;
        opt.ReportApiVersions = true;
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

// ----------------------- EF Core -----------------------
builder.Services.AddDbContext<FinanzasDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("FinanzasDb")));

// ----------------------- JWT (coincidir con auth.api) -----------------------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key missing. Configura user-secrets en finanzas.api");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer missing. Debe ser igual al de auth.api");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience missing. Debe ser igual al de auth.api");

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ----------------------- App -----------------------
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

// ----------------------- Versionado -----------------------
var vset = app.NewApiVersionSet()
              .HasApiVersion(new ApiVersion(1, 0))
              .ReportApiVersions()
              .Build();

var api = app.MapGroup("/api/v{version:apiVersion}")
             .WithApiVersionSet(vset);

api.MapGet("/ping", () => Results.Ok(new { ok = true }))
   .MapToApiVersion(new ApiVersion(1, 0))
   .WithTags("Ping");

// ----------------------- Helpers -----------------------
Guid RequireUserId(ClaimsPrincipal me)
{
    var s = me.FindFirstValue(JwtRegisteredClaimNames.Sub)
           ?? me.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(s)) throw new UnauthorizedAccessException("sub missing");
    return Guid.Parse(s);
}

// ----------------------- Endpoints V1 -----------------------
var v1 = api.MapGroup("/").MapToApiVersion(new ApiVersion(1, 0));

// Categories
v1.MapPost("categories", async (
        CreateCategoryRequest req,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);
    var cat = new Category { UserId = userId, Name = req.Name, Color = req.Color };
    db.Categories.Add(cat);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/categories/{cat.Id}", new CategoryResponse(cat.Id, cat.Name, cat.Color));
})
.RequireAuthorization()
.WithTags("Categories");

v1.MapGet("categories", async (ClaimsPrincipal me, FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);
    var list = await db.Categories
        .Where(c => c.UserId == userId)
        .Select(c => new CategoryResponse(c.Id, c.Name, c.Color))
        .ToListAsync();
    return Results.Ok(list);
})
.RequireAuthorization()
.WithTags("Categories");

// PUT /api/v1/categories/{id}
v1.MapPut("categories/{id:guid}", async (
        Guid id,
        UpdateCategoryRequest req,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);

    var cat = await db.Categories
        .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

    if (cat is null) return Results.NotFound();

    // Nombre único por usuario (excluyendo la misma categoría)
    var name = req.Name.Trim();
    var exists = await db.Categories.AnyAsync(c =>
        c.UserId == userId && c.Id != id && c.Name == name);

    if (exists) return Results.Conflict(new { error = "category_name_already_exists" });

    cat.Name = name;
    cat.Color = req.Color?.Trim() ?? cat.Color;

    await db.SaveChangesAsync();

    return Results.Ok(new CategoryResponse(cat.Id, cat.Name, cat.Color));
})
.RequireAuthorization()
.WithTags("Categories");

// DELETE /api/v1/categories/{id}
v1.MapDelete("categories/{id:guid}", async (
        Guid id,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);

    // No permitir borrar si tiene movimientos asociados del mismo usuario
    var inUse = await db.Movements.AnyAsync(m => m.UserId == userId && m.CategoryId == id);
    if (inUse) return Results.BadRequest(new { error = "category_in_use" });

    var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
    if (cat is null) return Results.NotFound();

    db.Categories.Remove(cat);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization()
.WithTags("Categories");


// Movements
v1.MapPost("movements", async (
        CreateMovementRequest req,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);

    // La categoría debe ser del usuario
    var owns = await db.Categories.AnyAsync(c => c.Id == req.CategoryId && c.UserId == userId);
    if (!owns) return Results.BadRequest(new { error = "invalid_category" });

    var m = new Movement
    {
        UserId = userId,
        CategoryId = req.CategoryId,
        Date = req.Date,
        Amount = req.Amount,
        Type = req.Type,
        Note = req.Note
    };

    db.Movements.Add(m);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/movements/{m.Id}",
        new MovementResponse(m.Id, m.CategoryId, m.Date, m.Amount, m.Type, m.Note));
})
.RequireAuthorization()
.WithTags("Movements");

v1.MapGet("movements", async (
        DateTime? from,
        DateTime? to,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);
    var q = db.Movements.AsQueryable().Where(m => m.UserId == userId);

    if (from is not null) q = q.Where(m => m.Date >= from);
    if (to is not null) q = q.Where(m => m.Date <= to);

    var list = await q.OrderByDescending(m => m.Date)
        .Select(m => new MovementResponse(m.Id, m.CategoryId, m.Date, m.Amount, m.Type, m.Note))
        .ToListAsync();

    return Results.Ok(list);
})
.RequireAuthorization()
.WithTags("Movements");

// PUT /api/v1/movements/{id}
v1.MapPut("movements/{id:guid}", async (
        Guid id,
        UpdateMovementRequest req,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);

    // El movimiento debe ser del usuario
    var mov = await db.Movements
        .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

    if (mov is null) return Results.NotFound();

    // La categoría nueva también debe ser del usuario
    var ownsCategory = await db.Categories
        .AnyAsync(c => c.Id == req.CategoryId && c.UserId == userId);

    if (!ownsCategory) return Results.BadRequest(new { error = "invalid_category" });

    mov.CategoryId = req.CategoryId;
    mov.Date = req.Date;
    mov.Amount = req.Amount;
    mov.Type = req.Type;
    mov.Note = req.Note;

    await db.SaveChangesAsync();

    return Results.Ok(new MovementResponse(
        mov.Id, mov.CategoryId, mov.Date, mov.Amount, mov.Type, mov.Note));
})
.RequireAuthorization()
.WithTags("Movements");

// DELETE /api/v1/movements/{id}
v1.MapDelete("movements/{id:guid}", async (
        Guid id,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);

    var mov = await db.Movements
        .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

    if (mov is null) return Results.NotFound();

    db.Movements.Remove(mov);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization()
.WithTags("Movements");

// GET /api/v1/movements/{id}
v1.MapGet("movements/{id:guid}", async (
        Guid id,
        ClaimsPrincipal me,
        FinanzasDbContext db) =>
{
    var userId = RequireUserId(me);

    var mov = await db.Movements
        .Where(m => m.Id == id && m.UserId == userId)
        .Select(m => new MovementResponse(m.Id, m.CategoryId, m.Date, m.Amount, m.Type, m.Note))
        .FirstOrDefaultAsync();

    return mov is null ? Results.NotFound() : Results.Ok(mov);
})
.RequireAuthorization()
.WithTags("Movements");



app.Run();
