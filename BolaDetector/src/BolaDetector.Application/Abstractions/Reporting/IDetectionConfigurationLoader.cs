namespace BolaDetector.Application;

public interface IDetectionConfigurationLoader
{
    Task<DetectionConfiguration> LoadAsync(string path, CancellationToken cancellationToken);
}
