using System.Xml.Linq;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using NUnit.Framework;
namespace BolaDetector.UnitTests;

public class DependencyRulesTests
{
    [TestCase(typeof(AccessRule), new string[0])]
    [TestCase(typeof(RunDetection), new[] { "BolaDetector.Domain" })]
    [TestCase(typeof(DetectionEngine), new[] { "BolaDetector.Application", "BolaDetector.Domain" })]
    public void InnerAssemblies_CompiledReferences_RespectLayerBoundaries(Type representativeType, string[] allowed)
    {
        var names = representativeType.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        var projectReferences = names.Where(name => name.StartsWith("BolaDetector.", StringComparison.Ordinal) ||
            name == "BolaTestApi");
        Assert.That(projectReferences, Is.SubsetOf(allowed));
        Assert.That(names, Does.Not.Contain("System.Net.Http"));
        Assert.That(names, Has.None.Matches<string>(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.OpenApi", StringComparison.Ordinal) ||
            name.StartsWith("Pomelo.EntityFrameworkCore", StringComparison.Ordinal)));
    }

    [TestCase(typeof(FindingAnalyzer))]
    [TestCase(typeof(RunDetection))]
    [TestCase(typeof(DetectionEngine))]
    public void InnerAssemblies_CompiledReferences_ExcludeJsonSerialization(Type representativeType)
    {
        var names = representativeType.Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.That(names, Does.Not.Contain("System.Text.Json"));
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
            ["BolaDetector.Demo"] = ["BolaDetector.Application", "BolaDetector.Domain", "BolaDetector.Infrastructure"],
            ["BolaTestApi"] = []
        };
        var root = directory!.FullName;
        foreach (var (project, dependencies) in expected)
        {
            var path = GetProjectPath(root, project);
            var document = XDocument.Load(path);
            var references = document.Descendants("ProjectReference")
                .Select(reference => Path.GetFullPath(reference.Attribute("Include")!.Value, Path.GetDirectoryName(path)!));
            Assert.That(references, Is.EquivalentTo(dependencies.Select(dependency => GetProjectPath(root, dependency))), project);
            if (project is "BolaDetector.Domain" or "BolaDetector.Application" or "BolaDetector.Engine")
            {
                Assert.That(document.Descendants("PackageReference"), Is.Empty, project);
                Assert.That(document.Descendants("FrameworkReference"), Is.Empty, project);
            }
        }
    }

    private static string GetProjectPath(string root, string project) =>
        Path.Combine(root, project == "BolaTestApi" ? "targets" : "src", project, project + ".csproj");
}
