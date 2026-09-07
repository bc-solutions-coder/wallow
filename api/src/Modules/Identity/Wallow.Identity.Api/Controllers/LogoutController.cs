using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("~/connect/logout")]
[AllowAnonymous]
public sealed partial class LogoutController(
    IRedirectUriValidator redirectUriValidator,
    IConfiguration configuration,
    ISsoClientSessionService ssoClientSessionService,
    IBackchannelLogoutNotifier backchannelLogoutNotifier,
    IAccessRevoker accessRevoker,
    IOptionsMonitor<OpenIddictServerOptions> serverOptions,
    ILogger<LogoutController> logger) : Controller
{
    /// <summary>
    /// Skips notifications on the return trip from the front-channel page.
    /// </summary>
    private const string FrontchannelCompletionMarker = "wallow_fc";

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        OpenIddictRequest? request = HttpContext.GetOpenIddictServerRequest();
        string? postLogoutRedirectUri = request?.PostLogoutRedirectUri;

        LogLogoutRequest(postLogoutRedirectUri, User.Identity?.IsAuthenticated == true);

        // Apply the redirect-origin allow-list in addition to OpenIddict validation.
        if (!string.IsNullOrEmpty(postLogoutRedirectUri)
            && !await redirectUriValidator.IsAllowedAsync(postLogoutRedirectUri, request?.ClientId))
        {
            LogLogoutInvalidRedirectUri(postLogoutRedirectUri);
            string authUrl = GetRequiredAuthUrl();
            return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
        }

        // The completion marker skips notification and session resolution.
        (Guid UserId, string Sid)? session = HttpContext.Request.Query.ContainsKey(FrontchannelCompletionMarker)
            ? null
            : await ResolveSessionAsync();
        string? sid = session?.Sid;

        IReadOnlyList<Uri> notificationUris = [];
        if (sid is not null)
        {
            notificationUris = await ssoClientSessionService.BuildLogoutNotificationUrisAsync(
                sid, GetIssuer(), HttpContext.RequestAborted);
        }

        // Notify before deleting the participation rows used to find recipients.
        await NotifyBackchannelAsync(session);

        // Revoke the resolved session before signing out the cookie.
        await RevokeSessionTokensAsync(session);


        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        LogLogoutSignedOut();

        if (sid is not null)
        {
            await ssoClientSessionService.ForgetAsync(sid, HttpContext.RequestAborted);

            if (notificationUris.Count > 0)
            {
                LogLogoutNotifyingClients(notificationUris.Count, sid);
                Response.Headers.CacheControl = "no-store";
                return Content(BuildNotificationPage(notificationUris), "text/html; charset=utf-8");
            }
        }

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private string GetRequiredAuthUrl() =>
        configuration["AuthUrl"] ?? throw new InvalidOperationException(
            "AuthUrl must be configured in appsettings.json. " +
            "Example: \"AuthUrl\": \"https://auth.yourdomain.com\"");

    /// <summary>
    /// Configured issuer, falling back to request scheme, host, and PathBase.
    /// </summary>
    private Uri GetIssuer() =>
        serverOptions.CurrentValue.Issuer
        ?? new Uri(string.Concat(Request.Scheme, "://", Request.Host.ToUriComponent(), Request.PathBase.ToUriComponent()));

    /// <summary>
    /// Loads hidden logout iframes, then redirects after 1.5 seconds without waiting for acknowledgments.
    /// A noscript link supports manual completion.
    /// </summary>
    private string BuildNotificationPage(IReadOnlyList<Uri> notificationUris)
    {
        string separator = Request.QueryString.HasValue ? "&" : "?";
        string completionUrl = string.Concat(
            Request.PathBase.ToUriComponent(),
            Request.Path.ToUriComponent(),
            Request.QueryString.Value,
            separator,
            FrontchannelCompletionMarker,
            "=done");

        StringBuilder page = new();
        page.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Signing out</title></head><body>");
        page.Append("<p>Signing you out of connected applications&hellip;</p>");

        foreach (Uri uri in notificationUris)
        {
            page.Append("<iframe src=\"")
                .Append(WebUtility.HtmlEncode(uri.AbsoluteUri))
                .Append("\" style=\"display:none\"></iframe>");
        }

        page.Append("<script>setTimeout(function(){window.location.replace(\"")
            .Append(JavaScriptEncoder.Default.Encode(completionUrl))
            .Append("\");},1500);</script>");
        page.Append("<noscript><a href=\"")
            .Append(WebUtility.HtmlEncode(completionUrl))
            .Append("\">Continue</a></noscript>");
        page.Append("</body></html>");

        return page.ToString();
    }

    [HttpPost]
    public async Task<IActionResult> LogoutPost()
    {
        LogLogoutPostRequest();
        (Guid UserId, string Sid)? session = await ResolveSessionAsync();

        // POST logout sends back-channel notifications without an iframe page.
        await NotifyBackchannelAsync(session);
        await RevokeSessionTokensAsync(session);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        if (session is not null)
        {
            await ssoClientSessionService.ForgetAsync(session.Value.Sid, HttpContext.RequestAborted);
        }

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Uses the current principal user and sid, falling back to the OpenIddict-authenticated hint principal.
    /// </summary>
    private async Task<(Guid UserId, string Sid)?> ResolveSessionAsync()
    {
        string? cookieSid = User.GetSessionId();
        if (cookieSid is not null && Guid.TryParse(User.GetUserId(), out Guid cookieUserId))
        {
            return (cookieUserId, cookieSid);
        }

        AuthenticateResult? hint = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        string? hintSid = hint?.Principal?.GetSessionId();
        if (hintSid is not null && Guid.TryParse(hint!.Principal!.GetUserId(), out Guid hintUserId))
        {
            return (hintUserId, hintSid);
        }

        return null;
    }

    /// <summary>
    /// Revokes the resolved session through IAccessRevoker; no session is a no-op.
    /// </summary>
    private async Task RevokeSessionTokensAsync((Guid UserId, string Sid)? session)
    {
        if (session is not null)
        {
            await accessRevoker.RevokeSessionAsync(
                session.Value.UserId, session.Value.Sid, HttpContext.RequestAborted);
        }
    }

    /// <summary>
    /// Invokes back-channel delivery when a session is resolved. Notifier lookup failures can propagate.
    /// </summary>
    private async Task NotifyBackchannelAsync((Guid UserId, string Sid)? session)
    {
        if (session is not null)
        {
            await backchannelLogoutNotifier.NotifyAsync(
                session.Value.Sid, session.Value.UserId, GetIssuer(), HttpContext.RequestAborted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC logout request: postLogoutRedirectUri={PostLogoutRedirectUri}, isAuthenticated={IsAuthenticated}")]
    private partial void LogLogoutRequest(string? postLogoutRedirectUri, bool isAuthenticated);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC logout rejected invalid redirect URI: {RedirectUri}")]
    private partial void LogLogoutInvalidRedirectUri(string redirectUri);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC logout: Identity.Application cookie signed out")]
    private partial void LogLogoutSignedOut();

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC logout: notifying {ClientCount} relying parties that session {Sid} ended")]
    private partial void LogLogoutNotifyingClients(int clientCount, string sid);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC logout POST request")]
    private partial void LogLogoutPostRequest();
}
