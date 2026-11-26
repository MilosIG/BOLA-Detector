using System.Net;
using BolaTestApi.Documents;

namespace BolaTestApi.Endpoints;

internal static class DocumentCrudEndpoints
{
    private const string IdentityHeader = "X-Test-User";
    private const string CreationKeyHeader = "X-Creation-Key";
    private const int MaximumTitleLength = 200;

    public static void MapDocumentCrudEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/documents");
        routes.AddEndpointFilter(RequireLocalTestIdentity);

        routes.MapPost("", (DocumentInput? input, HttpRequest request, TestDocumentStore store) =>
        {
            var title = ValidatedTitle(input);
            if (title is null) return InvalidTitle();

            if (request.Headers.TryGetValue(CreationKeyHeader, out var keys))
            {
                if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "N", out var key) || key == Guid.Empty)
                    return Results.BadRequest();
                var (record, conflict, deleted) = store.CreateCorrelated(Owner(request), key, title);
                if (conflict) return Results.Conflict();
                if (deleted) return Results.StatusCode(410);
                return Results.Created($"/documents/{record.Id}", record);
            }

            var document = store.Create(Owner(request), title);
            return Results.Created($"{request.PathBase}/documents/{document.Id}", document);
        });
        routes.MapGet("/creations/{key:guid}", (Guid key, HttpRequest request, TestDocumentStore store) =>
        {
            var (record, deleted) = store.Recover(Owner(request), key);
            if (record is null) return Results.NoContent();
            return Results.Json(record, statusCode: deleted ? 410 : 200);
        });
        routes.MapGet("/{id}", (string id, HttpRequest request, TestDocumentStore store) =>
        {
            var document = store.FindCreated(id);
            if (document is null) return Results.NotFound();
            return document.Owner == Owner(request) ? Results.Ok(document) : Results.StatusCode(403);
        });
        routes.MapPut("/{id}", (string id, DocumentInput? input, HttpRequest request, TestDocumentStore store) =>
        {
            var title = ValidatedTitle(input);
            return title is null ? InvalidTitle() : MutationResponse(store.Update(id, Owner(request), title));
        });
        routes.MapDelete("/{id}", (string id, HttpRequest request, TestDocumentStore store) =>
            MutationResponse(store.Delete(id, Owner(request))));
    }

    private static ValueTask<object?> RequireLocalTestIdentity(EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var remote = context.HttpContext.Connection.RemoteIpAddress;
        if (remote?.IsIPv4MappedToIPv6 == true)
            remote = remote.MapToIPv4();
        if (remote is null || !IPAddress.IsLoopback(remote))
            return ValueTask.FromResult<object?>(Results.StatusCode(403));

        var identity = context.HttpContext.Request.Headers[IdentityHeader];
        if (identity.Count != 1 || identity[0] is not ("alice" or "bob"))
            return ValueTask.FromResult<object?>(Results.Unauthorized());

        return next(context);
    }

    private static string Owner(HttpRequest request) => request.Headers[IdentityHeader].ToString();

    private static string? ValidatedTitle(DocumentInput? input)
    {
        var title = input?.Title?.Trim();
        return title is { Length: > 0 and <= MaximumTitleLength } ? title : null;
    }

    private static IResult InvalidTitle() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = [$"Title must contain between 1 and {MaximumTitleLength} characters after trimming."]
        });

    private static IResult MutationResponse(DocumentMutationResult result) => result switch
    {
        DocumentMutationResult.Applied => Results.NoContent(),
        DocumentMutationResult.NotFound => Results.NotFound(),
        _ => Results.StatusCode(403)
    };
}
