namespace BolaDetector.Application;

public interface IDetectionReportWriter
{
    Task WriteAsync(DetectionReport report, string path, CancellationToken cancellationToken);
}
