using System.Collections.Immutable;

namespace BolaDetector.Application;

public enum DocumentLifecycleState
{
    PendingCreation, CreateRejected, CreateUnconfirmed, Created, Observed, UpdateAcknowledged,
    ModificationVerified, DeletionAcknowledged, AbsenceVerified, AbsentAtCleanup
}

public enum ResourceCleanupStatus { NotRequired, Required, Completed, Failed, UnresolvedCreation }

public enum LifecycleEventKind { StepAttempt, StepResult, CleanupAttempt, CleanupResult, RecoveryAttempt, RecoveryResult, CleanupVerification }

public sealed record ExpectedDocumentOwnership(Guid OwnerId, string Owner, Guid OrganizationId);

public sealed record ObservedDocumentOwnership(string? Owner, string? Organization, Guid? OrganizationId,
    bool OwnerMatches, bool? OrganizationMatches);

public sealed record TrackedDocumentResult(string Resource, string ResourceType, string CreatingStepId,
    Guid? DocumentId, ExpectedDocumentOwnership ExpectedOwnership, ObservedDocumentOwnership? ObservedOwnership,
    DocumentLifecycleState State, bool DeletionAcknowledged, ResourceCleanupStatus CleanupStatus)
{
    public Guid CreationKey { get; init; }
    public bool CreationRecovered { get; init; }
    public bool CreationConfirmed => DocumentId.HasValue;
    public bool AbsenceVerified => State == DocumentLifecycleState.AbsenceVerified;
}

public sealed record DocumentLifecycleEvent(int Order, string Resource, Guid? DocumentId, string? StepId,
    DocumentStepOperation Operation, LifecycleEventKind Kind, DocumentLifecycleState PreviousState,
    DocumentLifecycleState State, SequenceStepOutcome? Outcome, string? Reason,
    ObservedDocumentOwnership? Observation)
{
    public Guid CreationKey { get; init; }
}

public sealed record DocumentLifecycleResult(Guid ExecutionId, ImmutableArray<TrackedDocumentResult> Resources,
    ImmutableArray<DocumentLifecycleEvent> History)
{
    public bool HasUnresolvedCleanup => Resources.Any(resource =>
        resource.CleanupStatus is ResourceCleanupStatus.Required or ResourceCleanupStatus.Failed or
            ResourceCleanupStatus.UnresolvedCreation);
}
