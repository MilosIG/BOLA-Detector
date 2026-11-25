using System.Text.Json;
using System.Text.RegularExpressions;
using BolaDetector.Application;
namespace BolaDetector.Infrastructure;

public sealed class JsonDetectionConfigurationLoader : IDetectionConfigurationLoader
{
    private static readonly string[] RequiredCollections = ["identities", "resources", "operations", "rules"];
    private static readonly string[] KnownProperties =
    [
        "targetOrigin",
        "timeoutSeconds",
        "identities",
        "resources",
        "operations",
        "rules",
        "protectedProperties",
        "name",
        "identity",
        "id",
        "organizationId",
        "role",
        "credentialReference",
        "ownerId",
        "resourceType",
        "operation",
        "operationId",
        "method",
        "path",
        "action",
        "requireOwner",
        "requireSameOrganization"
    ];

    public async Task<DetectionConfiguration> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text))
            throw Invalid("CFG_EMPTY_FILE", "$", "The configuration file is empty.");

        using var document = Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid("CFG_INVALID_TYPE", "$", "The configuration root must be a JSON object.");

        var names = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredCollections.Where(name => !names.Contains(name))
            .Select(name => new ConfigurationValidationError("CFG_REQUIRED_COLLECTION", name, "Required collection is missing."))
            .ToArray();
        if (missing.Length != 0)
            throw new ConfigurationValidationException(missing);

        try
        {
            return root.Deserialize<DetectionConfiguration>(DetectionJson.CreateOptions())
                ?? throw Invalid("CFG_INVALID_TYPE", "$", "The configuration root must produce a configuration object.");
        }
        catch (JsonException error)
        {
            throw Invalid("CFG_INVALID_TYPE", SafePath(error.Path), "Use the expected JSON type and value format for this property.");
        }
    }

    private static JsonDocument Parse(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            throw Invalid("CFG_INVALID_JSON", "$", "The file contains malformed JSON.");
        }
    }

    private static string SafePath(string? jsonPath)
    {
        if (jsonPath is null || !jsonPath.StartsWith('$'))
            return "$";

        // Include only known schema names and array indexes, never arbitrary input names or values.
        var path = "";
        foreach (Match part in Regex.Matches(jsonPath, @"\G(?:\$|\.([A-Za-z][A-Za-z0-9]*)|(\[[0-9]+\]))"))
        {
            if (part.Groups[2].Success)
                path += part.Groups[2].Value;
            if (!part.Groups[1].Success)
                continue;
            var known = KnownProperties.FirstOrDefault(name => name.Equals(part.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            if (known is null)
                break;
            path += path.Length == 0 ? known : "." + known;
        }

        return path.Length == 0 ? "$" : path;
    }

    private static ConfigurationValidationException Invalid(string code, string path, string message) =>
        new([new(code, path, message)]);
}
