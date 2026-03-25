using System.Text.Json;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Wallow.Branding.Api.Contracts.Requests;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Branding.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/apps/{clientId}/branding")]
[Tags("Client Branding")]
[Produces("application/json")]
public partial class ClientBrandingController(
    IClientBrandingRepository repository,
    IClientBrandingService brandingService,
    IStorageProvider storageProvider,
    IOpenIddictApplicationManager applicationManager) : ControllerBase
{
    private static readonly HashSet<string> _allowedImageTypes = ["image/png", "image/jpeg", "image/webp"];
    private static readonly Dictionary<string, byte[]> _magicBytes = new()
    {
        ["image/png"] = [0x89, 0x50, 0x4E, 0x47],
        ["image/jpeg"] = [0xFF, 0xD8, 0xFF],
        ["image/webp"] = [0x52, 0x49, 0x46, 0x46]
    };
    private const long MaxLogoSize = 2 * 1024 * 1024; // 2MB
    private static readonly Regex _colorPattern = _colorPatternRegex();

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ClientBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ClientBrandingDto>> GetBranding(string clientId, CancellationToken ct)
    {
        ClientBrandingDto? branding = await brandingService.GetBrandingAsync(clientId, ct);
        if (branding is null)
        {
            return NotFound();
        }

        return Ok(branding);
    }

    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ClientBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientBrandingDto>> UpsertBranding(
        string clientId,
        [FromForm] UpsertClientBrandingRequest request,
        IFormFile? logo,
        CancellationToken ct)
    {
        string? userId = User.GetUserId();
        if (!await IsClientOwnerAsync(clientId, userId, ct))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            ModelState.AddModelError(nameof(request.DisplayName), "Display name is required.");
            return ValidationProblem(ModelState);
        }

        if (!string.IsNullOrEmpty(request.ThemeJson))
        {
            if (!IsValidThemeJson(request.ThemeJson))
            {
                ModelState.AddModelError(nameof(request.ThemeJson), "Invalid theme JSON format or color values.");
                return ValidationProblem(ModelState);
            }
        }

        string? logoStorageKey = null;
        if (logo is not null)
        {
            string? validationError = await ValidateLogoAsync(logo);
            if (validationError is not null)
            {
                ModelState.AddModelError("logo", validationError);
                return ValidationProblem(ModelState);
            }

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
                request.DisplayName,
                request.Tagline,
                logo is not null ? logoStorageKey : existing.LogoStorageKey,
                request.ThemeJson ?? existing.ThemeJson);
        }
        else
        {
            ClientBranding branding = ClientBranding.Create(
                clientId,
                request.DisplayName,
                request.Tagline,
                logoStorageKey,
                request.ThemeJson);
            repository.Add(branding);
        }

        await repository.SaveChangesAsync(ct);

        if (logo is not null && logoStorageKey is not null)
        {
            await using Stream stream = logo.OpenReadStream();
            await storageProvider.UploadAsync(stream, logoStorageKey, logo.ContentType, ct);
        }

        brandingService.InvalidateCache(clientId);

        ClientBrandingDto? result = await brandingService.GetBrandingAsync(clientId, ct);
        return Ok(result);
    }

    [HttpDelete]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBranding(string clientId, CancellationToken ct)
    {
        string? userId = User.GetUserId();
        if (!await IsClientOwnerAsync(clientId, userId, ct))
        {
            return Forbid();
        }

        ClientBranding? existing = await repository.GetByClientIdAsync(clientId, ct);
        if (existing is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(existing.LogoStorageKey))
        {
            await storageProvider.DeleteAsync(existing.LogoStorageKey, ct);
        }

        repository.Remove(existing);
        await repository.SaveChangesAsync(ct);
        brandingService.InvalidateCache(clientId);

        return NoContent();
    }

    private async Task<bool> IsClientOwnerAsync(string clientId, string? userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        object? application = await applicationManager.FindByClientIdAsync(clientId, ct);
        if (application is null)
        {
            return false;
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);

        if (!descriptor.Properties.TryGetValue("creatorUserId", out JsonElement creatorElement))
        {
            return false;
        }

        string? creatorUserId = creatorElement.Deserialize<string>();
        return string.Equals(creatorUserId, userId, StringComparison.Ordinal);
    }

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

    private static bool IsValidThemeJson(string themeJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(themeJson);
            return ValidateThemeColors(doc.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly HashSet<string> _colorPropertyNames =
    [
        "background", "foreground", "card", "cardForeground", "popover", "popoverForeground",
        "primary", "primaryForeground", "secondary", "secondaryForeground", "muted", "mutedForeground",
        "accent", "accentForeground", "destructive", "destructiveForeground", "border", "input", "ring"
    ];

    private static bool ValidateThemeColors(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (_colorPropertyNames.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.String)
                {
                    string value = prop.Value.GetString() ?? "";
                    if (!_colorPattern.IsMatch(value))
                    {
                        return false;
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (!ValidateThemeColors(prop.Value))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    [GeneratedRegex(@"^(oklch\([^)]+\)|#[0-9a-fA-F]{3,8}|[0-9.]+rem)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex _colorPatternRegex();
}
