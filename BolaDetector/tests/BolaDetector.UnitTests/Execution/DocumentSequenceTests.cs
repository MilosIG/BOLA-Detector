using System.Collections.Immutable;
using BolaDetector.Application;
using BolaDetector.Engine;
using NUnit.Framework;

namespace BolaDetector.UnitTests;

public class DocumentSequenceTests
{
    private static DocumentSequence Sequence(params DocumentStep?[] steps) =>
        new("unit-sequence", "Unit sequence", "alice", steps.ToImmutableArray());

    private static readonly DocumentStep Create = new("create", DocumentStepOperation.Create, "document", [], "Initial");
    private static readonly DocumentStep Read = new("read", DocumentStepOperation.Read, "document", ["create"]);
    private static readonly DocumentStep Verify = new("verify", DocumentStepOperation.Verify, "document", ["create", "read"], "Initial");

    [TestCase("empty", "SEQ_REQUIRED")]
    [TestCase("null-step", "SEQ_REQUIRED")]
    [TestCase("duplicate", "SEQ_DUPLICATE_STEP")]
    [TestCase("later", "SEQ_DEPENDENCY")]
    [TestCase("unproduced", "SEQ_RESOURCE")]
    [TestCase("duplicate-resource", "SEQ_RESOURCE")]
    [TestCase("missing-title", "SEQ_TITLE")]
    [TestCase("operation", "SEQ_OPERATION")]
    [TestCase("no-observation", "SEQ_OBSERVATION")]
    public void InvalidDefinitionIsRejectedBeforeTransport(string example, string code)
    {
        var sequence = example switch
        {
            "empty" => Sequence(),
            "null-step" => Sequence(Create, null),
            "duplicate" => Sequence(Create, Read with { Id = Create.Id }),
            "later" => Sequence(Create, Read with { DependsOn = ["create", "verify"] }, Verify),
            "unproduced" => Sequence(Create, Read with { Resource = "missing" }),
            "duplicate-resource" => Sequence(Create, Create with { Id = "duplicate" }),
            "missing-title" => Sequence(Create with { Title = null }),
            "operation" => Sequence(Create with { Operation = (DocumentStepOperation)100 }),
            _ => Sequence(Create, Verify with { DependsOn = ["create"] })
        };
        var stub = new Stub((_, _, _) => throw new AssertionException("Transport was called."));
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());

        var error = Assert.ThrowsAsync<ConfigurationValidationException>(async () =>
            await new DocumentSequenceExecutor(stub).ExecuteAsync(sequence, configuration, default));

        Assert.That(error!.Errors.Select(item => item.Code), Does.Contain(code));
    }

    [Test]
    public void AmbiguousOwnerIsRejectedBeforeTransport()
    {
        var configuration = DetectionTestFixture.Config();
        var owner = configuration.Identities.Single(identity => identity.Name == "alice");
        configuration.Identities.Add(owner with { Identity = owner.Identity with { Id = Guid.NewGuid() } });
        var snapshot = ValidatedDetectionConfiguration.Create(configuration);
        var stub = new Stub((_, _, _) => throw new AssertionException("Transport was called."));

        var error = Assert.ThrowsAsync<ConfigurationValidationException>(async () =>
            await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create), snapshot, default));

        Assert.That(error!.Errors.Select(item => item.Code), Does.Contain("SEQ_IDENTITY"));
    }

    [Test]
    public async Task SuccessfulSequenceUsesOneSnapshotAndPassesOutputsInOrder()
    {
        var configuration = DetectionTestFixture.Config();
        var snapshot = ValidatedDetectionConfiguration.Create(configuration);
        configuration.Identities.Clear();
        var id = Guid.NewGuid();
        var calls = new List<(DocumentStepOperation, Guid?)>();
        var stub = new Stub((operation, resource, _) =>
        {
            calls.Add((operation, resource));
            return operation switch
            {
                DocumentStepOperation.Create => new(DocumentOperationOutcome.Succeeded, CreatedId: id),
                DocumentStepOperation.Read => new(DocumentOperationOutcome.Succeeded, State: new("alice", "Initial")),
                DocumentStepOperation.VerifyAbsent => new(DocumentOperationOutcome.Absent),
                _ => new(DocumentOperationOutcome.Succeeded)
            };
        });
        var definition = Sequence(Create, Read, Verify,
            new("delete", DocumentStepOperation.Delete, "document", ["create", "verify"]),
            new("absent", DocumentStepOperation.VerifyAbsent, "document", ["create", "delete"]));

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(definition, snapshot, default);

        Assert.That(calls, Is.EqualTo(new (DocumentStepOperation, Guid?)[]
        {
            (DocumentStepOperation.Create, null), (DocumentStepOperation.Read, id),
            (DocumentStepOperation.Delete, id), (DocumentStepOperation.VerifyAbsent, id)
        }));
        Assert.That(stub.Snapshots, Is.All.SameAs(snapshot));
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Steps.Select(step => step.DocumentId), Is.All.EqualTo(id));
        Assert.That(result.Cleanup, Is.Empty);
    }

    [Test]
    public async Task MissingCreateOutputSkipsDependents()
    {
        var stub = new Stub((_, _, _) => new(DocumentOperationOutcome.Succeeded));
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create, Read, Verify), configuration, default);

        Assert.That(result.Steps.Select(step => step.Outcome), Is.EqualTo(new[]
        {
            SequenceStepOutcome.Failed, SequenceStepOutcome.Skipped, SequenceStepOutcome.Skipped
        }));
        Assert.That(result.Steps[0].Error, Is.EqualTo("MissingOrDuplicateCreatedId"));
        Assert.That(stub.Snapshots, Has.Count.EqualTo(1));
    }

    [TestCase(false, DocumentCleanupOutcome.Deleted)]
    [TestCase(true, DocumentCleanupOutcome.Failed)]
    public async Task FailedReadSkipsVerificationAndReportsCleanup(bool cleanupFails, DocumentCleanupOutcome expected)
    {
        var id = Guid.NewGuid();
        var stub = new Stub((operation, resource, _) => operation switch
        {
            DocumentStepOperation.Create => new(DocumentOperationOutcome.Succeeded, CreatedId: id),
            DocumentStepOperation.Delete when resource == id && !cleanupFails => new(DocumentOperationOutcome.Succeeded),
            _ => new(DocumentOperationOutcome.Failed)
        });
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create, Read, Verify), configuration, default);

        Assert.That(result.Steps[1].Outcome, Is.EqualTo(SequenceStepOutcome.Failed));
        Assert.That(result.Steps[2].Outcome, Is.EqualTo(SequenceStepOutcome.Skipped));
        Assert.That(result.Cleanup.Single().Outcome, Is.EqualTo(expected));
    }

    [Test]
    public async Task CancellationReachesTransportAndCleanupUsesIndependentToken()
    {
        using var cancellation = new CancellationTokenSource();
        var id = Guid.NewGuid();
        var stub = new Stub((operation, resource, token) =>
        {
            if (operation == DocumentStepOperation.Create)
                return new(DocumentOperationOutcome.Succeeded, CreatedId: id);
            if (operation == DocumentStepOperation.Read)
            {
                Assert.That(token, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }
            Assert.That(operation, Is.EqualTo(DocumentStepOperation.Delete));
            Assert.That(resource, Is.EqualTo(id));
            Assert.That(token.IsCancellationRequested, Is.False);
            return new(DocumentOperationOutcome.Succeeded);
        });
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create, Read, Verify),
            configuration, cancellation.Token);

        Assert.That(result.Cancelled, Is.True);
        Assert.That(result.Steps.Skip(1).Select(step => step.Outcome), Is.All.EqualTo(SequenceStepOutcome.Cancelled));
        Assert.That(result.Cleanup.Single().Outcome, Is.EqualTo(DocumentCleanupOutcome.Deleted));
    }

    [Test]
    public async Task ReusingExecutorKeepsResourceResolutionLocalToEachRun()
    {
        var stub = new Stub((operation, _, _) => operation == DocumentStepOperation.Create
            ? new(DocumentOperationOutcome.Succeeded, CreatedId: Guid.NewGuid())
            : new(DocumentOperationOutcome.Succeeded));
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());
        var executor = new DocumentSequenceExecutor(stub);

        var first = await executor.ExecuteAsync(Sequence(Create), configuration, default);
        var second = await executor.ExecuteAsync(Sequence(Create), configuration, default);

        Assert.That(first.Steps.Single().DocumentId, Is.Not.EqualTo(second.Steps.Single().DocumentId));
        Assert.That(first.Succeeded && second.Succeeded, Is.True);
    }

    private sealed class Stub(Func<DocumentStepOperation, Guid?, CancellationToken, DocumentOperationResult> execute)
        : IOwnedDocumentClient
    {
        public List<ValidatedDetectionConfiguration> Snapshots { get; } = [];

        public Task<DocumentOperationResult> ExecuteAsync(DocumentStepOperation operation, Guid? id, string? title,
            ConfiguredIdentity owner, ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
        {
            Snapshots.Add(configuration);
            return Task.FromResult(execute(operation, id, cancellationToken));
        }
    }
}
