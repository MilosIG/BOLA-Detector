using BolaDetector.Domain;

namespace BolaDetector.Application;

public static class ConfigurationValidator
{
    public static void EnsureValid(DetectionConfiguration? configuration)
    {
        var errors = Validate(configuration);
        if (errors.Count != 0)
            throw new ConfigurationValidationException(errors);
    }

    public static IReadOnlyList<ConfigurationValidationError> Validate(DetectionConfiguration? configuration)
    {
        if (configuration is null)
            return [new("CFG_REQUIRED", "$", "A configuration object is required.")];

        var validation = new Validation();
        validation.Check(configuration);
        return validation.Errors.AsReadOnly();
    }

    private sealed class Validation
    {
        public List<ConfigurationValidationError> Errors { get; } = [];
        private readonly Dictionary<Guid, TestIdentity> identities = [];
        private readonly HashSet<string> resourceTypes = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Type, string Action), AccessRule?> rules = [];
        private readonly HashSet<string> baselineTypes = new(StringComparer.Ordinal);

        public void Check(DetectionConfiguration configuration)
        {
            if (!DetectionInputPolicy.TryGetTargetOrigin(configuration.TargetOrigin, out _))
                Add("CFG_INVALID_ORIGIN", "targetOrigin", $"Only {DetectionInputPolicy.LocalOrigin} is allowed.");
            if (configuration.TimeoutSeconds is < 1 or > 60)
                Add("CFG_OUT_OF_RANGE", "timeoutSeconds", "Timeout must be between 1 and 60 seconds.");

            CheckCollection(configuration.Identities, "identities", CheckIdentity);
            var resourceIds = new HashSet<(string Type, string Id)>();
            CheckCollection(configuration.Resources, "resources", (resource, path) => CheckResource(resource, path, resourceIds));
            CheckCollection(configuration.Rules, "rules", CheckRule);
            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            CheckCollection(configuration.Operations, "operations", (operation, path) => CheckOperation(operation, path, operationIds));
            CheckOwnerBaselines(configuration.Resources);

            var fields = new HashSet<string>(StringComparer.Ordinal);
            CheckCollection(configuration.ProtectedProperties, "protectedProperties", (field, path) =>
            {
                if (!DetectionInputPolicy.IsProtectedProperty(field))
                    Add("CFG_INVALID_VALUE", path, "A nonblank protected property other than id is required.");
                else if (!fields.Add(field))
                    Add("CFG_DUPLICATE_VALUE", path, "Duplicate protected property.");
            });
        }

        private void CheckIdentity(ConfiguredIdentity entry, string path)
        {
            RequireText(entry.Name, path + ".name");
            if (entry.Identity is not { } identity)
            {
                Add("CFG_REQUIRED", path + ".identity", "An identity object is required.");
                return;
            }

            if (RequireId(identity.Id, path + ".identity.id") && !identities.TryAdd(identity.Id, identity))
                Add("CFG_DUPLICATE_ID", path + ".identity.id", "Duplicate identity identifier.");
            RequireId(identity.OrganizationId, path + ".identity.organizationId");
            RequireText(identity.Role, path + ".identity.role");
            if (!DetectionInputPolicy.TryGetCredentialVariable(identity.CredentialReference, out _))
                Add("CFG_INVALID_CREDENTIAL_REFERENCE", path + ".identity.credentialReference", "Use an env: reference with a nonblank variable name.");
        }

        private void CheckResource(TestResource resource, string path, HashSet<(string Type, string Id)> resourceIds)
        {
            var hasId = RequireText(resource.Id, path + ".id");
            if (hasId && !DetectionInputPolicy.IsSupportedResourceId(resource.Id))
                Add("CFG_INVALID_VALUE", path + ".id", "Resource identifier cannot be a path navigation segment.");

            if (RequireText(resource.ResourceType, path + ".resourceType"))
            {
                if (hasId && !resourceIds.Add((resource.ResourceType, resource.Id)))
                    Add("CFG_DUPLICATE_ID", path + ".id", "Duplicate identifier for this resource type.");
                resourceTypes.Add(resource.ResourceType);
            }

            RequireId(resource.OrganizationId, path + ".organizationId");
            if (RequireId(resource.OwnerId, path + ".ownerId") && !identities.ContainsKey(resource.OwnerId))
                Add("CFG_INVALID_REFERENCE", path + ".ownerId", "Owner must reference a configured identity.");
        }

        private void CheckRule(AccessRule rule, string path)
        {
            var hasType = RequireText(rule.ResourceType, path + ".resourceType");
            var hasAction = RequireText(rule.Action, path + ".action");
            if (hasType && !resourceTypes.Contains(rule.ResourceType))
                Add("CFG_INVALID_REFERENCE", path + ".resourceType", "Rule must reference a configured resource type.");
            if (hasAction && rule.Action != "read")
                Add("CFG_UNSUPPORTED_OPERATION", path + ".action", "Only the read action is supported.");
            if (!hasType || !hasAction)
                return;

            var key = (rule.ResourceType, rule.Action);
            if (!rules.TryAdd(key, rule))
            {
                rules[key] = null;
                Add("CFG_AMBIGUOUS_RULE", path, "Multiple rules apply to the same resource type and action.");
            }
        }

        private void CheckOperation(ConfiguredOperation entry, string path, HashSet<string> operationIds)
        {
            if (entry.Operation is { } operation)
            {
                if (RequireText(operation.OperationId, path + ".operation.operationId") && !operationIds.Add(operation.OperationId))
                    Add("CFG_DUPLICATE_ID", path + ".operation.operationId", "Duplicate operation identifier.");
                if (RequireText(operation.Method, path + ".operation.method") && operation.Method != "GET")
                    Add("CFG_UNSUPPORTED_OPERATION", path + ".operation.method", "Only the GET method is supported.");
                if (RequireText(operation.Path, path + ".operation.path") && !DetectionInputPolicy.IsSupportedRoute(operation.Path))
                    Add("CFG_INVALID_ROUTE", path + ".operation.path", "Use a local path with exactly one {id} placeholder.");
            }
            else
            {
                Add("CFG_REQUIRED", path + ".operation", "An operation object is required.");
            }

            var hasType = RequireText(entry.ResourceType, path + ".resourceType");
            var hasAction = RequireText(entry.Action, path + ".action");
            if (hasType && !resourceTypes.Contains(entry.ResourceType))
                Add("CFG_INVALID_REFERENCE", path + ".resourceType", "Operation must reference a configured resource type.");
            if (!hasAction)
                return;
            if (entry.Action != "read")
                Add("CFG_UNSUPPORTED_OPERATION", path + ".action", "Only the read action is supported.");
            else if (hasType && !rules.ContainsKey((entry.ResourceType, entry.Action)))
                Add("CFG_MISSING_RULE", path + ".action", "An access rule is required for this resource type and action.");
            else if (hasType)
                baselineTypes.Add(entry.ResourceType);
        }

        private void CheckOwnerBaselines(IReadOnlyList<TestResource>? resources)
        {
            if (resources is null)
                return;

            for (var index = 0; index < resources.Count; index++)
            {
                if (resources[index] is not { ResourceType: { } type } resource ||
                    !baselineTypes.Contains(type) || !rules.TryGetValue((type, "read"), out var rule) || rule is null)
                    continue;

                if (identities.TryGetValue(resource.OwnerId, out var owner) && !rule.Allows(owner, resource))
                    Add("CFG_OWNER_BASELINE_DENIED", $"resources[{index}].ownerId", "The applicable rule must allow the owner baseline read.");
            }
        }

        private void CheckCollection<T>(IReadOnlyList<T>? items, string path, Action<T, string> check) where T : class
        {
            if (items is not { Count: > 0 })
            {
                Add("CFG_REQUIRED_COLLECTION", path, items is null ? "Collection must not be null." : "Collection must contain at least one item.");
                return;
            }

            for (var index = 0; index < items.Count; index++)
            {
                var itemPath = $"{path}[{index}]";
                if (items[index] is { } item)
                    check(item, itemPath);
                else
                    Add("CFG_REQUIRED", itemPath, "Collection item must not be null.");
            }
        }

        private bool RequireId(Guid id, string path)
        {
            if (id != Guid.Empty)
                return true;
            Add("CFG_REQUIRED", path, "A nonempty identifier is required.");
            return false;
        }

        private bool RequireText(string? value, string path)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return true;
            Add("CFG_REQUIRED", path, "A nonblank value is required.");
            return false;
        }

        private void Add(string code, string path, string message) => Errors.Add(new(code, path, message));
    }
}
