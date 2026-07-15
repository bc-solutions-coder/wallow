using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Api.Hubs;

[Authorize]
internal sealed partial class RealtimeHub(
    IPresenceService presenceService,
    IRealtimeDispatcher dispatcher,
    ITenantContext tenantContext,
    ILogger<RealtimeHub> logger) : Hub
{
    private static readonly string[] _allowedGroupPrefixes = ["tenant:", "user:", "page:"];
    public override async Task OnConnectedAsync()
    {
        string? userId = GetUserId();
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        await presenceService.TrackConnectionAsync(tenantContext.TenantId.Value, userId, Context.ConnectionId);
        LogUserConnected(userId, Context.ConnectionId);

        string tenantGroup = $"tenant:{tenantContext.TenantId.Value}";
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantGroup);

        if (IsStaffUser())
        {
            string staffGroup = $"tenant:{tenantContext.TenantId.Value}:staff";
            await Groups.AddToGroupAsync(Context.ConnectionId, staffGroup);
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Presence", "UserOnline", new { UserId = userId });
        await dispatcher.SendToGroupAsync(tenantGroup, envelope);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? userId = await presenceService.GetUserIdByConnectionAsync(Context.ConnectionId);
        await presenceService.RemoveConnectionAsync(Context.ConnectionId);

        if (userId is not null)
        {
            bool stillOnline = await presenceService.IsUserOnlineAsync(tenantContext.TenantId.Value, userId);
            if (!stillOnline)
            {
                string tenantGroup = $"tenant:{tenantContext.TenantId.Value}";
                RealtimeEnvelope envelope = RealtimeEnvelope.Create("Presence", "UserOffline", new { UserId = userId });
                await dispatcher.SendToGroupAsync(tenantGroup, envelope);
            }
        }

        LogConnectionDisconnected(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupId)
    {
        if (!HasAllowedPrefix(groupId))
        {
            LogInvalidGroupPrefix(Context.ConnectionId, groupId);
            throw new HubException("Invalid group name.");
        }

        ValidateTenantGroup(groupId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        LogConnectionJoinedGroup(Context.ConnectionId, groupId);
    }

    public async Task LeaveGroup(string groupId)
    {
        if (!HasAllowedPrefix(groupId))
        {
            LogInvalidGroupPrefix(Context.ConnectionId, groupId);
            throw new HubException("Invalid group name.");
        }

        ValidateTenantGroup(groupId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
    }

    public async Task UpdatePageContext(string pageContext)
    {
        string? userId = GetUserId();
        if (userId is null)
        {
            return;
        }

        Guid tenantId = tenantContext.TenantId.Value;
        string newPageGroup = $"page:{tenantId}:{pageContext}";

        // Remove from old page group if switching contexts
        if (Context.Items.TryGetValue("CurrentPageGroup", out object? oldGroupObj) && oldGroupObj is string oldPageGroup)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldPageGroup);
        }

        Context.Items["CurrentPageGroup"] = newPageGroup;

        await presenceService.SetPageContextAsync(tenantId, Context.ConnectionId, pageContext);
        await Groups.AddToGroupAsync(Context.ConnectionId, newPageGroup);

        IReadOnlyList<UserPresence> viewers = await presenceService.GetUsersOnPageAsync(tenantId, pageContext);
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Presence", "PageViewersUpdated", new
        {
            PageContext = pageContext,
            Viewers = viewers
        });
        await Clients.Group(newPageGroup).SendAsync("ReceivePresence", envelope);
    }

    private void ValidateTenantGroup(string groupId)
    {
        if (!groupId.StartsWith("tenant:", StringComparison.Ordinal))
        {
            return;
        }

        ReadOnlySpan<char> afterPrefix = groupId.AsSpan(7);
        int nextColon = afterPrefix.IndexOf(':');
        ReadOnlySpan<char> tenantSegment = nextColon >= 0 ? afterPrefix[..nextColon] : afterPrefix;

        if (!Guid.TryParse(tenantSegment, out Guid groupTenantId))
        {
            throw new HubException("Invalid tenant group format.");
        }

        if (groupTenantId != tenantContext.TenantId.Value)
        {
            LogCrossTenantJoinRejected(Context.ConnectionId, groupId, tenantContext.TenantId.Value);
            throw new HubException("Access denied: tenant mismatch.");
        }
    }

    private static bool HasAllowedPrefix(string groupId) =>
        Array.Exists(_allowedGroupPrefixes, prefix => groupId.StartsWith(prefix, StringComparison.Ordinal));

    private bool IsStaffUser()
    {
        IReadOnlyList<string> roles = Context.User?.GetRoles() ?? [];

        return roles.Any(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || r.Equals("manager", StringComparison.OrdinalIgnoreCase));
    }

    private string? GetUserId() =>
        Context.User?.GetUserId();
}

internal sealed partial class RealtimeHub
{
    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} connected with {ConnectionId}")]
    private partial void LogUserConnected(string userId, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection {ConnectionId} disconnected")]
    private partial void LogConnectionDisconnected(string connectionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection {ConnectionId} joined group {GroupId}")]
    private partial void LogConnectionJoinedGroup(string connectionId, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected cross-tenant group join: Connection {ConnectionId} attempted to join {GroupId} but belongs to tenant {TenantId}")]
    private partial void LogCrossTenantJoinRejected(string connectionId, string groupId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected group with invalid prefix: Connection {ConnectionId} attempted group {GroupId}")]
    private partial void LogInvalidGroupPrefix(string connectionId, string groupId);
}
