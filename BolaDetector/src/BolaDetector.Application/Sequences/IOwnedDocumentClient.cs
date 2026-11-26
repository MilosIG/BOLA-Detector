namespace BolaDetector.Application;

public sealed record OwnedDocumentState(string Owner, string Title);

public enum DocumentOperationOutcome { Succeeded, Absent, Failed }

public sealed record DocumentOperationResult(DocumentOperationOutcome Outcome, string? Error = null,
    Guid? CreatedId = null, OwnedDocumentState? State = null);

public interface IOwnedDocumentClient
{
    Task<DocumentOperationResult> ExecuteAsync(DocumentStepOperation operation, Guid? id, string? title,
        ConfiguredIdentity owner, ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken);
}
