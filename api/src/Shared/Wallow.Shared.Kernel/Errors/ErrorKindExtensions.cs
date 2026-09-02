using Microsoft.AspNetCore.Http;

namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// The one place an <see cref="ErrorKind"/> becomes an HTTP status code.
/// </summary>
public static class ErrorKindExtensions
{
    /// <summary>
    /// Maps the kind to the HTTP status code every writer answers it with.
    /// </summary>
    public static int ToHttpStatusCode(this ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.MethodNotAllowed => StatusCodes.Status405MethodNotAllowed,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorKind.Failure => StatusCodes.Status500InternalServerError,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown error kind"),
    };
}
