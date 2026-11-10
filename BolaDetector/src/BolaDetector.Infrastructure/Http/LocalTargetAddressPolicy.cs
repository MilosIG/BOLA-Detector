namespace BolaDetector.Infrastructure;

public sealed class LocalTargetAddressPolicy
{
    public Uri Build(string origin, string path, string resourceId)
    {
        // Exact numeric loopback/port restriction avoids DNS rebinding and accidental external scans.
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var root) || root.Scheme != "http" ||
            root.Host != "127.0.0.1" || root.Port != 5081 || root.AbsolutePath != "/" ||
            root.UserInfo.Length != 0 || root.Query.Length != 0 || root.Fragment.Length != 0)
            throw new ArgumentException("Only http://127.0.0.1:5081 is allowed.");
        if (!path.StartsWith('/') || path.StartsWith("//") || path.Contains('\\') || path.Contains('?') || path.Contains('#') ||
            path.Split("{id}").Length != 2 || path.Replace("{id}", "").Contains('{') || string.IsNullOrWhiteSpace(resourceId) || resourceId is "." or "..")
            throw new ArgumentException("A local path with exactly one {id} placeholder is required.");
        var uri = new Uri(root, path.Replace("{id}", Uri.EscapeDataString(resourceId)));
        if (uri.GetLeftPart(UriPartial.Authority) != root.GetLeftPart(UriPartial.Authority))
            throw new ArgumentException("Target origin changed.");
        return uri;
    }

}
