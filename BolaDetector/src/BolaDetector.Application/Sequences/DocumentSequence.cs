using System.Collections.Immutable;

namespace BolaDetector.Application;

public enum DocumentStepOperation { Create, Read, Verify, Update, Delete, VerifyAbsent }

public sealed record DocumentStep(string Id, DocumentStepOperation Operation, string Resource,
    ImmutableArray<string> DependsOn, string? Title = null);

public sealed record DocumentSequence(string Id, string Name, string Identity, ImmutableArray<DocumentStep?> Steps);
