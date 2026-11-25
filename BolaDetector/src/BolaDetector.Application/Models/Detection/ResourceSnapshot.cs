namespace BolaDetector.Application;

/// <summary>Previously collected input whose content has not yet been verified.</summary>
public sealed record ResourceSnapshot(string ResourceId, string? Content, bool IsReadable);
