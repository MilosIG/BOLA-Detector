using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using BolaTestApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
namespace BolaDetector.IntegrationTests;

public class DetectionIntegrationTests
{
    [Test]
    public async Task SameDetectorDistinguishesSecureAndVulnerableOperations()
    {
        var config = JsonSerializer.Deserialize<DetectionConfiguration>(
            File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "demo-detection.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        await using var factory = new WebApplicationFactory<TestApiProgram>();
        var services = new ServiceCollection().AddBolaDetection(config);
        services.Replace(ServiceDescriptor.Singleton<ITestCredentialProvider>(new FixtureCredentials(config)));
        // Exercise real ASP.NET routing/serialization over the deterministic in-process transport.
        services.AddHttpClient<IScenarioRequestExecutor, HttpScenarioRequestExecutor>().ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IDetectionEngine>();
        var results = new List<ScenarioResult>();
        foreach (var scenario in provider.GetRequiredService<IScenarioGenerator>().Generate(config))
            results.Add((await engine.ExecuteAsync(scenario, default)).Result!);
        Assert.That(results, Has.Count.EqualTo(8));
        Assert.That(results.Where(r => r.Path.StartsWith("/secure/")).All(r => r.Verdict == FindingVerdict.NoViolationObserved), Is.True);
        var violations = results.Where(r => r.Verdict == FindingVerdict.ConfirmedViolation).ToArray();
        Assert.That(violations, Has.Length.EqualTo(2));
        Assert.That(violations.All(r => !r.ExpectedAllowed && r.Path.StartsWith("/vulnerable/") && r.Evidence.Contains("SHA-256=")), Is.True);
        Assert.That(results.Where(r => r.ExpectedAllowed).All(r => r.Response.StatusCode == 200), Is.True);
        Assert.That(results.Where(r => !r.ExpectedAllowed && r.Path.StartsWith("/secure/")).All(r => r.Response.StatusCode == 403), Is.True);
        var report = JsonSerializer.Serialize(new DetectionReport(DateTimeOffset.UtcNow, results));
        Assert.That(report, Does.Not.Contain("private document"));
        Assert.That(report, Does.Not.Contain("env:"));
    }
    private sealed class FixtureCredentials(DetectionConfiguration config) : ITestCredentialProvider
    {
        public string Resolve(TestIdentity identity) => config.Identities.Single(i => i.Identity.Id == identity.Id).Name;
    }
}
