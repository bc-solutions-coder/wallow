using System.Diagnostics.CodeAnalysis;

using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Infrastructure.Services.ExtensionPoints;

/// <summary>
/// Default implementation for hosts that do not own realtime connections.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NoOpRealtimeAccessRevoker : IRealtimeAccessRevoker
{
    public Task RevokeAsync(string userId, Guid tenantId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RevokeClientAsync(string clientId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
