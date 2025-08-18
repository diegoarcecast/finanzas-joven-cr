using Asp.Versioning;
using Asp.Versioning.Builder;
using auth.api.Contracts;
using auth.api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using System.Text;

// Alias para usar SIEMPRE los contratos del proyecto Contracts
using C = auth.api.Contracts;

var builder = WebApplication.CreateBuilder(args);

// ------------ Swagger + Health + Versioning ------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "auth.api", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Introduce SOLO el token (sin el prefijo 'Bearer ').",
        Reference = new OpenApiReference
        {
            Id = "Bearer",
            Type = ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
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

// ----------------- EF Core + Identity ------------------
builder.Services.AddDbContext<AppIdentityDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("AuthDb")));

builder.Services
    .AddIdentityCore<AppUser>(opt =>
    {
        opt.User.RequireUniqueEmail = true;
        opt.Password.RequiredLength = 6;
        opt.Password.RequireDigit = false;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole<Guid>>() // <- usamos GUID
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddSignInManager();

// ------------------- JWT + Auth ------------------------
builder.Services.Configure<C.JwtOptions>(opt =>
{
    builder.Configuration.GetSection("Jwt").Bind(opt);

    // valor por defecto si no viene en config
    if (opt.ExpirationMinutes <= 0) opt.ExpirationMinutes = 60;

    opt.Key = builder.Configuration["Jwt:Key"]
              ?? throw new InvalidOperationException("Jwt:Key missing. Usa user-secrets.");
});

var keyBytes = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// generador de tokens
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

var app = builder.Build();

// ------------------ Middleware -------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ------------------ Salud ------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

// --------- VersionSet + grupos versionados -------------
var vset = app.NewApiVersionSet()
              .HasApiVersion(new ApiVersion(1, 0))
              .ReportApiVersions()
              .Build();

var api = app.MapGroup("/api/v{version:apiVersion}")
             .WithApiVersionSet(vset);

api.MapGet("/ping", () => Results.Ok(new { ok = true }))
   .MapToApiVersion(new ApiVersion(1, 0))
   .WithTags("Ping");

var authV1 = api.MapGroup("/auth")
                .MapToApiVersion(new ApiVersion(1, 0))
                .WithTags("Auth");

// -------------------- /register ------------------------
authV1.MapPost("/register",
    async (C.RegisterRequest req, UserManager<AppUser> users, IJwtTokenGenerator jwt) =>
    {
        var exists = await users.FindByEmailAsync(req.Email);
        if (exists is not null) return Results.Conflict(new { error = "email_already_registered" });

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = req.Email,
            UserName = req.Email,
            FirstName = req.FirstName ?? string.Empty,
            LastName = req.LastName ?? string.Empty,
            EmailConfirmed = true
        };

        var result = await users.CreateAsync(user, req.Password);
        if (!result.Succeeded) return Results.BadRequest(result.Errors);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(ClaimTypes.Name, user.Email!)
        };

        var (token, exp) = jwt.GenerateToken(claims);
        return Results.Ok(new C.AuthResponse(token, "Bearer", exp));
    });

// ---------------------- /login -------------------------
authV1.MapPost("/login",
    async (C.LoginRequest req, SignInManager<AppUser> signIn, UserManager<AppUser> users, IJwtTokenGenerator jwt) =>
    {
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null) return Results.Unauthorized();

        var check = await signIn.CheckPasswordSignInAsync(user, req.Password, false);
        if (!check.Succeeded) return Results.Unauthorized();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(ClaimTypes.Name, user.Email!)
        };

        var (token, exp) = jwt.GenerateToken(claims);
        return Results.Ok(new C.AuthResponse(token, "Bearer", exp));
    });

// ------------------------ /me --------------------------
authV1.MapGet("/me",
    async (ClaimsPrincipal me, UserManager<AppUser> users) =>
    {
        var id = me.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? me.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null) return Results.Unauthorized();

        var user = await users.FindByIdAsync(id);
        return user is null
            ? Results.NotFound()
            : Results.Ok(new { user.Id, user.Email, user.FirstName, user.LastName });
    })
    .RequireAuthorization();

app.Run();
