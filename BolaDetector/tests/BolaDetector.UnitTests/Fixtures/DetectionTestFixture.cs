using System.Net;
using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;
namespace BolaDetector.UnitTests;

internal static class DetectionTestFixture
{
    public static DetectionConfiguration Config() => JsonSerializer.Deserialize<DetectionConfiguration>(
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "demo-detection.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    public static TestScenario Scenario(DetectionConfiguration config) => new BolaScenarioGenerator(new AuthorizationOracle())
        .Generate(config).First(s => !s.ExpectedAllowed);

    public static DetectionEngine CreateEngine(HttpClient client, DetectionConfiguration config) =>
        new(new HttpScenarioRequestExecutor(client, new FixtureCredentials(), config, new LocalTargetAddressPolicy()),
            new JsonReadEvidenceVerifier(config), TimeProvider.System);
    public static HttpResponseMessage JsonResponse(string id) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { id, title = "private-test-content" })) };
    internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    internal sealed class FixtureCredentials : ITestCredentialProvider
    { public string Resolve(TestIdentity identity) => identity.Id.ToString(); }
}
