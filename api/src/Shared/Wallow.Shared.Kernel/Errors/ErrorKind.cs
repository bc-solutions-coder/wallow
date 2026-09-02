namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// The class of failure an error represents. The kind, never the code's text, decides the HTTP
/// status a failure is answered with (<see cref="ErrorKindExtensions.ToHttpStatusCode"/>).
/// </summary>
public enum ErrorKind
{
    /// <summary>The request was malformed or failed validation (400).</summary>
    Validation,

    /// <summary>The caller is not authenticated (401).</summary>
    Unauthenticated,

    /// <summary>The caller is authenticated but not allowed to do this (403).</summary>
    Forbidden,

    /// <summary>The target does not exist or is not visible to the caller (404).</summary>
    NotFound,

    /// <summary>The route exists but not for this HTTP method (405).</summary>
    MethodNotAllowed,

    /// <summary>The request conflicts with the current state of the target (409).</summary>
    Conflict,

    /// <summary>The request was well-formed but a business rule refuses it (422).</summary>
    BusinessRule,

    /// <summary>The caller exceeded a rate limit (429).</summary>
    RateLimited,

    /// <summary>The server failed (500).</summary>
    Failure,

    /// <summary>The service cannot answer right now, such as before setup completes (503).</summary>
    Unavailable,
}
