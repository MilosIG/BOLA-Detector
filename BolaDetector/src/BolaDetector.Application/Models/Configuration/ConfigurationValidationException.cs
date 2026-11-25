namespace BolaDetector.Application;

public sealed class ConfigurationValidationException(IEnumerable<ConfigurationValidationError> errors)
    : ArgumentException("Detection configuration is invalid.")
{
    public IReadOnlyList<ConfigurationValidationError> Errors { get; } = Array.AsReadOnly(errors.ToArray());
}
