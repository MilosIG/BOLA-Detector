namespace BolaDetector.Domain;

public record ReplayArtifact(Guid ExecutionId, string SanitizedRequests, string Preconditions);
