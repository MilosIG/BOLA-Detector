namespace BolaDetector.Application;

// Transient verification input only: never serialize this type as a report.
public record ObservedResponse(string Path, ResponseObservation Observation, string? Body);
