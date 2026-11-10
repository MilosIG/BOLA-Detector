using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Infrastructure;

public sealed class EnvironmentTestCredentialProvider : ITestCredentialProvider
{
    public string Resolve(TestIdentity identity)
    {
        if (!identity.CredentialReference.StartsWith("env:", StringComparison.Ordinal))
            throw new ArgumentException("Only env: references are supported.");
        var value = Environment.GetEnvironmentVariable(identity.CredentialReference[4..]);
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException("Credential is not configured.") : value;
    }
}
