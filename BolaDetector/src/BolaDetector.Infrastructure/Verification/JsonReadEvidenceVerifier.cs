using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BolaDetector.Application;
namespace BolaDetector.Infrastructure;

public sealed class JsonReadEvidenceVerifier(DetectionConfiguration configuration) : IReadEvidenceVerifier
{
    public ReadEvidence Verify(string resourceId, ObservedResponse baseline, ObservedResponse actual)
    {
        if (configuration.ProtectedProperties.Count == 0 || configuration.ProtectedProperties.Any(p => p == "id" || string.IsNullOrWhiteSpace(p)))
            throw new ArgumentException("At least one protected property other than id is required.");
        var expected = Project(baseline.Body, resourceId);
        var received = Project(actual.Body, resourceId);
        var verified = baseline.Observation.Error is null && baseline.Observation.StatusCode is >= 200 and < 300 && expected is not null;
        var matched = verified && actual.Observation.Error is null && received == expected;
        var fingerprint = matched ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expected!))) : null;
        return new ReadEvidence(verified, matched, fingerprint);
    }

    private string? Project(string? body, string resourceId)
    {
        if (body is null) return null;
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != resourceId)
                return null;
            var values = new List<string> { id.GetRawText() };
            foreach (var property in configuration.ProtectedProperties.Order(StringComparer.Ordinal))
            {
                if (!root.TryGetProperty(property, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                    (value.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(value.GetString()))) return null;
                values.Add(value.GetRawText());
            }
            return JsonSerializer.Serialize(values);
        }
        catch (JsonException) { return null; }
    }
}
