using System.Text.Json;
using Wallow.Shared.Contracts.Setup;

namespace Wallow.Api.Middleware;

internal sealed class SetupMiddleware
{
    private const string SetupPath = "/api/v1/identity/setup";
    private readonly RequestDelegate _next;

    public SetupMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ISetupStatusProvider setupStatusProvider = context.RequestServices.GetRequiredService<ISetupStatusProvider>();
        bool setupRequired = await setupStatusProvider.IsSetupRequiredAsync(context.RequestAborted);

        if (setupRequired
            && !context.Request.Path.StartsWithSegments(SetupPath, StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/.well-known", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new { message = "Initial setup is required. Please complete setup at the /api/v1/identity/setup endpoint." },
                cancellationToken: context.RequestAborted);
            return;
        }

        await _next(context);
    }
}
