using BolaTestApi.Documents;
namespace BolaTestApi.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/vulnerable/documents/{id}", (string id, TestDocumentStore documents) =>
        {
            var document = documents.Find(id);
            // Deliberately missing ownership check, strictly for the controlled local fixture.
            return document is null ? Results.NotFound() : Results.Ok(document);
        });
        endpoints.MapGet("/secure/documents/{id}", (string id, HttpRequest request, TestDocumentStore documents) =>
        {
            var document = documents.Find(id);
            if (document is null) return Results.NotFound();
            // Synthetic local identity header, not production authentication.
            if (request.Headers["X-Test-User"] != document.Owner) return Results.StatusCode(403);
            return Results.Ok(document);
        });
        return endpoints;
    }
}
