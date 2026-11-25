using System.Text.Json;
using System.Text.Json.Nodes;
using BolaDetector.Application;
using BolaDetector.Domain;

namespace BolaDetector.Infrastructure;

public sealed class JsonRecordedWriteEvidencePreparer : IRecordedWriteEvidencePreparer
{
    public RecordedWriteEvidence Prepare(string resourceId, ResourceSnapshot before, ResourceSnapshot after,
        string protectedField, string intendedValue)
    {
        try
        {
            var initial = ReadState(before, resourceId);
            var final = ReadState(after, resourceId);
            if (initial is null || final is null ||
                !initial.TryGetPropertyValue(protectedField, out var initialValue) ||
                !final.TryGetPropertyValue(protectedField, out var finalValue))
                return RecordedWriteEvidence.Unavailable;

            var intended = ParseValue(intendedValue);
            if (!JsonNode.DeepEquals(initialValue, intended) && JsonNode.DeepEquals(finalValue, intended))
                return RecordedWriteEvidence.IntendedChange;

            return JsonNode.DeepEquals(initial, final) ? RecordedWriteEvidence.Unchanged : RecordedWriteEvidence.OtherChange;
        }
        catch (JsonException)
        {
            return RecordedWriteEvidence.Unavailable;
        }
        catch (ArgumentException)
        {
            return RecordedWriteEvidence.Unavailable;
        }
    }

    private static JsonObject? ReadState(ResourceSnapshot snapshot, string resourceId) =>
        snapshot.IsReadable && snapshot.Content is not null && snapshot.ResourceId == resourceId
            ? ParseValue(snapshot.Content) as JsonObject
            : null;

    private static JsonNode? ParseValue(string content)
    {
        var value = JsonNode.Parse(content);
        ValidateProperties(value);
        return value;
    }

    // Materialize objects before comparison so duplicate keys make evidence unusable.
    private static void ValidateProperties(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var property in obj)
                ValidateProperties(property.Value);
        else if (node is JsonArray array)
            foreach (var element in array)
                ValidateProperties(element);
    }
}
