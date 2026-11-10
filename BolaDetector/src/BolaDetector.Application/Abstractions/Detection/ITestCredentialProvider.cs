using BolaDetector.Domain;

namespace BolaDetector.Application;

public interface ITestCredentialProvider
{
    string Resolve(TestIdentity identity);
}
