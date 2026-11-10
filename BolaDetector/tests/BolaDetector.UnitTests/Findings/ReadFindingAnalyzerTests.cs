using System.Net;
using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;
namespace BolaDetector.UnitTests;

public class ReadFindingAnalyzerTests
{
    [TestCase(200, FindingVerdict.PotentialViolation)]
    [TestCase(403, FindingVerdict.NoViolationObserved)]
    [TestCase(500, FindingVerdict.LimitedVerification)]
    public void StatusAloneCannotConfirmRead(int code, FindingVerdict expected) =>
        Assert.That(FindingAnalyzer.AnalyzeRead(false, code, true, false, null, null).Verdict, Is.EqualTo(expected));
    [Test]
    public void ProtectedDisclosureConfirmedEvenWithForbiddenStatus() =>
        Assert.That(FindingAnalyzer.AnalyzeRead(false, 403, true, true, "hash", null).Verdict,
            Is.EqualTo(FindingVerdict.ConfirmedViolation));

}
