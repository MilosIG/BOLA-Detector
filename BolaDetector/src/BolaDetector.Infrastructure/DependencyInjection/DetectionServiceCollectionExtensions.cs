using BolaDetector.Application;
using BolaDetector.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace BolaDetector.Infrastructure;

public static class DetectionServiceCollectionExtensions
{
    public static IServiceCollection AddLocalDocumentSequences(this IServiceCollection services)
    {
        services.TryAddSingleton<LocalTargetAddressPolicy>();
        services.TryAddSingleton<ITestCredentialProvider, EnvironmentTestCredentialProvider>();
        services.AddTransient<IDocumentSequenceExecutor, DocumentSequenceExecutor>();
        services.AddHttpClient<IOwnedDocumentClient, LocalOwnedDocumentClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false });
        return services;
    }

    public static IServiceCollection AddBolaDetection(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAuthorizationOracle, AuthorizationOracle>();
        services.AddSingleton<IScenarioGenerator, BolaScenarioGenerator>();
        services.AddTransient<IDetectionEngine, DetectionEngine>();
        services.AddTransient<RunDetection>();
        services.AddSingleton<LocalTargetAddressPolicy>();
        services.AddSingleton<ITestCredentialProvider, EnvironmentTestCredentialProvider>();
        services.AddSingleton<IReadEvidenceVerifier, JsonReadEvidenceVerifier>();
        services.AddSingleton<IOpenApiImporter, OpenApiImporter>();
        services.AddSingleton<IDetectionConfigurationLoader, JsonDetectionConfigurationLoader>();
        services.AddSingleton<IDetectionReportWriter, JsonDetectionReportWriter>();
        services.AddHttpClient<IScenarioRequestExecutor, HttpScenarioRequestExecutor>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false });
        return services;
    }
}
