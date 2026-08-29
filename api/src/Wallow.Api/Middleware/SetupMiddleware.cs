using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Contracts.Setup;

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
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            ProblemDetails problem = new()
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "First-run setup is required.",
                Detail = "No administrator exists yet, so the API serves only its setup, health, "
                    + "and OIDC metadata surface. Check GET /v1/identity/setup/status and create "
                    + "the bootstrap admin via POST /v1/identity/setup/admin (or the /setup page "
                    + "of the auth frontend).",
            };

            IProblemDetailsService problemDetailsService =
                context.RequestServices.GetRequiredService<IProblemDetailsService>();
            if (!await problemDetailsService.TryWriteAsync(
                new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
            {
                // The default writer refuses an Accept header that admits no JSON; the body
                // is still owed to whoever reads it, so write the same document directly.
                await context.Response.WriteAsJsonAsync(
                    problem, options: null, contentType: "application/problem+json", context.RequestAborted);
            }

            return;
        }

        await _next(context);
    }
}
