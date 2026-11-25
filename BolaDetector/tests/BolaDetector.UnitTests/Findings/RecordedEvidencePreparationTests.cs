using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Infrastructure;
using NUnit.Framework;

namespace BolaDetector.UnitTests;

public class RecordedEvidencePreparationTests
{
    [TestCase("{\"title\":null}", "null", RecordedWriteEvidence.IntendedChange)]
    [TestCase("{}", "null", RecordedWriteEvidence.Unavailable)]
    [TestCase("{\"title\":\"1\"}", "1", RecordedWriteEvidence.OtherChange)]
    [TestCase("{\"title\":1}", "1", RecordedWriteEvidence.IntendedChange)]
    [TestCase("{invalid", "null", RecordedWriteEvidence.Unavailable)]
    [TestCase("[]", "null", RecordedWriteEvidence.Unavailable)]
    [TestCase("{\"title\":null}", "{invalid", RecordedWriteEvidence.Unavailable)]
    [TestCase("{\"title\":null}", "{\"a\":1,\"a\":2}", RecordedWriteEvidence.Unavailable)]
    [TestCase("{\"title\":null,\"nested\":{\"a\":1,\"a\":2}}", "null", RecordedWriteEvidence.Unavailable)]
    public void Prepare_WhenValuesDifferOrAreIncomplete_ReportsTheEvidenceState(
        string finalState, string intendedValue, RecordedWriteEvidence expected)
    {
        var preparer = new JsonRecordedWriteEvidencePreparer();

        var evidence = preparer.Prepare("1", new("1", """{"title":"original"}""", true),
            new("1", finalState, true), "title", intendedValue);

        Assert.That(evidence, Is.EqualTo(expected));
    }

    [Test]
    public void Prepare_WhenResourceIdsDiffer_ReturnsUnavailable()
    {
        var evidence = new JsonRecordedWriteEvidencePreparer().Prepare("1", new("1", """{"title":1}""", true),
            new("2", """{"title":2}""", true), "title", "2");

        Assert.That(evidence, Is.EqualTo(RecordedWriteEvidence.Unavailable));
    }

    [Test]
    public void Prepare_WhenArrayOrderChanges_ReportsOtherChange()
    {
        var evidence = new JsonRecordedWriteEvidencePreparer().Prepare("1", new("1", """{"title":[1,2]}""", true),
            new("1", """{"title":[2,1]}""", true), "title", "[1,2]");

        Assert.That(evidence, Is.EqualTo(RecordedWriteEvidence.OtherChange));
    }

    [Test]
    public void AnalyzeRecordedWrite_WhenEvidenceIsPrepared_ClassifiesWithoutSnapshotContent()
    {
        var result = FindingAnalyzer.AnalyzeRecordedWrite(false, 403, RecordedWriteEvidence.IntendedChange);

        Assert.That(result.Verdict, Is.EqualTo(FindingVerdict.ConfirmedViolation));
    }

    [Test]
    public void AnalyzeOpaqueSnapshots_WhenOnlyTextFormattingDiffers_PreservesLegacyComparison()
    {
        var result = AnalyzeRecordedWrite.AnalyzeOpaqueSnapshots(false, 403,
            new ResourceSnapshot("1", """{"title":1}""", true),
            new ResourceSnapshot("1", """{ "title": 1 }""", true));

        Assert.That(result, Is.EqualTo(FindingVerdict.ConfirmedViolation));
    }
}
