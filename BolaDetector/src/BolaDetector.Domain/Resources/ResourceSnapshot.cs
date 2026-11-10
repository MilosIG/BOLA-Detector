namespace BolaDetector.Domain;

public record ResourceSnapshot(string ResourceId, string? Content, bool IsReadable);
