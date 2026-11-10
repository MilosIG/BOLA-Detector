using BolaDetector.Domain;

namespace BolaDetector.Application;

public record TestExecution(Guid Id, Guid ScenarioId, ExecutionStatus Status, DateTimeOffset StartedAt)
{
    public ScenarioResult? Result { get; init; }
    public VulnerabilityFinding? Finding { get; init; }
}
