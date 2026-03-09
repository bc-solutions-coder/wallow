using System.Text.Json;
using System.Text.Json.Nodes;
using Foundry.Api.Hubs;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Infrastructure.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace Foundry.Api.Services;

internal sealed partial class SignalRRealtimeDispatcher(
    IHubContext<RealtimeHub> hubContext,
    IHtmlSanitizationService sanitizer,
    ILogger<SignalRRealtimeDispatcher> logger) : IRealtimeDispatcher
{
    public async Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            RealtimeEnvelope sanitized = SanitizeEnvelope(envelope);
            string method = $"Receive{sanitized.Module}";
            await hubContext.Clients.User(userId).SendAsync(method, sanitized, ct);
            LogSentToUser(sanitized.Type, userId, method);
        }
        catch (Exception ex)
        {
            LogFailedSendToUser(ex, envelope.Type, userId);
        }
    }

    public async Task SendToGroupAsync(string groupId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            RealtimeEnvelope sanitized = SanitizeEnvelope(envelope);
            string method = $"Receive{sanitized.Module}";
            await hubContext.Clients.Group(groupId).SendAsync(method, sanitized, ct);
            LogSentToGroup(sanitized.Type, groupId, method);
        }
        catch (Exception ex)
        {
            LogFailedSendToGroup(ex, envelope.Type, groupId);
        }
    }

    public async Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            RealtimeEnvelope sanitized = SanitizeEnvelope(envelope);
            string method = $"Receive{sanitized.Module}";
            string group = $"tenant:{tenantId}";
            await hubContext.Clients.Group(group).SendAsync(method, sanitized, ct);
            LogSentToTenant(sanitized.Type, tenantId, method);
        }
        catch (Exception ex)
        {
            LogFailedSendToTenant(ex, envelope.Type, tenantId);
        }
    }

    private RealtimeEnvelope SanitizeEnvelope(RealtimeEnvelope envelope)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(envelope.Payload);
        if (node is null)
        {
            return envelope;
        }

        SanitizeNode(node);
        return envelope with { Payload = node };
    }

    private void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach ((string key, JsonNode? value) in obj.ToList())
            {
                if (value is JsonValue val && val.TryGetValue(out string? str))
                {
                    obj[key] = sanitizer.Sanitize(str);
                }
                else if (value is not null)
                {
                    SanitizeNode(value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is JsonValue val && val.TryGetValue(out string? str))
                {
                    arr[i] = sanitizer.Sanitize(str);
                }
                else if (arr[i] is not null)
                {
                    SanitizeNode(arr[i]!);
                }
            }
        }
    }
}

internal sealed partial class SignalRRealtimeDispatcher
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Sent {Type} to user {UserId} on {Method}")]
    private partial void LogSentToUser(string type, string userId, string method);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send {Type} to user {UserId}")]
    private partial void LogFailedSendToUser(Exception ex, string type, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sent {Type} to group {GroupId} on {Method}")]
    private partial void LogSentToGroup(string type, string groupId, string method);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send {Type} to group {GroupId}")]
    private partial void LogFailedSendToGroup(Exception ex, string type, string groupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sent {Type} to tenant {TenantId} on {Method}")]
    private partial void LogSentToTenant(string type, Guid tenantId, string method);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send {Type} to tenant {TenantId}")]
    private partial void LogFailedSendToTenant(Exception ex, string type, Guid tenantId);
}
