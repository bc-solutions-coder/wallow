namespace Wallow.Shared.Contracts.Identity;

public interface IScopeSubsetValidator
{
    Task<ScopeValidationResult> ValidateAsync(string serviceAccountId, IEnumerable<string> requestedScopes, CancellationToken ct);
}

public sealed record ScopeValidationResult(bool IsSuccess, string? ErrorMessage = null)
{
    public static ScopeValidationResult Success() => new(true);
    public static ScopeValidationResult Failure(string error) => new(false, error);
}
