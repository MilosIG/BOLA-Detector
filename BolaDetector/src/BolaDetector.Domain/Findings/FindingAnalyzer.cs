namespace BolaDetector.Domain;

public static class FindingAnalyzer
{
    public static FindingVerdict Analyze(bool expectedAllowed, int statusCode, SnapshotComparison comparison)
    {
        if (expectedAllowed) return FindingVerdict.NoViolationObserved;
        if (comparison == SnapshotComparison.Unavailable) return FindingVerdict.LimitedVerification;
        if (comparison == SnapshotComparison.Different) return FindingVerdict.ConfirmedViolation;
        return statusCode is >= 200 and < 300
            ? FindingVerdict.PotentialViolation : FindingVerdict.NoViolationObserved;
    }

    public static FindingAnalysis AnalyzeRecordedWrite(bool expectedAllowed, int? statusCode,
        RecordedWriteEvidence evidence)
    {
        if (evidence == RecordedWriteEvidence.Unavailable)
            return new(FindingVerdict.LimitedVerification, "Recorded protected state could not be verified.",
                "A snapshot was unavailable, invalid, or missing the protected field for the resource.");

        if (evidence == RecordedWriteEvidence.IntendedChange)
            return new(expectedAllowed ? FindingVerdict.NoViolationObserved : FindingVerdict.ConfirmedViolation,
                expectedAllowed ? "The expected authorized change was observed." : "The denied identity's intended change was observed.",
                "The final protected value matched the intended value and differed from the baseline.");

        if (expectedAllowed)
            return new(FindingVerdict.LimitedVerification, "The expected authorized change could not be verified.",
                "The recorded state did not demonstrate the intended change.");

        if (evidence == RecordedWriteEvidence.Unchanged && statusCode is 401 or 403 or 404)
            return new(FindingVerdict.NoViolationObserved, "The request was rejected and recorded state was unchanged.",
                "The baseline and final snapshots were semantically equal.");

        if (statusCode is >= 200 and < 300)
            return new(FindingVerdict.PotentialViolation, "The request returned success without a verified change.",
                "The recorded state did not demonstrate the intended change.");

        return new(FindingVerdict.LimitedVerification, "The recorded result was inconclusive.",
            "The response and snapshots did not establish either rejection with unchanged state or the intended change.");
    }

    public static FindingAnalysis AnalyzeRead(bool expectedAllowed, int? statusCode,
        bool baselineVerified, bool protectedDataMatched, string? fingerprint, string? error)
    {
        if (error is not null || !baselineVerified)
            return new(FindingVerdict.LimitedVerification, "Request or owner baseline could not be verified.", "No reliable comparison available.");
        if (!expectedAllowed && protectedDataMatched)
            return new(FindingVerdict.ConfirmedViolation, "Unauthorized identity received the resource's protected fields verified against the owner response.",
                $"Resource ID and protected fields matched; SHA-256={fingerprint}. Response content omitted.");
        if (expectedAllowed)
            return protectedDataMatched
                ? new(FindingVerdict.NoViolationObserved, "Authorized read matched the owner baseline.", "Resource ID and protected fields matched.")
                : new(FindingVerdict.LimitedVerification, "Expected authorized read could not be verified.", "Protected data did not match the baseline.");
        if (statusCode is 401 or 403 or 404)
            return new(FindingVerdict.NoViolationObserved, "Unauthorized read was rejected and no matching protected data was observed.", $"HTTP {statusCode}; verified owner baseline.");
        if (statusCode is >= 200 and < 300)
            return new(FindingVerdict.PotentialViolation, "Successful status without proof of protected data disclosure.", $"HTTP {statusCode}; protected data did not match.");
        return new(FindingVerdict.LimitedVerification, "Unexpected response prevents a reliable authorization decision.", $"HTTP {statusCode}.");
    }
}
