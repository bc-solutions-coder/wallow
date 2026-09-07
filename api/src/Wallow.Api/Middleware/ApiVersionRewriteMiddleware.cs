namespace Wallow.Api.Middleware;

/// <summary>
/// Prefixes unversioned paths with /v1 except root and configured infrastructure prefixes.
/// Runs after PathBase extraction.
/// </summary>
internal sealed class ApiVersionRewriteMiddleware(RequestDelegate next)
{
    private static readonly string[] _skipPrefixes =
    [
        "/connect/",
        "/health",
        "/hubs/",
        "/events",
        "/alive",
        "/asyncapi",
        "/.well-known/",
        "/scim/",
        "/scalar/",
        "/openapi/",
        "/hangfire"
    ];

    public Task InvokeAsync(HttpContext context)
    {
        string? path = context.Request.Path.Value;

        if (!string.IsNullOrEmpty(path)
            && path.Length > 1
            && !IsSkipListed(path)
            && !HasVersionSegment(path))
        {
            context.Request.Path = "/v1" + path;
        }

        return next(context);
    }

    private static bool HasVersionSegment(string path)
    {
        // A leading v/V followed by a digit is treated as a version prefix.
        return path.Length > 2
            && (path[1] == 'v' || path[1] == 'V')
            && char.IsDigit(path[2]);
    }

    private static bool IsSkipListed(string path)
    {
        foreach (string prefix in _skipPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
