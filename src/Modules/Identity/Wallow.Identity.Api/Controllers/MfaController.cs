using System.Security.Cryptography;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/mfa")]
[Authorize]
[IgnoreAntiforgeryToken]
public sealed partial class MfaController(
    IMfaService mfaService,
    IMfaPartialAuthService mfaPartialAuthService,
    IMfaLockoutService mfaLockoutService,
    UserManager<WallowUser> userManager,
    IMessageBus messageBus,
    ITenantContext tenantContext,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<MfaController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        string userId = GetUserIdClaim();

        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        int backupCodeCount = 0;
        if (!string.IsNullOrEmpty(user.BackupCodesHash))
        {
            try
            {
                List<string>? hashes = JsonSerializer.Deserialize<List<string>>(user.BackupCodesHash);
                backupCodeCount = hashes?.Count ?? 0;
            }
            catch (JsonException) { }
        }

        return Ok(new { enabled = user.MfaEnabled, method = user.MfaMethod, backupCodeCount });
    }

    [HttpPost("enroll/totp")]
    [AllowAnonymous]
    public async Task<IActionResult> EnrollTotp(CancellationToken ct)
    {
        string? userId = await ResolveEnrollmentUserIdAsync(ct);
        if (userId is null)
        {
            return Unauthorized(new { succeeded = false, error = "no_auth_session" });
        }

        (string secret, string qrUri) = await mfaService.GenerateEnrollmentSecretAsync(userId, ct);

        return Ok(new { secret, qrUri });
    }

    [HttpPost("enroll/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEnrollment([FromBody] MfaConfirmRequest request, CancellationToken ct)
    {
        string? userId = await ResolveEnrollmentUserIdAsync(ct);
        if (userId is null)
        {
            return Unauthorized(new { succeeded = false, error = "no_auth_session" });
        }

        bool isValid = await mfaService.ValidateTotpAsync(request.Secret, request.Code, ct);
        if (!isValid)
        {
            return BadRequest(new { succeeded = false, error = "invalid_code" });
        }

        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return BadRequest(new { succeeded = false, error = "user_not_found" });
        }

        user.EnableMfa("totp", request.Secret);

        List<string> backupCodes = await mfaService.GenerateBackupCodesAsync(ct);
        string backupCodesHash = mfaService.SerializeBackupCodesForStorage(backupCodes);
        user.SetBackupCodes(backupCodesHash);

        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, error = "update_failed" });
        }

        await messageBus.PublishAsync(new UserMfaEnabledEvent
        {
            UserId = Guid.Parse(userId),
            TenantId = tenantContext.TenantId.Value
        });

        // Upgrade partial auth to full auth when enrollment was triggered by the MFA enrollment flow
        MfaPartialAuthPayload? partial = await mfaPartialAuthService.ValidatePartialCookieAsync(ct);
        if (partial is not null)
        {
            await mfaPartialAuthService.UpgradeToFullAuthAsync(userId, partial.RememberMe, ct);
        }

        return Ok(new { succeeded = true, backupCodes });
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable([FromBody] MfaDisableRequest request, CancellationToken _)
    {
        string currentUserId = GetUserIdClaim();

        WallowUser? user = await userManager.FindByIdAsync(currentUserId);
        if (user is null)
        {
            return BadRequest(new { succeeded = false, error = "user_not_found" });
        }

        bool passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return BadRequest(new { succeeded = false, error = "invalid_password" });
        }

        if (!user.MfaEnabled)
        {
            return BadRequest(new { succeeded = false, error = "mfa_not_enabled" });
        }

        user.DisableMfa();
        await userManager.UpdateAsync(user);

        await messageBus.PublishAsync(new UserMfaDisabledEvent
        {
            UserId = Guid.Parse(currentUserId),
            TenantId = tenantContext.TenantId.Value
        });

        return Ok(new { succeeded = true });
    }

    [HttpPost("backup-codes/regenerate")]
    public async Task<IActionResult> RegenerateBackupCodes([FromBody] MfaRegenerateBackupCodesRequest request, CancellationToken ct)
    {
        string currentUserId = GetUserIdClaim();

        WallowUser? user = await userManager.FindByIdAsync(currentUserId);
        if (user is null)
        {
            return BadRequest(new { succeeded = false, error = "user_not_found" });
        }

        bool passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return BadRequest(new { succeeded = false, error = "invalid_password" });
        }

        List<string> codes = await mfaService.GenerateBackupCodesAsync(ct);
        string backupCodesHash = mfaService.SerializeBackupCodesForStorage(codes);
        user.SetBackupCodes(backupCodesHash);
        await userManager.UpdateAsync(user);

        await messageBus.PublishAsync(new UserMfaBackupCodesRegeneratedEvent
        {
            UserId = Guid.Parse(currentUserId),
            TenantId = tenantContext.TenantId.Value
        });

        return Ok(new { codes });
    }

    [HttpPost("admin/{userId}/disable")]
    public async Task<IActionResult> AdminDisableMfa(string userId, CancellationToken _)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new { succeeded = false, error = "user_not_found" });
        }

        user.DisableMfa();
        await userManager.UpdateAsync(user);

        return Ok(new { succeeded = true });
    }

    [HttpPost("admin/{userId}/clear-lockout")]
    public async Task<IActionResult> AdminClearLockout(string userId, CancellationToken ct)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new { succeeded = false, error = "user_not_found" });
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await mfaLockoutService.ResetAsync(user.Id, ct);

        string currentUserId = GetUserIdClaim();
        await messageBus.PublishAsync(new UserMfaLockoutClearedEvent
        {
            UserId = user.Id,
            TenantId = tenantContext.TenantId.Value,
            ClearedByUserId = Guid.Parse(currentUserId)
        });

        return Ok(new { succeeded = true });
    }

    /// <summary>
    /// Issues a short-lived enrollment token for a fully-authenticated user.
    /// The Web app calls this (with a bearer token) to get a token it can pass to the
    /// Auth app's /mfa/enroll page, which exchanges it for an Identity.MfaPartial cookie
    /// so the enrollment API calls can authenticate the user.
    /// </summary>
    [HttpPost("enroll/issue-token")]
    public IActionResult IssueEnrollmentToken()
    {
        string userId = GetUserIdClaim();
        string? email = User.GetEmail();

        if (string.IsNullOrEmpty(email))
        {
            return BadRequest(new { succeeded = false, error = "email_claim_missing" });
        }

        string token = CreateEnrollmentToken(userId, email);
        LogEnrollmentTokenIssued(userId);
        return Ok(new { token });
    }

    /// <summary>
    /// Exchanges a short-lived enrollment token for an Identity.MfaPartial cookie.
    /// Called during Auth app prerender so the CookieForwardingHandler relays the
    /// partial cookie to the browser, enabling subsequent enrollment API calls.
    /// </summary>
    [HttpPost("enroll/exchange-token")]
    [AllowAnonymous]
    public async Task<IActionResult> ExchangeEnrollmentToken([FromQuery] string token, CancellationToken ct)
    {
        EnrollmentTokenPayload? payload = ValidateEnrollmentToken(token);
        if (payload is null)
        {
            return BadRequest(new { succeeded = false, error = "invalid_or_expired_token" });
        }

        await mfaPartialAuthService.IssuePartialCookieAsync(
            new MfaPartialAuthPayload(payload.UserId, payload.Email, "web_settings", false, DateTimeOffset.UtcNow),
            ct);

        return Ok(new { succeeded = true });
    }

    private string GetUserIdClaim() =>
        User.GetUserId()
        ?? throw new InvalidOperationException("User ID claim not found.");

    /// <summary>
    /// Resolves the user ID for MFA enrollment. Accepts either full authentication
    /// (standard Authorize) or a valid MFA partial cookie (issued when enrollment is required).
    /// </summary>
    private async Task<string?> ResolveEnrollmentUserIdAsync(CancellationToken ct)
    {
        string? userId = User.GetUserId();
        if (userId is not null)
        {
            return userId;
        }

        MfaPartialAuthPayload? partial = await mfaPartialAuthService.ValidatePartialCookieAsync(ct);
        return partial?.UserId;
    }

    private static readonly TimeSpan _enrollmentTokenLifetime = TimeSpan.FromSeconds(60);
    private const string EnrollmentTokenPurpose = "Wallow.Identity.MfaEnrollmentToken";

    private string CreateEnrollmentToken(string userId, string email)
    {
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(EnrollmentTokenPurpose)
            .ToTimeLimitedDataProtector();

        EnrollmentTokenPayload payload = new(userId, email);
        string json = JsonSerializer.Serialize(payload);
        return protector.Protect(json, _enrollmentTokenLifetime);
    }

    private EnrollmentTokenPayload? ValidateEnrollmentToken(string token)
    {
        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(EnrollmentTokenPurpose)
                .ToTimeLimitedDataProtector();

            string json = protector.Unprotect(token);
            return JsonSerializer.Deserialize<EnrollmentTokenPayload>(json);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            LogEnrollmentTokenValidationFailed(ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrollment token issued for user {UserId}")]
    private partial void LogEnrollmentTokenIssued(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Enrollment token validation failed: {Reason}")]
    private partial void LogEnrollmentTokenValidationFailed(string reason);
}

public sealed record MfaConfirmRequest(string Secret, string Code);
public sealed record MfaDisableRequest(string Password);
public sealed record MfaRegenerateBackupCodesRequest(string Password);
public sealed record EnrollmentTokenPayload(string UserId, string Email);
