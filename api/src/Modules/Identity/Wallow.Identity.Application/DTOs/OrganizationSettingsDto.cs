using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Application.DTOs;

public record OrganizationSettingsDto(
    Guid OrganizationId,
    bool RequireMfa,
    bool AllowPasswordlessLogin,
    int MfaGracePeriodDays,
    EnrollmentPolicy EnrollmentPolicy,
    string? AccessRequestEmail,
    Guid? DefaultRoleId);
