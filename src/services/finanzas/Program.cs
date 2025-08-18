using Asp.Versioning;
using Asp.Versioning.Builder;

var builder = WebApplication.CreateBuilder(args);

// Swagger + Health + Versioning
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
}).AddApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

// ----- Version set + grupos -----
var vset = app.NewApiVersionSet()
              .HasApiVersion(new ApiVersion(1, 0))
              .ReportApiVersions()
              .Build();

var api = app.MapGroup("/api/v{version:apiVersion}")
             .WithApiVersionSet(vset);

// GET /api/v1/ping
api.MapGet("/ping", () => Results.Ok(new { ok = true }))
   .MapToApiVersion(new ApiVersion(1, 0))
   .WithTags("Ping");

app.Run();
