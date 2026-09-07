using System.ComponentModel.DataAnnotations;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Enrollment policy, request-email address, and default role. Updating these requires
/// member-management permission; <see cref="UpdateOrganizationSettingsRequest"/> uses settings permission.
/// </summary>
public record UpdateOrganizationEnrollmentRequest(
    [Required] EnrollmentPolicy EnrollmentPolicy,
    [EmailAddress][MaxLength(256)] string? AccessRequestEmail,
    Guid? DefaultRoleId);
