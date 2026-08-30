using System.Text.Json;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wallow.Branding.Api.Contracts.Requests;
using Wallow.Branding.Application.DTOs;
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
using Wolverine;

namespace Wallow.Branding.Api.Controllers;

/// <summary>
/// Branding as an organization manages it: a sub-resource of the org-scoped client surface. The
/// route mirrors Identity's, but the controller lives here — Branding owns the data — and asks
/// Identity "does this client belong to this organization" only through
/// <see cref="IOrganizationClientDirectory"/>. A client of another organization, an unknown
/// client and a service account (which faces no end user) are all answered as not found.
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
    IMessageBus messageBus,
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

    /// <summary>The client's branding as its organization sees it.</summary>
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
    /// Replace the client's branding: display name, tagline, optional logo upload and the curated
    /// theme (<c>primary</c> and <c>primaryForeground</c> per <c>light</c>/<c>dark</c> mode). The
    /// display name may never read as the platform itself.
    /// </summary>
    [HttpPut]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ClientBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
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
            if (logo is not null && !string.IsNullOrEmpty(existing.LogoStorageKey))
            {
                await storageProvider.DeleteAsync(existing.LogoStorageKey, ct);
            }

            existing.Update(
                displayName,
                tagline,
                logo is not null ? logoStorageKey : existing.LogoStorageKey,
                themeJson,
                timeProvider);
        }
        else
        {
            // Registration creates the row, but a client registered before this surface existed
            // (or whose event is still in flight) still deserves a working PUT — and the new row
            // must carry the organization's tenant even when a caller far from it writes it.
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

        // Upload the logo to storage BEFORE saving so a stored key always points at a real object.
        if (logo is not null && logoStorageKey is not null)
        {
            await using Stream stream = logo.OpenReadStream();
            await storageProvider.UploadAsync(stream, logoStorageKey, logo.ContentType, ct);
        }

        await repository.SaveChangesAsync(ct);
        brandingService.InvalidateCache(clientId);

        await PublishUpdatedAsync(clientId, orgId, actorId, displayName);

        ClientBrandingDto? result = await brandingService.GetBrandingAsync(clientId, ct);
        return Ok(result);
    }

    /// <summary>Remove the client's logo. The rest of the branding stays.</summary>
    [HttpDelete("logo")]
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
        await repository.SaveChangesAsync(ct);
        brandingService.InvalidateCache(clientId);

        await PublishUpdatedAsync(clientId, orgId, actorId, existing.DisplayName);

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

    // Mirrors the parent client surface: the caller's own tenant and the global admin reach every
    // organization; anyone else only through a membership that carries the permission.
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

    private async Task PublishUpdatedAsync(string clientId, Guid orgId, Guid actorId, string displayName) =>
        await messageBus.PublishAsync(new ClientBrandingUpdatedEvent
        {
            ClientId = clientId,
            OrganizationId = orgId,
            ActorId = actorId,
            DisplayName = displayName,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });

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
    /// The theme is curated, not free-form: only the two modes, only the two color keys per mode,
    /// only color values. Everything else the older free-form surface accepted is rejected so the
    /// stored theme never outgrows what the sign-in screen renders.
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
