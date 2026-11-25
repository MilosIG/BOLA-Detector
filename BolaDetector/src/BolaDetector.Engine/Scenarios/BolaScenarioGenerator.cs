using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Engine;

public sealed class BolaScenarioGenerator(IAuthorizationOracle oracle) : IScenarioGenerator
{
    public IReadOnlyList<TestScenario> Generate(DetectionConfiguration configuration)
    {
        try
        {
            return Generate(ValidatedDetectionConfiguration.Create(configuration));
        }
        catch (ConfigurationValidationException error)
        {
            throw new ArgumentException("Detection configuration is invalid.", nameof(configuration),
                error);
        }
    }

    public IReadOnlyList<TestScenario> Generate(ValidatedDetectionConfiguration configuration)
    {
        var owners = configuration.Identities.ToDictionary(entry => entry.Identity.Id);
        var rules = configuration.Rules.ToDictionary(rule => (rule.ResourceType, rule.Action));
        var scenarios = new List<TestScenario>();
        foreach (var operation in configuration.Operations)
        {
            var rule = rules[(operation.ResourceType, operation.Action)];
            foreach (var resource in configuration.Resources.Where(r => r.ResourceType == operation.ResourceType))
            {
                var owner = owners[resource.OwnerId];
                if (!oracle.IsAllowed(owner.Identity, resource, rule))
                    throw new ArgumentException("The resource owner must be authorized for baseline verification.");
                foreach (var identity in configuration.Identities)
                    scenarios.Add(new TestScenario(Guid.NewGuid(), $"{identity.Name}: {operation.Operation.OperationId} / {resource.Id}", [operation.Operation])
                    {
                        TestUserName = identity.Name,
                        Identity = identity.Identity,
                        OwnerIdentity = owner.Identity,
                        Resource = resource,
                        ExpectedAllowed = oracle.IsAllowed(identity.Identity, resource, rule)
                    });
            }
        }
        return scenarios;
    }
}
