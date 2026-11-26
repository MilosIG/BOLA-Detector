namespace BolaDetector.Application;

public enum SequenceStepOutcome { Succeeded, Failed, Skipped, Cancelled }
public enum DocumentCleanupOutcome { Deleted, AlreadyAbsent, Failed }

public sealed record DocumentStepResult(string StepId, SequenceStepOutcome Outcome, string? Error = null,
    Guid? DocumentId = null);

public sealed record DocumentCleanupResult(string Resource, DocumentCleanupOutcome Outcome);

public sealed record DocumentSequenceResult(string SequenceId, IReadOnlyList<DocumentStepResult> Steps,
    IReadOnlyList<DocumentCleanupResult> Cleanup, bool Cancelled)
{
    public DocumentLifecycleResult? Lifecycle { get; init; }

    public bool Succeeded => !Cancelled && Steps.All(step => step.Outcome == SequenceStepOutcome.Succeeded) &&
        Cleanup.All(item => item.Outcome != DocumentCleanupOutcome.Failed) && Lifecycle?.HasUnresolvedCleanup != true;
}
