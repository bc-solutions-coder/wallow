using Wallow.Shared.Api.Problems;
using Wallow.Shared.Contracts.Setup;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Api.Middleware;

internal sealed class SetupMiddleware
{
    private const string SetupPath = "/v1/identity/setup";
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
            && !context.Request.Path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase)
            // Keep contract and API-reference endpoints reachable before setup.
            && !context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
        {
            // Setup.Required directs clients to the still-accessible setup endpoints.
            IProblemDetailsService problemDetailsService =
                context.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.TryWriteProblemAsync(context, SharedErrors.SetupRequired);
            return;
        }

        await _next(context);
    }
}
