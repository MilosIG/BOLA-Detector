using BolaDetector.Application;

namespace BolaDetector.Engine;

public sealed class DocumentSequenceExecutor(IOwnedDocumentClient documents) : IDocumentSequenceExecutor
{
    public async Task<DocumentSequenceResult> ExecuteAsync(DocumentSequence sequence,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        var owner = DocumentSequenceValidator.Validate(sequence, configuration);
        var resources = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var pendingCleanup = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var results = new Dictionary<string, DocumentStepResult>(StringComparer.Ordinal);
        var observations = new Dictionary<(string StepId, string Resource), OwnedDocumentState>();
        var cleanup = new List<DocumentCleanupResult>();

        try
        {
            foreach (var entry in sequence.Steps)
            {
                var step = entry!;
                var dependencies = step.DependsOn.IsDefault ? [] : step.DependsOn;
                var id = resources.TryGetValue(step.Resource, out var resolved) ? (Guid?)resolved : null;
                DocumentStepResult result;

                if (cancellationToken.IsCancellationRequested)
                    result = new(step.Id, SequenceStepOutcome.Cancelled);
                else if (dependencies.Any(dependency => results[dependency].Outcome != SequenceStepOutcome.Succeeded))
                    result = new(step.Id, SequenceStepOutcome.Skipped, "PrerequisiteFailed");
                else if (step.Operation == DocumentStepOperation.Verify)
                {
                    var observed = dependencies.Select(key => observations.GetValueOrDefault((key, step.Resource)))
                        .SingleOrDefault(state => state is not null);
                    result = observed == new OwnedDocumentState(owner.Name, step.Title!.Trim())
                        ? new(step.Id, SequenceStepOutcome.Succeeded, DocumentId: id)
                        : new(step.Id, SequenceStepOutcome.Failed, "UnexpectedDocumentState", id);
                }
                else
                {
                    try
                    {
                        var operation = await documents.ExecuteAsync(step.Operation, id, step.Title?.Trim(),
                            owner, configuration, cancellationToken);
                        result = RecordOperation(step, id, operation);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        result = new(step.Id, SequenceStepOutcome.Cancelled);
                    }
                }

                results.Add(step.Id, result);
            }
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            foreach (var (resource, id) in pendingCleanup.Reverse())
            {
                var outcome = DocumentCleanupOutcome.Failed;
                if (!timeout.IsCancellationRequested)
                {
                    try
                    {
                        var result = await documents.ExecuteAsync(DocumentStepOperation.Delete, id, null,
                            owner, configuration, timeout.Token);
                        outcome = result.Outcome switch
                        {
                            DocumentOperationOutcome.Succeeded => DocumentCleanupOutcome.Deleted,
                            DocumentOperationOutcome.Absent => DocumentCleanupOutcome.AlreadyAbsent,
                            _ => DocumentCleanupOutcome.Failed
                        };
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                    {
                        // Record this attempt and any remaining work as failed.
                    }
                }
                cleanup.Add(new(resource, outcome));
            }
        }

        return new(sequence.Id, Array.AsReadOnly(sequence.Steps.Select(step => results[step!.Id]).ToArray()),
            cleanup.AsReadOnly(), cancellationToken.IsCancellationRequested);

        DocumentStepResult RecordOperation(DocumentStep step, Guid? id, DocumentOperationResult operation)
        {
            if (operation.Outcome == DocumentOperationOutcome.Failed)
                return new(step.Id, SequenceStepOutcome.Failed, operation.Error ?? "OperationFailed", id);

            switch (step.Operation)
            {
                case DocumentStepOperation.Create when operation.Outcome == DocumentOperationOutcome.Succeeded:
                    if (operation.CreatedId is not { } created || created == Guid.Empty || resources.ContainsValue(created))
                        return new(step.Id, SequenceStepOutcome.Failed, "MissingOrDuplicateCreatedId");
                    resources.Add(step.Resource, created);
                    pendingCleanup.Add(step.Resource, created);
                    id = created;
                    break;
                case DocumentStepOperation.Read when operation.Outcome == DocumentOperationOutcome.Succeeded:
                    if (operation.State is null)
                        return new(step.Id, SequenceStepOutcome.Failed, "MissingDocumentState", id);
                    observations.Add((step.Id, step.Resource), operation.State);
                    break;
                case DocumentStepOperation.Delete when operation.Outcome == DocumentOperationOutcome.Succeeded:
                    pendingCleanup.Remove(step.Resource);
                    break;
                case DocumentStepOperation.VerifyAbsent when operation.Outcome == DocumentOperationOutcome.Absent:
                case DocumentStepOperation.Update when operation.Outcome == DocumentOperationOutcome.Succeeded:
                    break;
                default:
                    return new(step.Id, SequenceStepOutcome.Failed, "UnexpectedOperationOutcome", id);
            }

            return new(step.Id, SequenceStepOutcome.Succeeded, DocumentId: id);
        }
    }
}
