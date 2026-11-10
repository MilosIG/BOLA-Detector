using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Engine;

public sealed class DetectionEngine(IScenarioRequestExecutor requests, IReadEvidenceVerifier verifier,
    TimeProvider timeProvider) : IDetectionEngine
{
    public async Task<TestExecution> ExecuteAsync(TestScenario scenario, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetUtcNow();
        var identity = scenario.Identity ?? throw new ArgumentException("Scenario identity is required.");
        var owner = scenario.OwnerIdentity ?? throw new ArgumentException("Owner identity is required.");
        var resource = scenario.Resource ?? throw new ArgumentException("Resource is required.");
        if (scenario.Steps.Count != 1 || scenario.Steps[0].Method != "GET")
            throw new ArgumentException("Only a single GET operation is supported.");
        cancellationToken.ThrowIfCancellationRequested();
        var operation = scenario.Steps[0];
        var baseline = await requests.ExecuteAsync(operation, resource, owner, cancellationToken);
        var actual = await requests.ExecuteAsync(operation, resource, identity, cancellationToken);
        var evidence = verifier.Verify(resource.Id, baseline, actual);
        var analysis = FindingAnalyzer.AnalyzeRead(scenario.ExpectedAllowed, actual.Observation.StatusCode,
            evidence.BaselineVerified, evidence.ProtectedDataMatched, evidence.Fingerprint,
            baseline.Observation.Error ?? actual.Observation.Error);
        var result = new ScenarioResult(scenario.Name, scenario.TestUserName ?? identity.Id.ToString(), resource.Id,
            operation.Method, actual.Path, scenario.ExpectedAllowed, actual.Observation,
            analysis.Verdict, analysis.Explanation, analysis.Evidence);
        var id = Guid.NewGuid();
        return new TestExecution(id, scenario.Id,
            actual.Observation.Error is null && baseline.Observation.Error is null ? ExecutionStatus.Completed : ExecutionStatus.Failed, started)
        {
            Result = result,
            Finding = analysis.Verdict is FindingVerdict.ConfirmedViolation or FindingVerdict.PotentialViolation
                ? new VulnerabilityFinding(Guid.NewGuid(), id, analysis.Verdict, analysis.Explanation) : null
        };
    }
}
