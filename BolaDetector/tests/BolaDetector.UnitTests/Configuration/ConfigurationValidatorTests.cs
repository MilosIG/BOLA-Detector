using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;

namespace BolaDetector.UnitTests;

public class ConfigurationValidatorTests
{
    [Test]
    public void Validate_WhenDemoConfigurationIsUsed_ReturnsNoErrors() =>
        Assert.That(ConfigurationValidator.Validate(Config()), Is.Empty);

    [Test]
    public void Validate_WhenConfigurationIsNull_IdentifiesRoot() =>
        AssertError(ConfigurationValidator.Validate(null), "CFG_REQUIRED", "$");

    [Test]
    public void Validate_WhenCollectionsAreNullEmptyOrContainNull_ReportsEachPath()
    {
        var configuration = new DetectionConfiguration
        {
            Identities = null!,
            Resources = [],
            Operations = [null!],
            Rules = [null!],
            ProtectedProperties = [null!]
        };

        var errors = ConfigurationValidator.Validate(configuration);

        AssertError(errors, "CFG_REQUIRED_COLLECTION", "identities");
        AssertError(errors, "CFG_REQUIRED_COLLECTION", "resources");
        AssertError(errors, "CFG_REQUIRED", "operations[0]");
        AssertError(errors, "CFG_REQUIRED", "rules[0]");
        AssertError(errors, "CFG_REQUIRED", "protectedProperties[0]");
    }

    [Test]
    public void Validate_WhenEntriesAreIncomplete_ReportsNestedPaths()
    {
        var configuration = Config();
        configuration.Identities[0] = new(" ", null!);
        configuration.Identities[1] = new("member", new(Guid.Empty, Guid.Empty, " ", null!));
        configuration.Resources[0] = new(" ", Guid.Empty, Guid.Empty, " ");
        configuration.Operations[0] = new(null!, " ", " ");

        var errors = ConfigurationValidator.Validate(configuration);

        AssertError(errors, "CFG_REQUIRED", "identities[0].name");
        AssertError(errors, "CFG_REQUIRED", "identities[0].identity");
        AssertError(errors, "CFG_REQUIRED", "identities[1].identity.id");
        AssertError(errors, "CFG_REQUIRED", "identities[1].identity.organizationId");
        AssertError(errors, "CFG_REQUIRED", "identities[1].identity.role");
        AssertError(errors, "CFG_REQUIRED", "resources[0].id");
        AssertError(errors, "CFG_REQUIRED", "resources[0].ownerId");
        AssertError(errors, "CFG_REQUIRED", "resources[0].organizationId");
        AssertError(errors, "CFG_REQUIRED", "resources[0].resourceType");
        AssertError(errors, "CFG_REQUIRED", "operations[0].operation");
    }

    [Test]
    public void Validate_WhenIdentifiersRepeat_ReportsDuplicatesByDomainScope()
    {
        var configuration = Config();
        configuration.Identities.Add(configuration.Identities[0]);
        configuration.Resources.Add(configuration.Resources[0]);
        configuration.Operations.Add(configuration.Operations[0]);

        var errors = ConfigurationValidator.Validate(configuration);

        AssertError(errors, "CFG_DUPLICATE_ID", "identities[2].identity.id");
        AssertError(errors, "CFG_DUPLICATE_ID", "resources[2].id");
        AssertError(errors, "CFG_DUPLICATE_ID", "operations[2].operation.operationId");
    }

    [Test]
    public void Validate_WhenReferencesAreUnknownOrRulesAreAmbiguous_ReportsTheirPaths()
    {
        var configuration = Config();
        configuration.Resources[0] = configuration.Resources[0] with { OwnerId = Guid.NewGuid() };
        configuration.Operations[1] = configuration.Operations[1] with { ResourceType = "unconfigured" };
        configuration.Rules.Add(configuration.Rules[0]);
        configuration.Rules.Add(new("unknown", "read", true, true));

        var errors = ConfigurationValidator.Validate(configuration);

        AssertError(errors, "CFG_INVALID_REFERENCE", "resources[0].ownerId");
        AssertError(errors, "CFG_INVALID_REFERENCE", "operations[1].resourceType");
        AssertError(errors, "CFG_INVALID_REFERENCE", "rules[2].resourceType");
        AssertError(errors, "CFG_AMBIGUOUS_RULE", "rules[1]");
        AssertError(errors, "CFG_MISSING_RULE", "operations[1].action");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Validate_WhenOwnerOrganizationDiffers_UsesConfiguredRule(bool requireSameOrganization)
    {
        var configuration = Config();
        configuration.Resources[0] = configuration.Resources[0] with { OrganizationId = Guid.NewGuid() };
        configuration.Rules[0] = configuration.Rules[0] with { RequireSameOrganization = requireSameOrganization };

        var errors = ConfigurationValidator.Validate(configuration);

        if (requireSameOrganization)
            AssertError(errors, "CFG_OWNER_BASELINE_DENIED", "resources[0].ownerId");
        else
            Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Validate_WhenOperationAndProofSettingsAreInvalid_ReportsEachIssue()
    {
        var configuration = Config();
        configuration.Operations[0] = new(new("op", "PUT", "/documents/{id}/{unexpected}"), "document", "write");
        configuration.ProtectedProperties.AddRange(["id", " ", "title"]);

        var errors = ConfigurationValidator.Validate(configuration);

        AssertError(errors, "CFG_UNSUPPORTED_OPERATION", "operations[0].operation.method");
        AssertError(errors, "CFG_INVALID_ROUTE", "operations[0].operation.path");
        AssertError(errors, "CFG_UNSUPPORTED_OPERATION", "operations[0].action");
        AssertError(errors, "CFG_INVALID_VALUE", "protectedProperties[1]");
        AssertError(errors, "CFG_INVALID_VALUE", "protectedProperties[2]");
        AssertError(errors, "CFG_DUPLICATE_VALUE", "protectedProperties[3]");
    }

    [Test]
    public void Validate_WhenOriginAndTimeoutAreInvalid_RejectsBoth()
    {
        var errors = ConfigurationValidator.Validate(new DetectionConfiguration
        {
            TargetOrigin = "http://localhost:5081",
            TimeoutSeconds = 61
        });

        AssertError(errors, "CFG_INVALID_ORIGIN", "targetOrigin");
        AssertError(errors, "CFG_OUT_OF_RANGE", "timeoutSeconds");
    }

    [Test]
    public void Validate_WhenCredentialReferenceIsInvalid_ReturnsStableRedactedErrors()
    {
        const string sensitive = "synthetic-secret-do-not-print";
        var configuration = Config();
        var entry = configuration.Identities[0];
        configuration.Identities[0] = entry with { Identity = entry.Identity with { CredentialReference = sensitive } };
        configuration.Rules.Clear();

        var errors = ConfigurationValidator.Validate(configuration);

        AssertError(errors, "CFG_INVALID_CREDENTIAL_REFERENCE", "identities[0].identity.credentialReference");
        AssertError(errors, "CFG_MISSING_RULE", "operations[0].action");
        Assert.That(ConfigurationValidator.Validate(configuration), Is.EqualTo(errors));
        Assert.That(string.Join("\n", errors), Does.Not.Contain(sensitive));
    }

    [Test]
    public void Generate_WhenCalledDirectlyWithInvalidConfiguration_RejectsBeforeGenerating()
    {
        var configuration = Config();
        configuration.ProtectedProperties.Clear();
        var generator = new BolaScenarioGenerator(new AuthorizationOracle());

        var error = Assert.Throws<ArgumentException>(() => generator.Generate(configuration));

        Assert.That(error!.InnerException, Is.TypeOf<ConfigurationValidationException>());
    }

    private static void AssertError(IEnumerable<ConfigurationValidationError> errors, string code, string path) =>
        Assert.That(errors.Select(error => (error.Code, error.Path)), Does.Contain((code, path)));
}
