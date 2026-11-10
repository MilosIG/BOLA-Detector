using System.Xml.Linq;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using NUnit.Framework;
namespace BolaDetector.UnitTests;

public class DependencyRulesTests
{
    [TestCase(typeof(AccessRule))]
    [TestCase(typeof(RunDetection))]
    [TestCase(typeof(DetectionEngine))]
    public void InnerAssembliesDoNotReferenceInfrastructureOrFrameworks(Type representativeType)
    {
        var names = representativeType.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.That(names.Any(n => n.StartsWith("Microsoft.AspNetCore") || n.StartsWith("Microsoft.EntityFrameworkCore") ||
            n.StartsWith("Microsoft.Extensions") || n.StartsWith("Microsoft.OpenApi") || n == "BolaDetector.Infrastructure"), Is.False);
    }

    [Test]
    public void ProductionProjectReferencesPointTowardTheCore()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BolaDetector.sln"))) directory = directory.Parent;
        Assert.That(directory, Is.Not.Null, "Run architecture checks from the source checkout.");
        var expected = new Dictionary<string, string[]>
        {
            ["BolaDetector.Domain"] = [],
            ["BolaDetector.Application"] = ["BolaDetector.Domain"],
            ["BolaDetector.Engine"] = ["BolaDetector.Domain", "BolaDetector.Application"],
            ["BolaDetector.Infrastructure"] = ["BolaDetector.Domain", "BolaDetector.Application", "BolaDetector.Engine"],
            ["BolaDetector.Api"] = ["BolaDetector.Application", "BolaDetector.Infrastructure", "BolaDetector.Engine"],
            ["BolaDetector.Demo"] = ["BolaDetector.Infrastructure"]
        };
        foreach (var (project, dependencies) in expected)
        {
            var document = XDocument.Load(Path.Combine(directory!.FullName, "src", project, project + ".csproj"));
            var references = document.Descendants("ProjectReference").Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value)).ToArray();
            Assert.That(references, Is.EquivalentTo(dependencies), project);
            if (project is "BolaDetector.Domain" or "BolaDetector.Application" or "BolaDetector.Engine")
            {
                Assert.That(document.Descendants("PackageReference"), Is.Empty, project);
                Assert.That(document.Descendants("FrameworkReference"), Is.Empty, project);
            }
        }
    }
}
