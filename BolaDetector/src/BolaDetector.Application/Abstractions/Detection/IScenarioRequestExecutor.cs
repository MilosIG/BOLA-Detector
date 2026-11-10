using BolaDetector.Domain;
namespace BolaDetector.Application;

/// <summary>Executes one supported read. Transport failures are observations; caller cancellation propagates.
/// Credentials and response bodies must never be copied into public reports.</summary>
public interface IScenarioRequestExecutor
{
    Task<ObservedResponse> ExecuteAsync(ApiOperation operation, TestResource resource,
        TestIdentity identity, CancellationToken cancellationToken);
}
