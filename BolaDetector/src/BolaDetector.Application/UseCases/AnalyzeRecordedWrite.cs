using BolaDetector.Domain;

namespace BolaDetector.Application;

/// <summary>Analyzes previously collected snapshots.</summary>
public sealed class AnalyzeRecordedWrite(IRecordedWriteEvidencePreparer preparer)
{
    public FindingAnalysis Execute(bool expectedAllowed, int? statusCode, string resourceId,
        ResourceSnapshot before, ResourceSnapshot after, string protectedField, string intendedValue)
    {
        var evidence = preparer.Prepare(resourceId, before, after, protectedField, intendedValue);
        return FindingAnalyzer.AnalyzeRecordedWrite(expectedAllowed, statusCode, evidence);
    }

    public static FindingVerdict AnalyzeOpaqueSnapshots(bool expectedAllowed, int statusCode,
        ResourceSnapshot before, ResourceSnapshot after)
    {
        var comparison = SnapshotComparison.Unavailable;
        if (before.IsReadable && after.IsReadable && before.Content is not null && after.Content is not null &&
            before.ResourceId == after.ResourceId)
            comparison = before.Content == after.Content ? SnapshotComparison.Equal : SnapshotComparison.Different;

        return FindingAnalyzer.Analyze(expectedAllowed, statusCode, comparison);
    }
}
