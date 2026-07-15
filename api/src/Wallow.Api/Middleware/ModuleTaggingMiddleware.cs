using System.Text.RegularExpressions;

namespace Wallow.Api.Middleware;

internal sealed partial class ModuleTaggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (System.Diagnostics.Activity.Current is { } activity)
        {
            Endpoint? endpoint = context.GetEndpoint();
            System.Reflection.TypeInfo? controllerType = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()?.ControllerTypeInfo;

            if (controllerType?.Namespace is { } ns)
            {
                Match match = ModuleNamePattern().Match(ns);
                if (match.Success)
                {
                    activity.SetTag("wallow.module", match.Groups[1].Value);
                }
            }
        }

        await next(context);
    }

    [GeneratedRegex(@"^Wallow\.(\w+)\.Api\b", RegexOptions.NonBacktracking)]
    private static partial Regex ModuleNamePattern();
}
