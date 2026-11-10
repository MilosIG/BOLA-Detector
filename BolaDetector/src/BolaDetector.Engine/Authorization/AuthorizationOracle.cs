using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Engine;

public sealed class AuthorizationOracle : IAuthorizationOracle
{
    public bool IsAllowed(TestIdentity identity, TestResource resource, AccessRule rule) => rule.Allows(identity, resource);
}
