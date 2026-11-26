namespace BolaDetector.Application;

public static class DocumentSequenceValidator
{
    public static ConfiguredIdentity Validate(DocumentSequence sequence, ValidatedDetectionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(configuration);
        var errors = new List<ConfigurationValidationError>();
        if (string.IsNullOrWhiteSpace(sequence.Id) || string.IsNullOrWhiteSpace(sequence.Name))
            errors.Add(new("SEQ_REQUIRED", "sequence", "An identifier and descriptive name are required."));
        var owners = configuration.Identities.Where(identity => identity.Name == "alice").Take(2).ToArray();
        if (sequence.Identity != "alice" || owners.Length != 1)
            errors.Add(new("SEQ_IDENTITY", "sequence.identity", "Exactly one configured Alice owner is required."));
        if (sequence.Steps.IsDefaultOrEmpty)
            errors.Add(new("SEQ_REQUIRED", "sequence.steps", "At least one step is required."));
        else
            ValidateSteps(sequence, errors);
        if (errors.Count > 0)
            throw new ConfigurationValidationException(errors);
        return owners[0];
    }

    private static void ValidateSteps(DocumentSequence sequence, List<ConfigurationValidationError> errors)
    {
        var preceding = new Dictionary<string, DocumentStep>(StringComparer.Ordinal);
        var producers = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < sequence.Steps.Length; index++)
        {
            var step = sequence.Steps[index];
            var path = $"sequence.steps[{index}]";
            if (step is null || string.IsNullOrWhiteSpace(step.Id) || string.IsNullOrWhiteSpace(step.Resource))
            {
                errors.Add(new("SEQ_REQUIRED", path, "A step identifier and logical resource reference are required."));
                continue;
            }

            if (!Enum.IsDefined(step.Operation))
                errors.Add(new("SEQ_OPERATION", path + ".operation", "Unsupported document operation."));

            var dependencies = step.DependsOn.IsDefault ? [] : step.DependsOn;
            if (dependencies.Any(id => string.IsNullOrWhiteSpace(id) || !preceding.ContainsKey(id)) ||
                dependencies.Distinct(StringComparer.Ordinal).Count() != dependencies.Length)
                errors.Add(new("SEQ_DEPENDENCY", path + ".dependsOn", "Dependencies must uniquely identify preceding steps."));

            if (step.Operation == DocumentStepOperation.Create)
            {
                if (!producers.TryAdd(step.Resource, step.Id))
                    errors.Add(new("SEQ_RESOURCE", path + ".resource", "A logical resource can have only one producer."));
            }
            else if (!producers.TryGetValue(step.Resource, out var producer) || !dependencies.Contains(producer))
                errors.Add(new("SEQ_RESOURCE", path + ".resource", "A dependency on the resource's Create step is required."));

            if (step.Operation == DocumentStepOperation.Verify &&
                dependencies.Count(id => id is not null && preceding.TryGetValue(id, out var read) &&
                    read.Operation == DocumentStepOperation.Read && read.Resource == step.Resource) != 1)
                errors.Add(new("SEQ_OBSERVATION", path + ".dependsOn", "Verification requires one Read dependency for this resource."));

            var needsTitle = step.Operation is DocumentStepOperation.Create or DocumentStepOperation.Update or DocumentStepOperation.Verify;
            if (needsTitle ? step.Title is null || step.Title.Trim().Length is < 1 or > 200 : step.Title is not null)
                errors.Add(new("SEQ_TITLE", path + ".title", "Supply a title of 1 to 200 characters only for operations that require one."));

            if (!preceding.TryAdd(step.Id, step))
                errors.Add(new("SEQ_DUPLICATE_STEP", path + ".id", "Step identifiers must be unique."));
        }
    }
}
