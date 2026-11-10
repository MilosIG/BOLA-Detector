namespace BolaDetector.Domain;

public record AccessRule(string ResourceType, string Action, bool RequireOwner, bool RequireSameOrganization)
{
    public bool Allows(TestIdentity identity, TestResource resource) =>
        ResourceType == resource.ResourceType &&
        (!RequireOwner || identity.Id == resource.OwnerId) &&
        (!RequireSameOrganization || identity.OrganizationId == resource.OrganizationId);
}
