using BolaDetector.Application;
namespace BolaDetector.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/projects", async (IApiProjectReader projects, CancellationToken ct) =>
            Results.Ok(await projects.ReadAsync(ct))).RequireAuthorization();
        return endpoints;
    }
}
