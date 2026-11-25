using BolaDetector.Application;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;

namespace BolaDetector.UnitTests;

public class ValidatedDetectionConfigurationTests
{
    [Test]
    public void Create_WhenSourceListsChange_KeepsAllSnapshotCollections()
    {
        var input = Config();
        var snapshot = ValidatedDetectionConfiguration.Create(input);
        var counts = new[] { input.Identities.Count, input.Resources.Count, input.Operations.Count, input.Rules.Count, input.ProtectedProperties.Count };

        input.Identities.Clear();
        input.Resources.Clear();
        input.Operations.Clear();
        input.Rules.Clear();
        input.ProtectedProperties.Clear();

        Assert.That(new[] { snapshot.Identities.Count, snapshot.Resources.Count, snapshot.Operations.Count, snapshot.Rules.Count, snapshot.ProtectedProperties.Count },
            Is.EqualTo(counts));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.ProtectedProperties)[0] = "changed");
        Assert.That(snapshot.ProtectedProperties.Single(), Is.EqualTo("title"));
    }

    [Test]
    public void Create_WhenSourceReplacesNestedValues_KeepsOriginalIdentityAndOperation()
    {
        var input = Config();
        var identity = input.Identities[0];
        var operation = input.Operations[0];
        var snapshot = ValidatedDetectionConfiguration.Create(input);

        input.Identities[0] = identity with { Identity = identity.Identity with { Role = "changed" } };
        input.Operations[0] = operation with { Operation = operation.Operation with { Path = "/other/{id}" } };

        Assert.That(snapshot.Identities[0].Identity.Role, Is.EqualTo(identity.Identity.Role));
        Assert.That(snapshot.Operations[0].Operation.Path, Is.EqualTo(operation.Operation.Path));
        Assert.That(snapshot.Identities[0].Identity, Is.Not.EqualTo(input.Identities[0].Identity));
        Assert.That(snapshot.Operations[0].Operation, Is.Not.EqualTo(input.Operations[0].Operation));
    }
}
