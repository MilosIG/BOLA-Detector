using BolaTestApi.Documents;
using BolaTestApi.Endpoints;
namespace BolaTestApi;

public class TestApiProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<TestDocumentStore>();
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapDocumentEndpoints();
        app.Run();
    }
}
