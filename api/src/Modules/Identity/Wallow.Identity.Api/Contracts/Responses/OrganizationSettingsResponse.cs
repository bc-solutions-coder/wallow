using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Api.Contracts.Responses;

public record OrganizationSettingsResponse(
    bool RequireMfa,
    bool AllowPasswordlessLogin,
    int MfaGracePeriodDays,
    LoginMethod AllowedLoginMethods,
    string? DefaultMemberRole);
