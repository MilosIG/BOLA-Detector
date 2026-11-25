using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Infrastructure;

public sealed class EnvironmentTestCredentialProvider : ITestCredentialProvider
{
    public string Resolve(TestIdentity identity)
    {
        if (!DetectionInputPolicy.TryGetCredentialVariable(identity.CredentialReference, out var variable))
            throw new ArgumentException("Only env: references are supported.");
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException("Credential is not configured.") : value;
    }
}
