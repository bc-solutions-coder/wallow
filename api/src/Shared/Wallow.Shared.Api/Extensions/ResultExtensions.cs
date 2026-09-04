using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Shared.Api.Extensions;

/// <summary>
/// Extension methods for converting Result objects to ActionResult responses.
/// Uses Problem Details format (RFC 7807) for errors; the status comes from the error's
/// <see cref="ErrorKind"/>, never from the text of its code.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new OkResult();
        }

        return ToErrorResult(result.Error);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return ToErrorResult(result.Error);
    }

    public static IActionResult ToCreatedResult<T>(
        this Result<T> result,
        string actionName,
        string controllerName,
        Func<T, object> routeValuesFactory)
    {
        if (result.IsSuccess)
        {
            object routeValues = routeValuesFactory(result.Value);
            return new CreatedAtActionResult(actionName, controllerName, routeValues, result.Value);
        }

        return ToErrorResult(result.Error);
    }

    public static IActionResult ToCreatedResult<T>(
        this Result<T> result,
        string location)
    {
        if (result.IsSuccess)
        {
            return new CreatedResult(location, result.Value);
        }

        return ToErrorResult(result.Error);
    }

    public static IActionResult ToNoContentResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return ToErrorResult(result.Error);
    }

    private static ProblemResult ToErrorResult(Error error) =>
        new(error.Kind.ToHttpStatusCode(), error.Code, error.Message);
}
