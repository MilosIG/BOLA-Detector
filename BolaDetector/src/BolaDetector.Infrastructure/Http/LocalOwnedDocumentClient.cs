using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BolaDetector.Application;

namespace BolaDetector.Infrastructure;

public sealed class LocalOwnedDocumentClient(HttpClient client, ITestCredentialProvider credentials,
    LocalTargetAddressPolicy targetPolicy) : IOwnedDocumentClient
{
    private const string DocumentRoute = "/documents/{id}";
    private const string LocationPrefix = "/documents/";
    private const int MaximumBodyBytes = 64 * 1024;
    private static readonly Guid LocalOrganizationId = new("00000000-0000-0000-0000-000000000001");

    public async Task<DocumentOperationResult> ExecuteAsync(DocumentStepOperation operation, Guid? id, string? title,
        ConfiguredIdentity owner, ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!DetectionInputPolicy.TryGetTargetOrigin(configuration.TargetOrigin, out var origin))
            return new(DocumentOperationOutcome.Failed, "InvalidLocalTarget");

        var method = operation switch
        {
            DocumentStepOperation.Create => HttpMethod.Post,
            DocumentStepOperation.Read or DocumentStepOperation.VerifyAbsent => HttpMethod.Get,
            DocumentStepOperation.Update => HttpMethod.Put,
            DocumentStepOperation.Delete => HttpMethod.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        if (method != HttpMethod.Post && (id is null || id == Guid.Empty))
            return new(DocumentOperationOutcome.Failed, "MissingDocumentId");

        var uri = method == HttpMethod.Post ? new Uri(origin, "/documents") :
            targetPolicy.Build(configuration.TargetOrigin, DocumentRoute, id!.Value.ToString("N"));

        // Once a Create is dispatched, allow the bounded request to return its cleanup identifier.
        using var timeout = method == HttpMethod.Post ? new CancellationTokenSource() :
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
        try
        {
            var credential = credentials.Resolve(owner.Identity);
            if (owner.Name != "alice" || credential != "alice")
                return new(DocumentOperationOutcome.Failed, "InvalidOwnerCredential");

            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Add("X-Test-User", credential);
            if (title is not null)
                request.Content = JsonContent.Create(new { title });
            cancellationToken.ThrowIfCancellationRequested();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return await PrepareResultAsync(operation, id, response, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested &&
            (method == HttpMethod.Post || !cancellationToken.IsCancellationRequested))
        {
            return new(DocumentOperationOutcome.Failed, method == HttpMethod.Post ? "CreateUnconfirmed" : "Timeout");
        }
        catch (HttpRequestException)
        {
            return new(DocumentOperationOutcome.Failed, method == HttpMethod.Post ? "CreateUnconfirmed" : "HttpRequestFailed");
        }
        catch (IOException)
        {
            return new(DocumentOperationOutcome.Failed, method == HttpMethod.Post ? "CreateUnconfirmed" : "ResponseReadFailed");
        }
        catch (JsonException)
        {
            return new(DocumentOperationOutcome.Failed, "InvalidDocumentResponse");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return new(DocumentOperationOutcome.Failed, "InvalidOwnerCredential");
        }
    }

    private static async Task<DocumentOperationResult> PrepareResultAsync(DocumentStepOperation operation, Guid? id,
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        switch (operation, response.StatusCode)
        {
            case (DocumentStepOperation.Create, HttpStatusCode.Created):
                var location = response.Headers.Location?.OriginalString;
                return location is not null && location.StartsWith(LocationPrefix, StringComparison.Ordinal) &&
                    Guid.TryParseExact(location.AsSpan(LocationPrefix.Length), "N", out var created) && created != Guid.Empty
                    ? new(DocumentOperationOutcome.Succeeded, CreatedId: created)
                    : new(DocumentOperationOutcome.Failed, "CreateUnconfirmed");
            case (DocumentStepOperation.Read, HttpStatusCode.OK):
                return await ReadStateAsync(id!.Value, response.Content, cancellationToken);
            case (DocumentStepOperation.Update or DocumentStepOperation.Delete, HttpStatusCode.NoContent):
                return new(DocumentOperationOutcome.Succeeded);
            case (_, HttpStatusCode.NotFound):
                return new(DocumentOperationOutcome.Absent);
            default:
                return new(DocumentOperationOutcome.Failed, "UnexpectedStatus");
        }
    }

    private static async Task<DocumentOperationResult> ReadStateAsync(Guid id, HttpContent content,
        CancellationToken cancellationToken)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaximumBodyBytes)
                return new(DocumentOperationOutcome.Failed, "ResponseTooLarge");
            buffer.Write(bytes, 0, count);
        }

        using var json = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("id", out var documentId) || documentId.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(documentId.GetString(), "N", out var parsed) || parsed != id ||
            !root.TryGetProperty("owner", out var owner) || owner.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String)
            return new(DocumentOperationOutcome.Failed, "InvalidDocumentResponse");

        var organization = root.TryGetProperty("organization", out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
        return new(DocumentOperationOutcome.Succeeded, State: new(owner.GetString()!, title.GetString()!)
        {
            Organization = organization,
            OrganizationId = organization == "org-a" ? LocalOrganizationId : null
        });
    }
}
