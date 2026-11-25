using BolaDetector.Domain;
using BolaDetector.Engine;
using NUnit.Framework;
using System.Text.Json.Nodes;
namespace BolaDetector.UnitTests;

public class StateChangeAnalyzerTests
{
    [TestCase(false, 403, "{\"title\":\"updated\"}", true, FindingVerdict.ConfirmedViolation)]
    [TestCase(false, 200, "{\"title\":\"original\"}", true, FindingVerdict.PotentialViolation)]
    [TestCase(false, 403, "{\"title\":\"original\"}", true, FindingVerdict.NoViolationObserved)]
    [TestCase(false, 200, "{\"title\":\"updated\"}", false, FindingVerdict.LimitedVerification)]
    [TestCase(false, 200, "{\"title\":\"original\",\"title\":\"updated\"}", true, FindingVerdict.LimitedVerification)]
    [TestCase(true, 200, "{\"title\":\"original\"}", true, FindingVerdict.LimitedVerification)]
    [TestCase(true, 200, "{\"title\":\"updated\"}", true, FindingVerdict.NoViolationObserved)]
    public void ClassifiesRecordedWriteEvidence(bool expectedAllowed, int statusCode, string finalState,
        bool isReadable, FindingVerdict verdict)
    {
        var analysis = FindingAnalyzer.AnalyzeRecordedWrite(expectedAllowed, statusCode, "1",
            new("1", """{"title":"original"}""", true), new("1", finalState, isReadable),
            "title", JsonValue.Create("updated"));

        Assert.That(analysis.Verdict, Is.EqualTo(verdict));
    }

    [Test]
    public void ComparesRecordedJsonSemantically()
    {
        var analysis = FindingAnalyzer.AnalyzeRecordedWrite(false, 403, "1",
            new("1", """{"title":{"first":1,"second":2},"other":true}""", true),
            new("1", """{ "other": true, "title": { "second": 2, "first": 1 } }""", true),
            "title", JsonNode.Parse("""{"first":1,"second":2}"""));

        Assert.That(analysis.Verdict, Is.EqualTo(FindingVerdict.NoViolationObserved));
    }

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
