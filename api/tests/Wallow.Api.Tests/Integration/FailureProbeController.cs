using Asp.Versioning;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Domain.Errors;
using Wallow.Inquiries.Domain.Errors;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Provides failure endpoints for ErrorContractTests on a derived test host.
/// </summary>
[ApiController]
[ApiVersion(1)]
[AllowAnonymous]
[Route("v{version:apiVersion}/failure-probe")]
public sealed class FailureProbeController : ControllerBase
{
    public const string ProbePath = "/v1/failure-probe";

    [HttpGet("throw")]
    public IActionResult Throw() =>
        throw new InvalidOperationException("The probe's internal detail must never reach the client.");

    [HttpGet("tenant-required")]
    public IActionResult TenantRequired()
    {
        TenantScope.Require(default);
        return NoContent();
    }

    [HttpGet("failure-result")]
    public IActionResult FailureResult() =>
        Result.Failure(IdentityErrors.MfaUpdateFailed, "Internal MFA persistence details.").ToActionResult();

    [HttpGet("business-rule")]
    public IActionResult BusinessRule() =>
        Result.Failure(InquiriesErrors.InvalidStatusTransition).ToActionResult();

    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ProbeRequest request) => Ok(request);

    [HttpPost("fluent")]
    public IActionResult Fluent() =>
        throw new FluentValidation.ValidationException(
        [
            new ValidationFailure("Branding.DisplayName", "Display name is required."),
            new ValidationFailure("Branding.DisplayName", "Display name must be shorter."),
            new ValidationFailure("Name", "Name is required.")
        ]);

}

public sealed record ProbeRequest(
    [System.ComponentModel.DataAnnotations.Required] string Name,
    ProbeBranding? Branding);

public sealed record ProbeBranding([System.ComponentModel.DataAnnotations.Required] string DisplayName);
