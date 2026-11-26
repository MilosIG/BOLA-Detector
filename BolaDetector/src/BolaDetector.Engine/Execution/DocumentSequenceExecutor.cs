using BolaDetector.Application;

namespace BolaDetector.Engine;

public sealed class DocumentSequenceExecutor(IOwnedDocumentClient documents) : IDocumentSequenceExecutor
{
    public async Task<DocumentSequenceResult> ExecuteAsync(DocumentSequence sequence,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        var owner = DocumentSequenceValidator.Validate(sequence, configuration);
        var registry = new DocumentResourceRegistry(sequence, owner);
        var results = new Dictionary<string, DocumentStepResult>(StringComparer.Ordinal);
        var cleanup = new List<DocumentCleanupResult>();

        try
        {
            foreach (var entry in sequence.Steps)
            {
                var step = entry!;
                var dependencies = step.DependsOn.IsDefault ? [] : step.DependsOn;
                var result = cancellationToken.IsCancellationRequested
                    ? registry.RecordStepResult(step, SequenceStepOutcome.Cancelled)
                    : dependencies.Any(dependency => results[dependency].Outcome != SequenceStepOutcome.Succeeded)
                        ? registry.RecordStepResult(step, SequenceStepOutcome.Skipped, "PrerequisiteFailed")
                        : await ExecuteStepAsync(step, registry, owner, configuration, cancellationToken);
                results.Add(step.Id, result);
            }
        }
        finally
        {
            await CleanUpAsync(registry, owner, configuration, cleanup);
        }

        return new(sequence.Id, Array.AsReadOnly(sequence.Steps.Select(step => results[step!.Id]).ToArray()),
            cleanup.AsReadOnly(), cancellationToken.IsCancellationRequested)
        {
            Lifecycle = registry.Snapshot()
        };
    }

    private async Task<DocumentStepResult> ExecuteStepAsync(DocumentStep step, DocumentResourceRegistry registry,
        ConfiguredIdentity owner, ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!registry.CanExecute(step))
            return registry.RecordStepResult(step, SequenceStepOutcome.Failed, "ResourceUnavailable");

        registry.RecordAttempt(step);
        if (step.Operation == DocumentStepOperation.Verify)
            return registry.Verify(step);

        try
        {
            var result = await documents.ExecuteAsync(step.Operation, registry.Resolve(step.Resource), step.Title?.Trim(),
                owner, configuration, cancellationToken);
            return registry.Complete(step, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return registry.RecordStepResult(step, SequenceStepOutcome.Cancelled);
        }
    }

    private async Task CleanUpAsync(DocumentResourceRegistry registry, ConfiguredIdentity owner,
        ValidatedDetectionConfiguration configuration, List<DocumentCleanupResult> cleanup)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var (resource, id) in registry.PendingCleanup())
        {
            var outcome = DocumentCleanupOutcome.Failed;
            var error = "CleanupTimeout";
            if (!timeout.IsCancellationRequested)
            {
                registry.RecordCleanupAttempt(resource);
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
                    error = result.Error ?? "CleanupOperationFailed";
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    // Record the timed-out attempt and any remaining work as failed.
                }
            }
            registry.CompleteCleanup(resource, outcome, error);
            cleanup.Add(new(resource, outcome));
        }
    }
}
