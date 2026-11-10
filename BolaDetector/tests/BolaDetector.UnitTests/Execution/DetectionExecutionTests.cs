using System.Net;
using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using NUnit.Framework;
using static BolaDetector.UnitTests.DetectionTestFixture;
namespace BolaDetector.UnitTests;

public class DetectionExecutionTests
{
    [Test]
    public async Task HttpExecutionSubstitutesIdAndUsesSeparateIdentities()
    {
        var config = Config(); var scenario = Scenario(config);
        var seen = new List<string>();
        using var client = new HttpClient(new StubHandler((request, ct) =>
        {
            seen.Add(request.Headers.GetValues("X-Test-User").Single());
            Assert.That(request.RequestUri!.AbsolutePath, Does.EndWith(scenario.Resource!.Id));
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            return Task.FromResult(JsonResponse(scenario.Resource.Id));
        }));
        var result = await CreateEngine(client, config).ExecuteAsync(scenario, default);
        Assert.That(seen, Is.EqualTo(new[] { scenario.OwnerIdentity!.Id.ToString(), scenario.Identity!.Id.ToString() }));
        Assert.That(result.Result!.Verdict, Is.EqualTo(FindingVerdict.ConfirmedViolation));
        Assert.That(JsonSerializer.Serialize(result), Does.Not.Contain("private-test-content"));
        Assert.That(JsonSerializer.Serialize(result), Does.Not.Contain("CredentialReference"));
    }
    [Test]
    public async Task UnavailableApiIsLimitedAndFailed()
    {
        var config = Config();
        using var client = new HttpClient(new StubHandler((_, _) => throw new HttpRequestException("secret-must-not-leak")));
        var result = await CreateEngine(client, config).ExecuteAsync(Scenario(config), default);
        Assert.That(result.Status, Is.EqualTo(ExecutionStatus.Failed));
        Assert.That(result.Result!.Verdict, Is.EqualTo(FindingVerdict.LimitedVerification));
        Assert.That(JsonSerializer.Serialize(result), Does.Not.Contain("secret-must-not-leak"));
    }
    [Test]
    public async Task FailedBaselineNeverConfirms()
    {
        var config = Config(); var scenario = Scenario(config); var calls = 0;
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.Forbidden) : JsonResponse(scenario.Resource!.Id))));
        var result = await CreateEngine(client, config).ExecuteAsync(scenario, default);
        Assert.That(result.Result!.Verdict, Is.EqualTo(FindingVerdict.LimitedVerification));
    }
    [Test]
    public async Task SuccessWithoutProtectedFieldsRemainsPotential()
    {
        var config = Config(); var scenario = Scenario(config); var calls = 0;
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(scenario.Resource!.Id) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") })));
        var result = await CreateEngine(client, config).ExecuteAsync(scenario, default);
        Assert.That(result.Result!.Verdict, Is.EqualTo(FindingVerdict.PotentialViolation));
    }
    [Test]
    public async Task TimeoutIsControlled()
    {
        var config = Config();
        using var client = new HttpClient(new StubHandler((_, _) => throw new TaskCanceledException()));
        var result = await CreateEngine(client, config).ExecuteAsync(Scenario(config), default);
        Assert.That(result.Result!.Response.Error, Is.EqualTo("Timeout"));
    }
    [Test]
    public void CallerCancellationPropagates()
    {
        var config = Config(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        using var client = new HttpClient(new StubHandler((_, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(JsonResponse("1")); }));
        Assert.ThrowsAsync<OperationCanceledException>(async () => await CreateEngine(client, config)
            .ExecuteAsync(Scenario(config), cancellation.Token));
    }

    [TestCase("not-json")]
    [TestCase("{\"id\":\"wrong-id\",\"title\":\"private-test-content\"}")]
    [TestCase("{\"id\":\"doc-1\",\"title\":null}")]
    public async Task InvalidBaselineHasLimitedVerification(string body)
    {
        var config = Config();
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        var result = await CreateEngine(client, config).ExecuteAsync(Scenario(config), default);
        Assert.That(result.Result!.Verdict, Is.EqualTo(FindingVerdict.LimitedVerification));
    }
    [Test]
    public async Task LargeResponsesAreBounded()
    {
        var config = Config();
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 70000)) })));
        var result = await CreateEngine(client, config).ExecuteAsync(Scenario(config), default);
        Assert.That(result.Result!.Response.Error, Is.EqualTo("ResponseTooLarge"));
        Assert.That(result.Result.Verdict, Is.EqualTo(FindingVerdict.LimitedVerification));
    }
}
