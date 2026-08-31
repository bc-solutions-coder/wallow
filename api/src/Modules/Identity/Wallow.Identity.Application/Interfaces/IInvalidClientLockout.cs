namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The per-client_id brake on client-authentication guessing: every failed authentication is
/// counted, and a client that fails often enough inside the counting window is temporarily
/// rejected at the token endpoint — correct secret or not — until the lockout expires.
/// </summary>
public interface IInvalidClientLockout
{
    /// <summary>Counts one failed client authentication; trips the lockout at the threshold.</summary>
    Task RecordFailureAsync(string clientId, CancellationToken ct);

    /// <summary>Whether the client is currently inside a tripped lockout.</summary>
    Task<bool> IsLockedOutAsync(string clientId, CancellationToken ct);
}
