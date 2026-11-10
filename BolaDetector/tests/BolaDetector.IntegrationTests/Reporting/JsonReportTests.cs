using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Infrastructure;
using NUnit.Framework;
namespace BolaDetector.IntegrationTests;

public class JsonReportTests
{
    [Test]
    public async Task ReportWriterCreatesDirectoriesAndPreservesPublicEvidence()
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "report-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "nested", "report.json");
        try
        {
            var report = new DetectionReport(DateTimeOffset.UnixEpoch,
                [new("Bob reading Alice resource", "bob", "doc-1", "GET", "/fixture/doc-1", false,
                    new(200, 10, null), FindingVerdict.ConfirmedViolation, "Protected data matched.", "SHA-256=fixture")]);
            IDetectionReportWriter writer = new JsonDetectionReportWriter();
            await writer.WriteAsync(report, path, default);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var result = json.RootElement.GetProperty("Results")[0];
            Assert.That(result.GetProperty("Verdict").GetString(), Is.EqualTo("ConfirmedViolation"));
            Assert.That(result.GetProperty("Response").GetProperty("StatusCode").GetInt32(), Is.EqualTo(200));
            Assert.That(result.GetProperty("Evidence").GetString(), Is.EqualTo("SHA-256=fixture"));
            Assert.That(result.TryGetProperty("Body", out _), Is.False);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
