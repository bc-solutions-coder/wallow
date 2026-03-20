using Asp.Versioning;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Constants;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/apps")]
[Authorize]
[Tags("Apps")]
[Produces("application/json")]
[Consumes("application/json")]
public class AppsController(IDeveloperAppService developerAppService) : ControllerBase
{
    [HttpPost("register")]
    [HasPermission(PermissionType.ApiKeysCreate)]
    [EnableRateLimiting("developer-app-registration")]
    [ProducesResponseType(typeof(AppRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppRegistrationResponse>> Register(
        [FromBody] RegisterAppRequest request,
        CancellationToken ct)
    {
        if (!request.ClientName.StartsWith("app-", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(request.ClientName), "Client name must start with 'app-' prefix.");
            return ValidationProblem(ModelState);
        }

        HashSet<string> invalidScopes = request.RequestedScopes
            .Where(s => !ApiScopes.DeveloperAppScopes.Contains(s))
            .ToHashSet();

        if (invalidScopes.Count > 0)
        {
            ModelState.AddModelError(
                nameof(request.RequestedScopes),
                $"Invalid scopes: {string.Join(", ", invalidScopes)}. Allowed: {string.Join(", ", ApiScopes.DeveloperAppScopes)}");
            return ValidationProblem(ModelState);
        }

        DeveloperAppRegistrationResult result = await developerAppService.RegisterClientAsync(
            request.ClientName,
            request.ClientName,
            request.RequestedScopes,
            ct);

        AppRegistrationResponse response = new(
            result.ClientId,
            result.ClientSecret,
            result.RegistrationAccessToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }
}
