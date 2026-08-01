using System.ComponentModel.DataAnnotations;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Who may join an organization and on what terms. Separate from
/// <see cref="UpdateOrganizationSettingsRequest"/> because these fields are gated on the right to
/// manage members, not the right to edit settings.
/// </summary>
public record UpdateOrganizationEnrollmentRequest(
    [Required] EnrollmentPolicy EnrollmentPolicy,
    [EmailAddress][MaxLength(256)] string? AccessRequestEmail,
    Guid? DefaultRoleId);
