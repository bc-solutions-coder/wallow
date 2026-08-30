using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;

namespace Wallow.Branding.Api.Controllers;

/// <summary>
/// The anonymous read the sign-in screen renders from: which name, logo and theme to show for the
/// client asking a person to sign in. Everything an organization manages about branding lives on
/// the org-scoped sub-resource (<see cref="OrganizationClientBrandingController"/>); this route
/// serves only the public copy, uncached at the HTTP layer so a branding edit shows up on the
/// very next sign-in.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/apps/{clientId}/branding")]
[Tags("Client Branding")]
[Produces("application/json")]
public class ClientBrandingController(IClientBrandingService brandingService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ClientBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientBrandingDto>> GetBranding(string clientId, CancellationToken ct)
    {
        ClientBrandingDto? branding = await brandingService.GetBrandingAsync(clientId, ct);
        if (branding is null)
        {
            return NotFound();
        }

        return Ok(branding);
    }
}
