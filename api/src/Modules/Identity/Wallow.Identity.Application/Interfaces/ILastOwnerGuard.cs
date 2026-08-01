namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The one membership invariant no membership can enforce on its own: an organization that has an
/// active owner must still have one when the write finishes.
///
/// Two owners are two aggregates, so neither sees the other's concurrent departure. Checked
/// independently, two simultaneous departures each count a spare owner and both proceed, and the
/// organization is left with none. What prevents that is a mechanism rather than a rule: this seam
/// runs the departure inside a transaction that has first locked the organization's active-owner
/// rows, so the count and the write acting on it are one serialized step. A plain count taken
/// before the write is the implementation this interface exists to keep anyone from writing.
/// </summary>
public interface ILastOwnerGuard
{
    /// <summary>
    /// Runs <paramref name="departure"/> — a write that costs <paramref name="departingUserId"/>
    /// their active membership of the organization — and refuses it with
    /// <c>Identity.LastOwner</c> when they are the only active owner it has.
    /// </summary>
    Task ExecuteDepartureAsync(
        Guid organizationId,
        Guid departingUserId,
        Func<CancellationToken, Task> departure,
        CancellationToken ct = default);
}
