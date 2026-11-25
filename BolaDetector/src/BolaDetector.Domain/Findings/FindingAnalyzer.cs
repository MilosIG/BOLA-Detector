using System.Text.Json;
using System.Text.Json.Nodes;

namespace BolaDetector.Domain;

public static class FindingAnalyzer
{
    public static FindingVerdict Analyze(bool expectedAllowed, int statusCode, ResourceSnapshot before, ResourceSnapshot after)
    {
        if (expectedAllowed) return FindingVerdict.NoViolationObserved;
        if (!before.IsReadable || !after.IsReadable || before.Content is null || after.Content is null || before.ResourceId != after.ResourceId) return FindingVerdict.LimitedVerification;
        if (before.Content != after.Content) return FindingVerdict.ConfirmedViolation;
        return statusCode is >= 200 and < 300
            ? FindingVerdict.PotentialViolation : FindingVerdict.NoViolationObserved;
    }

    public static FindingAnalysis AnalyzeRecordedWrite(bool expectedAllowed, int? statusCode,
        string resourceId, ResourceSnapshot before, ResourceSnapshot after,
        string protectedField, JsonNode? intendedValue)
    {
        var initial = ReadRecordedState(before, resourceId);
        var final = ReadRecordedState(after, resourceId);
        if (initial is null || final is null ||
            !initial.TryGetPropertyValue(protectedField, out var initialValue) ||
            !final.TryGetPropertyValue(protectedField, out var finalValue))
            return new(FindingVerdict.LimitedVerification, "Recorded protected state could not be verified.",
                "A snapshot was unavailable, invalid, or missing the protected field for the resource.");

        var intendedChange = !JsonNode.DeepEquals(initialValue, intendedValue) &&
            JsonNode.DeepEquals(finalValue, intendedValue);
        if (intendedChange)
            return new(expectedAllowed ? FindingVerdict.NoViolationObserved : FindingVerdict.ConfirmedViolation,
                expectedAllowed ? "The expected authorized change was observed." : "The denied identity's intended change was observed.",
                "The final protected value matched the intended value and differed from the baseline.");

        if (expectedAllowed)
            return new(FindingVerdict.LimitedVerification, "The expected authorized change could not be verified.",
                "The recorded state did not demonstrate the intended change.");

        if (JsonNode.DeepEquals(initial, final) && statusCode is 401 or 403 or 404)
            return new(FindingVerdict.NoViolationObserved, "The request was rejected and recorded state was unchanged.",
                "The baseline and final snapshots were semantically equal.");

        if (statusCode is >= 200 and < 300)
            return new(FindingVerdict.PotentialViolation, "The request returned success without a verified change.",
                "The recorded state did not demonstrate the intended change.");

        return new(FindingVerdict.LimitedVerification, "The recorded result was inconclusive.",
            "The response and snapshots did not establish either rejection with unchanged state or the intended change.");
    }

    private static JsonObject? ReadRecordedState(ResourceSnapshot snapshot, string resourceId)
    {
        if (!snapshot.IsReadable || snapshot.Content is null || snapshot.ResourceId != resourceId)
            return null;

        try
        {
            var state = JsonNode.Parse(snapshot.Content) as JsonObject;
            // Materialize nested objects so duplicate properties are rejected before comparison.
            ValidateRecordedState(state);
            return state;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void ValidateRecordedState(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var property in obj)
                ValidateRecordedState(property.Value);
        else if (node is JsonArray array)
            foreach (var element in array)
                ValidateRecordedState(element);
    }

    public static FindingAnalysis AnalyzeRead(bool expectedAllowed, int? statusCode,
        bool baselineVerified, bool protectedDataMatched, string? fingerprint, string? error)
    {
        if (error is not null || !baselineVerified)
            return new(FindingVerdict.LimitedVerification, "Request or owner baseline could not be verified.", "No reliable comparison available.");
        if (!expectedAllowed && protectedDataMatched)
            return new(FindingVerdict.ConfirmedViolation, "Unauthorized identity received the resource's protected fields verified against the owner response.",
                $"Resource ID and protected fields matched; SHA-256={fingerprint}. Response content omitted.");
        if (expectedAllowed)
            return protectedDataMatched
                ? new(FindingVerdict.NoViolationObserved, "Authorized read matched the owner baseline.", "Resource ID and protected fields matched.")
                : new(FindingVerdict.LimitedVerification, "Expected authorized read could not be verified.", "Protected data did not match the baseline.");
        if (statusCode is 401 or 403 or 404)
            return new(FindingVerdict.NoViolationObserved, "Unauthorized read was rejected and no matching protected data was observed.", $"HTTP {statusCode}; verified owner baseline.");
        if (statusCode is >= 200 and < 300)
            return new(FindingVerdict.PotentialViolation, "Successful status without proof of protected data disclosure.", $"HTTP {statusCode}; protected data did not match.");
        return new(FindingVerdict.LimitedVerification, "Unexpected response prevents a reliable authorization decision.", $"HTTP {statusCode}.");
    }
}
