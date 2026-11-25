using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using BolaDetector.Application;
using BolaDetector.Domain;
using BolaDetector.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace BolaDetector.IntegrationTests;

public class ConfigurationFlowTests
{
    [TestCase("", "CFG_EMPTY_FILE", "$")]
    [TestCase("{bad", "CFG_INVALID_JSON", "$")]
    [TestCase("null", "CFG_INVALID_TYPE", "$")]
    [TestCase("[]", "CFG_INVALID_TYPE", "$")]
    [TestCase("{}", "CFG_REQUIRED_COLLECTION", "identities")]
    [TestCase("{\"identities\":[],\"resources\":[],\"operations\":[],\"rules\":[],\"timeoutSeconds\":\"secret\"}", "CFG_INVALID_TYPE", "timeoutSeconds")]
    [TestCase("{\"identities\":[{\"identity\":{\"credentialReference\":{\"secret\":true}}}],\"resources\":[],\"operations\":[],\"rules\":[]}", "CFG_INVALID_TYPE", "identities[0].identity.credentialReference")]
    public void LoadAsync_WhenJsonStructureIsInvalid_ReturnsSafeDiagnostics(string json, string code, string path)
    {
        var error = Assert.ThrowsAsync<ConfigurationValidationException>(async () => await Load(json));

        Assert.That(error!.Errors.Select(item => (item.Code, item.Path)), Does.Contain((code, path)));
        Assert.That(string.Join("\n", error.Errors), Does.Not.Contain("secret"));
    }

    [TestCase("null", "CFG_REQUIRED_COLLECTION", "identities")]
    [TestCase("[]", "CFG_REQUIRED_COLLECTION", "identities")]
    [TestCase("[null]", "CFG_REQUIRED", "identities[0]")]
    [TestCase("[{\"identity\":null}]", "CFG_REQUIRED", "identities[0].name")]
    public async Task ExecuteAsync_WhenCollectionsAreInvalid_DoesNotGenerateOrSendRequests(string replacement, string code, string path)
    {
        var json = await DemoJson();
        json["identities"] = JsonNode.Parse(replacement);
        var configuration = await Load(json.ToJsonString());
        var generator = new CountingGenerator();
        var handler = new CountingHandler(configuration.Resources[0].Id);
        using var services = Services(handler, generator);

        var error = Assert.ThrowsAsync<ConfigurationValidationException>(async () =>
            await services.GetRequiredService<RunDetection>().ExecuteAsync(configuration, default));

        Assert.That(error!.Errors.Select(item => (item.Code, item.Path)), Does.Contain((code, path)));
        Assert.That(generator.Calls, Is.Zero);
        Assert.That(handler.Calls, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_WhenInputIsInvalid_CannotUseUnrelatedValidDiConfiguration()
    {
        var valid = await Load((await DemoJson()).ToJsonString());
        var invalid = await Load((await DemoJson()).ToJsonString());
        invalid.ProtectedProperties.Clear();
        var generator = new CountingGenerator();
        var handler = new CountingHandler(valid.Resources[0].Id);
        using var services = Services(handler, generator, valid);

        Assert.ThrowsAsync<ConfigurationValidationException>(async () =>
            await services.GetRequiredService<RunDetection>().ExecuteAsync(invalid, default));

        Assert.That(generator.Calls, Is.Zero);
        Assert.That(handler.Calls, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_WhenJsonIsValidAndOptionalSettingsAreOmitted_ContinuesThroughExistingPorts()
    {
        var json = await DemoJson();
        json.Remove("targetOrigin");
        json.Remove("timeoutSeconds");
        json.Remove("protectedProperties");
        var configuration = await Load(json.ToJsonString());
        var generator = new CountingGenerator();
        var handler = new CountingHandler(configuration.Resources[0].Id);
        using var services = Services(handler, generator);

        var report = await services.GetRequiredService<RunDetection>().ExecuteAsync(configuration, default);

        Assert.That(generator.Calls, Is.EqualTo(1));
        Assert.That(handler.Calls, Is.EqualTo(2));
        Assert.That(report.Results.Single().Verdict, Is.EqualTo(FindingVerdict.NoViolationObserved));
    }

    [Test]
    public async Task ExecuteAsync_WhenUnrelatedConfigurationIsRegistered_UsesSessionFields()
    {
        var session = await Load((await DemoJson()).ToJsonString());
        var unrelated = await Load((await DemoJson()).ToJsonString());
        unrelated.ProtectedProperties[0] = "other";
        Assert.That(ConfigurationValidator.Validate(unrelated), Is.Empty);
        var handler = new CountingHandler(session.Resources[0].Id);
        using var services = Services(handler, new CountingGenerator(), unrelated);

        var report = await services.GetRequiredService<RunDetection>().ExecuteAsync(session, default);

        Assert.That(handler.Calls, Is.EqualTo(2));
        Assert.That(report.Results.Single().Verdict, Is.EqualTo(FindingVerdict.NoViolationObserved));
    }

    [Test]
    public async Task ExecuteAsync_WhenSessionsOverlap_KeepsEachConfigurationIsolated()
    {
        var input = await Load((await DemoJson()).ToJsonString());
        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CountingHandler(input.Resources[0].Id, async (call, cancellationToken) =>
        {
            if (call == 1)
            {
                firstRequestStarted.SetResult();
                await releaseFirstRequest.Task.WaitAsync(cancellationToken);
            }
        });
        using var services = Services(handler, new CountingGenerator());
        var runner = services.GetRequiredService<RunDetection>();
        var first = runner.ExecuteAsync(input, default);
        DetectionReport second;
        try
        {
            await firstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            input.ProtectedProperties[0] = "other";
            second = await runner.ExecuteAsync(input, default);
        }
        finally
        {
            releaseFirstRequest.TrySetResult();
        }

        Assert.That((await first).Results.Single().Verdict, Is.EqualTo(FindingVerdict.NoViolationObserved));
        Assert.That(second.Results.Single().Verdict, Is.EqualTo(FindingVerdict.LimitedVerification));
        Assert.That(handler.Calls, Is.EqualTo(4));
    }

    private static ServiceProvider Services(CountingHandler handler, CountingGenerator generator,
        DetectionConfiguration? unrelated = null)
    {
        var services = new ServiceCollection().AddBolaDetection();
        if (unrelated is not null)
            services.AddSingleton(unrelated);
        services.Replace(ServiceDescriptor.Singleton<IScenarioGenerator>(generator));
        services.Replace(ServiceDescriptor.Singleton<ITestCredentialProvider>(new FixtureCredentials()));
        services.AddHttpClient<IScenarioRequestExecutor, HttpScenarioRequestExecutor>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static async Task<JsonObject> DemoJson() =>
        JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory, "demo-detection.json")))!.AsObject();

    private static async Task<DetectionConfiguration> Load(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            return await new JsonDetectionConfigurationLoader().LoadAsync(path, default);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class CountingGenerator : IScenarioGenerator
    {
        public int Calls { get; private set; }

        public IReadOnlyList<TestScenario> Generate(ValidatedDetectionConfiguration configuration)
        {
            Calls++;
            var owner = configuration.Identities[0].Identity;
            return [new(Guid.NewGuid(), "owner read", [configuration.Operations[0].Operation])
            {
                Resource = configuration.Resources[0],
                Identity = owner,
                OwnerIdentity = owner,
                ExpectedAllowed = true
            }];
        }
    }

    private sealed class CountingHandler(string resourceId,
        Func<int, CancellationToken, Task>? beforeResponse = null) : HttpMessageHandler
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref calls);
            if (beforeResponse is not null)
                await beforeResponse(call, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = resourceId, title = "fixture" })
            };
        }
    }

    private sealed class FixtureCredentials : ITestCredentialProvider
    {
        public string Resolve(TestIdentity identity) => "fixture";
    }
}
