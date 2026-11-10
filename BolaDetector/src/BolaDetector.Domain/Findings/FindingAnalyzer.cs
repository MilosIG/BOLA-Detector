namespace BolaDetector.Domain;

public static class FindingAnalyzer
{
    public static FindingVerdict Analyze(bool expectedAllowed, int statusCode, ResourceSnapshot before, ResourceSnapshot after)
    {
        if (expectedAllowed) return FindingVerdict.NoViolationObserved;
        if (!before.IsReadable || !after.IsReadable || before.Content is null || after.Content is null || before.ResourceId != after.ResourceId) return FindingVerdict.LimitedVerification;
        if (before.Content != after.Content) return FindingVerdict.ConfirmedViolation;
        return statusCode is >= 200 and < 300
            ? FindingVerdict.PotentialViolation : FindingVerdict.NoViolationObserved;
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
