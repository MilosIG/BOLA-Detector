namespace BolaDetector.Application;

public sealed record DocumentCreationIntent(Guid Key, string Title);

public enum DocumentRecoveryOutcome
{
    Confirmed, PreviouslyDeleted, Missing, Conflict, Unsupported, TransportFailure, InvalidResponse, Rejected
}

public sealed record DocumentCreationEvidence(Guid Key, Guid DocumentId, string Owner, Guid? OrganizationId,
    bool IntentMatches)
{
    public string? Organization { get; init; }
}

public sealed record DocumentRecoveryResult(DocumentRecoveryOutcome Outcome, DocumentCreationEvidence? Evidence = null);

public interface ICorrelatedDocumentClient : IOwnedDocumentClient
{
    Task<DocumentRecoveryResult> CreateAsync(DocumentCreationIntent intent, ConfiguredIdentity owner,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken);

    Task<DocumentRecoveryResult> RecoverAsync(DocumentCreationIntent intent, ConfiguredIdentity owner,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken);
}
