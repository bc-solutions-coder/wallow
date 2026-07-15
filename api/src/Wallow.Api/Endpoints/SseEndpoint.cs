using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Api.Endpoints;

public static partial class SseEndpoint
{
    public static async Task HandleSseConnection(
        HttpContext httpContext,
        [FromQuery] string? subscribe,
        SseConnectionManager connectionManager,
        ITenantContext tenantContext,
        IHostApplicationLifetime lifetime,
        ILogger<SseConnectionManager> logger,
        CancellationToken cancellationToken)
    {
        // Link to application stopping so SSE connections close promptly on shutdown
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, lifetime.ApplicationStopping);
        cancellationToken = linkedCts.Token;
        if (!tenantContext.IsResolved)
        {
            bool isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
            string? sub = httpContext.User.GetUserId();
            string? orgId = httpContext.User.GetTenantId();
            string claims = string.Join(", ", httpContext.User.Claims.Select(c => $"{c.Type}={c.Value}"));

            LogSseConnectionRejected(logger, isAuthenticated, sub, orgId, claims);

            httpContext.Response.StatusCode = 400;
            return;
        }

        HashSet<string> modules = string.IsNullOrEmpty(subscribe)
            ? []
            : subscribe.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        string userId = httpContext.User.GetUserId() ?? string.Empty;
        Guid tenantId = tenantContext.TenantId.Value;

        HashSet<string> permissions = httpContext.User.GetPermissions().ToHashSet();

        HashSet<string> roles = httpContext.User.GetRoles().ToHashSet();

        string connectionId = Guid.NewGuid().ToString();
        string moduleList = string.Join(",", modules);
        LogSseConnectionRegistered(logger, userId, tenantId, connectionId, moduleList);
        connectionManager.AddConnection(connectionId, userId, tenantId, modules, permissions, roles);

        try
        {
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";
            await httpContext.Response.Body.FlushAsync(cancellationToken);

            System.Threading.Channels.ChannelReader<RealtimeEnvelope>? reader = connectionManager.GetReader(connectionId);
            if (reader is null)
            {
                return;
            }

            using PeriodicTimer heartbeatTimer = new(TimeSpan.FromSeconds(15));

            while (!cancellationToken.IsCancellationRequested)
            {
                Task<bool> readTask = reader.WaitToReadAsync(cancellationToken).AsTask();
                Task timerTask = heartbeatTimer.WaitForNextTickAsync(cancellationToken).AsTask();

                Task completed = await Task.WhenAny(readTask, timerTask);

                if (completed == readTask && await readTask)
                {
                    while (reader.TryRead(out RealtimeEnvelope? envelope))
                    {
                        string json = JsonSerializer.Serialize(envelope);
                        await httpContext.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                        await httpContext.Response.Body.FlushAsync(cancellationToken);
                    }
                }
                else if (completed == timerTask)
                {
                    await httpContext.Response.WriteAsync(": heartbeat\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            LogSseConnectionClosed(logger, userId, connectionId);
            connectionManager.RemoveConnection(connectionId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SSE connection registered: UserId={UserId}, TenantId={TenantId}, ConnectionId={ConnectionId}, Modules=[{Modules}]")]
    private static partial void LogSseConnectionRegistered(
        ILogger logger, string userId, Guid tenantId, string connectionId, string modules);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SSE connection closed: UserId={UserId}, ConnectionId={ConnectionId}")]
    private static partial void LogSseConnectionClosed(
        ILogger logger, string userId, string connectionId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SSE connection rejected: tenant not resolved. Authenticated={IsAuthenticated}, Sub={Sub}, OrgId={OrgId}, Claims=[{Claims}]")]
    private static partial void LogSseConnectionRejected(
        ILogger logger, bool isAuthenticated, string? sub, string? orgId, string claims);
}
