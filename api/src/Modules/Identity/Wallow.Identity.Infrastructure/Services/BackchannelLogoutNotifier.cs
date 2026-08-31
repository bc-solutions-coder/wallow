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
/// OIDC back-channel logout, OP side: mints one logout token per participating relying party and
/// POSTs it (<c>logout_token=</c>, form-encoded) to the client's registered back-channel logout
/// URI. Tokens are signed with the server's own signing credentials — the same keys the JWKS
/// endpoint publishes — deliberately outside OpenIddict's id-token pipeline, which OpenIddict 8's
/// native back-channel support can replace wholesale.
/// </summary>
/// <remarks>
/// Delivery is best-effort and bounded: attempts run in parallel, each attempt gets
/// <see cref="BackchannelLogoutOptions.PerClientTimeout"/>, a failed delivery gets exactly one
/// retry after <see cref="BackchannelLogoutOptions.RetryDelay"/>, and the whole fan-out is cut
/// off at <see cref="BackchannelLogoutOptions.OverallTimeout"/>. Nothing here ever throws to the
/// caller: a dead relying party must not block the user's own sign-out.
/// </remarks>
public sealed partial class BackchannelLogoutNotifier(
    HttpClient httpClient,
    ISsoClientSessionService sessions,
    IOptionsMonitor<OpenIddictServerOptions> serverOptions,
    IOptions<BackchannelLogoutOptions> options,
    TimeProvider timeProvider,
    ILogger<BackchannelLogoutNotifier> logger) : IBackchannelLogoutNotifier
{
    /// <summary>The spec's cap: a logout token is a fresh instruction, not a credential to hold.</summary>
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

        // The first asymmetric credential is the key JWKS publishes; a symmetric credential
        // would mint a token no relying party could validate.
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

            if (await TryDeliverAsync(recipient.LogoutUri, token, ct))
            {
                LogDelivered(recipient.ClientId, sid);
                return;
            }

            await Task.Delay(options.Value.RetryDelay, timeProvider, ct);

            if (await TryDeliverAsync(recipient.LogoutUri, token, ct))
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

    private async Task<bool> TryDeliverAsync(Uri uri, string token, CancellationToken ct)
    {
        using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        attempt.CancelAfter(options.Value.PerClientTimeout);

        try
        {
            // Fresh content per attempt: HttpClient disposes request content after sending.
            using FormUrlEncodedContent content = new([new KeyValuePair<string, string>("logout_token", token)]);
            using HttpResponseMessage response = await httpClient.PostAsync(uri, content, attempt.Token);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The per-attempt timeout fired but the fan-out is still live: report a failed
            // attempt so the one retry can run, rather than aborting the recipient.
            return false;
        }
    }

    private string MintLogoutToken(
        string clientId, string sid, Guid userId, Uri issuer, SigningCredentials credentials)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        // Per the back-channel logout spec: iss/sub/aud/iat/exp/jti, the logout event, and sid.
        // No nonce — its presence is what lets relying parties reject a replayed id token here.
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
    /// The SSRF gate: back-channel URIs are registered by org admins, so by default the notifier
    /// refuses to POST at anything that resolves to a loopback, private, link-local, or
    /// unique-local address. <see cref="BackchannelLogoutOptions.AllowPrivateNetworkHosts"/>
    /// opts a private-network deployment back in. An unresolvable host is refused too — the
    /// delivery could only fail, and resolving here is what makes the gate see the address.
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout delivery to {LogoutUri} of client {ClientId} ran out of time for session {Sid}")]
    private partial void LogDeliveryTimedOut(string clientId, Uri logoutUri, string sid);

    [LoggerMessage(Level = LogLevel.Error, Message = "Back-channel logout delivery to {LogoutUri} of client {ClientId} threw for session {Sid}")]
    private partial void LogDeliveryThrew(Exception exception, string clientId, Uri logoutUri, string sid);
}
