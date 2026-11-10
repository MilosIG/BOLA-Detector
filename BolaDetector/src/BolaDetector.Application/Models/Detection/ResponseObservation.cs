using BolaDetector.Domain;

namespace BolaDetector.Application;

public record ResponseObservation(int? StatusCode, int BodyBytes, string? Error);
