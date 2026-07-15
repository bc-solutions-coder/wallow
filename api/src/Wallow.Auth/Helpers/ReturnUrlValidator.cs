namespace Wallow.Auth.Helpers;

public static class ReturnUrlValidator
{
    /// <summary>
    /// Returns true if the URL is a safe relative path (starts with "/" but not "//").
    /// Rejects absolute URLs, protocol-relative URLs, and dangerous schemes.
    /// </summary>
    public static bool IsSafe(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Must start with exactly one forward slash (relative path).
        // Reject "//" (protocol-relative), "http://", "javascript:", etc.
        return url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the URL if safe, otherwise returns the fallback.
    /// </summary>
    public static string Sanitize(string? url, string fallback = "/")
    {
        return IsSafe(url) ? url! : fallback;
    }
}
