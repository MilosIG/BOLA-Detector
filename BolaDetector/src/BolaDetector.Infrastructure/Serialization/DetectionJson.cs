using System.Text.Json;
using System.Text.Json.Serialization;
namespace BolaDetector.Infrastructure;

internal static class DetectionJson
{
    public static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
