namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Counts failed client authentications in a window and temporarily blocks the client
/// at the token endpoint, including correct-secret attempts. Redis failures fail open.
/// </summary>
public interface IInvalidClientLockout
{
    /// <summary>Counts one failed client authentication; trips the lockout at the threshold.</summary>
    Task RecordFailureAsync(string clientId, CancellationToken ct);

    /// <summary>Whether the client is currently inside a tripped lockout.</summary>
    Task<bool> IsLockedOutAsync(string clientId, CancellationToken ct);
}
