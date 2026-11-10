using BolaDetector.Application;
namespace BolaDetector.Demo;

public sealed class ConsoleReportPresenter(TextWriter output)
{
    public void Present(DetectionReport report, string outputPath)
    {
        foreach (var result in report.Results)
        {
            output.WriteLine($"{result.TestUser,-8} {result.Method} {result.Path,-40} expected={result.ExpectedAllowed,-5} HTTP={result.Response.StatusCode?.ToString() ?? "none"} {result.Verdict}");
            output.WriteLine($"  {result.Explanation} {result.Evidence}");
        }
        output.WriteLine($"Report: {Path.GetFullPath(outputPath)}\n{report.Limitation}");
    }
}
