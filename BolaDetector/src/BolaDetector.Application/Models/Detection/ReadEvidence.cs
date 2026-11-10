namespace BolaDetector.Application;

public record ReadEvidence(bool BaselineVerified, bool ProtectedDataMatched, string? Fingerprint);
