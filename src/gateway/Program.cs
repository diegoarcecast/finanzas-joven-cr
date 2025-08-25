using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// Cargar YARP desde appsettings.json
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Health del gateway
app.MapGet("/health", () => Results.Ok(new { status = "Gateway OK" }));

// Todas las rutas definidas en ReverseProxy -> proxy
app.MapReverseProxy();

app.Run();
