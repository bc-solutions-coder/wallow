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
    IOptionsMonitor<OpenIddictServerOptions> serverOptions,
    ILogger<LogoutController> logger) : Controller
{
    /// <summary>
    /// Marks the browser's return trip from the notification page. Phase one renders the page
    /// (front-channel iframes) and phase two — this marker present — runs OpenIddict's normal
    /// end-session redirect back to the relying party.
    /// </summary>
    private const string FrontchannelCompletionMarker = "wallow_fc";

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        OpenIddictRequest? request = HttpContext.GetOpenIddictServerRequest();
        string? postLogoutRedirectUri = request?.PostLogoutRedirectUri;

        LogLogoutRequest(postLogoutRedirectUri, User.Identity?.IsAuthenticated == true);

        // Defense-in-depth: validate the post-logout redirect URI even though OpenIddict also validates
        if (!string.IsNullOrEmpty(postLogoutRedirectUri)
            && !await redirectUriValidator.IsAllowedAsync(postLogoutRedirectUri, request?.ClientId))
        {
            LogLogoutInvalidRedirectUri(postLogoutRedirectUri);
            string authUrl = GetRequiredAuthUrl();
            return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
        }

        // Phase two skips notification: the iframes already fired on the first pass, and the
        // cookie sign-out below has already stripped the sid from the principal anyway.
        string? sid = HttpContext.Request.Query.ContainsKey(FrontchannelCompletionMarker)
            ? null
            : User.GetSessionId();

        IReadOnlyList<Uri> notificationUris = [];
        if (sid is not null)
        {
            notificationUris = await ssoClientSessionService.BuildLogoutNotificationUrisAsync(
                sid, GetIssuer(), HttpContext.RequestAborted);
        }

        // Sign out the Identity cookie and let OpenIddict handle the end-session redirect
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
    /// The <c>iss</c> every notification carries, which relying parties compare against their
    /// configured issuer before destroying a session. Falls back to the request's own address
    /// when OpenIddict has no explicit issuer configured (it then derives it the same way).
    /// </summary>
    private Uri GetIssuer() =>
        serverOptions.CurrentValue.Issuer
        ?? new Uri(string.Concat(Request.Scheme, "://", Request.Host.ToUriComponent(), Request.PathBase.ToUriComponent()));

    /// <summary>
    /// A self-contained page that loads each relying party's front-channel logout URI in a hidden
    /// iframe, then returns to this endpoint with the completion marker so phase two can finish.
    /// The delay gives the iframes time to land; the noscript link keeps script-less browsers moving.
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
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
