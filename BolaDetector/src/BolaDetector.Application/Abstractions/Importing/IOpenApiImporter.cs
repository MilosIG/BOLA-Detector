using BolaDetector.Domain;

namespace BolaDetector.Application;

public interface IOpenApiImporter
{
    IReadOnlyList<ApiOperation> Parse(string document);
}
