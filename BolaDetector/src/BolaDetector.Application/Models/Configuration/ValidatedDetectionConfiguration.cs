using System.Collections.Immutable;
using BolaDetector.Domain;

namespace BolaDetector.Application;

/// <summary>An owned, validated configuration snapshot for a single detection session.</summary>
public sealed class ValidatedDetectionConfiguration
{
    private ValidatedDetectionConfiguration(DetectionConfiguration copy)
    {
        ConfigurationValidator.EnsureValid(copy);
        TargetOrigin = copy.TargetOrigin;
        TimeoutSeconds = copy.TimeoutSeconds;
        Identities = copy.Identities.ToImmutableList();
        Resources = copy.Resources.ToImmutableList();
        Operations = copy.Operations.ToImmutableList();
        Rules = copy.Rules.ToImmutableList();
        ProtectedProperties = copy.ProtectedProperties.ToImmutableList();
    }

    public string TargetOrigin { get; }
    public int TimeoutSeconds { get; }
    public IReadOnlyList<ConfiguredIdentity> Identities { get; }
    public IReadOnlyList<TestResource> Resources { get; }
    public IReadOnlyList<ConfiguredOperation> Operations { get; }
    public IReadOnlyList<AccessRule> Rules { get; }
    public IReadOnlyList<string> ProtectedProperties { get; }

    public static ValidatedDetectionConfiguration Create(DetectionConfiguration? input)
    {
        if (input is null)
            throw new ConfigurationValidationException(ConfigurationValidator.Validate(null));

        // All entries and nested values are sealed records with init-only state.
        // Validate private copies so the frozen data is exactly what passed validation.
        return new ValidatedDetectionConfiguration(new DetectionConfiguration
        {
            TargetOrigin = input.TargetOrigin,
            TimeoutSeconds = input.TimeoutSeconds,
            Identities = CopyList(input.Identities),
            Resources = CopyList(input.Resources),
            Operations = CopyList(input.Operations),
            Rules = CopyList(input.Rules),
            ProtectedProperties = CopyList(input.ProtectedProperties)
        });
    }

    // Preserve explicit null from JSON for the structured validation result.
    private static List<T> CopyList<T>(List<T>? source) => source?.ToList()!;
}
