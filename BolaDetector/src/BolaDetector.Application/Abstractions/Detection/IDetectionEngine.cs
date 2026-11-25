using BolaDetector.Domain;

namespace BolaDetector.Application;

public interface IDetectionEngine
{
    /// <summary>
    /// Executes a supported scenario with one GET operation, both identities and a resource.
    /// The returned execution includes a scenario result. Caller cancellation propagates.
    /// </summary>
    Task<TestExecution> ExecuteAsync(TestScenario scenario, ValidatedDetectionConfiguration configuration,
        CancellationToken cancellationToken);
}
