using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Userinfo;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("~/connect/userinfo")]
[AllowAnonymous]
public sealed class UserinfoController : Controller
{
#pragma warning disable CA5391
    [HttpGet, HttpPost]
    public async Task<IActionResult> Userinfo()
#pragma warning restore CA5391
    {
        AuthenticateResult result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = result.Principal
            ?? throw new InvalidOperationException("The authenticated principal cannot be retrieved.");

        return Ok(UserinfoClaims.Project(principal));
    }
}
