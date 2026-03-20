using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace Wallow.Identity.Api.Controllers;

[Controller]
[Route("~/connect/logout")]
public sealed class LogoutController : Controller
{
    [HttpGet]
    public IActionResult Logout()
    {
        return Redirect("/Account/Logout");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
