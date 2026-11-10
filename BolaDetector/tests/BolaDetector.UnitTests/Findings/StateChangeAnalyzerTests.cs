using BolaDetector.Domain;
using BolaDetector.Engine;
using NUnit.Framework;
namespace BolaDetector.UnitTests;

public class StateChangeAnalyzerTests
{
    [Test]
    public void ForeignOwnerIsDenied()
    {
        var org = Guid.NewGuid();
        var identity = new TestIdentity(Guid.NewGuid(), org, "member", "env:TOKEN");
        var resource = new TestResource("1", Guid.NewGuid(), org, "document");
        Assert.That(new AuthorizationOracle().IsAllowed(identity, resource,
            new AccessRule("document", "read", true, true)), Is.False);
    }
    [Test]
    public void ForbiddenResponseDoesNotHideStateChange()
    {
        Assert.That(FindingAnalyzer.Analyze(false, 403, new("1", "before", true), new("1", "after", true)),
            Is.EqualTo(FindingVerdict.ConfirmedViolation));
    }
    [Test]
    public void UnreadableStateHasLimitedVerification()
    {
        Assert.That(FindingAnalyzer.Analyze(false, 200, new("1", null, false), new("1", null, false)),
            Is.EqualTo(FindingVerdict.LimitedVerification));
    }
}
