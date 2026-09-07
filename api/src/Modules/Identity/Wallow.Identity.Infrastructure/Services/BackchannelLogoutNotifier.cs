using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Signs and delivers OIDC logout tokens to registered back-channel recipients.
/// HTTP deliveries run in parallel with per-attempt timeouts and at most one retry.
/// Recipient delivery failures are logged; failures while querying recipients can propagate.
/// </summary>
public sealed partial class BackchannelLogoutNotifier(
    HttpClient httpClient,
    ISsoClientSessionService sessions,
    IOptionsMonitor<OpenIddictServerOptions> serverOptions,
    IOptions<BackchannelLogoutOptions> options,
    TimeProvider timeProvider,
    ILogger<BackchannelLogoutNotifier> logger) : IBackchannelLogoutNotifier
{
    /// <summary>
    /// Two-minute lifetime follows the back-channel logout specification recommendation.
    /// </summary>
    private static readonly TimeSpan _logoutTokenLifetime = TimeSpan.FromMinutes(2);

    private const string LogoutTokenType = "logout+jwt";
    private const string BackchannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    public async Task NotifyAsync(string sid, Guid userId, Uri issuer, CancellationToken ct)
    {
        IReadOnlyList<BackchannelLogoutRecipient> recipients =
            await sessions.ListBackchannelRecipientsAsync(sid, ct);
        if (recipients.Count == 0)
        {
            return;
        }

        // Use an asymmetric signing credential that relying parties can validate with public keys.
        SigningCredentials? credentials = serverOptions.CurrentValue.SigningCredentials
            .FirstOrDefault(c => c.Key is AsymmetricSecurityKey or X509SecurityKey);
        if (credentials is null)
        {
            LogNoSigningCredentials(sid);
            return;
        }

        using CancellationTokenSource overall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        overall.CancelAfter(options.Value.OverallTimeout);

        await Task.WhenAll(recipients.Select(recipient =>
            NotifyOneAsync(recipient, sid, userId, issuer, credentials, overall.Token)));
    }

    private async Task NotifyOneAsync(
        BackchannelLogoutRecipient recipient,
        string sid,
        Guid userId,
        Uri issuer,
        SigningCredentials credentials,
        CancellationToken ct)
    {
        try
        {
            if (!await IsAllowedTargetAsync(recipient.LogoutUri, ct))
            {
                LogTargetRefused(recipient.ClientId, recipient.LogoutUri);
                return;
            }

            string token = MintLogoutToken(recipient.ClientId, sid, userId, issuer, credentials);

            DeliveryOutcome first = await TryDeliverAsync(recipient.LogoutUri, token, ct);
            if (first == DeliveryOutcome.Delivered)
            {
                LogDelivered(recipient.ClientId, sid);
                return;
            }

            if (first == DeliveryOutcome.Rejected)
            {
                LogDeliveryRejected(recipient.ClientId, recipient.LogoutUri, sid);
                return;
            }

            await Task.Delay(options.Value.RetryDelay, timeProvider, ct);

            if (await TryDeliverAsync(recipient.LogoutUri, token, ct) == DeliveryOutcome.Delivered)
            {
                LogDeliveredOnRetry(recipient.ClientId, sid);
                return;
            }

            LogDeliveryFailed(recipient.ClientId, recipient.LogoutUri, sid);
        }
        catch (OperationCanceledException)
        {
            LogDeliveryTimedOut(recipient.ClientId, recipient.LogoutUri, sid);
        }
        catch (Exception exception)
        {
            LogDeliveryThrew(exception, recipient.ClientId, recipient.LogoutUri, sid);
        }
    }

    private enum DeliveryOutcome
    {
        Delivered,

        /// <summary>
        /// Transport failure, attempt timeout, or server error eligible for retry.
        /// </summary>
        Retryable,

        /// <summary>
        /// Non-success response below 500; not retried.
        /// </summary>
        Rejected,
    }

    private async Task<DeliveryOutcome> TryDeliverAsync(Uri uri, string token, CancellationToken ct)
    {
        using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        attempt.CancelAfter(options.Value.PerClientTimeout);

        try
        {
            // Each attempt owns a fresh form payload.
            using FormUrlEncodedContent content = new([new KeyValuePair<string, string>("logout_token", token)]);
            using HttpResponseMessage response = await httpClient.PostAsync(uri, content, attempt.Token);
            if (response.IsSuccessStatusCode)
            {
                return DeliveryOutcome.Delivered;
            }

            return (int)response.StatusCode >= 500 ? DeliveryOutcome.Retryable : DeliveryOutcome.Rejected;
        }
        catch (HttpRequestException)
        {
            return DeliveryOutcome.Retryable;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Retry an attempt timeout only while the overall delivery remains active.
            return DeliveryOutcome.Retryable;
        }
    }

    private string MintLogoutToken(
        string clientId, string sid, Guid userId, Uri issuer, SigningCredentials credentials)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        // Omit nonce so the logout token cannot substitute for an ID token.
        SecurityTokenDescriptor descriptor = new()
        {
            TokenType = LogoutTokenType,
            Issuer = issuer.AbsoluteUri,
            Audience = clientId,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + _logoutTokenLifetime,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["sid"] = sid,
                ["jti"] = Guid.NewGuid().ToString("N"),
                ["events"] = new Dictionary<string, object>
                {
                    [BackchannelLogoutEvent] = new Dictionary<string, object>(),
                },
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>
    /// Rejects unresolvable or private recipient addresses unless private hosts are enabled.
    /// This lookup does not pin the address used by the subsequent HTTP request.
    /// </summary>
    private async Task<bool> IsAllowedTargetAsync(Uri uri, CancellationToken ct)
    {
        if (options.Value.AllowPrivateNetworkHosts)
        {
            return true;
        }

        IPAddress[] addresses;
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            addresses = [IPAddress.Parse(uri.IdnHost)];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, ct);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        return addresses.Length > 0 && addresses.All(a => !IsPrivateOrLocal(a));
    }

    private static bool IsPrivateOrLocal(IPAddress address)
    {
        IPAddress ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && (bytes[1] & 0xF0) == 16)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        return ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6SiteLocal;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout for session {Sid} skipped: no asymmetric signing credentials registered")]
    private partial void LogNoSigningCredentials(string sid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout refused target {LogoutUri} of client {ClientId}: host is private or unresolvable and AllowPrivateNetworkHosts is off")]
    private partial void LogTargetRefused(string clientId, Uri logoutUri);

    [LoggerMessage(Level = LogLevel.Information, Message = "Back-channel logout token delivered to client {ClientId} for session {Sid}")]
    private partial void LogDelivered(string clientId, string sid);

    [LoggerMessage(Level = LogLevel.Information, Message = "Back-channel logout token delivered to client {ClientId} for session {Sid} on retry")]
    private partial void LogDeliveredOnRetry(string clientId, string sid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout delivery to {LogoutUri} of client {ClientId} failed twice for session {Sid}")]
    private partial void LogDeliveryFailed(string clientId, Uri logoutUri, string sid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout token rejected by {LogoutUri} of client {ClientId} for session {Sid}; not retrying")]
    private partial void LogDeliveryRejected(string clientId, Uri logoutUri, string sid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout delivery to {LogoutUri} of client {ClientId} ran out of time for session {Sid}")]
    private partial void LogDeliveryTimedOut(string clientId, Uri logoutUri, string sid);

    [LoggerMessage(Level = LogLevel.Error, Message = "Back-channel logout delivery to {LogoutUri} of client {ClientId} threw for session {Sid}")]
    private partial void LogDeliveryThrew(Exception exception, string clientId, Uri logoutUri, string sid);
}
