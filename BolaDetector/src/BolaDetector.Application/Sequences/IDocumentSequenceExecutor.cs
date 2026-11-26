namespace BolaDetector.Application;

public interface IDocumentSequenceExecutor
{
    Task<DocumentSequenceResult> ExecuteAsync(DocumentSequence sequence,
        ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken);
}
