using System.Text.Json;
using BolaDetector.Application;
namespace BolaDetector.Infrastructure;

public sealed class JsonDetectionConfigurationLoader : IDetectionConfigurationLoader
{
    public async Task<DetectionConfiguration> LoadAsync(string path, CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize<DetectionConfiguration>(await File.ReadAllTextAsync(path, cancellationToken), DetectionJson.CreateOptions())
        ?? throw new ArgumentException("Missing configuration.");
}
