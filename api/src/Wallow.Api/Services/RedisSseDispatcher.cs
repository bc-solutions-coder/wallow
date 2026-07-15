using System.Text.Json;
using StackExchange.Redis;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

internal sealed partial class RedisSseDispatcher(
    IConnectionMultiplexer redis,
    ILogger<RedisSseDispatcher> logger) : ISseDispatcher
{
    public async Task SendToTenantAsync(Guid tenantId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            ISubscriber subscriber = redis.GetSubscriber();
            string json = JsonSerializer.Serialize(envelope);
            RedisChannel channel = new($"sse:tenant:{tenantId}", RedisChannel.PatternMode.Literal);
            await subscriber.PublishAsync(channel, json);
            LogSentToTenant(envelope.Type, tenantId);
        }
        catch (Exception ex)
        {
            LogFailedSendToTenant(ex, envelope.Type, tenantId);
        }
    }

    public async Task SendToUserAsync(string userId, RealtimeEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            ISubscriber subscriber = redis.GetSubscriber();
            string json = JsonSerializer.Serialize(envelope);
            RedisChannel channel = new($"sse:user:{userId}", RedisChannel.PatternMode.Literal);
            await subscriber.PublishAsync(channel, json);
            LogSentToUser(envelope.Type, userId);
        }
        catch (Exception ex)
        {
            LogFailedSendToUser(ex, envelope.Type, userId);
        }
    }

    public async Task SendToTenantPermissionAsync(Guid tenantId, string permission, RealtimeEnvelope envelope,
        CancellationToken ct = default)
    {
        try
        {
            ISubscriber subscriber = redis.GetSubscriber();
            RealtimeEnvelope stamped = envelope with { RequiredPermission = permission };
            string json = JsonSerializer.Serialize(stamped);
            RedisChannel channel = new($"sse:tenant:{tenantId}", RedisChannel.PatternMode.Literal);
            await subscriber.PublishAsync(channel, json);
            LogSentToTenantPermission(stamped.Type, tenantId, permission);
        }
        catch (Exception ex)
        {
            LogFailedSendToTenant(ex, envelope.Type, tenantId);
        }
    }

    public async Task SendToTenantRoleAsync(Guid tenantId, string role, RealtimeEnvelope envelope,
        CancellationToken ct = default)
    {
        try
        {
            ISubscriber subscriber = redis.GetSubscriber();
            RealtimeEnvelope stamped = envelope with { RequiredRole = role };
            string json = JsonSerializer.Serialize(stamped);
            RedisChannel channel = new($"sse:tenant:{tenantId}", RedisChannel.PatternMode.Literal);
            await subscriber.PublishAsync(channel, json);
            LogSentToTenantRole(stamped.Type, tenantId, role);
        }
        catch (Exception ex)
        {
            LogFailedSendToTenant(ex, envelope.Type, tenantId);
        }
    }
}

internal sealed partial class RedisSseDispatcher
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "SSE sent {Type} to tenant {TenantId}")]
    private partial void LogSentToTenant(string type, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SSE sent {Type} to user {UserId}")]
    private partial void LogSentToUser(string type, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SSE sent {Type} to tenant {TenantId} with permission {Permission}")]
    private partial void LogSentToTenantPermission(string type, Guid tenantId, string permission);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SSE sent {Type} to tenant {TenantId} with role {Role}")]
    private partial void LogSentToTenantRole(string type, Guid tenantId, string role);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSE failed to send {Type} to tenant {TenantId}")]
    private partial void LogFailedSendToTenant(Exception ex, string type, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSE failed to send {Type} to user {UserId}")]
    private partial void LogFailedSendToUser(Exception ex, string type, string userId);
}
