using BolaDetector.Domain;

namespace BolaDetector.Application;

public record DetectionReport(DateTimeOffset CreatedAt, IReadOnlyList<ScenarioResult> Results,
    string Limitation = "Only the configured local operations and fixtures were tested; absence of findings does not prove API security.");
