using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Wallow.Api.Hubs;

/// <summary>
/// Resolves the SignalR user identifier from NameIdentifier or the OIDC "sub" claim,
/// ensuring Clients.User(userId) matches the connected user's identity.
/// </summary>
internal sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? connection.User?.FindFirst("sub")?.Value;
}
