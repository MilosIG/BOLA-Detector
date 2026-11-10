using System.Text.Json;
using BolaDetector.Application;
namespace BolaDetector.Infrastructure;

public sealed class JsonDetectionReportWriter : IDetectionReportWriter
{
    public async Task WriteAsync(DetectionReport report, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, DetectionJson.CreateOptions()), cancellationToken);
    }
}
