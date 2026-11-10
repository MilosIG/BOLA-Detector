namespace BolaDetector.Application;

/// <summary>Missing, malformed or unverifiable data yields unverified evidence, not a confirmed finding.</summary>
public interface IReadEvidenceVerifier
{
    ReadEvidence Verify(string resourceId, ObservedResponse baseline, ObservedResponse actual);
}
