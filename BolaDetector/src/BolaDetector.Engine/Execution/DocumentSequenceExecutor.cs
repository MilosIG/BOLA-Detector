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
            if (step.Operation == DocumentStepOperation.Create && documents is ICorrelatedDocumentClient correlated)
            {
                var creation = await correlated.CreateAsync(registry.GetIntent(step.Resource), owner, configuration, cancellationToken);
                return registry.CompleteCorrelatedCreate(step, creation);
            }

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
        await RecoverAsync(registry, owner, configuration, timeout.Token);
        foreach (var (resource, id, skipDelete) in registry.PendingCleanup())
        {
            if (skipDelete)
            {
                if (!await VerifyCleanupAsync(registry, resource, id, owner, configuration, timeout.Token))
                    cleanup.Add(new(resource, DocumentCleanupOutcome.Failed));
                continue;
            }

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
            if (outcome != DocumentCleanupOutcome.Failed &&
                !await VerifyCleanupAsync(registry, resource, id, owner, configuration, timeout.Token))
                outcome = DocumentCleanupOutcome.Failed;
            cleanup.Add(new(resource, outcome));
        }
    }

    private async Task RecoverAsync(DocumentResourceRegistry registry, ConfiguredIdentity owner,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        foreach (var resource in registry.PendingRecovery())
        {
            var result = new DocumentRecoveryResult(DocumentRecoveryOutcome.Unsupported);
            string? failure = null;
            if (documents is ICorrelatedDocumentClient correlated && !cancellationToken.IsCancellationRequested)
            {
                registry.RecordRecoveryAttempt(resource);
                try
                {
                    result = await correlated.RecoverAsync(registry.GetIntent(resource), owner, configuration, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    failure = "RecoveryTimeout";
                }
            }
            if (cancellationToken.IsCancellationRequested)
                failure = "RecoveryTimeout";
            registry.CompleteRecovery(resource, result, failure);
        }
    }

    private async Task<bool> VerifyCleanupAsync(DocumentResourceRegistry registry, string resource, Guid id,
        ConfiguredIdentity owner, ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
    {
        var error = "CleanupVerificationTimeout";
        if (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await documents.ExecuteAsync(DocumentStepOperation.VerifyAbsent, id, null, owner, configuration,
                    cancellationToken);
                if (result.Outcome == DocumentOperationOutcome.Absent)
                {
                    registry.CompleteCleanupVerification(resource, absent: true, error: null);
                    return true;
                }
                error = result.Error ?? "CleanupAbsenceUnconfirmed";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Preserve the deletion acknowledgement when verification times out.
            }
        }
        registry.CompleteCleanupVerification(resource, absent: false, error);
        return false;
    }
}
