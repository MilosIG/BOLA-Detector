namespace BolaDetector.Application;

public sealed class RunDetection(IScenarioGenerator generator, IDetectionEngine engine, TimeProvider timeProvider)
{
    public async Task<DetectionReport> ExecuteAsync(DetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = ValidatedDetectionConfiguration.Create(configuration);
        var results = new List<ScenarioResult>();
        foreach (var scenario in generator.Generate(snapshot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = await engine.ExecuteAsync(scenario, snapshot, cancellationToken);
            results.Add(execution.Result ?? throw new InvalidOperationException("An execution must provide a scenario result."));
        }
        return new DetectionReport(timeProvider.GetUtcNow(), results);
    }
}
