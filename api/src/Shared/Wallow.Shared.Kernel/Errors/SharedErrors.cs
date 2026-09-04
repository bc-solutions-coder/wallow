namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// The status-generic entries every host carries. These are the codes a response falls back to
/// when nothing more specific applies (a framework 404, an authentication challenge, an
/// unhandled exception) and the only entries the shared kernel owns; everything else belongs to
/// the module that raises it.
/// </summary>
public static class SharedErrors
{
    /// <summary>The request failed validation (400).</summary>
    public static readonly ErrorCatalogEntry ValidationFailed = new(
        "Validation.Failed",
        ErrorKind.Validation,
        "The request is invalid.");

    /// <summary>The caller is not authenticated (401).</summary>
    public static readonly ErrorCatalogEntry Unauthenticated = new(
        "Auth.Unauthenticated",
        ErrorKind.Unauthenticated,
        "Sign in to continue.");

    /// <summary>The caller is not allowed to do this (403).</summary>
    public static readonly ErrorCatalogEntry Forbidden = new(
        "Auth.Forbidden",
        ErrorKind.Forbidden,
        "You do not have permission to do this.");

    /// <summary>Nothing exists at the requested location (404).</summary>
    public static readonly ErrorCatalogEntry NotFound = new(
        "Http.NotFound",
        ErrorKind.NotFound,
        "The requested resource was not found.");

    /// <summary>The route exists but not for this method (405).</summary>
    public static readonly ErrorCatalogEntry MethodNotAllowed = new(
        "Http.MethodNotAllowed",
        ErrorKind.MethodNotAllowed,
        "That method is not allowed here.");

    /// <summary>The caller exceeded a rate limit (429).</summary>
    public static readonly ErrorCatalogEntry RateLimitExceeded = new(
        "RateLimit.Exceeded",
        ErrorKind.RateLimited,
        "Too many requests. Try again later.");

    /// <summary>The platform has not completed first-run setup (503).</summary>
    public static readonly ErrorCatalogEntry SetupRequired = new(
        "Setup.Required",
        ErrorKind.Unavailable,
        "The platform has not been set up yet.");

    /// <summary>
    /// Generic client-side failure for a 4xx status the table has no dedicated entry for (409, 410, 415, ...).
    /// </summary>
    public static readonly ErrorCatalogEntry ClientError = new(
        "Http.ClientError",
        ErrorKind.Validation,
        "The request could not be processed.");

    /// <summary>The server failed (500).</summary>
    public static readonly ErrorCatalogEntry ServerError = new(
        "Server.Error",
        ErrorKind.Failure,
        "Something went wrong. Try again later.");
}
