namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// Validates entity custom fields against tenant's field definitions.
/// </summary>
public interface ICustomFieldValidator
{
    /// <summary>
    /// Validates an entity's custom fields against the tenant's configured field definitions.
    /// </summary>
    /// <typeparam name="T">Entity type implementing IHasCustomFields</typeparam>
    /// <param name="entity">The entity to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any errors</returns>
    Task<CustomFieldValidationResult> ValidateAsync<T>(T entity, CancellationToken cancellationToken = default)
        where T : IHasCustomFields;
}

/// <summary>
/// Result of custom field validation.
/// </summary>
public sealed record CustomFieldValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<CustomFieldValidationError> Errors { get; init; } = [];

    public static CustomFieldValidationResult Success() => new() { Errors = [] };
    public static CustomFieldValidationResult Failure(IEnumerable<CustomFieldValidationError> errors)
        => new() { Errors = errors.ToList() };
}

/// <summary>
/// A single custom field validation error.
/// </summary>
public sealed record CustomFieldValidationError(string FieldKey, string Message);
