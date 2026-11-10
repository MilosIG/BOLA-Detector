using BolaDetector.Domain;

namespace BolaDetector.Application;

public interface IAuthorizationOracle
{
    bool IsAllowed(TestIdentity identity, TestResource resource, AccessRule rule);
}
