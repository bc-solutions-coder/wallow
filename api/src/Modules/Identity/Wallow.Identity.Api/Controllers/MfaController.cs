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
    /// <summary>
    /// Get the current user MFA status.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user. Returns whether MFA is enabled, the configured method, and the number of
    /// remaining backup codes. MFA belongs to the account across organizations.
    /// </remarks>
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

    /// <summary>
    /// Generate a TOTP enrollment secret.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user or a valid MFA partial-auth browser cookie. Returns a base32 secret and an
    /// otpauth URI for an authenticator app. MFA is enabled only after enroll/confirm receives the secret and a valid
    /// code.
    /// </remarks>
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

    /// <summary>
    /// Confirm TOTP enrollment and return backup codes.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user or a valid MFA partial-auth browser cookie. Accepts the enrollment Secret and a
    /// current authenticator Code, enables account MFA, and replaces backup codes with ten new codes. Returns the
    /// plaintext backup codes for storage by the user. A partial-auth enrollment also establishes the full browser
    /// sign-in cookie.
    /// </remarks>
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

    /// <summary>
    /// Disable MFA for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user and their current password. Disables account MFA and clears its secret and
    /// backup codes. Returns a problem when the password is invalid or MFA is already disabled.
    /// </remarks>
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

    /// <summary>
    /// Replace the current user MFA backup codes.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user and their current password. Replaces all stored backup codes with ten new codes
    /// and returns their plaintext values. Previously issued backup codes stop working.
    /// </remarks>
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

    /// <summary>
    /// Disable MFA for a specified account.
    /// </summary>
    /// <remarks>
    /// Requires authentication. Targets the account by userId and clears its MFA configuration and backup codes
    /// without asking for its password. Returns succeeded=true, or a user-not-found problem if the account does not
    /// exist.
    /// </remarks>
    /// <param name="userId">Account user identifier whose MFA configuration is cleared.</param>
    /// <param name="_">Unused cancellation token.</param>
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

    /// <summary>
    /// Clear password and MFA lockout for an account.
    /// </summary>
    /// <remarks>
    /// Requires authentication. Clears the target account password lockout, failed-password count, and MFA lockout
    /// state. Returns succeeded=true and records the current user as the actor in the lockout-cleared event.
    /// </remarks>
    /// <param name="userId">Account user identifier whose lockout state is cleared.</param>
    /// <param name="ct">Cancels the request.</param>
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
    /// Issue a token for browser MFA enrollment.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user with an email claim. Returns a protected enrollment token valid for 60 seconds.
    /// The authentication app exchanges this token for an MFA partial-auth cookie to continue enrollment.
    /// </remarks>
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
    /// Exchange an enrollment token for an MFA cookie.
    /// </summary>
    /// <remarks>
    /// Available without authentication and intended for the browser enrollment flow. Accepts an unexpired token from
    /// enroll/issue-token and sets a five-minute MFA partial-auth cookie. Returns succeeded=true so the browser can
    /// continue with TOTP enrollment.
    /// </remarks>
    /// <param name="token">Enrollment token returned by enroll/issue-token within the last 60 seconds.</param>
    /// <param name="ct">Cancels the request.</param>
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
