using Microsoft.AspNetCore.SignalR;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Api.Hubs;

/// <summary>
/// Uses the shared NameIdentifier/sub lookup for SignalR user addressing.
/// </summary>
internal sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.GetUserId();
}
