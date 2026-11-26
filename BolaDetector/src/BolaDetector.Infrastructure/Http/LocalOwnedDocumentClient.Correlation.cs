using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BolaDetector.Application;

namespace BolaDetector.Infrastructure;

public sealed partial class LocalOwnedDocumentClient
{
    public Task<DocumentRecoveryResult> CreateAsync(DocumentCreationIntent intent, ConfiguredIdentity owner,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken) =>
        SendCorrelatedAsync(intent, owner, configuration, create: true, cancellationToken);

    public Task<DocumentRecoveryResult> RecoverAsync(DocumentCreationIntent intent, ConfiguredIdentity owner,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken) =>
        SendCorrelatedAsync(intent, owner, configuration, create: false, cancellationToken);

    private async Task<DocumentRecoveryResult> SendCorrelatedAsync(DocumentCreationIntent intent, ConfiguredIdentity owner,
        ValidatedDetectionConfiguration configuration, bool create, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!DetectionInputPolicy.TryGetTargetOrigin(configuration.TargetOrigin, out var origin))
            return new(DocumentRecoveryOutcome.Unsupported);
        var credential = ResolveLocalOwner(owner);
        if (credential is null)
            return new(DocumentRecoveryOutcome.Rejected);

        using var deadline = create ? new CancellationTokenSource() :
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
        var path = create ? "/documents" : $"/documents/creations/{intent.Key:N}";
        using var request = new HttpRequestMessage(create ? HttpMethod.Post : HttpMethod.Get, new Uri(origin, path));
        request.Headers.Add("X-Test-User", credential);
        if (create)
        {
            request.Headers.Add("X-Creation-Key", intent.Key.ToString("N"));
            request.Content = JsonContent.Create(new { title = intent.Title });
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            return await PrepareCreationEvidenceAsync(intent, response, create, deadline.Token);
        }
        catch (OperationCanceledException) when (create || !cancellationToken.IsCancellationRequested)
        {
            return new(DocumentRecoveryOutcome.TransportFailure);
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            return new(DocumentRecoveryOutcome.TransportFailure);
        }
        catch (JsonException)
        {
            return new(DocumentRecoveryOutcome.InvalidResponse);
        }
    }

    private string? ResolveLocalOwner(ConfiguredIdentity owner)
    {
        try
        {
            var credential = credentials.Resolve(owner.Identity);
            return owner.Name == "alice" && credential == "alice" ? credential : null;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<DocumentRecoveryResult> PrepareCreationEvidenceAsync(DocumentCreationIntent intent,
        HttpResponseMessage response, bool create, CancellationToken cancellationToken)
    {
        var outcome = (create, response.StatusCode) switch
        {
            (true, HttpStatusCode.Created) or (false, HttpStatusCode.OK) => DocumentRecoveryOutcome.Confirmed,
            (false, HttpStatusCode.Gone) => DocumentRecoveryOutcome.PreviouslyDeleted,
            (false, HttpStatusCode.NoContent) => DocumentRecoveryOutcome.Missing,
            (false, HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented) =>
                DocumentRecoveryOutcome.Unsupported,
            (_, HttpStatusCode.Conflict) => DocumentRecoveryOutcome.Conflict,
            (true, HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Gone) =>
                DocumentRecoveryOutcome.Rejected,
            _ => DocumentRecoveryOutcome.InvalidResponse
        };
        if (outcome is not (DocumentRecoveryOutcome.Confirmed or DocumentRecoveryOutcome.PreviouslyDeleted))
            return new(outcome);

        using var json = await ReadBoundedJsonAsync(response.Content, cancellationToken);
        var record = json?.RootElement.Deserialize<CreationResponse>(CreationJsonOptions);
        if (record is null || !Guid.TryParseExact(record.Id, "N", out var id) || id == Guid.Empty ||
            string.IsNullOrEmpty(record.Owner) || string.IsNullOrEmpty(record.Organization) ||
            string.IsNullOrEmpty(record.IntentDigest) ||
            (create && response.Headers.Location?.OriginalString != $"/documents/{id:N}"))
            return new(DocumentRecoveryOutcome.InvalidResponse);

        var expectedDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(intent.Title)));
        return new(outcome, new DocumentCreationEvidence(record.Key, id, record.Owner,
            record.Organization == "org-a" ? LocalOrganizationId : null, record.IntentDigest == expectedDigest)
        {
            Organization = record.Organization
        });
    }

    private static readonly JsonSerializerOptions CreationJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record CreationResponse(Guid Key, string Id, string Owner, string Organization, string IntentDigest);
}
