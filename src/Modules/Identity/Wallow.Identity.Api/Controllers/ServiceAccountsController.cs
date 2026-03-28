using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Manages OAuth2 service accounts for API access via client credentials flow.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/service-accounts")]
[Authorize]
[Tags("Service Accounts")]
[Produces("application/json")]
[Consumes("application/json")]
[IgnoreAntiforgeryToken]
public class ServiceAccountsController(IServiceAccountService serviceAccountService) : ControllerBase
{

    /// <summary>
    /// List all service accounts for the current tenant.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.ApiKeysRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceAccountDto>>> List(CancellationToken ct)
    {
        IReadOnlyList<ServiceAccountDto> accounts = await serviceAccountService.ListAsync(ct);
        return Ok(accounts);
    }

    /// <summary>
    /// Create a new service account.
    /// Returns the client secret which will NOT be shown again.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.ApiKeysCreate)]
    [ProducesResponseType(typeof(ServiceAccountCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceAccountCreatedResponse>> Create(
        [FromBody] Contracts.Requests.CreateServiceAccountRequest request,
        CancellationToken ct)
    {
        Application.DTOs.CreateServiceAccountRequest appRequest = new(
            request.Name,
            request.Description,
            request.Scopes);

        ServiceAccountCreatedResult result = await serviceAccountService.CreateAsync(appRequest, ct);

        ServiceAccountCreatedResponse response = new()
        {
            Id = result.Id.Value.ToString(),
            ClientId = result.ClientId,
            ClientSecret = result.ClientSecret,
            TokenEndpoint = result.TokenEndpoint,
            Scopes = result.Scopes.ToList()
        };

        return CreatedAtAction(nameof(Get), new { id = result.Id.Value }, response);
    }

    /// <summary>
    /// Get a specific service account by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.ApiKeysRead)]
    [ProducesResponseType(typeof(ServiceAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceAccountDto>> Get(Guid id, CancellationToken ct)
    {
        ServiceAccountDto? account = await serviceAccountService.GetAsync(ServiceAccountMetadataId.Create(id), ct);
        return account is null ? NotFound() : Ok(account);
    }

    /// <summary>
    /// Update the scopes assigned to a service account.
    /// </summary>
    [HttpPut("{id:guid}/scopes")]
    [HasPermission(PermissionType.ApiKeysUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateScopes(
        Guid id,
        [FromBody] UpdateScopesRequest request,
        CancellationToken ct)
    {
        await serviceAccountService.UpdateScopesAsync(
            ServiceAccountMetadataId.Create(id),
            request.Scopes,
            ct);
        return NoContent();
    }

    /// <summary>
    /// Rotate the client secret for a service account.
    /// Returns the new secret which will NOT be shown again.
    /// </summary>
    [HttpPost("{id:guid}/rotate-secret")]
    [HasPermission(PermissionType.ApiKeysUpdate)]
    [ProducesResponseType(typeof(SecretRotatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecretRotatedResponse>> RotateSecret(Guid id, CancellationToken ct)
    {
        SecretRotatedResult result = await serviceAccountService.RotateSecretAsync(
            ServiceAccountMetadataId.Create(id),
            ct);

        return Ok(new SecretRotatedResponse
        {
            NewClientSecret = result.NewClientSecret,
            RotatedAt = result.RotatedAt
        });
    }

    /// <summary>
    /// Revoke and delete a service account.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionType.ApiKeysDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await serviceAccountService.RevokeAsync(
            ServiceAccountMetadataId.Create(id),
            ct);
        return NoContent();
    }
}
