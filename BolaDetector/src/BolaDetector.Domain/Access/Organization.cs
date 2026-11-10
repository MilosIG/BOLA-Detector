namespace BolaDetector.Domain;

public record Organization(Guid Id, string Name);
// Store only a secret reference; credentials must not be committed or logged.
