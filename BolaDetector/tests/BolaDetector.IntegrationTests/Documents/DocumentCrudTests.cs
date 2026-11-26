using System.Net;
using System.Net.Http.Json;
using BolaDetector.Application;
using BolaDetector.Engine;
using BolaDetector.Infrastructure;
using BolaDetector.Domain;
using BolaTestApi;
using BolaTestApi.Documents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BolaDetector.IntegrationTests;

public class DocumentCrudTests
{
    [Test]
    public async Task CorrelatedCreate_WhenConcurrentOrRepeated_UsesOneOwnerScopedIntentAndRetainsTombstone()
    {
        await using var factory = CreateFactory(IPAddress.Loopback);
        using var alice = CreateClient(factory, "alice");
        using var bob = CreateClient(factory, "bob");
        var key = Guid.NewGuid().ToString("N");
        alice.DefaultRequestHeaders.Add("X-Creation-Key", key);
        bob.DefaultRequestHeaders.Add("X-Creation-Key", key);

        var calls = await Task.WhenAll(alice.PostAsJsonAsync("/documents", new { title = "Original" }),
            alice.PostAsJsonAsync("/documents", new { title = " Original " }));
        using var first = calls[0];
        using var repeated = calls[1];
        var path = first.Headers.Location!;
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(repeated.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(repeated.Headers.Location, Is.EqualTo(path));
        using var hidden = await bob.GetAsync($"/documents/creations/{key}");
        Assert.That(hidden.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var other = await bob.PostAsJsonAsync("/documents", new { title = "Original", owner = "alice" });
        var otherPath = other.Headers.Location!;
        Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(otherPath, Is.Not.EqualTo(path));

        using var changed = await alice.PutAsJsonAsync(path, new { title = "Changed" });
        using var equivalent = await alice.PostAsJsonAsync("/documents", new { title = "Original" });
        using var conflict = await alice.PostAsJsonAsync("/documents", new { title = "Changed" });
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(equivalent.Headers.Location, Is.EqualTo(path));
        Assert.That(conflict.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await alice.GetFromJsonAsync<TestDocument>(path))?.Title, Is.EqualTo("Changed"));

        using var deleted = await alice.DeleteAsync(path);
        using var replayAfterDelete = await alice.PostAsJsonAsync("/documents", new { title = "Original" });
        using var tombstone = await alice.GetAsync($"/documents/creations/{key}");
        using var missing = await alice.GetAsync(path);
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(replayAfterDelete.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
        Assert.That(tombstone.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await bob.GetFromJsonAsync<TestDocument>(otherPath))?.Owner, Is.EqualTo("bob"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DocumentSequencesUseIsolatedResourcesAndCleanUp(bool failVerification)
    {
        await using var factory = CreateFactory(IPAddress.Loopback);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var observer = CreateClient(factory, "alice");
        var configuration = await LoadSequenceConfigurationAsync();
        var executor = new DocumentSequenceExecutor(new LocalOwnedDocumentClient(client, new AliceCredential(),
            new LocalTargetAddressPolicy()));
        var sequence = new DocumentSequence("integration", "Document lifecycle", "alice",
        [
            new("create", DocumentStepOperation.Create, "doc", [], "Original"),
            new("read", DocumentStepOperation.Read, "doc", ["create"]),
            new("verify", DocumentStepOperation.Verify, "doc", ["create", "read"], "Original"),
            new("update", DocumentStepOperation.Update, "doc", ["create", "verify"], "Modified"),
            new("read-updated", DocumentStepOperation.Read, "doc", ["create", "update"]),
            new("verify-updated", DocumentStepOperation.Verify, "doc", ["create", "read-updated"],
                failVerification ? "Wrong title" : "Modified"),
            new("delete", DocumentStepOperation.Delete, "doc", ["create", "verify-updated"]),
            new("verify-deleted", DocumentStepOperation.VerifyAbsent, "doc", ["create", "delete"])
        ]);
        var originalFixture = await observer.GetFromJsonAsync<TestDocument>("/secure/documents/doc-1");

        var results = await Task.WhenAll(executor.ExecuteAsync(sequence, configuration, default),
            executor.ExecuteAsync(sequence, configuration, default));

        var ids = results.Select(result => result.Steps[0].DocumentId).ToArray();
        Assert.That(ids, Is.All.Not.Null);
        Assert.That(ids.Distinct().Count(), Is.EqualTo(2));
        Assert.That(results.Select(result => result.Lifecycle!.ExecutionId).Distinct().Count(), Is.EqualTo(2));
        foreach (var result in results)
        {
            var id = result.Steps[0].DocumentId!.Value;
            var lifecycle = result.Lifecycle!;
            var resource = lifecycle.Resources.Single();
            Assert.That(resource.DocumentId, Is.EqualTo(id));
            Assert.That(resource.CreatingStepId, Is.EqualTo("create"));
            Assert.That(resource.CreationConfirmed, Is.True);
            Assert.That(resource.ExpectedOwnership.Owner, Is.EqualTo("alice"));
            Assert.That(resource.ObservedOwnership, Is.EqualTo(new ObservedDocumentOwnership("alice", "org-a",
                resource.ExpectedOwnership.OrganizationId, true, true)));
            Assert.That(resource.DeletionAcknowledged, Is.True);
            Assert.That(resource.AbsenceVerified, Is.True);
            Assert.That(lifecycle.HasUnresolvedCleanup, Is.False);
            Assert.That(lifecycle.History.Select(item => item.Order), Is.EqualTo(Enumerable.Range(1, lifecycle.History.Length)));
            Assert.That(lifecycle.History.Where(item => item.Kind == LifecycleEventKind.StepResult).Select(item => item.StepId),
                Is.EqualTo(result.Steps.Select(step => step.StepId)));
            Assert.That(lifecycle.History.Single(item => item.StepId == "update" && item.Kind == LifecycleEventKind.StepResult).State,
                Is.EqualTo(DocumentLifecycleState.UpdateAcknowledged));
            Assert.That(lifecycle.History.Single(item => item.StepId == "verify-updated" && item.Kind == LifecycleEventKind.StepResult).State,
                Is.EqualTo(failVerification ? DocumentLifecycleState.UpdateAcknowledged : DocumentLifecycleState.ModificationVerified));
            Assert.That(result.Succeeded, Is.EqualTo(!failVerification));
            Assert.That(result.Steps.Take(5).Select(step => step.Outcome), Is.All.EqualTo(SequenceStepOutcome.Succeeded));
            Assert.That(result.Steps.Where(step => step.DocumentId.HasValue).Select(step => step.DocumentId), Is.All.EqualTo(id));
            if (failVerification)
            {
                Assert.That(result.Steps[5].Error, Is.EqualTo("UnexpectedDocumentState"));
                Assert.That(result.Steps.Skip(6).Select(step => step.Outcome), Is.All.EqualTo(SequenceStepOutcome.Skipped));
                Assert.That(result.Cleanup.Single().Outcome, Is.EqualTo(DocumentCleanupOutcome.Deleted));
            }
            else
            {
                Assert.That(result.Cleanup, Is.Empty);
                Assert.That(resource.State, Is.EqualTo(DocumentLifecycleState.AbsenceVerified));
            }
            using var absent = await observer.GetAsync($"/documents/{id:N}");
            Assert.That(absent.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
        Assert.That(originalFixture, Is.Not.Null);
        Assert.That(await observer.GetFromJsonAsync<TestDocument>("/secure/documents/doc-1"), Is.EqualTo(originalFixture));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DocumentSequence_WhenCreateResponseIsLost_UsesOnlyVerifiedRecovery(bool loseRecovery)
    {
        await using var factory = CreateFactory(IPAddress.Loopback);
        var handler = new LoseCreateResponse(loseRecovery);
        using var client = factory.CreateDefaultClient(handler);
        using var observer = CreateClient(factory, "alice");
        using var unrelated = await observer.PostAsJsonAsync("/documents", new { title = "Unrelated" });
        var unrelatedPath = unrelated.Headers.Location!;
        var configuration = await LoadSequenceConfigurationAsync();
        var executor = new DocumentSequenceExecutor(new LocalOwnedDocumentClient(client, new AliceCredential(),
            new LocalTargetAddressPolicy()));
        var sequence = new DocumentSequence("lost-create", "Lost Create response", "alice",
        [
            new("create", DocumentStepOperation.Create, "doc", [], "Original"),
            new("read", DocumentStepOperation.Read, "doc", ["create"])
        ]);

        var result = await executor.ExecuteAsync(sequence, configuration, default);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Steps[0].Error, Is.EqualTo("CreateUnconfirmed"));
        Assert.That(result.Steps[1].Outcome, Is.EqualTo(SequenceStepOutcome.Skipped));
        var lifecycle = result.Lifecycle!;
        var resource = lifecycle.Resources.Single();
        Assert.That(lifecycle.HasUnresolvedCleanup, Is.EqualTo(loseRecovery));
        Assert.That(resource.CreationKey.ToString("N"), Is.EqualTo(handler.Key));
        Assert.That(resource.CreationRecovered, Is.EqualTo(!loseRecovery));
        Assert.That(resource.DocumentId, Is.EqualTo(loseRecovery ? (Guid?)null : Guid.Parse(handler.CommittedPath!["/documents/".Length..])));
        Assert.That(resource.CleanupStatus, Is.EqualTo(loseRecovery ? ResourceCleanupStatus.UnresolvedCreation : ResourceCleanupStatus.Completed));
        Assert.That(resource.AbsenceVerified, Is.EqualTo(!loseRecovery));
        Assert.That(lifecycle.History[0].Kind, Is.EqualTo(LifecycleEventKind.StepAttempt));
        Assert.That(lifecycle.History[1].State, Is.EqualTo(DocumentLifecycleState.CreateUnconfirmed));
        Assert.That(lifecycle.History.All(item => item.CreationKey == resource.CreationKey), Is.True);
        Assert.That(handler.Requests, Is.EqualTo(loseRecovery ? new[] { "POST", "GET" } : new[] { "POST", "GET", "DELETE", "GET" }));
        using var committed = await observer.GetAsync(handler.CommittedPath);
        Assert.That(committed.StatusCode, Is.EqualTo(loseRecovery ? HttpStatusCode.OK : HttpStatusCode.NotFound));
        using var stillPresent = await observer.GetAsync(unrelatedPath);
        Assert.That(stillPresent.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static async Task<ValidatedDetectionConfiguration> LoadSequenceConfigurationAsync()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "demo-detection.json");
        var input = await new JsonDetectionConfigurationLoader().LoadAsync(path, default);
        return ValidatedDetectionConfiguration.Create(input);
    }

    private sealed class LoseCreateResponse(bool loseRecovery) : DelegatingHandler
    {
        public List<string> Requests { get; } = [];
        public string? Key { get; private set; }
        public string? CommittedPath { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Method.Method);
            Assert.That(request.Headers.GetValues("X-Test-User").Single(), Is.EqualTo("alice"));
            if (request.Method != HttpMethod.Post)
            {
                var recovery = request.RequestUri!.AbsolutePath == $"/documents/creations/{Key}";
                Assert.That(recovery || request.RequestUri.AbsolutePath == CommittedPath, Is.True);
                if (recovery && loseRecovery)
                    throw new HttpRequestException("Simulated recovery transport failure.");
                return await base.SendAsync(request, cancellationToken);
            }
            Assert.That(Requests, Has.Count.EqualTo(1));
            Key = request.Headers.GetValues("X-Creation-Key").Single();
            using var response = await base.SendAsync(request, cancellationToken);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            CommittedPath = response.Headers.Location!.OriginalString;
            throw new HttpRequestException("Simulated connection loss after creation.");
        }
    }

    private sealed class AliceCredential : ITestCredentialProvider
    {
        public string Resolve(TestIdentity identity) => "alice";
    }

    [Test]
    public async Task OwnerCanCreateReadUpdateAndDeleteDocument()
    {
        await using var factory = CreateFactory(IPAddress.Loopback);
        using var alice = CreateClient(factory, "alice");
        using var created = await alice.PostAsJsonAsync("/documents", new { title = " Original ", owner = "bob", id = "doc-1" });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var document = (await created.Content.ReadFromJsonAsync<TestDocument>())!;
        var location = created.Headers.Location;
        Assert.That(document.Owner, Is.EqualTo("alice"));
        Assert.That(document.Title, Is.EqualTo("Original"));
        Assert.That(Guid.TryParseExact(document.Id, "N", out _), Is.True);
        Assert.That(location?.OriginalString, Is.EqualTo($"/documents/{document.Id}"));
        Assert.That(await alice.GetFromJsonAsync<TestDocument>(location), Is.EqualTo(document));

        using var updated = await alice.PutAsJsonAsync(location, new { title = "Updated", owner = "bob" });
        Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await alice.GetFromJsonAsync<TestDocument>(location), Is.EqualTo(document with { Title = "Updated" }));

        using var deleted = await alice.DeleteAsync(location);
        using var missing = await alice.GetAsync(location);
        using var missingUpdate = await alice.PutAsJsonAsync(location, new { title = "Updated" });
        using var missingDelete = await alice.DeleteAsync(location);
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(missingUpdate.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(missingDelete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ForeignIdentityIsDeniedAndDocumentRemainsPrivateAndUnchanged()
    {
        await using var factory = CreateFactory(IPAddress.IPv6Loopback);
        using var alice = CreateClient(factory, "alice");
        using var bob = CreateClient(factory, "bob");
        using var created = await alice.PostAsJsonAsync("/documents", new { title = "Original" });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var document = (await created.Content.ReadFromJsonAsync<TestDocument>())!;
        var location = created.Headers.Location;
        using var read = await bob.GetAsync(location);
        using var update = await bob.PutAsJsonAsync(location, new { title = "Changed" });
        using var delete = await bob.DeleteAsync(location);
        using var legacy = await bob.GetAsync($"/vulnerable/documents/{document.Id}");

        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(legacy.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await alice.GetFromJsonAsync<TestDocument>(location), Is.EqualTo(document));
    }

    [TestCase(null)]
    [TestCase("unknown")]
    [TestCase("alice,bob")]
    public async Task CrudRequiresRecognizedIdentity(string? identity)
    {
        await using var factory = CreateFactory(IPAddress.Loopback);
        using var client = CreateClient(factory, identity);
        using var result = await client.PostAsJsonAsync("/documents", new { title = "Title" });

        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task NonLoopbackConnectionIsDenied()
    {
        await using var factory = CreateFactory(IPAddress.Parse("192.0.2.10"));
        using var client = CreateClient(factory, "alice");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
        using var result = await client.PostAsJsonAsync("/documents", new { title = "Title" });

        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task InvalidInputIsRejectedWithoutChangingStoredDocument()
    {
        await using var factory = CreateFactory(IPAddress.Loopback);
        using var client = CreateClient(factory, "bob");
        using var missingTitle = await client.PostAsJsonAsync("/documents", new { owner = "alice" });
        using var longTitle = await client.PostAsJsonAsync("/documents", new { title = new string('a', 201) });
        using var created = await client.PostAsJsonAsync("/documents", new { title = "Original" });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var update = await client.PutAsJsonAsync(created.Headers.Location, new { title = "   " });

        Assert.That(missingTitle.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(longTitle.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await client.GetFromJsonAsync<TestDocument>(created.Headers.Location))?.Title, Is.EqualTo("Original"));
    }

    private static WebApplicationFactory<TestApiProgram> CreateFactory(IPAddress address) => new LocalApiFactory(address);

    private static HttpClient CreateClient(WebApplicationFactory<TestApiProgram> factory, string? identity)
    {
        var client = factory.CreateClient();
        if (identity is not null)
            client.DefaultRequestHeaders.Add("X-Test-User", identity);
        return client;
    }

    private sealed class LocalApiFactory(IPAddress address) : WebApplicationFactory<TestApiProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new ConnectionAddressFilter(address)));
    }

    private sealed class ConnectionAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
