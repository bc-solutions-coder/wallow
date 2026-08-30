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
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// The org-scoped client surface: an organization's admins and managers register and manage the
/// clients it owns, developer applications and service accounts alike. A client of another
/// organization is answered as not found, never forbidden.
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
    IOrganizationAccessPolicy accessPolicy,
    IMessageBus messageBus) : ControllerBase
{
    private const string RedirectUrisField = "redirectUris";
    private const string PostLogoutRedirectUrisField = "postLogoutRedirectUris";
    private const string BackchannelLogoutUriField = "backchannelLogoutUri";
    private const string ScopesField = "scopes";

    /// <summary>
    /// Register a developer application or a service account for the organization. The response
    /// carries the client secret exactly once. A service account ignores every URI field.
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

        RegisteredClientKind? kind = OrganizationClientResponse.ParseKind(request.Kind);
        if (kind is null)
        {
            ModelState.AddModelError(
                nameof(request.Kind),
                $"Kind must be '{OrganizationClientResponse.ApplicationKind}' or '{OrganizationClientResponse.ServiceAccountKind}'.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
        }

        ClientConfigurationInput? configuration = ParseConfiguration(
            kind ?? RegisteredClientKind.Application,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            request.BackchannelLogoutUri,
            request.Scopes);
        if (kind is null || configuration is null || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        OrganizationClientRegistrationResult result = await clients.RegisterAsync(
            orgId,
            new RegisterClientInput(kind.Value, request.Name.Trim(), configuration),
            ActorId(),
            ct);

        await messageBus.PublishAsync(new ClientRegisteredEvent
        {
            ClientId = result.Client.ClientId,
            OrganizationId = orgId,
            ActorId = ActorId(),
            IpAddress = CallerIpAddress(),
        });

        return CreatedAtAction(nameof(GetById), new { orgId, clientId = result.Client.ClientId }, Reveal(result));
    }

    /// <summary>
    /// Replace the client secret. The response carries the new secret exactly once; the old one
    /// stops working immediately. <c>revokeActiveTokens</c> also ends every token the client was
    /// already issued.
    /// </summary>
    [HttpPost("{clientId}/rotate-secret")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationClientRegistrationResponse>> RotateSecret(
        Guid orgId,
        string clientId,
        [FromBody] RotateOrganizationClientSecretRequest request,
        CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        OrganizationClientRegistrationResult? result = await clients.RotateSecretAsync(
            orgId, clientId, request.RevokeActiveTokens, ActorId(), ct);
        if (result is null)
        {
            return NotFound();
        }

        await messageBus.PublishAsync(new ClientSecretRotatedEvent
        {
            ClientId = result.Client.ClientId,
            OrganizationId = orgId,
            ActorId = ActorId(),
            ActiveTokensRevoked = request.RevokeActiveTokens,
            IpAddress = CallerIpAddress(),
        });

        return Ok(Reveal(result));
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
    /// Replace a client's redirect URIs, logout URI and scopes. Name and client id are immutable;
    /// a service account's URI fields are ignored.
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

        // The kind decides which fields the request must carry, so it is read before validation.
        OrganizationClientDto? existing = await clients.GetAsync(orgId, clientId, ct);
        if (existing is null)
        {
            return NotFound();
        }

        ClientConfigurationInput? configuration = ParseConfiguration(
            existing.Kind, request.RedirectUris, request.PostLogoutRedirectUris, request.BackchannelLogoutUri, request.Scopes);
        if (configuration is null || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        OrganizationClientDto? client = await clients.UpdateAsync(orgId, clientId, configuration, ct);

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

    private string? CallerIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private OrganizationClientRegistrationResponse Reveal(OrganizationClientRegistrationResult result) =>
        new()
        {
            Client = OrganizationClientResponse.From(result.Client),
            ClientSecret = result.ClientSecret,
            Issuer = result.Issuer ?? RequestOrigin(),
            ApiBaseUrl = result.ApiBaseUrl ?? RequestOrigin(),
        };

    // What OpenIddict advertises as the issuer when none is configured: the origin it was reached on.
    private string RequestOrigin() => $"{Request.Scheme}://{Request.Host}";

    /// <summary>
    /// Validates the configuration half of a register or update request under the shared client
    /// URI rules, recording every refusal against its field so one response names them all.
    /// Returns <see langword="null"/> when anything was refused. A service account has no URI
    /// fields to validate: whatever the request carries there is dropped, not refused.
    /// </summary>
    private ClientConfigurationInput? ParseConfiguration(
        RegisteredClientKind kind,
        IReadOnlyList<string> redirectValues,
        IReadOnlyList<string> postLogoutValues,
        string? backchannelValue,
        IReadOnlyList<string> scopes)
    {
        bool valid = true;
        if (scopes.Count == 0)
        {
            valid = false;
            ModelState.AddModelError(ScopesField, "At least one scope is required.");
        }

        if (kind == RegisteredClientKind.ServiceAccount)
        {
            return valid ? new ClientConfigurationInput([], [], null, scopes) : null;
        }

        if (redirectValues.Count == 0)
        {
            valid = false;
            ModelState.AddModelError(RedirectUrisField, "At least one redirect URI is required.");
        }

        valid &= TryParseRedirectUris(redirectValues, RedirectUrisField, out List<Uri> redirectUris);
        valid &= TryParseRedirectUris(postLogoutValues, PostLogoutRedirectUrisField, out List<Uri> postLogoutRedirectUris);

        Uri? backchannelLogoutUri = null;
        if (!string.IsNullOrWhiteSpace(backchannelValue)
            && !ClientUriRules.TryParseLogoutUri(backchannelValue, out backchannelLogoutUri))
        {
            valid = false;
            ModelState.AddModelError(BackchannelLogoutUriField, ClientUriRules.LogoutUriError);
        }

        return valid
            ? new ClientConfigurationInput(redirectUris, postLogoutRedirectUris, backchannelLogoutUri, scopes)
            : null;
    }

    private bool TryParseRedirectUris(IReadOnlyList<string> values, string field, out List<Uri> uris)
    {
        uris = new List<Uri>(values.Count);
        bool valid = true;
        foreach (string value in values)
        {
            if (ClientUriRules.TryParseRedirectUri(value, out Uri? uri))
            {
                uris.Add(uri);
            }
            else
            {
                valid = false;
                ModelState.AddModelError(field, $"'{value}': {ClientUriRules.RedirectUriError}");
            }
        }

        return valid;
    }
}
