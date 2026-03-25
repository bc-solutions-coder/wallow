using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("~/connect/userinfo")]
[AllowAnonymous]
public sealed class UserinfoController : Controller
{
    // OAuth userinfo endpoint — antiforgery tokens are not applicable for OAuth flows
#pragma warning disable CA5391
    [HttpGet, HttpPost, IgnoreAntiforgeryToken]
    public async Task<IActionResult> Userinfo()
#pragma warning restore CA5391
    {
        AuthenticateResult result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = result.Principal
            ?? throw new InvalidOperationException("The authenticated principal cannot be retrieved.");

        Dictionary<string, object> claims = new()
        {
            [Claims.Subject] = principal.GetClaim(Claims.Subject)!
        };

        if (principal.HasScope(Scopes.Profile))
        {
            string? name = principal.GetClaim(Claims.Name);
            if (name is not null)
            {
                claims[Claims.Name] = name;
            }

            string? givenName = principal.GetClaim(Claims.GivenName);
            if (givenName is not null)
            {
                claims[Claims.GivenName] = givenName;
            }

            string? familyName = principal.GetClaim(Claims.FamilyName);
            if (familyName is not null)
            {
                claims[Claims.FamilyName] = familyName;
            }
        }

        if (principal.HasScope(Scopes.Email))
        {
            string? email = principal.GetClaim(Claims.Email);
            if (email is not null)
            {
                claims[Claims.Email] = email;
            }
        }

        if (principal.HasScope(Scopes.Roles))
        {
            ImmutableArray<string> roles = [.. principal.GetClaims(Claims.Role)];
            if (roles.Length > 0)
            {
                claims[Claims.Role] = roles;
            }
        }

        return Ok(claims);
    }
}
