using System.Diagnostics.CodeAnalysis;

using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Infrastructure.Services.ExtensionPoints;

/// <summary>
/// The default for hosts that serve no realtime traffic — the seeder and the migration worker
/// both build the identity module and neither owns a connection to close.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class NoOpRealtimeAccessRevoker : IRealtimeAccessRevoker
{
    public Task RevokeAsync(string userId, Guid tenantId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
