using System.Collections.Immutable;
using BolaDetector.Application;

namespace BolaDetector.Engine;

/// <summary>Owns resource bindings and lifecycle transitions for one validated sequence execution.</summary>
internal sealed class DocumentResourceRegistry
{
    private readonly Guid executionId = Guid.NewGuid();
    private readonly Dictionary<string, TrackedDocument> resources = new(StringComparer.Ordinal);
    private readonly List<DocumentLifecycleEvent> history = [];

    public DocumentResourceRegistry(DocumentSequence sequence, ConfiguredIdentity owner)
    {
        var ownership = new ExpectedDocumentOwnership(owner.Identity.Id, owner.Name, owner.Identity.OrganizationId);
        foreach (var step in sequence.Steps.OfType<DocumentStep>().Where(step => step.Operation == DocumentStepOperation.Create))
        {
            if (!resources.TryAdd(step.Resource, new TrackedDocument(step, ownership)))
                throw new InvalidOperationException("The logical resource is already registered.");
        }
    }

    public Guid? Resolve(string resource) => Get(resource).Id;

    public bool CanExecute(DocumentStep step)
    {
        var resource = Get(step.Resource);
        return step.Operation switch
        {
            DocumentStepOperation.Create => resource.State == DocumentLifecycleState.PendingCreation,
            DocumentStepOperation.VerifyAbsent => resource.Id.HasValue,
            _ => resource.Id.HasValue &&
                resource.State is not (DocumentLifecycleState.DeletionAcknowledged or DocumentLifecycleState.AbsenceVerified)
        };
    }

    public void RecordAttempt(DocumentStep step)
    {
        var resource = Get(step.Resource);
        Record(resource, step.Id, step.Operation, LifecycleEventKind.StepAttempt, resource.State);
    }

    public DocumentStepResult RecordStepResult(DocumentStep step, SequenceStepOutcome outcome, string? error = null)
    {
        var resource = Get(step.Resource);
        Record(resource, step.Id, step.Operation, LifecycleEventKind.StepResult, resource.State, outcome, error);
        return new(step.Id, outcome, error, outcome == SequenceStepOutcome.Failed ? resource.Id : null);
    }

    public DocumentStepResult Complete(DocumentStep step, DocumentOperationResult operation)
    {
        var resource = Get(step.Resource);
        var previous = resource.State;
        var error = Apply(step, resource, operation);
        var outcome = error is null ? SequenceStepOutcome.Succeeded : SequenceStepOutcome.Failed;
        Record(resource, step.Id, step.Operation, LifecycleEventKind.StepResult, previous, outcome, error,
            step.Operation == DocumentStepOperation.Read && error is null ? resource.ObservedOwnership : null);
        return new(step.Id, outcome, error, resource.Id);
    }

    public DocumentStepResult Verify(DocumentStep step)
    {
        var resource = Get(step.Resource);
        var previous = resource.State;
        var dependencies = step.DependsOn.IsDefault ? [] : step.DependsOn;
        var observation = dependencies.Select(key => resource.Observations.GetValueOrDefault(key))
            .SingleOrDefault(item => item is not null);
        var verified = observation is not null && observation.State.Owner == resource.Ownership.Owner &&
            observation.State.Title == step.Title!.Trim();
        if (verified && resource.State == DocumentLifecycleState.UpdateAcknowledged &&
            observation!.Revision == resource.Revision && observation.State.Title == resource.UpdateTitle &&
            resource.BeforeUpdate is not null && resource.BeforeUpdate != observation.State.Title)
            resource.State = DocumentLifecycleState.ModificationVerified;

        var outcome = verified ? SequenceStepOutcome.Succeeded : SequenceStepOutcome.Failed;
        var error = verified ? null : "UnexpectedDocumentState";
        Record(resource, step.Id, step.Operation, LifecycleEventKind.StepResult, previous, outcome, error);
        return new(step.Id, outcome, error, resource.Id);
    }

    public (string Resource, Guid Id)[] PendingCleanup() => resources.Values
        .Where(resource => resource.Cleanup == ResourceCleanupStatus.Required)
        .OrderByDescending(resource => resource.CreationOrder)
        .Select(resource => (resource.CreateStep.Resource, resource.Id!.Value)).ToArray();

    public void RecordCleanupAttempt(string reference)
    {
        var resource = Get(reference);
        Record(resource, null, DocumentStepOperation.Delete, LifecycleEventKind.CleanupAttempt, resource.State);
    }

    public void CompleteCleanup(string reference, DocumentCleanupOutcome outcome, string? error)
    {
        var resource = Get(reference);
        var previous = resource.State;
        if (outcome == DocumentCleanupOutcome.Deleted)
            AcknowledgeDeletion(resource);
        else if (outcome == DocumentCleanupOutcome.AlreadyAbsent && resource.State != DocumentLifecycleState.AbsenceVerified)
            resource.State = DocumentLifecycleState.AbsentAtCleanup;
        resource.Cleanup = outcome == DocumentCleanupOutcome.Failed ? ResourceCleanupStatus.Failed : ResourceCleanupStatus.Completed;
        var succeeded = outcome != DocumentCleanupOutcome.Failed;
        Record(resource, null, DocumentStepOperation.Delete, LifecycleEventKind.CleanupResult, previous,
            succeeded ? SequenceStepOutcome.Succeeded : SequenceStepOutcome.Failed, succeeded ? outcome.ToString() : error);
    }

    public DocumentLifecycleResult Snapshot() => new(executionId,
        resources.Values.Select(resource => new TrackedDocumentResult(resource.CreateStep.Resource, "document", resource.CreateStep.Id,
            resource.Id, resource.Ownership, resource.ObservedOwnership, resource.State, resource.DeletionAcknowledged,
            resource.Cleanup)).ToImmutableArray(), history.ToImmutableArray());

    private string? Apply(DocumentStep step, TrackedDocument resource, DocumentOperationResult operation)
    {
        if (step.Operation == DocumentStepOperation.Create)
            return CompleteCreation(resource, operation);
        if (operation.Outcome == DocumentOperationOutcome.Failed)
            return operation.Error ?? "OperationFailed";

        switch (step.Operation, operation.Outcome)
        {
            case (DocumentStepOperation.Read, DocumentOperationOutcome.Succeeded):
                if (operation.State is null)
                    return "MissingDocumentState";
                Observe(resource, step.Id, operation.State);
                break;
            case (DocumentStepOperation.Update, DocumentOperationOutcome.Succeeded):
                resource.State = DocumentLifecycleState.UpdateAcknowledged;
                resource.Revision++;
                resource.BeforeUpdate = resource.LatestReadTitle;
                resource.UpdateTitle = step.Title!.Trim();
                break;
            case (DocumentStepOperation.Delete, DocumentOperationOutcome.Succeeded):
                AcknowledgeDeletion(resource);
                resource.Cleanup = ResourceCleanupStatus.Completed;
                break;
            case (DocumentStepOperation.VerifyAbsent, DocumentOperationOutcome.Absent):
                resource.State = DocumentLifecycleState.AbsenceVerified;
                break;
            default:
                return "UnexpectedOperationOutcome";
        }

        return null;
    }

    private string? CompleteCreation(TrackedDocument resource, DocumentOperationResult operation)
    {
        if (operation.Outcome == DocumentOperationOutcome.Succeeded &&
            operation.CreatedId is { } id && id != Guid.Empty && resources.Values.All(item => item.Id != id))
        {
            resource.Id = id;
            resource.CreationOrder = history.Count;
            resource.State = DocumentLifecycleState.Created;
            resource.Cleanup = ResourceCleanupStatus.Required;
            return null;
        }

        var error = operation.Outcome switch
        {
            DocumentOperationOutcome.Succeeded => "MissingOrDuplicateCreatedId",
            DocumentOperationOutcome.Failed => operation.Error ?? "OperationFailed",
            _ => "UnexpectedOperationOutcome"
        };
        var uncertain = error is "CreateUnconfirmed" or "MissingOrDuplicateCreatedId";
        resource.State = uncertain ? DocumentLifecycleState.CreateUnconfirmed : DocumentLifecycleState.CreateRejected;
        resource.Cleanup = uncertain ? ResourceCleanupStatus.UnresolvedCreation : ResourceCleanupStatus.NotRequired;
        return error;
    }

    private static void Observe(TrackedDocument resource, string stepId, OwnedDocumentState state)
    {
        resource.Observations.Add(stepId, new ReadObservation(state, resource.Revision));
        resource.LatestReadTitle = state.Title;
        resource.ObservedOwnership = new(ToPublicLabel(state.Owner), ToPublicLabel(state.Organization), state.OrganizationId,
            state.Owner == resource.Ownership.Owner, state.Organization is null && state.OrganizationId is null
                ? null : state.OrganizationId == resource.Ownership.OrganizationId);
        if (resource.State == DocumentLifecycleState.Created)
            resource.State = DocumentLifecycleState.Observed;
    }

    private static string? ToPublicLabel(string? value) => value is { Length: > 0 and <= 64 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') ? value : null;

    private static void AcknowledgeDeletion(TrackedDocument resource)
    {
        resource.DeletionAcknowledged = true;
        if (resource.State != DocumentLifecycleState.AbsenceVerified)
            resource.State = DocumentLifecycleState.DeletionAcknowledged;
    }

    private TrackedDocument Get(string reference) => resources.TryGetValue(reference, out var resource)
        ? resource : throw new InvalidOperationException("The logical resource is not registered.");

    private void Record(TrackedDocument resource, string? stepId, DocumentStepOperation operation, LifecycleEventKind kind,
        DocumentLifecycleState previous, SequenceStepOutcome? outcome = null, string? reason = null,
        ObservedDocumentOwnership? observation = null) =>
        history.Add(new(history.Count + 1, resource.CreateStep.Resource, resource.Id, stepId, operation, kind, previous,
            resource.State, outcome, reason, observation));

    private sealed record ReadObservation(OwnedDocumentState State, int Revision);

    private sealed class TrackedDocument(DocumentStep createStep, ExpectedDocumentOwnership ownership)
    {
        public DocumentStep CreateStep { get; } = createStep;
        public ExpectedDocumentOwnership Ownership { get; } = ownership;
        public Guid? Id { get; set; }
        public int CreationOrder { get; set; }
        public DocumentLifecycleState State { get; set; }
        public bool DeletionAcknowledged { get; set; }
        public ResourceCleanupStatus Cleanup { get; set; }
        public ObservedDocumentOwnership? ObservedOwnership { get; set; }
        public Dictionary<string, ReadObservation> Observations { get; } = new(StringComparer.Ordinal);
        public int Revision { get; set; }
        public string? LatestReadTitle { get; set; }
        public string? BeforeUpdate { get; set; }
        public string? UpdateTitle { get; set; }
    }
}
