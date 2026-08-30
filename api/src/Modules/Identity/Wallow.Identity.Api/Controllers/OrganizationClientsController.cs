using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// The org-scoped client surface: an organization's admins and managers register and manage the
/// clients it owns. A client of another organization is answered as not found, never forbidden.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/organizations/{orgId:guid}/clients")]
[Authorize]
[Tags("Organization Clients")]
[Produces("application/json")]
[Consumes("application/json")]
public class OrganizationClientsController(
    IOrganizationClientService clients,
    ITenantContext tenantContext,
    IOrganizationAccessPolicy accessPolicy) : ControllerBase
{
    /// <summary>
    /// Register a developer application for the organization. The response carries the client
    /// secret exactly once.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [EnableRateLimiting("developer-app-registration")]
    [ProducesResponseType(typeof(OrganizationClientRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationClientRegistrationResponse>> Register(
        Guid orgId,
        [FromBody] RegisterOrganizationClientRequest request,
        CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        if (!string.Equals(request.Kind, OrganizationClientResponse.ApplicationKind, StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                nameof(request.Kind),
                request.Kind == OrganizationClientResponse.ServiceAccountKind
                    ? "Service accounts cannot be registered on this surface yet."
                    : $"Kind must be '{OrganizationClientResponse.ApplicationKind}'.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
        }

        if (request.RedirectUris.Count == 0)
        {
            ModelState.AddModelError(nameof(request.RedirectUris), "At least one redirect URI is required.");
        }

        if (request.Scopes.Count == 0)
        {
            ModelState.AddModelError(nameof(request.Scopes), "At least one scope is required.");
        }

        if (!TryParseConfiguration(
                request.RedirectUris,
                request.PostLogoutRedirectUris,
                request.BackchannelLogoutUri,
                out List<Uri> redirectUris,
                out List<Uri> postLogoutRedirectUris,
                out Uri? backchannelLogoutUri)
            || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        OrganizationClientRegistrationResult result = await clients.RegisterApplicationAsync(
            orgId,
            new RegisterApplicationInput(
                request.Name.Trim(),
                redirectUris,
                postLogoutRedirectUris,
                backchannelLogoutUri,
                request.Scopes),
            ActorId(),
            ct);

        OrganizationClientRegistrationResponse response = new()
        {
            Client = OrganizationClientResponse.From(result.Client),
            ClientSecret = result.ClientSecret,
            Issuer = result.Issuer ?? RequestOrigin(),
            ApiBaseUrl = result.ApiBaseUrl ?? RequestOrigin(),
        };

        return CreatedAtAction(nameof(GetById), new { orgId, clientId = result.Client.ClientId }, response);
    }

    /// <summary>List the clients the organization owns.</summary>
    [HttpGet]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationClientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrganizationClientResponse>>> List(Guid orgId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        IReadOnlyList<OrganizationClientDto> result = await clients.ListAsync(orgId, ct);
        return Ok(result.Select(OrganizationClientResponse.From).ToList());
    }

    /// <summary>Get one of the organization's clients.</summary>
    [HttpGet("{clientId}")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationClientResponse>> GetById(Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        OrganizationClientDto? client = await clients.GetAsync(orgId, clientId, ct);
        return client is null ? NotFound() : Ok(OrganizationClientResponse.From(client));
    }

    /// <summary>
    /// Replace a client's redirect URIs, logout URI and scopes. Name and client id are immutable.
    /// </summary>
    [HttpPatch("{clientId}")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationClientResponse>> Update(
        Guid orgId,
        string clientId,
        [FromBody] UpdateOrganizationClientRequest request,
        CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        if (request.RedirectUris.Count == 0)
        {
            ModelState.AddModelError(nameof(request.RedirectUris), "At least one redirect URI is required.");
        }

        if (request.Scopes.Count == 0)
        {
            ModelState.AddModelError(nameof(request.Scopes), "At least one scope is required.");
        }

        if (!TryParseConfiguration(
                request.RedirectUris,
                request.PostLogoutRedirectUris,
                request.BackchannelLogoutUri,
                out List<Uri> redirectUris,
                out List<Uri> postLogoutRedirectUris,
                out Uri? backchannelLogoutUri)
            || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        OrganizationClientDto? client = await clients.UpdateAsync(
            orgId,
            clientId,
            new UpdateOrganizationClientInput(redirectUris, postLogoutRedirectUris, backchannelLogoutUri, request.Scopes),
            ct);

        return client is null ? NotFound() : Ok(OrganizationClientResponse.From(client));
    }

    /// <summary>Delete one of the organization's clients.</summary>
    [HttpDelete("{clientId}")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        return await clients.DeleteAsync(orgId, clientId, ct) ? NoContent() : NotFound();
    }

    // Mirrors OrganizationsController: the caller's own tenant and the global admin reach every
    // organization; anyone else only through a membership that carries the permission.
    private async Task<bool> CanAddressOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        if (orgId == tenantContext.TenantId.Value || User.IsGlobalAdmin())
        {
            return true;
        }

        return Guid.TryParse(User.GetUserId(), out Guid callerId)
            && await accessPolicy.HasPermissionInOrganizationAsync(
                orgId, callerId, PermissionType.OrganizationClientsManage, ct);
    }

    private Guid ActorId() => Guid.Parse(User.GetUserId()!);

    // What OpenIddict advertises as the issuer when none is configured: the origin it was reached on.
    private string RequestOrigin() => $"{Request.Scheme}://{Request.Host}";

    /// <summary>
    /// Parses every URI the request carries under the shared client URI rules, recording each
    /// refusal against its field so one response names them all.
    /// </summary>
    private bool TryParseConfiguration(
        IReadOnlyList<string> redirectValues,
        IReadOnlyList<string> postLogoutValues,
        string? backchannelValue,
        out List<Uri> redirectUris,
        out List<Uri> postLogoutRedirectUris,
        out Uri? backchannelLogoutUri)
    {
        redirectUris = [];
        postLogoutRedirectUris = [];
        backchannelLogoutUri = null;
        bool valid = true;

        foreach (string value in redirectValues)
        {
            if (ClientUriRules.TryParseRedirectUri(value, out Uri? uri))
            {
                redirectUris.Add(uri);
            }
            else
            {
                valid = false;
                ModelState.AddModelError("redirectUris", $"'{value}': {ClientUriRules.RedirectUriError}");
            }
        }

        foreach (string value in postLogoutValues)
        {
            if (ClientUriRules.TryParseRedirectUri(value, out Uri? uri))
            {
                postLogoutRedirectUris.Add(uri);
            }
            else
            {
                valid = false;
                ModelState.AddModelError("postLogoutRedirectUris", $"'{value}': {ClientUriRules.RedirectUriError}");
            }
        }

        if (!string.IsNullOrWhiteSpace(backchannelValue))
        {
            if (ClientUriRules.TryParseLogoutUri(backchannelValue, out Uri? uri))
            {
                backchannelLogoutUri = uri;
            }
            else
            {
                valid = false;
                ModelState.AddModelError("backchannelLogoutUri", ClientUriRules.LogoutUriError);
            }
        }

        return valid;
    }
}
