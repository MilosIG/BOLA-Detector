using BolaDetector.Domain;

namespace BolaDetector.Application;

public interface IDetectionEngine
{
    Task<TestExecution> ExecuteAsync(TestScenario scenario, CancellationToken cancellationToken);
}
