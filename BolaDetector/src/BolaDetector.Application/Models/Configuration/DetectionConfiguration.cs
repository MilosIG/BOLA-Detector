using BolaDetector.Domain;

namespace BolaDetector.Application;

public sealed class DetectionConfiguration
{
    public string TargetOrigin { get; init; } = "http://127.0.0.1:5081";
    public int TimeoutSeconds { get; init; } = 5;
    public List<ConfiguredIdentity> Identities { get; init; } = [];
    public List<TestResource> Resources { get; init; } = [];
    public List<ConfiguredOperation> Operations { get; init; } = [];
    public List<AccessRule> Rules { get; init; } = [];
    public List<string> ProtectedProperties { get; init; } = ["title"];
}
