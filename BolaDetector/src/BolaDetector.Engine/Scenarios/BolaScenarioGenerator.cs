using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Engine;

public sealed class BolaScenarioGenerator(IAuthorizationOracle oracle) : IScenarioGenerator
{
    public IReadOnlyList<TestScenario> Generate(DetectionConfiguration configuration)
    {
        if (configuration.Identities.Count == 0 || configuration.Resources.Count == 0 || configuration.Operations.Count == 0)
            throw new ArgumentException("Identities, resources and operations are required.");
        if (configuration.Identities.Select(i => i.Identity.Id).Distinct().Count() != configuration.Identities.Count)
            throw new ArgumentException("Identity IDs must be unique.");
        var scenarios = new List<TestScenario>();
        foreach (var operation in configuration.Operations)
        {
            // This first executor supports reads only. Never infer mutation semantics from a verb.
            if (operation.Operation.Method != "GET" || operation.Action != "read")
                throw new ArgumentException("The first executor supports GET/read operations only.");
            var rules = configuration.Rules.Where(r => r.ResourceType == operation.ResourceType && r.Action == operation.Action).ToArray();
            if (rules.Length != 1) throw new ArgumentException("Exactly one explicit rule per resource type and action is required.");
            foreach (var resource in configuration.Resources.Where(r => r.ResourceType == operation.ResourceType))
            {
                var owner = configuration.Identities.SingleOrDefault(i => i.Identity.Id == resource.OwnerId)
                    ?? throw new ArgumentException("Every resource needs a configured owner identity.");
                if (!oracle.IsAllowed(owner.Identity, resource, rules[0]))
                    throw new ArgumentException("The resource owner must be authorized for baseline verification.");
                foreach (var identity in configuration.Identities)
                    scenarios.Add(new TestScenario(Guid.NewGuid(), $"{identity.Name}: {operation.Operation.OperationId} / {resource.Id}", [operation.Operation])
                    {
                        TestUserName = identity.Name,
                        Identity = identity.Identity,
                        OwnerIdentity = owner.Identity,
                        Resource = resource,
                        ExpectedAllowed = oracle.IsAllowed(identity.Identity, resource, rules[0])
                    });
            }
        }
        return scenarios;
    }
}
