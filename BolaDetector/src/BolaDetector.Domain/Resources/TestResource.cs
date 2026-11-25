namespace BolaDetector.Domain;

public sealed record TestResource(string Id, Guid OwnerId, Guid OrganizationId, string ResourceType);
