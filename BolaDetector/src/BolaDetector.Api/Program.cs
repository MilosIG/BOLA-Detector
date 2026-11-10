using BolaDetector.Api.Endpoints;
using BolaDetector.Application;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
builder.Services.AddBolaPersistence(connection);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationOracle, AuthorizationOracle>();
builder.Services.AddSingleton<IOpenApiImporter, OpenApiImporter>();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapPlatformEndpoints();
app.MapProjectEndpoints();
app.Run();
public partial class Program { }
