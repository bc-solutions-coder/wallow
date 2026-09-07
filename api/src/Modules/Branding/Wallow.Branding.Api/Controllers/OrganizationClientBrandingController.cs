using System.Text.Json;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Wallow.Branding.Api.Contracts.Requests;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Exceptions;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Branding.Events;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Configuration;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Branding.Api.Controllers;

/// <summary>
/// Manages application branding under an organization-owned client.
/// Ownership comes from <see cref="IOrganizationClientDirectory"/>. Unknown clients,
/// clients owned by another organization and service accounts return 404.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/organizations/{orgId:guid}/clients/{clientId}/branding")]
[Authorize]
[HasPermission(PermissionType.OrganizationClientsManage)]
[Tags("Organization Client Branding")]
[Produces("application/json")]
public partial class OrganizationClientBrandingController(
    IClientBrandingRepository repository,
    IClientBrandingService brandingService,
    IStorageProvider storageProvider,
    IOrganizationClientDirectory clientDirectory,
    ITenantContext tenantContext,
    IOptions<ForkBrandingOptions> forkBranding,
    TimeProvider timeProvider) : ControllerBase
{
    private static readonly HashSet<string> _allowedImageTypes = ["image/png", "image/jpeg", "image/webp"];
    private static readonly Dictionary<string, byte[]> _magicBytes = new()
    {
        ["image/png"] = [0x89, 0x50, 0x4E, 0x47],
        ["image/jpeg"] = [0xFF, 0xD8, 0xFF],
        ["image/webp"] = [0x52, 0x49, 0x46, 0x46]
    };
    private const long MaxLogoSize = 2 * 1024 * 1024; // 2MB
    private static readonly string[] _themeModes = ["light", "dark"];
    private static readonly string[] _themeColorKeys = ["primary", "primaryForeground"];
    private static readonly Regex _colorPattern = _colorPatternRegex();

    /// <summary>
    /// Get an application client's branding.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage and access to the owning organization through the current tenant,
    /// global administration, or client-management membership. Returns the display name, tagline, theme JSON,
    /// and a temporary logo URL when a logo exists. Returns 404 for inaccessible organizations, unknown clients,
    /// service accounts, or missing branding.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ClientBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientBrandingDto>> GetBranding(
        Guid orgId, string clientId, CancellationToken ct)
    {
        if (await OwnedApplicationAsync(orgId, clientId, ct) is null)
        {
            return NotFound();
        }

        ClientBrandingDto? branding = await brandingService.GetBrandingAsync(clientId, ct);
        if (branding is null)
        {
            return NotFound();
        }

        return Ok(branding);
    }

    /// <summary>
    /// Replace an application client's branding.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage and access to the owning organization through the current tenant,
    /// global administration, or client-management membership. Returns the saved branding and updates the client
    /// display name used for authentication. Omitting tagline or theme clears it; omitting the logo preserves
    /// it. Invalid names, themes, or image content return a validation error; inaccessible clients and service
    /// accounts return 404.
    /// </remarks>
    /// <param name="request">Replacement display name, optional tagline, and theme JSON. The display name cannot match the platform name; themes accept primary and primaryForeground colors in light and dark modes.</param>
    /// <param name="logo">Optional PNG, JPEG, or WebP image up to 2 MiB. Replaces the existing logo; use the logo deletion endpoint to remove it.</param>
    /// <param name="orgId">Organization that owns the client.</param>
    /// <param name="clientId">Client identifier within the organization.</param>
    /// <param name="ct">Cancels the request.</param>
    [HttpPut]
    [EnableRateLimiting("registration")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ClientBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientBrandingDto>> UpsertBranding(
        Guid orgId,
        string clientId,
        [FromForm] UpsertClientBrandingRequest request,
        IFormFile? logo,
        CancellationToken ct)
    {
        OrganizationClientInfo? client = await OwnedApplicationAsync(orgId, clientId, ct);
        if (client is null)
        {
            return NotFound();
        }

        string displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
        {
            ModelState.AddModelError(nameof(request.DisplayName), "Display name is required.");
        }
        else if (displayName.Length > 200)
        {
            ModelState.AddModelError(nameof(request.DisplayName), "Display name must be at most 200 characters.");
        }
        else if (forkBranding.Value.IsReservedDisplayName(displayName))
        {
            ModelState.AddModelError(
                nameof(request.DisplayName),
                $"'{forkBranding.Value.AppName}' is reserved for the platform itself.");
        }

        string? tagline = string.IsNullOrWhiteSpace(request.Tagline) ? null : request.Tagline.Trim();
        if (tagline is { Length: > 500 })
        {
            ModelState.AddModelError(nameof(request.Tagline), "Tagline must be at most 500 characters.");
        }

        string? themeJson = string.IsNullOrWhiteSpace(request.ThemeJson) ? null : request.ThemeJson;
        if (themeJson is not null && !IsValidThemeJson(themeJson))
        {
            ModelState.AddModelError(
                nameof(request.ThemeJson),
                "Theme must be JSON with only 'light' and 'dark' modes, each carrying only " +
                "'primary' and 'primaryForeground' color values.");
        }

        if (logo is not null)
        {
            string? validationError = await ValidateLogoAsync(logo);
            if (validationError is not null)
            {
                ModelState.AddModelError("logo", validationError);
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        Guid actorId = ActorId();

        string? logoStorageKey = null;
        if (logo is not null)
        {
            string safeFileName = $"{Guid.NewGuid():N}{Path.GetExtension(Path.GetFileName(logo.FileName))}";
            logoStorageKey = $"client-logos/{clientId}/{safeFileName}";
        }

        ClientBranding? existing = await repository.GetByClientIdAsync(clientId, ct);
        if (existing is not null)
        {
            await ApplyRequestAsync(existing);
        }
        else
        {
            // Create a missing row under the owning organization, including when registration
            // has not finished or the caller belongs to another tenant.
            repository.UseTenant(TenantId.Create(orgId));
            ClientBranding branding = ClientBranding.Create(
                clientId,
                displayName,
                tagline,
                logoStorageKey,
                themeJson,
                timeProvider);
            repository.Add(branding);
        }

        // Upload before saving the new key so a successful save references an uploaded object.
        if (logo is not null && logoStorageKey is not null)
        {
            await using Stream stream = logo.OpenReadStream();
            await storageProvider.UploadAsync(stream, logoStorageKey, logo.ContentType, ct);
        }

        // Save and event commit together through the outbox; a rejected save publishes nothing.
        ClientBrandingUpdatedEvent updated = UpdatedEvent(clientId, orgId, actorId, displayName);
        try
        {
            await repository.SaveChangesAndPublishAsync(updated, ct);
        }
        catch (DuplicateClientBrandingException)
        {
            // A concurrent insert won. The repository detached our insert; apply this PUT
            // to the winning row so explicit branding overrides the registration default.
            ClientBranding? winner = await repository.GetByClientIdAsync(clientId, ct);
            if (winner is null)
            {
                return await VanishedAsync();
            }

            await ApplyRequestAsync(winner);
            try
            {
                await repository.SaveChangesAndPublishAsync(updated, ct);
            }
            catch (ClientBrandingConcurrentlyDeletedException)
            {
                return await VanishedAsync();
            }
        }
        brandingService.InvalidateCache(clientId);

        ClientBrandingDto? result = await brandingService.GetBrandingAsync(clientId, ct);
        return Ok(result);

        // Apply identical replacement rules on the initial write and duplicate-insert retry.
        async Task ApplyRequestAsync(ClientBranding target)
        {
            if (logo is not null && !string.IsNullOrEmpty(target.LogoStorageKey))
            {
                await storageProvider.DeleteAsync(target.LogoStorageKey, ct);
            }

            target.Update(
                displayName,
                tagline,
                logo is not null ? logoStorageKey : target.LogoStorageKey,
                themeJson,
                timeProvider);
        }

        // A concurrent deletion leaves no branding row; remove the new upload before 404.
        async Task<ActionResult<ClientBrandingDto>> VanishedAsync()
        {
            if (logoStorageKey is not null)
            {
                await storageProvider.DeleteAsync(logoStorageKey, ct);
            }

            return NotFound();
        }
    }

    /// <summary>
    /// Remove an application client's logo.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage and access to the owning organization through the current tenant,
    /// global administration, or client-management membership. Deletes the stored logo and clears its URL while
    /// preserving the other branding fields. Returns no content if the branding already has no logo, or 404 for
    /// inaccessible clients, service accounts, or missing branding.
    /// </remarks>
    [HttpDelete("logo")]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLogo(Guid orgId, string clientId, CancellationToken ct)
    {
        if (await OwnedApplicationAsync(orgId, clientId, ct) is null)
        {
            return NotFound();
        }

        ClientBranding? existing = await repository.GetByClientIdAsync(clientId, ct);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(existing.LogoStorageKey))
        {
            return NoContent();
        }

        Guid actorId = ActorId();

        await storageProvider.DeleteAsync(existing.LogoStorageKey, ct);
        existing.ClearLogo(timeProvider);
        await repository.SaveChangesAndPublishAsync(
            UpdatedEvent(clientId, orgId, actorId, existing.DisplayName), ct);
        brandingService.InvalidateCache(clientId);

        return NoContent();
    }

    /// <summary>
    /// The organization's developer application, or null — for a foreign organization, an unknown
    /// client or a service account alike, so every one of them is answered as not found.
    /// </summary>
    private async Task<OrganizationClientInfo?> OwnedApplicationAsync(
        Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return null;
        }

        OrganizationClientInfo? client = await clientDirectory.FindAsync(orgId, clientId, ct);
        return client is { Kind: OrganizationClientKind.Application } ? client : null;
    }

    // The caller may address their own tenant; global admins may address any organization.
    // Other organizations require a membership with client-management permission.
    private async Task<bool> CanAddressOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        if (orgId == tenantContext.TenantId.Value || User.IsGlobalAdmin())
        {
            return true;
        }

        return Guid.TryParse(User.GetUserId(), out Guid callerId)
            && await clientDirectory.CanManageClientsAsync(orgId, callerId, ct);
    }

    private Guid ActorId() => Guid.Parse(User.GetUserId()!);

    private ClientBrandingUpdatedEvent UpdatedEvent(string clientId, Guid orgId, Guid actorId, string displayName) =>
        new()
        {
            ClientId = clientId,
            OrganizationId = orgId,
            ActorId = actorId,
            DisplayName = displayName,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        };

    private static async Task<string?> ValidateLogoAsync(IFormFile logo)
    {
        if (logo.Length > MaxLogoSize)
        {
            return "Logo must be under 2MB.";
        }

        if (!_allowedImageTypes.Contains(logo.ContentType))
        {
            return "Logo must be PNG, JPEG, or WebP.";
        }

        if (_magicBytes.TryGetValue(logo.ContentType, out byte[]? expected))
        {
            byte[] header = new byte[12];
            await using Stream stream = logo.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header.AsMemory(0, 12));

            if (bytesRead < expected.Length || !header.AsSpan(0, expected.Length).SequenceEqual(expected))
            {
                return "File content does not match the declared content type.";
            }

            if (logo.ContentType == "image/webp")
            {
                byte[] webpMarker = "WEBP"u8.ToArray();
                if (bytesRead < 12 || !header.AsSpan(8, 4).SequenceEqual(webpMarker))
                {
                    return "File content does not match the declared content type.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Accepts only light/dark objects with primary/primaryForeground color strings
    /// matching the configured color pattern.
    /// </summary>
    private static bool IsValidThemeJson(string themeJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(themeJson);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (JsonProperty mode in root.EnumerateObject())
            {
                if (!_themeModes.Contains(mode.Name) || mode.Value.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                foreach (JsonProperty color in mode.Value.EnumerateObject())
                {
                    if (!_themeColorKeys.Contains(color.Name)
                        || color.Value.ValueKind != JsonValueKind.String
                        || !_colorPattern.IsMatch(color.Value.GetString() ?? string.Empty))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^(oklch\([^)]+\)|#[0-9a-fA-F]{3,8})$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex _colorPatternRegex();
}
