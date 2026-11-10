using BolaDetector.Domain;
namespace BolaDetector.Application;

public interface IScenarioGenerator
{
    IReadOnlyList<TestScenario> Generate(DetectionConfiguration configuration);
}
