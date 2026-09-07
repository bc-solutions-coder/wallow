namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Serializes active-owner departures by locking owner rows and running the departure
/// in the same transaction. This prevents concurrent departures from each counting
/// the other owner as a remaining owner.
/// </summary>
public interface ILastOwnerGuard
{
    /// <summary>
    /// Runs the departure unless it would remove the organization's sole active owner,
    /// in which case it throws Identity.LastOwner.
    /// </summary>
    Task ExecuteDepartureAsync(
        Guid organizationId,
        Guid departingUserId,
        Func<CancellationToken, Task> departure,
        CancellationToken ct = default);
}
