using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/apps")]
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

        HashSet<string> allowedScopes = [.. ApiScopes.DeveloperAppScopes, .. ApiScopes.LoginScopes];

        HashSet<string> invalidScopes = request.RequestedScopes
            .Where(s => !allowedScopes.Contains(s))
            .ToHashSet();

        if (invalidScopes.Count > 0)
        {
            ModelState.AddModelError(
                nameof(request.RequestedScopes),
                $"Invalid scopes: {string.Join(", ", invalidScopes)}. Allowed: {string.Join(", ", allowedScopes)}");
            return ValidationProblem(ModelState);
        }

        if (FirstInvalidUri(request.RedirectUris) is { } invalidRedirectUri)
        {
            ModelState.AddModelError(
                nameof(request.RedirectUris),
                $"Invalid redirect URI '{invalidRedirectUri}'. URIs must be absolute HTTPS (localhost may use HTTP).");
            return ValidationProblem(ModelState);
        }

        if (FirstInvalidUri(request.PostLogoutRedirectUris) is { } invalidPostLogoutUri)
        {
            ModelState.AddModelError(
                nameof(request.PostLogoutRedirectUris),
                $"Invalid post-logout redirect URI '{invalidPostLogoutUri}'. URIs must be absolute HTTPS (localhost may use HTTP).");
            return ValidationProblem(ModelState);
        }

        string? creatorUserId = User.GetUserId();

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
        string? userId = User.GetUserId();
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
        string? userId = User.GetUserId();
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

    [HttpGet("consent-info/{clientId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConsentInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsentInfoResponse>> GetConsentInfo(
        string clientId,
        [FromQuery] string? scopes,
        CancellationToken ct)
    {
        IReadOnlyList<string> scopeList = string.IsNullOrWhiteSpace(scopes)
            ? []
            : scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        ConsentInfoDto? dto = await developerAppService.GetConsentInfoAsync(clientId, scopeList, ct);
        if (dto is null)
        {
            return NotFound();
        }

        ConsentInfoResponse response = new(
            dto.ClientId,
            dto.DisplayName,
            dto.LogoUrl,
            dto.RequestedScopes.Select(s => new ScopeInfo(s.Name, s.Description)).ToList());

        return Ok(response);
    }

    // A registered client URI must be absolute and use HTTPS, except that localhost may use HTTP
    // for local development. Returns the first URI that violates the rule, or null when all are valid.
    private static string? FirstInvalidUri(IReadOnlyList<string>? uris)
    {
        if (uris is null)
        {
            return null;
        }

        foreach (string uri in uris)
        {
            if (!IsValidClientUri(uri))
            {
                return uri;
            }
        }

        return null;
    }

    private static bool IsValidClientUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        if (string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parsed.Host, "127.0.0.1", StringComparison.Ordinal);
    }
}
