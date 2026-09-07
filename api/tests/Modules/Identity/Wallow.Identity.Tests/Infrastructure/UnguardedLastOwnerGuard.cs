using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Executes departures without enforcing the last-owner rule.
/// Use for departure behavior tests; PostgreSQL integration tests cover the real guard and its locks.
/// </summary>
internal sealed class UnguardedLastOwnerGuard : ILastOwnerGuard
{
    public int Departures { get; private set; }

    public Task ExecuteDepartureAsync(
        Guid organizationId,
        Guid departingUserId,
        Func<CancellationToken, Task> departure,
        CancellationToken ct = default)
    {
        Departures++;
        return departure(ct);
    }
}
