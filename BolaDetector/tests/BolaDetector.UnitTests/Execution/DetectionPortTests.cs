using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;
namespace BolaDetector.UnitTests;

public class DetectionPortTests
{
    [TestCase(false, FindingVerdict.ConfirmedViolation)]
    [TestCase(true, FindingVerdict.LimitedVerification)]
    public async Task EngineAcceptsAlternativeTransportWithoutHttpOrJson(bool unavailable, FindingVerdict expected)
    {
        var configuration = Config();
        var scenario = Scenario(configuration);
        var transport = new InMemoryReadExecutor(unavailable);
        var engine = new DetectionEngine(transport, new FixtureEvidenceVerifier(), TimeProvider.System);
        var execution = await engine.ExecuteAsync(scenario, ValidatedDetectionConfiguration.Create(configuration), default);
        Assert.That(execution.Result!.Verdict, Is.EqualTo(expected));
        Assert.That(transport.Identities, Is.EqualTo(new[] { scenario.OwnerIdentity!.Id, scenario.Identity!.Id }));
        Assert.That(execution.Finding is not null, Is.EqualTo(!unavailable));
    }

    [Test]
    public async Task UseCaseRunsThroughContractsAndUsesInjectedClock()
    {
        var configuration = Config();
        var scenario = Scenario(configuration);
        var runner = new RunDetection(new SingleScenarioGenerator(scenario),
            new DetectionEngine(new InMemoryReadExecutor(false), new FixtureEvidenceVerifier(), new FixedClock()), new FixedClock());
        var report = await runner.ExecuteAsync(configuration, default);
        Assert.That(report.CreatedAt, Is.EqualTo(new FixedClock().GetUtcNow()));
        Assert.That(report.Results, Has.Count.EqualTo(1));
        Assert.That(report.Results[0].Verdict, Is.EqualTo(FindingVerdict.ConfirmedViolation));
    }

    [Test]
    public async Task ExecuteAsync_WhenSessionRuns_PassesSameSnapshotToGeneratorTransportAndVerifier()
    {
        var configuration = Config();
        var generator = new SingleScenarioGenerator(Scenario(configuration));
        var transport = new InMemoryReadExecutor(false);
        var verifier = new FixtureEvidenceVerifier();
        var runner = new RunDetection(generator, new DetectionEngine(transport, verifier, new FixedClock()), new FixedClock());

        await runner.ExecuteAsync(configuration, default);

        Assert.That(generator.Snapshot, Is.Not.Null);
        Assert.That(transport.Snapshots, Has.Count.EqualTo(2));
        Assert.That(transport.Snapshots[0], Is.SameAs(generator.Snapshot));
        Assert.That(transport.Snapshots[1], Is.SameAs(generator.Snapshot));
        Assert.That(verifier.Snapshot, Is.SameAs(generator.Snapshot));
    }

    [Test]
    public void UseCasePropagatesCallerCancellation()
    {
        var configuration = Config();
        var runner = new RunDetection(new SingleScenarioGenerator(Scenario(configuration)),
            new DetectionEngine(new InMemoryReadExecutor(false), new FixtureEvidenceVerifier(), TimeProvider.System), TimeProvider.System);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () => await runner.ExecuteAsync(configuration, cancellation.Token));
    }

    private sealed class SingleScenarioGenerator(TestScenario scenario) : IScenarioGenerator
    {
        public ValidatedDetectionConfiguration? Snapshot { get; private set; }

        public IReadOnlyList<TestScenario> Generate(ValidatedDetectionConfiguration configuration)
        {
            Snapshot = configuration;
            return [scenario];
        }
    }
    private sealed class InMemoryReadExecutor(bool unavailable) : IScenarioRequestExecutor
    {
        public List<Guid> Identities { get; } = [];
        public List<ValidatedDetectionConfiguration> Snapshots { get; } = [];

        public Task<ObservedResponse> ExecuteAsync(ApiOperation operation, TestResource resource, TestIdentity identity,
            ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Identities.Add(identity.Id);
            Snapshots.Add(configuration);
            return Task.FromResult(new ObservedResponse("/fixture/" + resource.Id,
                unavailable ? new(null, 0, "HttpRequestFailed") : new(200, 10, null), unavailable ? null : "protected-data"));
        }
    }
    private sealed class FixtureEvidenceVerifier : IReadEvidenceVerifier
    {
        public ValidatedDetectionConfiguration? Snapshot { get; private set; }

        public ReadEvidence Verify(string resourceId, ObservedResponse baseline, ObservedResponse actual,
            ValidatedDetectionConfiguration configuration)
        {
            Snapshot = configuration;
            return new(baseline.Observation.Error is null, baseline.Body is not null && baseline.Body == actual.Body, "fixture-fingerprint");
        }
    }
    private sealed class FixedClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); }
}
