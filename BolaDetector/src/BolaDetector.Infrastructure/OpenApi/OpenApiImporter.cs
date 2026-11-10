using BolaDetector.Application;
using BolaDetector.Domain;
using Microsoft.OpenApi.Readers;
namespace BolaDetector.Infrastructure;

public sealed class OpenApiImporter : IOpenApiImporter
{
    public IReadOnlyList<ApiOperation> Parse(string document)
    {
        var specification = new OpenApiStringReader().Read(document, out var diagnostic);
        if (diagnostic.Errors.Count > 0)
            throw new ArgumentException(string.Join("; ", diagnostic.Errors.Select(e => e.Message)), nameof(document));
        return specification.Paths.SelectMany(path => path.Value.Operations.Select(op =>
            new ApiOperation(op.Value.OperationId ?? $"{op.Key}:{path.Key}", op.Key.ToString().ToUpperInvariant(), path.Key))).ToArray();
    }
}
