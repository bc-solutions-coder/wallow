using Wallow.Shared.Kernel.Errors;

namespace Wallow.Announcements.Domain.Errors;

/// <summary>
/// The error catalog the Announcements module owns. Registered by <c>AddAnnouncementsModule</c>.
/// </summary>
public static class AnnouncementsErrors
{
    public static readonly ErrorCatalogEntry AnnouncementNotFound = new(
        "Announcement.NotFound", ErrorKind.NotFound, "Announcement not found");

    public static readonly ErrorCatalogEntry AnnouncementNotDismissible = new(
        "Announcement.NotDismissible", ErrorKind.BusinessRule, "This announcement cannot be dismissed");

    public static readonly ErrorCatalogEntry ChangelogNotFound = new(
        "Changelog.NotFound", ErrorKind.NotFound, "Changelog entry not found");
}
