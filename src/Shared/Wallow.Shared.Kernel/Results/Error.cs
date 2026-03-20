namespace Wallow.Shared.Kernel.Results;

/// <summary>
/// Represents an error with a code and message.
/// Use static factory methods for common error types.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided");

    public static Error NotFound(string entity, object id) =>
        new($"{entity}.NotFound", $"{entity} with ID '{id}' was not found");

    public static Error Validation(string message) =>
        new("Validation.Error", message);

    public static Error Validation(string code, string message) =>
        new(code, message);

    public static Error Conflict(string message) =>
        new("Conflict.Error", message);

    public static Error Unauthorized(string message = "Unauthorized access") =>
        new("Unauthorized.Error", message);

    public static Error Forbidden(string message = "Access denied") =>
        new("Forbidden.Error", message);

    public static Error BusinessRule(string code, string message) =>
        new($"BusinessRule.{code}", message);
}
