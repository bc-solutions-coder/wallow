using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("~/connect/logout")]
[AllowAnonymous]
public sealed class LogoutController(
    IRedirectUriValidator redirectUriValidator,
    IConfiguration configuration) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        OpenIddictRequest? request = HttpContext.GetOpenIddictServerRequest();
        string? postLogoutRedirectUri = request?.PostLogoutRedirectUri;

        // Defense-in-depth: validate the post-logout redirect URI even though OpenIddict also validates
        if (!string.IsNullOrEmpty(postLogoutRedirectUri)
            && !await redirectUriValidator.IsAllowedAsync(postLogoutRedirectUri))
        {
            string authUrl = GetRequiredAuthUrl();
            return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
        }

        // Sign out the Identity cookie and let OpenIddict handle the end-session redirect
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

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
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
