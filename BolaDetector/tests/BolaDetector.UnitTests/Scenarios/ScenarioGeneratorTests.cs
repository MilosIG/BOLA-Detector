using System.Net;
using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;
namespace BolaDetector.UnitTests;

public class ScenarioGeneratorTests
{
    [Test]
    public void GeneratorCombinesAllIdentitiesResourcesAndOperations()
    {
        var scenarios = new BolaScenarioGenerator(new AuthorizationOracle()).Generate(Config());
        Assert.That(scenarios, Has.Count.EqualTo(8));
        Assert.That(scenarios.Count(s => s.ExpectedAllowed), Is.EqualTo(4));
        Assert.That(scenarios.Count(s => !s.ExpectedAllowed), Is.EqualTo(4));
        Assert.That(scenarios.Where(s => s.ExpectedAllowed).All(s => s.Identity!.Id == s.Resource!.OwnerId), Is.True);
    }
    [Test]
    public void GeneratorUsesInjectedOracle()
    {
        var config = Config();
        var scenarios = new BolaScenarioGenerator(new AllowAllOracle()).Generate(config);
        Assert.That(scenarios.All(s => s.ExpectedAllowed), Is.True);
    }
    private sealed class AllowAllOracle : IAuthorizationOracle
    { public bool IsAllowed(TestIdentity i, TestResource r, AccessRule rule) => true; }

    [Test]
    public void MissingOrAmbiguousRulesAreRejected()
    {
        var config = Config();
        config.Rules.Add(config.Rules[0]);
        Assert.Throws<ArgumentException>(() => new BolaScenarioGenerator(new AuthorizationOracle()).Generate(config));
        config.Rules.Clear();
        Assert.Throws<ArgumentException>(() => new BolaScenarioGenerator(new AuthorizationOracle()).Generate(config));
    }
}
