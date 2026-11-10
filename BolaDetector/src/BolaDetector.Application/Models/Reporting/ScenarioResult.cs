using BolaDetector.Domain;

namespace BolaDetector.Application;

public record ScenarioResult(string ScenarioName, string TestUser, string ResourceId,
    string Method, string Path, bool ExpectedAllowed, ResponseObservation Response,
    FindingVerdict Verdict, string Explanation, string Evidence);
