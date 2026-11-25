using System.Net;
using System.Net.Http.Json;
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
