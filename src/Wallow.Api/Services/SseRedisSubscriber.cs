using System.Text.Json;
using StackExchange.Redis;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

public partial class SseRedisSubscriber : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly SseConnectionManager _connectionManager;
    private readonly ILogger<SseRedisSubscriber> _logger;
    private ISubscriber? _subscriber;

    public SseRedisSubscriber(
        IConnectionMultiplexer redis,
        SseConnectionManager connectionManager,
        ILogger<SseRedisSubscriber> logger)
    {
        _redis = redis;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscriber = _redis.GetSubscriber();

        await _subscriber.SubscribeAsync(
            new RedisChannel("sse:tenant:*", RedisChannel.PatternMode.Pattern),
            (channel, message) => HandleTenantMessage(channel, message));

        await _subscriber.SubscribeAsync(
            new RedisChannel("sse:user:*", RedisChannel.PatternMode.Pattern),
            (channel, message) => HandleUserMessage(channel, message));

        // Keep the service alive until shutdown is requested
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            await _subscriber.UnsubscribeAllAsync();
            LogUnsubscribed();
        }

        await base.StopAsync(cancellationToken);
    }

    private void HandleTenantMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            string json = message!;
            RealtimeEnvelope? envelope = JsonSerializer.Deserialize<RealtimeEnvelope>(json);
            if (envelope is null)
            {
                return;
            }

            string channelStr = channel.ToString();
            string tenantIdStr = channelStr["sse:tenant:".Length..];
            if (!Guid.TryParse(tenantIdStr, out Guid tenantId))
            {
                LogInvalidTenantId(_logger, channelStr);
                return;
            }

            IEnumerable<string> connections = _connectionManager.GetConnectionsForTenant(tenantId);
            DeliverToConnections(connections, envelope);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(_logger, ex, channel.ToString());
        }
    }

    private void HandleUserMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            string json = message!;
            RealtimeEnvelope? envelope = JsonSerializer.Deserialize<RealtimeEnvelope>(json);
            if (envelope is null)
            {
                return;
            }

            string channelStr = channel.ToString();
            string userId = channelStr["sse:user:".Length..];
            LogReceivedUserMessage(_logger, envelope.Type, userId);

            IEnumerable<string> connections = _connectionManager.GetConnectionForUser(userId);
            DeliverToConnections(connections, envelope);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(_logger, ex, channel.ToString());
        }
    }

    private void DeliverToConnections(IEnumerable<string> connectionIds, RealtimeEnvelope envelope)
    {
        int delivered = 0;
        int filtered = 0;

        foreach (string connectionId in connectionIds)
        {
            SseConnectionState? state = _connectionManager.GetConnectionState(connectionId);
            if (state is null)
            {
                continue;
            }

            if (_connectionManager.ShouldDeliver(state, envelope, envelope.Module))
            {
                state.Channel.Writer.TryWrite(envelope);
                delivered++;
                LogDelivered(_logger, envelope.Type, envelope.Module, state.UserId, connectionId);
            }
            else
            {
                filtered++;
                string subscribedModules = string.Join(",", state.Modules);
                LogFiltered(_logger, envelope.Type, envelope.Module, state.UserId, connectionId,
                    subscribedModules);
            }
        }

        if (delivered == 0 && filtered == 0)
        {
            LogNoConnections(_logger, envelope.Type, envelope.Module);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Unsubscribed from all SSE Redis channels")]
    private partial void LogUnsubscribed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid tenant ID in channel: {Channel}")]
    private static partial void LogInvalidTenantId(ILogger logger, string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize SSE message from channel {Channel}")]
    private static partial void LogDeserializationFailed(ILogger logger, Exception ex, string channel);

    [LoggerMessage(Level = LogLevel.Information, Message = "SSE received {Type} for user {UserId} from Redis")]
    private static partial void LogReceivedUserMessage(ILogger logger, string type, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SSE delivered {Type} ({Module}) to user {UserId} on connection {ConnectionId}")]
    private static partial void LogDelivered(ILogger logger, string type, string module, string userId, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SSE filtered {Type} ({Module}) for user {UserId} on connection {ConnectionId} (subscribed: {SubscribedModules})")]
    private static partial void LogFiltered(ILogger logger, string type, string module, string userId, string connectionId, string subscribedModules);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSE no active connections found for {Type} ({Module})")]
    private static partial void LogNoConnections(ILogger logger, string type, string module);
}
