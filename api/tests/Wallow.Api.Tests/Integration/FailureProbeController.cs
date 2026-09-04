using Asp.Versioning;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wallow.Inquiries.Domain.Errors;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Test-only endpoints that fail in the ways a real action can, so the failure sweep can observe
/// the wire body for a thrown exception, a failed <see cref="Result"/>, an automatic model-state
/// 400, and a FluentValidation failure without depending on any module's business rules. Mounted
/// on a derived host by <see cref="ErrorContractTests"/>; the shared fixture never sees it.
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
