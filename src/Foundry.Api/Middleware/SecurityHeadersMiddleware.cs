namespace Foundry.Api.Middleware;

internal sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isProduction;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _isProduction = environment.IsProduction();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        PathString path = context.Request.Path;

        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Content-Security-Policy"] = GetContentSecurityPolicy(path);

            if (_isProduction)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static string GetContentSecurityPolicy(PathString path)
    {
        if (path.HasValue && path.StartsWithSegments("/hangfire"))
        {
            return "default-src 'self'; " +
                   "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                   "style-src 'self' 'unsafe-inline'";
        }

        if (path.HasValue && path.StartsWithSegments("/scalar"))
        {
            return "default-src 'self'; " +
                   "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                   "style-src 'self' 'unsafe-inline'; " +
                   "img-src 'self' data:; " +
                   "font-src 'self' data:";
        }

        if (path.HasValue && path.StartsWithSegments("/hubs"))
        {
            return "default-src 'self'; " +
                   "connect-src 'self' ws: wss:";
        }

        return "default-src 'self'";
    }
}
