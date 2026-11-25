namespace BolaDetector.Application;

public sealed record ConfigurationValidationError(string Code, string Path, string Message);
