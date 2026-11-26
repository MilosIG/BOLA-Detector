using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace BolaDetector.EndToEndTests;

[NonParallelizable]
public class LocalRegressionTests
{
    private DirectoryInfo workspace = null!;

    [SetUp]
    public void CreateTemporaryWorkspace() => workspace = Directory.CreateTempSubdirectory("bola-e2e-");

    [TearDown]
    public void RemoveTemporaryWorkspace() => workspace.Delete(recursive: true);

    [Test]
    public async Task Demo_WhenDocumentSequenceIsSelected_ReportsLifecycleAndLeavesNoDocument()
    {
        await using var api = await LocalApiProcess.StartAsync(workspace.FullName, 5081);
        api.Client.DefaultRequestHeaders.Add("X-Test-User", "alice");

        var first = await RunDocumentSequenceAsync(api.Client);
        var second = await RunDocumentSequenceAsync(api.Client);

        Assert.That(second.DocumentId, Is.Not.EqualTo(first.DocumentId));
        Assert.That(second.ExecutionId, Is.Not.EqualTo(first.ExecutionId));
        Assert.That(Directory.EnumerateFileSystemEntries(workspace.FullName), Is.Empty);
    }

    private async Task<(Guid DocumentId, Guid ExecutionId)> RunDocumentSequenceAsync(HttpClient client)
    {
        await using var demo = await TestProcess.StartAsync("src", "BolaDetector.Demo", workspace.FullName,
            ["--document-sequence"], new Dictionary<string, string> { ["ALICE_TEST_USER"] = "alice" });

        var exitCode = await demo.WaitForExitAsync();

        Assert.That(exitCode, Is.Zero, demo.Diagnostics);
        Assert.That(demo.StandardError, Is.Empty, demo.Diagnostics);
        using var output = JsonDocument.Parse(demo.StandardOutput);
        var result = output.RootElement;
        Assert.That(result.GetProperty("Succeeded").GetBoolean(), Is.True);
        var steps = result.GetProperty("Steps").EnumerateArray().ToArray();
        Assert.That(steps.Select(step => step.GetProperty("StepId").GetString()), Is.EqualTo(new[]
        {
            "create", "read-initial", "verify-initial", "update", "read-updated", "verify-updated", "delete", "verify-deleted"
        }));
        Assert.That(steps.Select(step => step.GetProperty("Outcome").GetString()), Is.All.EqualTo("Succeeded"));
        var id = steps[0].GetProperty("DocumentId").GetGuid();
        Assert.That(id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(steps.Select(step => step.GetProperty("DocumentId").GetGuid()), Is.All.EqualTo(id));
        Assert.That(result.GetProperty("Cleanup").GetArrayLength(), Is.Zero);
        var lifecycle = result.GetProperty("Lifecycle");
        var resource = lifecycle.GetProperty("Resources").EnumerateArray().Single();
        Assert.That(resource.GetProperty("DocumentId").GetGuid(), Is.EqualTo(id));
        Assert.That(resource.GetProperty("State").GetString(), Is.EqualTo("AbsenceVerified"));
        Assert.That(resource.GetProperty("DeletionAcknowledged").GetBoolean(), Is.True);
        Assert.That(resource.GetProperty("AbsenceVerified").GetBoolean(), Is.True);
        Assert.That(resource.GetProperty("CleanupStatus").GetString(), Is.EqualTo("Completed"));
        Assert.That(lifecycle.GetProperty("HasUnresolvedCleanup").GetBoolean(), Is.False);
        var expected = resource.GetProperty("ExpectedOwnership");
        var observed = resource.GetProperty("ObservedOwnership");
        Assert.That(expected.GetProperty("Owner").GetString(), Is.EqualTo("alice"));
        Assert.That(observed.GetProperty("Owner").GetString(), Is.EqualTo("alice"));
        Assert.That(observed.GetProperty("Organization").GetString(), Is.EqualTo("org-a"));
        Assert.That(observed.GetProperty("OrganizationId").GetGuid(), Is.EqualTo(expected.GetProperty("OrganizationId").GetGuid()));
        Assert.That(observed.GetProperty("OrganizationMatches").GetBoolean(), Is.True);
        var history = lifecycle.GetProperty("History").EnumerateArray().ToArray();
        Assert.That(history.Select(item => item.GetProperty("Order").GetInt32()),
            Is.EqualTo(Enumerable.Range(1, history.Length)));
        Assert.That(history[^1].GetProperty("State").GetString(), Is.EqualTo("AbsenceVerified"));
        using var missing = await client.GetAsync($"/documents/{id:N}");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        return (id, lifecycle.GetProperty("ExecutionId").GetGuid());
    }

    [Test]
    public async Task Demo_WhenUsingDefaultReadConfiguration_PreservesVerdictCounts()
    {
        await using var api = await LocalApiProcess.StartAsync(workspace.FullName, 5081);
        await using var demo = await TestProcess.StartAsync("src", "BolaDetector.Demo", workspace.FullName, [],
            new Dictionary<string, string> { ["ALICE_TEST_USER"] = "alice", ["BOB_TEST_USER"] = "bob" });

        var exitCode = await demo.WaitForExitAsync();

        Assert.That(exitCode, Is.Zero, demo.Diagnostics);
        using var output = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(workspace.FullName, "bola-report.json")));
        var verdicts = output.RootElement.GetProperty("Results").EnumerateArray()
            .Select(result => result.GetProperty("Verdict").GetString()).ToArray();
        Assert.That(verdicts, Is.EquivalentTo(Enumerable.Repeat("NoViolationObserved", 6)
            .Concat(Enumerable.Repeat("ConfirmedViolation", 2))));
    }

    [Test]
    public async Task Documents_WhenOwnerUsesCrud_CompletesTheResourceLifecycle()
    {
        await using var api = await LocalApiProcess.StartAsync(workspace.FullName);
        var client = api.Client;
        client.DefaultRequestHeaders.Add("X-Test-User", "alice");
        string? documentPath = null;

        try
        {
            using var created = await client.PostAsJsonAsync("/documents", new { title = "Initial title" });
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), api.Diagnostics);
            var document = await created.Content.ReadFromJsonAsync<Document>();
            Assert.That(document, Is.Not.Null);
            Assert.That(Guid.TryParseExact(document!.Id, "N", out var id) && id != Guid.Empty, Is.True);
            documentPath = $"/documents/{document.Id}";
            Assert.That(created.Headers.Location?.OriginalString, Is.EqualTo(documentPath));
            Assert.That(document.Owner, Is.EqualTo("alice"));
            Assert.That(await ReadDocumentAsync(client, documentPath), Is.EqualTo(document));

            using var updated = await client.PutAsJsonAsync(documentPath, new { title = "Updated title" });
            Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), api.Diagnostics);
            Assert.That(await ReadDocumentAsync(client, documentPath), Is.EqualTo(document with { Title = "Updated title" }));

            using var deleted = await client.DeleteAsync(documentPath);
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), api.Diagnostics);
            var deletedPath = documentPath;
            documentPath = null;
            using var missing = await client.GetAsync(deletedPath);
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), api.Diagnostics);
        }
        finally
        {
            if (documentPath is not null)
                await TryDeleteDocumentAsync(client, documentPath);
        }
    }

    [Test]
    public async Task Demo_WhenOwnerReadsAuthorizedDocument_WritesSuccessfulReport()
    {
        await using var api = await LocalApiProcess.StartAsync(workspace.FullName, 5081);
        var (configuration, report) = await WriteConfigurationAsync(invalid: false);
        await using var demo = await StartDemoAsync(configuration, report, "alice");

        var exitCode = await demo.WaitForExitAsync();

        Assert.That(exitCode, Is.Zero, demo.Diagnostics);
        Assert.That(demo.StandardError, Is.Empty, demo.Diagnostics);
        Assert.That(File.Exists(report), Is.True, demo.Diagnostics);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(report));
        var root = document.RootElement;
        Assert.That(root.GetProperty("CreatedAt").TryGetDateTimeOffset(out _), Is.True);
        Assert.That(root.GetProperty("Limitation").GetString(), Is.Not.Empty);
        var result = root.GetProperty("Results").EnumerateArray().Single();
        Assert.That(result.GetProperty("TestUser").GetString(), Is.EqualTo("alice"));
        Assert.That(result.GetProperty("ResourceId").GetString(), Is.EqualTo("doc-1"));
        Assert.That(result.GetProperty("Method").GetString(), Is.EqualTo("GET"));
        Assert.That(result.GetProperty("Path").GetString(), Is.EqualTo("/secure/documents/doc-1"));
        Assert.That(result.GetProperty("ExpectedAllowed").GetBoolean(), Is.True);
        Assert.That(result.GetProperty("Verdict").GetString(), Is.EqualTo("NoViolationObserved"));
        var response = result.GetProperty("Response");
        Assert.That(response.GetProperty("StatusCode").GetInt32(), Is.EqualTo(200));
        Assert.That(response.GetProperty("Error").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Demo_WhenIdentitiesAreNull_ExitsWithSafeValidationErrorAndNoReport()
    {
        const string syntheticCredential = "e2e-synthetic-credential-not-for-output";
        var (configuration, report) = await WriteConfigurationAsync(invalid: true);
        await using var demo = await StartDemoAsync(configuration, report, syntheticCredential);

        var exitCode = await demo.WaitForExitAsync();

        Assert.That(exitCode, Is.EqualTo(1), demo.Diagnostics);
        Assert.That(demo.Diagnostics, Does.Contain("[CFG_REQUIRED_COLLECTION] identities:"));
        Assert.That(demo.Diagnostics, Does.Not.Contain(syntheticCredential));
        Assert.That(File.Exists(report), Is.False);
    }

    private static async Task<Document?> ReadDocumentAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<Document>();
    }

    private static async Task TryDeleteDocumentAsync(HttpClient client, string path)
    {
        try
        {
            using var response = await client.DeleteAsync(path);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                TestContext.Out.WriteLine($"Document cleanup returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            TestContext.Out.WriteLine($"Explicit document cleanup did not complete: {error.Message}");
        }
        // Stopping the owned API also discards its in-memory store.
    }

    private Task<TestProcess> StartDemoAsync(string configuration, string report, string credential) =>
        TestProcess.StartAsync("src", "BolaDetector.Demo", workspace.FullName, [configuration, report],
            new Dictionary<string, string> { ["ALICE_TEST_USER"] = credential });

    private async Task<(string Configuration, string Report)> WriteConfigurationAsync(bool invalid)
    {
        var fixturePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "demo-detection.json");
        var config = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
        var owner = Find(config, "identities", entry => entry["name"]?.GetValue<string>() == "alice");
        var resource = Find(config, "resources", entry => entry["id"]?.GetValue<string>() == "doc-1");
        var operation = Find(config, "operations",
            entry => entry["operation"]?["path"]?.GetValue<string>() == "/secure/documents/{id}");
        config["identities"] = invalid ? null : new JsonArray(owner.DeepClone());
        config["resources"] = new JsonArray(resource.DeepClone());
        config["operations"] = new JsonArray(operation.DeepClone());

        var configuration = Path.Combine(workspace.FullName, "input.json");
        await File.WriteAllTextAsync(configuration, config.ToJsonString());
        return (configuration, Path.Combine(workspace.FullName, "report.json"));
    }

    private static JsonObject Find(JsonObject config, string collection, Func<JsonObject, bool> match) =>
        config[collection]!.AsArray().OfType<JsonObject>().Single(match);

    private sealed record Document(string Id, string Owner, string Title);
}
