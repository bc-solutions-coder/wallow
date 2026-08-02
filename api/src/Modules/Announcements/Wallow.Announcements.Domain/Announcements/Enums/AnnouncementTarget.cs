namespace Wallow.Announcements.Domain.Announcements.Enums;

public enum AnnouncementTarget
{
    All = 0,
    Tenant = 1,
    // 2 was Plan (removed): nothing issues a subscription plan, so the branch matched nobody.
    // Billing reintroduces it once a plan exists to target.
    Role = 3
}
