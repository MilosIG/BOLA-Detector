using BolaDetector.Application;

namespace BolaDetector.Infrastructure;

public sealed class LocalTargetAddressPolicy
{
    public Uri Build(string origin, string path, string resourceId)
    {
        // Exact numeric loopback/port restriction avoids DNS rebinding and accidental external scans.
        if (!DetectionInputPolicy.TryGetTargetOrigin(origin, out var root))
            throw new ArgumentException($"Only {DetectionInputPolicy.LocalOrigin} is allowed.");
        if (!DetectionInputPolicy.IsSupportedRoute(path) || !DetectionInputPolicy.IsSupportedResourceId(resourceId))
            throw new ArgumentException("A local path with exactly one {id} placeholder is required.");
        var uri = new Uri(root, path.Replace("{id}", Uri.EscapeDataString(resourceId)));
        if (uri.GetLeftPart(UriPartial.Authority) != root.GetLeftPart(UriPartial.Authority))
            throw new ArgumentException("Target origin changed.");
        return uri;
    }

}
