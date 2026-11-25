using System.Diagnostics.CodeAnalysis;

namespace BolaDetector.Application;

public static class DetectionInputPolicy
{
    public const string LocalOrigin = "http://127.0.0.1:5081";

    public static bool TryGetTargetOrigin(string? origin, [NotNullWhen(true)] out Uri? root)
    {
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == "http" &&
            uri.Host == "127.0.0.1" && uri.Port == 5081 && uri.AbsolutePath == "/" &&
            uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0)
        {
            root = uri;
            return true;
        }

        root = null;
        return false;
    }

    public static bool IsSupportedRoute(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\') || path.Contains('?') || path.Contains('#') || path.Any(char.IsControl))
            return false;

        var parts = path.Split("{id}", StringSplitOptions.None);
        return parts.Length == 2 && parts.All(part => !part.Contains('{') && !part.Contains('}'));
    }

    public static bool IsSupportedResourceId(string? id) => !string.IsNullOrWhiteSpace(id) && id is not ("." or "..");

    public static bool IsProtectedProperty(string? name) => !string.IsNullOrWhiteSpace(name) && name != "id";

    public static bool TryGetCredentialVariable(string? reference, out string variable)
    {
        variable = reference?.StartsWith("env:", StringComparison.Ordinal) == true ? reference[4..] : "";
        return !string.IsNullOrWhiteSpace(variable) && !variable.Any(char.IsWhiteSpace) &&
            !variable.Contains('=') && !variable.Contains('\0');
    }
}
