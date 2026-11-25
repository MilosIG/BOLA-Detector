namespace BolaDetector.Domain;

public sealed record TestIdentity(Guid Id, Guid OrganizationId, string Role, string CredentialReference);
