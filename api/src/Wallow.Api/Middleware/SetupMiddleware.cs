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
            // OpenAPI contract and Scalar docs are anonymous, development-only metadata
            // endpoints (not tenant operations) - keep them reachable before setup so
            // tooling and the CI OpenAPI drift check can read the contract.
            && !context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
        {
            // 503 with Setup.Required: the code tells the client what to do (the setup status
            // endpoint and the bootstrap-admin endpoint stay reachable); the detail is the
            // contract's fixed 5xx sentence.
            IProblemDetailsService problemDetailsService =
                context.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.TryWriteProblemAsync(context, SharedErrors.SetupRequired);
            return;
        }

        await _next(context);
    }
}
