using Asp.Versioning;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Application.Constants;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Foundry.Shared.Kernel.Services;

namespace Foundry.Identity.Api.Controllers;

/// <summary>
/// API key management endpoints for service-to-service authentication.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/auth/keys")]
[Authorize]
public sealed class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public ApiKeysController(
        IApiKeyService apiKeyService,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService)
    {
        _apiKeyService = apiKeyService;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Create a new API key for the current user.
    /// </summary>
    /// <remarks>
    /// Creates an API key that can be used for service-to-service authentication.
    /// The full API key is only returned once in this response - store it securely!
    ///
    /// The key will be scoped to the current user's tenant.
    ///
    /// Example request:
    /// ```json
    /// {
    ///   "name": "Production Backend",
    ///   "scopes": ["billing:read", "billing:write"],
    ///   "expiresAt": "2027-01-01T00:00:00Z"
    /// }
    /// ```
    /// </remarks>
    [HttpPost]
    [HasPermission(PermissionType.ApiKeyManage)]
    [ProducesResponseType(typeof(ApiKeyCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateApiKey(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "API key name is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Guid tenantId = _tenantContext.TenantId.Value;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Tenant required",
                Detail = "You must be associated with an organization to create API keys",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Scopes is { Count: > 0 })
        {
            List<string> invalidScopes = request.Scopes
                .Where(s => !ApiScopes.ValidScopes.Contains(s))
                .ToList();

            if (invalidScopes.Count > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid scopes",
                    Detail = $"The following scopes are not valid: {string.Join(", ", invalidScopes)}",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        ApiKeyCreateResult result = await _apiKeyService.CreateApiKeyAsync(
            request.Name,
            userId.Value,
            tenantId,
            request.Scopes,
            request.ExpiresAt,
            ct);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to create API key",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        ApiKeyCreatedResponse response = new(
            KeyId: result.KeyId!,
            ApiKey: result.ApiKey!,
            Prefix: result.Prefix!,
            Name: request.Name,
            Scopes: request.Scopes ?? [],
            ExpiresAt: request.ExpiresAt);

        return CreatedAtAction(nameof(ListApiKeys), response);
    }

    /// <summary>
    /// List all API keys for the current user.
    /// </summary>
    /// <remarks>
    /// Returns metadata for all API keys belonging to the authenticated user.
    /// The actual key values are not returned - only the prefix for identification.
    /// </remarks>
    [HttpGet]
    [HasPermission(PermissionType.ApiKeyManage)]
    [ProducesResponseType(typeof(List<ApiKeyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListApiKeys(CancellationToken ct)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        IReadOnlyList<ApiKeyMetadata> keys = await _apiKeyService.ListApiKeysAsync(userId.Value, ct);

        List<ApiKeyResponse> response = keys.Select(k => new ApiKeyResponse(
            KeyId: k.KeyId,
            Name: k.Name,
            Prefix: k.Prefix,
            Scopes: k.Scopes.ToList(),
            CreatedAt: k.CreatedAt,
            ExpiresAt: k.ExpiresAt,
            LastUsedAt: k.LastUsedAt)).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Revoke an API key.
    /// </summary>
    /// <remarks>
    /// Permanently revokes an API key. This action cannot be undone.
    /// Any requests using this key will be rejected immediately.
    /// </remarks>
    [HttpDelete("{keyId}")]
    [HasPermission(PermissionType.ApiKeyManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeApiKey(string keyId, CancellationToken ct)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        bool revoked = await _apiKeyService.RevokeApiKeyAsync(keyId, userId.Value, ct);

        if (!revoked)
        {
            return NotFound(new ProblemDetails
            {
                Title = "API key not found",
                Detail = "The specified API key does not exist or does not belong to you",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

}
