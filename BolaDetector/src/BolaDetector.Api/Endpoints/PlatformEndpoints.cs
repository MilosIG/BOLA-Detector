using Microsoft.AspNetCore.Identity;
namespace BolaDetector.Api.Endpoints;

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        endpoints.MapGroup("/auth").MapIdentityApi<IdentityUser>();
        return endpoints;
    }
}
