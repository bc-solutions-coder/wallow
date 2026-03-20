namespace Wallow.Api.Middleware;

/// <summary>
/// Rewrites requests from /api/{path} to /api/v1/{path} when no version segment is present,
/// ensuring backward compatibility for clients that don't specify an API version.
/// </summary>
internal sealed class ApiVersionRewriteMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        string? path = context.Request.Path.Value;

        if (path is not null
            && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && !HasVersionSegment(path))
        {
            // Rewrite /api/{rest} → /api/v1/{rest}
            context.Request.Path = "/api/v1" + path[4..];
        }

        return next(context);
    }

    private static bool HasVersionSegment(string path)
    {
        // Check for /api/v{digit} pattern (e.g., /api/v1/..., /api/v2/...)
        return path.Length > 5
            && (path[5] == 'v' || path[5] == 'V')
            && path.Length > 6
            && char.IsDigit(path[6]);
    }
}
