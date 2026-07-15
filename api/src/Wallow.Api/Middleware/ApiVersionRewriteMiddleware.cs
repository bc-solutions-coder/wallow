namespace Wallow.Api.Middleware;

/// <summary>
/// Rewrites unversioned requests to include a /v1 prefix when no version segment is present,
/// ensuring backward compatibility for clients that don't specify an API version.
/// PathBase is expected to have already stripped the /api prefix.
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
        "/error",
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
        // Check for /v{digit} pattern at start (e.g., /v1/..., /v2/...)
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
