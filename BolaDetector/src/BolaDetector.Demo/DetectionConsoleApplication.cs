using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Demo;

public sealed class DetectionConsoleApplication(RunDetection detection,
    IDetectionReportWriter reports, ConsoleReportPresenter presenter)
{
    public async Task<int> RunAsync(DetectionConfiguration configuration, string outputPath, CancellationToken cancellationToken)
    {
        var report = await detection.ExecuteAsync(configuration, cancellationToken);
        await reports.WriteAsync(report, outputPath, cancellationToken);
        presenter.Present(report, outputPath);
        return report.Results.Any(r => r.Response.Error is not null || r.Verdict == FindingVerdict.LimitedVerification) ? 2 : 0;
    }
}
