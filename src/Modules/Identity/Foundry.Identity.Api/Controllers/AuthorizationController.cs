using System.Security.Claims;
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Foundry.Identity.Api.Controllers;

[Controller]
[Route("connect/authorize")]
public class AuthorizationController(UserManager<FoundryUser> userManager) : Controller
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

        FoundryUser user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        ClaimsIdentity identity = new(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        identity.AddClaim(Claims.Subject, userId);

        string? userName = await userManager.GetUserNameAsync(user);
        if (userName is not null)
            identity.AddClaim(Claims.Name, userName);

        string? email = await userManager.GetEmailAsync(user);
        if (email is not null)
            identity.AddClaim(Claims.Email, email);

        IList<string> roles = await userManager.GetRolesAsync(user);
        foreach (string role in roles)
            identity.AddClaim(Claims.Role, role);

        IList<Claim> existingClaims = await userManager.GetClaimsAsync(user);

        Claim? givenName = existingClaims.FirstOrDefault(c => c.Type == Claims.GivenName);
        if (givenName is not null)
            identity.AddClaim(givenName);

        Claim? familyName = existingClaims.FirstOrDefault(c => c.Type == Claims.FamilyName);
        if (familyName is not null)
            identity.AddClaim(familyName);

        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost]
    public Task<IActionResult> AuthorizePost() => Authorize();

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Name:
                yield return Destinations.AccessToken;
                if (claim.Subject?.HasScope(Scopes.Profile) is true)
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;
                if (claim.Subject?.HasScope(Scopes.Email) is true)
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.GivenName:
            case Claims.FamilyName:
                yield return Destinations.AccessToken;
                if (claim.Subject?.HasScope(Scopes.Profile) is true)
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (claim.Subject?.HasScope(Scopes.Roles) is true)
                    yield return Destinations.IdentityToken;
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
