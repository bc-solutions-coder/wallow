using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Shared.Api.Extensions;

/// <summary>
/// Extension methods for converting Result objects to ActionResult responses.
/// Uses Problem Details format (RFC 7807) for errors.
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

    private static ObjectResult ToErrorResult(Error error)
    {
        int statusCode = GetStatusCode(error.Code);

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = error.Message,
            Type = GetTypeUri(statusCode),
            Extensions =
            {
                ["code"] = error.Code
            }
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }

    private static int GetStatusCode(string errorCode)
    {
        return errorCode switch
        {
            _ when errorCode.EndsWith(".NotFound", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            _ when errorCode.StartsWith("Validation", StringComparison.Ordinal) => StatusCodes.Status400BadRequest,
            _ when errorCode.StartsWith("Unauthorized", StringComparison.Ordinal) => StatusCodes.Status401Unauthorized,
            _ when errorCode.StartsWith("Forbidden", StringComparison.Ordinal) => StatusCodes.Status403Forbidden,
            _ when errorCode.StartsWith("Conflict", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
    }

    internal static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
            _ => "Error"
        };
    }

    internal static string GetTypeUri(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
            StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            StatusCodes.Status422UnprocessableEntity => "https://tools.ietf.org/html/rfc4918#section-11.2",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
    }
}
