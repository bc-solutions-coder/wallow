using System.Security.Claims;
using Asp.Versioning;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Shared.Contracts.Identity;
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

        string? creatorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        DeveloperAppRegistrationResult result = await developerAppService.RegisterClientAsync(
            request.ClientName,
            request.ClientName,
            request.RequestedScopes,
            request.ClientType,
            request.RedirectUris,
            creatorUserId,
            ct);

        AppRegistrationResponse response = new(
            result.ClientId,
            result.ClientSecret,
            result.RegistrationAccessToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [HasPermission(PermissionType.ApiKeysRead)]
    [ProducesResponseType(typeof(IReadOnlyList<DeveloperAppResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeveloperAppResponse>>> GetUserApps(CancellationToken ct)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        IReadOnlyList<DeveloperAppInfo> apps = await developerAppService.GetUserAppsAsync(userId, ct);

        List<DeveloperAppResponse> response = apps.Select(a => new DeveloperAppResponse(
            a.ClientId,
            a.DisplayName,
            a.ClientType,
            a.RedirectUris,
            a.CreatedAt)).ToList();

        return Ok(response);
    }

    [HttpGet("{clientId}")]
    [HasPermission(PermissionType.ApiKeysRead)]
    [ProducesResponseType(typeof(DeveloperAppResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeveloperAppResponse>> GetUserApp(string clientId, CancellationToken ct)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        DeveloperAppInfo? app = await developerAppService.GetUserAppAsync(userId, clientId, ct);
        if (app is null)
        {
            return NotFound();
        }

        DeveloperAppResponse response = new(
            app.ClientId,
            app.DisplayName,
            app.ClientType,
            app.RedirectUris,
            app.CreatedAt);

        return Ok(response);
    }
}
