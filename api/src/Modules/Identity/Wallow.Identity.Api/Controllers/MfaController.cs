using System.Security.Cryptography;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Extensions;
using Wolverine;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/mfa")]
[Authorize]
public sealed partial class MfaController(
    IMfaService mfaService,
    IMfaPartialAuthService mfaPartialAuthService,
    IMfaLockoutService mfaLockoutService,
    UserManager<WallowUser> userManager,
    IMessageBus messageBus,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<MfaController> logger) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(MfaStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        string userId = GetUserIdClaim();

        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return this.Problem(IdentityErrors.UserNotFound);
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
    [ProducesResponseType(typeof(MfaEnrollmentSecretResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnrollTotp(CancellationToken ct)
    {
        string? userId = await ResolveEnrollmentUserIdAsync(ct);
        if (userId is null)
        {
            return this.Problem(IdentityErrors.MfaSessionMissing);
        }

        (string secret, string qrUri) = await mfaService.GenerateEnrollmentSecretAsync(userId, ct);

        return Ok(new { secret, qrUri });
    }

    [HttpPost("enroll/confirm")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MfaEnrollmentConfirmedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmEnrollment([FromBody] MfaConfirmRequest request, CancellationToken ct)
    {
        string? userId = await ResolveEnrollmentUserIdAsync(ct);
        if (userId is null)
        {
            return this.Problem(IdentityErrors.MfaSessionMissing);
        }

        bool isValid = await mfaService.ValidateTotpAsync(request.Secret, request.Code, ct);
        if (!isValid)
        {
            return this.Problem(IdentityErrors.MfaCodeInvalid);
        }

        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return this.Problem(IdentityErrors.UserNotFound);
        }

        user.EnableMfa("totp", request.Secret);

        List<string> backupCodes = await mfaService.GenerateBackupCodesAsync(ct);
        string backupCodesHash = mfaService.SerializeBackupCodesForStorage(backupCodes);
        user.SetBackupCodes(backupCodesHash);

        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return this.Problem(IdentityErrors.MfaUpdateFailed);
        }

        await messageBus.PublishAsync(new UserMfaEnabledEvent
        {
            UserId = Guid.Parse(userId)
        });

        // Complete sign-in for an enrollment flow carrying a partial-auth cookie.
        MfaPartialAuthPayload? partial = await mfaPartialAuthService.ValidatePartialCookieAsync(ct);
        if (partial is not null)
        {
            await mfaPartialAuthService.UpgradeToFullAuthAsync(userId, partial.RememberMe, ct);
        }

        return Ok(new { succeeded = true, backupCodes });
    }

    [HttpPost("disable")]
    [ProducesResponseType(typeof(MfaOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable([FromBody] MfaDisableRequest request, CancellationToken _)
    {
        string currentUserId = GetUserIdClaim();

        WallowUser? user = await userManager.FindByIdAsync(currentUserId);
        if (user is null)
        {
            return this.Problem(IdentityErrors.UserNotFound);
        }

        bool passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return this.Problem(IdentityErrors.MfaPasswordInvalid);
        }

        if (!user.MfaEnabled)
        {
            return this.Problem(IdentityErrors.MfaNotEnabled);
        }

        user.DisableMfa();
        await userManager.UpdateAsync(user);

        await messageBus.PublishAsync(new UserMfaDisabledEvent
        {
            UserId = Guid.Parse(currentUserId)
        });

        return Ok(new { succeeded = true });
    }

    [HttpPost("backup-codes/regenerate")]
    [ProducesResponseType(typeof(MfaBackupCodesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegenerateBackupCodes([FromBody] MfaRegenerateBackupCodesRequest request, CancellationToken ct)
    {
        string currentUserId = GetUserIdClaim();

        WallowUser? user = await userManager.FindByIdAsync(currentUserId);
        if (user is null)
        {
            return this.Problem(IdentityErrors.UserNotFound);
        }

        bool passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return this.Problem(IdentityErrors.MfaPasswordInvalid);
        }

        List<string> codes = await mfaService.GenerateBackupCodesAsync(ct);
        string backupCodesHash = mfaService.SerializeBackupCodesForStorage(codes);
        user.SetBackupCodes(backupCodesHash);
        await userManager.UpdateAsync(user);

        await messageBus.PublishAsync(new UserMfaBackupCodesRegeneratedEvent
        {
            UserId = Guid.Parse(currentUserId)
        });

        return Ok(new { codes });
    }

    [HttpPost("admin/{userId}/disable")]
    [ProducesResponseType(typeof(MfaOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminDisableMfa(string userId, CancellationToken _)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return this.Problem(IdentityErrors.UserNotFound);
        }

        user.DisableMfa();
        await userManager.UpdateAsync(user);

        return Ok(new { succeeded = true });
    }

    [HttpPost("admin/{userId}/clear-lockout")]
    [ProducesResponseType(typeof(MfaOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminClearLockout(string userId, CancellationToken ct)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return this.Problem(IdentityErrors.UserNotFound);
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await mfaLockoutService.ResetAsync(user.Id, ct);

        string currentUserId = GetUserIdClaim();
        await messageBus.PublishAsync(new UserMfaLockoutClearedEvent
        {
            UserId = user.Id,
            ClearedByUserId = Guid.Parse(currentUserId)
        });

        return Ok(new { succeeded = true });
    }

    /// <summary>
    /// Issues a sixty-second token that the enrollment exchange endpoint accepts for partial authentication.
    /// </summary>
    [HttpPost("enroll/issue-token")]
    [ProducesResponseType(typeof(MfaEnrollmentTokenResponse), StatusCodes.Status200OK)]
    public IActionResult IssueEnrollmentToken()
    {
        string userId = GetUserIdClaim();
        string? email = User.GetEmail();

        if (string.IsNullOrEmpty(email))
        {
            return this.Problem(IdentityErrors.AuthEmailClaimMissing);
        }

        string token = CreateEnrollmentToken(userId, email);
        LogEnrollmentTokenIssued(userId);
        return Ok(new { token });
    }

    /// <summary>
    /// Exchanges an unexpired enrollment token for an MFA partial-auth cookie.
    /// </summary>
    [HttpPost("enroll/exchange-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MfaOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExchangeEnrollmentToken([FromQuery] string token, CancellationToken ct)
    {
        EnrollmentTokenPayload? payload = ValidateEnrollmentToken(token);
        if (payload is null)
        {
            return this.Problem(IdentityErrors.MfaEnrollmentTokenInvalid);
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
    /// Uses the principal user id when present, otherwise a valid MFA partial-auth cookie.
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
