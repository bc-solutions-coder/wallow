using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Runs the departure and refuses nothing. The real guard's whole job is a row lock held across a
/// transaction, which no in-memory provider can honour, so the rule it enforces is asserted against
/// a real Postgres in Wallow.Identity.IntegrationTests — asserting it here would only assert this
/// class. Specs that reach for this one are specs about what a departure DOES.
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
