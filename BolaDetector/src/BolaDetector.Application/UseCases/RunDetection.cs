namespace BolaDetector.Application;

public sealed class RunDetection(IScenarioGenerator generator, IDetectionEngine engine, TimeProvider timeProvider)
{
    public async Task<DetectionReport> ExecuteAsync(DetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        var results = new List<ScenarioResult>();
        foreach (var scenario in generator.Generate(configuration))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = await engine.ExecuteAsync(scenario, cancellationToken);
            results.Add(execution.Result ?? throw new InvalidOperationException("An execution must provide a scenario result."));
        }
        return new DetectionReport(timeProvider.GetUtcNow(), results);
    }
}
