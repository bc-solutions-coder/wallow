using System.Collections.Immutable;
using System.Security.Claims;
using Wallow.Identity.Domain.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Controllers;

[Controller]
[Route("connect/authorize")]
public class AuthorizationController(UserManager<WallowUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (User.Identity is not { IsAuthenticated: true })
        {
            string returnUrl = Request.PathBase + Request.Path + Request.QueryString;
            return Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        string userId = userManager.GetUserId(User)
            ?? throw new InvalidOperationException("The user identifier cannot be retrieved.");

        WallowUser user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        ClaimsIdentity identity = new(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        identity.AddClaim(Claims.Subject, userId);

        string? userName = await userManager.GetUserNameAsync(user);
        if (userName is not null)
        {
            identity.AddClaim(Claims.Name, userName);
        }

        string? email = await userManager.GetEmailAsync(user);
        if (email is not null)
        {
            identity.AddClaim(Claims.Email, email);
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        foreach (string role in roles)
        {
            identity.AddClaim(Claims.Role, role);
        }

        IList<Claim> existingClaims = await userManager.GetClaimsAsync(user);

        Claim? givenName = existingClaims.FirstOrDefault(c => c.Type == Claims.GivenName);
        if (givenName is not null)
        {
            identity.AddClaim(givenName);
        }

        Claim? familyName = existingClaims.FirstOrDefault(c => c.Type == Claims.FamilyName);
        if (familyName is not null)
        {
            identity.AddClaim(familyName);
        }

        identity.SetScopes(request.GetScopes());

        foreach (Claim claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // OAuth authorization endpoint — antiforgery tokens are not applicable for OAuth flows
#pragma warning disable CA5391
    [HttpPost, IgnoreAntiforgeryToken]
    public Task<IActionResult> AuthorizePost() => Authorize();
#pragma warning restore CA5391

    private static ImmutableArray<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Name
                when claim.Subject?.HasScope(Scopes.Profile) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Email
                when claim.Subject?.HasScope(Scopes.Email) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.GivenName or Claims.FamilyName
                when claim.Subject?.HasScope(Scopes.Profile) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Role
                when claim.Subject?.HasScope(Scopes.Roles) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            _ => [Destinations.AccessToken]
        };
    }
}
