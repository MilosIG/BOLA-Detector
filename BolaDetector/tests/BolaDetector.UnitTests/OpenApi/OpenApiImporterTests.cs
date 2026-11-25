using BolaDetector.Domain;
using BolaDetector.Infrastructure;
using NUnit.Framework;

namespace BolaDetector.UnitTests;

public class OpenApiImporterTests
{
    [Test]
    public void Parse_WhenSpecificationDefinesAnOperation_ReturnsOperationMetadata()
    {
        const string specification = """
            openapi: 3.0.3
            info:
              title: Sample API
              version: '1.0'
            paths:
              /health:
                get:
                  operationId: getHealth
                  responses:
                    '200':
                      description: Healthy
            """;

        var operations = new OpenApiImporter().Parse(specification);

        Assert.That(operations, Is.EqualTo(new[] { new ApiOperation("getHealth", "GET", "/health") }));
    }

    [Test]
    public void Parse_WhenSpecificationIsInvalid_ReportsAnArgumentError()
    {
        var importer = new OpenApiImporter();

        var error = Assert.Throws<ArgumentException>(() => importer.Parse("openapi: [invalid"));

        Assert.That(error?.ParamName, Is.EqualTo("document"));
    }
}
