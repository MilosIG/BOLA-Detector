namespace BolaDetector.Domain;

public record TestScenario(Guid Id, string Name, IReadOnlyList<ApiOperation> Steps)
{
    public string? TestUserName { get; init; }
    public TestIdentity? Identity { get; init; }
    public TestIdentity? OwnerIdentity { get; init; }
    public TestResource? Resource { get; init; }
    public bool ExpectedAllowed { get; init; }
}
