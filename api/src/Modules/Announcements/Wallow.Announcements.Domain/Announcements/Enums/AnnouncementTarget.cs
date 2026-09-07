namespace Wallow.Announcements.Domain.Announcements.Enums;

public enum AnnouncementTarget
{
    All = 0,
    Tenant = 1,
    // Value 2 is reserved for the removed Plan target.
    Role = 3
}
