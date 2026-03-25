using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Api.Contracts.Requests;

public record UpdateOrganizationSettingsRequest(
    bool? RequireMfa,
    int? MfaGracePeriodDays,
    LoginMethod? AllowedLoginMethods,
    string? DefaultMemberRole);
