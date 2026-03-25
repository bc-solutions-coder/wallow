using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/mfa")]
[Authorize]
public sealed class MfaController(
    IMfaService mfaService,
    UserManager<WallowUser> userManager) : ControllerBase
{
    [HttpPost("enroll/totp")]
    public async Task<IActionResult> EnrollTotp(CancellationToken ct)
    {
        string userId = GetUserId();

        (string secret, string qrUri) = await mfaService.GenerateEnrollmentSecretAsync(userId, ct);

        return Ok(new { secret, qrUri });
    }

    [HttpPost("enroll/confirm")]
    public async Task<IActionResult> ConfirmEnrollment([FromBody] MfaConfirmRequest request, CancellationToken ct)
    {
        string userId = GetUserId();

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
        string backupCodesHash = string.Join(",", backupCodes);
        user.SetBackupCodes(backupCodesHash);

        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, error = "update_failed" });
        }

        return Ok(new { succeeded = true, backupCodes });
    }

    [HttpPost("challenge")]
    public async Task<IActionResult> IssueChallenge(CancellationToken ct)
    {
        string userId = GetUserId();

        string challengeToken = await mfaService.IssueChallengeAsync(userId, ct);

        return Ok(new { challengeToken });
    }

    [HttpPost("challenge/verify")]
    public async Task<IActionResult> VerifyChallenge([FromBody] MfaVerifyRequest request, CancellationToken ct)
    {
        string userId = GetUserId();

        Result result = await mfaService.ValidateChallengeAsync(userId, request.Code, ct);

        if (result.IsFailure)
        {
            return BadRequest(new { succeeded = false, error = result.Error.Message });
        }

        return Ok(new { succeeded = true });
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User ID claim not found.");
}

public sealed record MfaConfirmRequest(string Secret, string Code);
public sealed record MfaVerifyRequest(string Code);
