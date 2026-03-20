using System.Collections.Concurrent;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Wallow.Notifications.Infrastructure.Services;

/// <summary>
/// Manages a pool of SMTP connections keyed by host:port, reusing connections across sends.
/// </summary>
public sealed partial class SmtpConnectionPool : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, PooledSmtpClient> _clients = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpConnectionPool> _logger;
    private bool _disposed;

    public SmtpConnectionPool(IOptions<SmtpSettings> settings, ILogger<SmtpConnectionPool> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string key = $"{_settings.Host}:{_settings.Port}";
        SmtpClient client = await GetOrCreateClientAsync(key, cancellationToken);

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (!client.IsConnected)
            {
                await ConnectAndAuthenticateAsync(client, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
        }
        catch (Exception) when (!client.IsConnected)
        {
            // Connection was lost — remove stale entry, reconnect, and retry once
            _clients.TryRemove(key, out _);
            client = await GetOrCreateClientAsync(key, cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<SmtpClient> GetOrCreateClientAsync(string key, CancellationToken cancellationToken)
    {
        if (_clients.TryGetValue(key, out PooledSmtpClient? pooled) && pooled.Client.IsConnected)
        {
            return pooled.Client;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_clients.TryGetValue(key, out pooled) && pooled.Client.IsConnected)
            {
                return pooled.Client;
            }

            // Dispose old client if it exists but is disconnected
            if (pooled is not null)
            {
                pooled.Client.Dispose();
                _clients.TryRemove(key, out _);
            }

            SmtpClient client = new SmtpClient();
            client.Timeout = _settings.TimeoutSeconds * 1000;
            await ConnectAndAuthenticateAsync(client, cancellationToken);

            _clients[key] = new PooledSmtpClient(client);
            LogConnectionCreated(_logger, key);

            return client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task ConnectAndAuthenticateAsync(SmtpClient client, CancellationToken cancellationToken)
    {
        SecureSocketOptions secureSocketOptions = _settings.UseSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_settings.Host, _settings.Port, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (KeyValuePair<string, PooledSmtpClient> entry in _clients)
        {
            try
            {
                if (entry.Value.Client.IsConnected)
                {
                    await entry.Value.Client.DisconnectAsync(true);
                }

                entry.Value.Client.Dispose();
                LogConnectionDisposed(_logger, entry.Key);
            }
            catch (Exception ex)
            {
                LogConnectionDisposeFailed(_logger, ex, entry.Key);
            }
        }

        _clients.Clear();
        _connectionLock.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (KeyValuePair<string, PooledSmtpClient> entry in _clients)
        {
            try
            {
                entry.Value.Client.Dispose();
            }
            catch
            {
                // Best-effort cleanup in sync dispose
            }
        }

        _clients.Clear();
        _connectionLock.Dispose();
    }

    private sealed record PooledSmtpClient(SmtpClient Client);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SMTP connection created for {Endpoint}")]
    private static partial void LogConnectionCreated(ILogger logger, string endpoint);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SMTP connection disposed for {Endpoint}")]
    private static partial void LogConnectionDisposed(ILogger logger, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispose SMTP connection for {Endpoint}")]
    private static partial void LogConnectionDisposeFailed(ILogger logger, Exception ex, string endpoint);
}
