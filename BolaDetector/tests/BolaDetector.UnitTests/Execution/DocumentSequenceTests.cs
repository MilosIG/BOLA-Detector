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
        var resource = result.Lifecycle!.Resources.Single();
        Assert.That(resource.DocumentId, Is.Null);
        Assert.That(resource.CreationConfirmed, Is.False);
        Assert.That(resource.State, Is.EqualTo(DocumentLifecycleState.CreateUnconfirmed));
        Assert.That(result.Lifecycle.HasUnresolvedCleanup, Is.True);
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
            DocumentStepOperation.VerifyAbsent => new(DocumentOperationOutcome.Absent),
            _ => new(DocumentOperationOutcome.Failed)
        });
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create, Read, Verify), configuration, default);

        Assert.That(result.Steps[1].Outcome, Is.EqualTo(SequenceStepOutcome.Failed));
        Assert.That(result.Steps[2].Outcome, Is.EqualTo(SequenceStepOutcome.Skipped));
        Assert.That(result.Cleanup.Single().Outcome, Is.EqualTo(expected));
        var resource = result.Lifecycle!.Resources.Single();
        Assert.That(resource.DocumentId, Is.EqualTo(id));
        Assert.That(resource.State, Is.EqualTo(cleanupFails
            ? DocumentLifecycleState.Created : DocumentLifecycleState.AbsenceVerified));
        Assert.That(resource.CleanupStatus, Is.EqualTo(cleanupFails ? ResourceCleanupStatus.Failed : ResourceCleanupStatus.Completed));
        Assert.That(resource.AbsenceVerified, Is.EqualTo(!cleanupFails));
        Assert.That(result.Lifecycle.HasUnresolvedCleanup, Is.EqualTo(cleanupFails));
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
            Assert.That(operation, Is.AnyOf(DocumentStepOperation.Delete, DocumentStepOperation.VerifyAbsent));
            Assert.That(resource, Is.EqualTo(id));
            Assert.That(token.IsCancellationRequested, Is.False);
            return new(operation == DocumentStepOperation.Delete ? DocumentOperationOutcome.Succeeded : DocumentOperationOutcome.Absent);
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
            : new(operation == DocumentStepOperation.VerifyAbsent ? DocumentOperationOutcome.Absent : DocumentOperationOutcome.Succeeded));
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());
        var executor = new DocumentSequenceExecutor(stub);

        var first = await executor.ExecuteAsync(Sequence(Create), configuration, default);
        var second = await executor.ExecuteAsync(Sequence(Create), configuration, default);

        Assert.That(first.Steps.Single().DocumentId, Is.Not.EqualTo(second.Steps.Single().DocumentId));
        Assert.That(first.Succeeded && second.Succeeded, Is.True);
        Assert.That(first.Lifecycle!.ExecutionId, Is.Not.EqualTo(second.Lifecycle!.ExecutionId));
        Assert.That(first.Lifecycle.Resources[0].CreationKey, Is.Not.EqualTo(second.Lifecycle.Resources[0].CreationKey));
        var snapshot = first.Lifecycle.Resources.SetItem(0, first.Lifecycle.Resources[0] with { DocumentId = null });
        Assert.That(snapshot[0].DocumentId, Is.Null);
        Assert.That(first.Lifecycle.Resources[0].DocumentId, Is.EqualTo(first.Steps.Single().DocumentId));
    }

    [Test]
    public async Task ExecuteAsync_WhenCancelledBeforeCreate_KeepsRegistrationPending()
    {
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());
        var stub = new Stub((_, _, _) => throw new AssertionException("Transport was called."));

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create), configuration,
            new CancellationToken(canceled: true));

        Assert.That(result.Lifecycle!.Resources.Single().State, Is.EqualTo(DocumentLifecycleState.PendingCreation));
        Assert.That(result.Lifecycle.History.Single().Outcome, Is.EqualTo(SequenceStepOutcome.Cancelled));
        Assert.That(result.Lifecycle.HasUnresolvedCleanup, Is.False);
    }

    [Test]
    public async Task ExecuteAsync_WhenCreateReturnsDuplicateId_DoesNotBindOrCleanUpTheSecondResource()
    {
        var id = Guid.NewGuid();
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());
        var stub = new Stub((operation, _, _) => operation == DocumentStepOperation.Create
            ? new(DocumentOperationOutcome.Succeeded, CreatedId: id)
            : new(operation == DocumentStepOperation.VerifyAbsent ? DocumentOperationOutcome.Absent : DocumentOperationOutcome.Succeeded));

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(
            Sequence(Create, Create with { Id = "other-create", Resource = "other" }), configuration, default);

        Assert.That(result.Lifecycle!.Resources.Single(resource => resource.Resource == "other").DocumentId, Is.Null);
        Assert.That(result.Lifecycle.HasUnresolvedCleanup, Is.True);
        Assert.That(result.Cleanup.Single().Resource, Is.EqualTo(Create.Resource));
        Assert.That(stub.Snapshots, Has.Count.EqualTo(4));
        Assert.That(result.Lifecycle.Resources.Select(resource => resource.CreationKey).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task ExecuteAsync_WhenObservedOwnershipConflicts_RetainsExpectationsAndRecordsMismatch()
    {
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());
        var expected = configuration.Identities.Single(identity => identity.Name == "alice");
        var stub = new Stub((operation, _, _) => operation switch
        {
            DocumentStepOperation.Create => new(DocumentOperationOutcome.Succeeded, CreatedId: Guid.NewGuid()),
            DocumentStepOperation.Read => new(DocumentOperationOutcome.Succeeded,
                State: new("bob", "Initial") { Organization = "org-b" }),
            _ => new(DocumentOperationOutcome.Succeeded)
        });

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create, Read, Verify), configuration, default);

        var resource = result.Lifecycle!.Resources.Single();
        Assert.That(resource.ExpectedOwnership, Is.EqualTo(new ExpectedDocumentOwnership(expected.Identity.Id, expected.Name,
            expected.Identity.OrganizationId)));
        Assert.That(resource.ObservedOwnership, Is.EqualTo(new ObservedDocumentOwnership("bob", "org-b", null, false, false)));
        Assert.That(result.Steps[2].Error, Is.EqualTo("UnexpectedDocumentState"));
        var read = result.Lifecycle.History.Single(item => item.StepId == "read" && item.Kind == LifecycleEventKind.StepResult);
        Assert.That(read.Observation, Is.EqualTo(resource.ObservedOwnership));
        Assert.That(read.State, Is.EqualTo(DocumentLifecycleState.Observed));
    }

    [Test]
    public async Task ExecuteAsync_WhenAbsenceCheckFails_KeepsDeleteAcknowledgementWithoutRepeatingCleanup()
    {
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());
        var stub = new Stub((operation, _, _) => operation switch
        {
            DocumentStepOperation.Create => new(DocumentOperationOutcome.Succeeded, CreatedId: Guid.NewGuid()),
            DocumentStepOperation.Delete => new(DocumentOperationOutcome.Succeeded),
            DocumentStepOperation.VerifyAbsent => new(DocumentOperationOutcome.Failed, "UnexpectedStatus"),
            _ => throw new AssertionException("An operation on the deleted resource reached the transport.")
        });
        var definition = Sequence(Create,
            new("delete", DocumentStepOperation.Delete, "document", ["create"]),
            new("absent", DocumentStepOperation.VerifyAbsent, "document", ["create", "delete"]),
            Read with { DependsOn = ["create", "delete"] });

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(definition, configuration, default);

        var resource = result.Lifecycle!.Resources.Single();
        Assert.That(resource.State, Is.EqualTo(DocumentLifecycleState.DeletionAcknowledged));
        Assert.That(resource.AbsenceVerified, Is.False);
        Assert.That(resource.CleanupStatus, Is.EqualTo(ResourceCleanupStatus.Failed));
        Assert.That(result.Steps[3].Error, Is.EqualTo("ResourceUnavailable"));
        Assert.That(result.Cleanup, Is.Empty);
        Assert.That(stub.Snapshots, Has.Count.EqualTo(3));
    }

    [TestCase("confirmed", null)]
    [TestCase("foreign-owner", "RecoveryOwnerMismatch")]
    [TestCase("foreign-org", "RecoveryOrganizationMismatch")]
    [TestCase("different-key", "CreationIntentMismatch")]
    [TestCase("different-intent", "CreationIntentMismatch")]
    [TestCase("empty-id", "InvalidCreationEvidence")]
    [TestCase("missing", "Missing")]
    [TestCase("unverified-absence", null)]
    public async Task ExecuteAsync_WhenCreateIsAmbiguous_RequiresVerifiedEvidenceAndRetainsOriginalFailure(
        string variant, string? recoveryError)
    {
        using var caller = new CancellationTokenSource();
        var configuration = ValidatedDetectionConfiguration.Create(DetectionTestFixture.Config());
        var documentId = Guid.NewGuid();
        var stub = new RecoveryStub((intent, owner) =>
        {
            if (variant == "missing")
                return new(DocumentRecoveryOutcome.Missing);
            return new(DocumentRecoveryOutcome.Confirmed, new(
                variant == "different-key" ? Guid.NewGuid() : intent.Key,
                variant == "empty-id" ? Guid.Empty : documentId,
                variant == "foreign-owner" ? "bob" : owner.Name,
                variant == "foreign-org" ? Guid.NewGuid() : owner.Identity.OrganizationId,
                variant != "different-intent"));
        }, () => caller.Cancel(), variant != "unverified-absence");

        var result = await new DocumentSequenceExecutor(stub).ExecuteAsync(Sequence(Create, Read), configuration, caller.Token);

        var resource = result.Lifecycle!.Resources.Single();
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Cancelled, Is.True);
        Assert.That(result.Steps[0].Error, Is.EqualTo("CreateUnconfirmed"));
        Assert.That(resource.CreationKey, Is.EqualTo(stub.OriginalIntent!.Key));
        Assert.That(resource.CreationRecovered, Is.EqualTo(recoveryError is null));
        Assert.That(resource.DocumentId, Is.EqualTo(recoveryError is null ? documentId : (Guid?)null));
        Assert.That(result.Lifecycle.HasUnresolvedCleanup, Is.EqualTo(variant != "confirmed"));
        Assert.That(result.Lifecycle.History.Single(item => item.Kind == LifecycleEventKind.RecoveryResult).Reason,
            Is.EqualTo(recoveryError ?? "Confirmed"));
        Assert.That(stub.CleanupCalls, Is.EqualTo(recoveryError is null
            ? new[] { DocumentStepOperation.Delete, DocumentStepOperation.VerifyAbsent } : Array.Empty<DocumentStepOperation>()));
        if (variant == "unverified-absence")
            Assert.That(resource.State, Is.EqualTo(DocumentLifecycleState.DeletionAcknowledged));
    }

    private sealed class RecoveryStub(Func<DocumentCreationIntent, ConfiguredIdentity, DocumentRecoveryResult> recover,
        Action cancelCaller, bool verifiesAbsence) : ICorrelatedDocumentClient
    {
        public DocumentCreationIntent? OriginalIntent { get; private set; }
        public List<DocumentStepOperation> CleanupCalls { get; } = [];
        private CancellationToken recoveryToken;

        public Task<DocumentRecoveryResult> CreateAsync(DocumentCreationIntent intent, ConfiguredIdentity owner,
            ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
        {
            Assert.That(intent.Key, Is.Not.EqualTo(Guid.Empty));
            OriginalIntent = intent;
            cancelCaller();
            return Task.FromResult(new DocumentRecoveryResult(DocumentRecoveryOutcome.TransportFailure));
        }

        public Task<DocumentRecoveryResult> RecoverAsync(DocumentCreationIntent intent, ConfiguredIdentity owner,
            ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
        {
            Assert.That(intent, Is.EqualTo(OriginalIntent));
            Assert.That(cancellationToken.CanBeCanceled, Is.True);
            Assert.That(cancellationToken.IsCancellationRequested, Is.False);
            recoveryToken = cancellationToken;
            return Task.FromResult(recover(intent, owner));
        }

        public Task<DocumentOperationResult> ExecuteAsync(DocumentStepOperation operation, Guid? id, string? title,
            ConfiguredIdentity owner, ValidatedDetectionConfiguration configuration, CancellationToken cancellationToken)
        {
            Assert.That(cancellationToken, Is.EqualTo(recoveryToken));
            CleanupCalls.Add(operation);
            return Task.FromResult(new DocumentOperationResult(operation == DocumentStepOperation.VerifyAbsent
                ? verifiesAbsence ? DocumentOperationOutcome.Absent : DocumentOperationOutcome.Failed
                : DocumentOperationOutcome.Succeeded));
        }
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
