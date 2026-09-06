using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// Declares the problem responses every operation can produce (400, 401, 403, 404, 429, 500) so the
/// OpenAPI document carries them without per-action attributes. An action that binds input declares
/// its 400 as <see cref="HttpValidationProblemDetails"/>; one that binds nothing declares plain
/// <see cref="ProblemDetails"/>. A status the action or controller already declares is left alone.
/// </summary>
/// <remarks>
/// The API explorer infers a 200 from an <c>ActionResult&lt;T&gt;</c> (or plain <c>T</c>) return
/// type only when the action declares no response metadata at all, and the problem responses are
/// response metadata. So when the action starts with none, the convention declares that inferred
/// 200 itself, and the success schema stays in the document.
/// </remarks>
public sealed class ProblemResponsesConvention : IActionModelConvention
{
    /// <summary>The statuses every operation declares, whatever its own attributes say.</summary>
    public static readonly IReadOnlyList<int> SharedStatusCodes =
    [
        StatusCodes.Status400BadRequest,
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden,
        StatusCodes.Status404NotFound,
        StatusCodes.Status429TooManyRequests,
        StatusCodes.Status500InternalServerError,
    ];

    /// <inheritdoc/>
    public void Apply(ActionModel action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // A bare [Produces("application/json")] is an IApiResponseMetadataProvider with a null Type:
        // it sets content types but declares no response, and the API explorer ignores it when
        // deciding whether to infer the 200. Mirror that so the inference survives it.
        HashSet<int> declared = action.Filters
            .Concat(action.Controller.Filters)
            .OfType<IApiResponseMetadataProvider>()
            .Where(provider => provider.Type is not null)
            .Select(provider => provider.StatusCode)
            .ToHashSet();

        bool acceptsInput = action.Parameters.Any(IsBoundInput);

        if (declared.Count == 0 && InferredSuccessType(action) is Type successType)
        {
            action.Filters.Add(new ProducesResponseTypeAttribute(successType, StatusCodes.Status200OK));
        }

        foreach (int status in SharedStatusCodes)
        {
            if (declared.Contains(status))
            {
                continue;
            }

            Type type = status == StatusCodes.Status400BadRequest && acceptsInput
                ? typeof(HttpValidationProblemDetails)
                : typeof(ProblemDetails);
            action.Filters.Add(new ProducesResponseTypeAttribute(type, status));
        }
    }

    /// <summary>
    /// The body type the API explorer would infer for a 200: <c>T</c> from <c>ActionResult&lt;T&gt;</c>
    /// or a plain <c>T</c> return, unwrapped from <see cref="Task{TResult}"/> or
    /// <see cref="ValueTask{TResult}"/>; <see langword="null"/> for <c>void</c>, a bare task, or any
    /// <see cref="IActionResult"/>, which say nothing about the body.
    /// </summary>
    private static Type? InferredSuccessType(ActionModel action)
    {
        Type returnType = action.ActionMethod.ReturnType;
        if (returnType.IsGenericType
            && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            returnType = returnType.GetGenericArguments()[0];
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        if (returnType == typeof(void)
            || returnType == typeof(Task)
            || returnType == typeof(ValueTask)
            || typeof(IActionResult).IsAssignableFrom(returnType)
            || typeof(IConvertToActionResult).IsAssignableFrom(returnType))
        {
            return null;
        }

        return returnType;
    }

    private static bool IsBoundInput(ParameterModel parameter) =>
        parameter.ParameterType != typeof(CancellationToken)
        && parameter.BindingInfo?.BindingSource != BindingSource.Services;
}
