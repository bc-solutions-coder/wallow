namespace Wallow.Api.Middleware;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    private readonly bool _isProduction = environment.IsProduction();

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
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            }

            return Task.CompletedTask;
        });

        await next(context);
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

        // OpenIddict authorization endpoints may render HTML responses (e.g., consent pages,
        // error pages) that require inline scripts and styles to function correctly.
        if (path.HasValue && path.StartsWithSegments("/connect"))
        {
            return "default-src 'self'; " +
                   "script-src 'self' 'unsafe-inline'; " +
                   "style-src 'self' 'unsafe-inline'";
        }

        return "default-src 'self'";
    }
}
