using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("~/connect/logout")]
[AllowAnonymous]
public sealed partial class LogoutController(
    IRedirectUriValidator redirectUriValidator,
    IConfiguration configuration,
    ILogger<LogoutController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        OpenIddictRequest? request = HttpContext.GetOpenIddictServerRequest();
        string? postLogoutRedirectUri = request?.PostLogoutRedirectUri;

        LogLogoutRequest(postLogoutRedirectUri, User.Identity?.IsAuthenticated == true);

        // Defense-in-depth: validate the post-logout redirect URI even though OpenIddict also validates
        if (!string.IsNullOrEmpty(postLogoutRedirectUri)
            && !await redirectUriValidator.IsAllowedAsync(postLogoutRedirectUri))
        {
            LogLogoutInvalidRedirectUri(postLogoutRedirectUri);
            string authUrl = GetRequiredAuthUrl();
            return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
        }

        // Sign out the Identity cookie and let OpenIddict handle the end-session redirect
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        LogLogoutSignedOut();

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private string GetRequiredAuthUrl() =>
        configuration["AuthUrl"] ?? throw new InvalidOperationException(
            "AuthUrl must be configured in appsettings.json. " +
            "Example: \"AuthUrl\": \"https://auth.yourdomain.com\"");

    [HttpPost, ValidateAntiForgeryToken]
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

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC logout POST request")]
    private partial void LogLogoutPostRequest();
}
