namespace Wallow.Identity.Application.DTOs;

public record OrganizationSettingsDto(
    Guid OrganizationId,
    bool RequireMfa,
    bool AllowPasswordlessLogin,
    int MfaGracePeriodDays);
