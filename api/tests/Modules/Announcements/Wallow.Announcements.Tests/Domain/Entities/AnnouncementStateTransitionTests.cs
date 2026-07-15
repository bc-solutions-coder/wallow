using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Announcements.Tests.Domain.Entities;

public class AnnouncementStateTransitionTests
{
    private static readonly TenantId _testTenantId = TenantId.New();

    private static Announcement CreateDraftAnnouncement()
    {
        Announcement announcement = Announcement.Create(
            _testTenantId, "Title", "Content", AnnouncementType.Feature, TimeProvider.System);
        announcement.ClearDomainEvents();
        return announcement;
    }

    private static Announcement CreateScheduledAnnouncement()
    {
        Announcement announcement = Announcement.Create(
            _testTenantId, "Title", "Content", AnnouncementType.Feature, TimeProvider.System,
            publishAt: DateTime.UtcNow.AddDays(1));
        announcement.ClearDomainEvents();
        return announcement;
    }

    // --- Publish ---

    [Fact]
    public void Publish_FromDraft_SetsStatusToPublished()
    {
        Announcement announcement = CreateDraftAnnouncement();

        announcement.Publish(TimeProvider.System);

        announcement.Status.Should().Be(AnnouncementStatus.Published);
    }

    [Fact]
    public void Publish_FromScheduled_SetsStatusToPublished()
    {
        Announcement announcement = CreateScheduledAnnouncement();

        announcement.Publish(TimeProvider.System);

        announcement.Status.Should().Be(AnnouncementStatus.Published);
    }

    [Fact]
    public void Publish_SetsPublishAtToCurrentTime()
    {
        Announcement announcement = CreateDraftAnnouncement();
        DateTime before = DateTime.UtcNow;

        announcement.Publish(TimeProvider.System);

        announcement.PublishAt.Should().NotBeNull();
        announcement.PublishAt.Should().BeOnOrAfter(before);
        announcement.PublishAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Publish_SetsUpdatedTimestamp()
    {
        Announcement announcement = CreateDraftAnnouncement();
        DateTime before = DateTime.UtcNow;

        announcement.Publish(TimeProvider.System);

        announcement.UpdatedAt.Should().NotBeNull();
        announcement.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_DoesNotChangeState()
    {
        Announcement announcement = CreateDraftAnnouncement();
        announcement.Publish(TimeProvider.System);
        DateTime? originalPublishAt = announcement.PublishAt;
        DateTime? originalUpdatedAt = announcement.UpdatedAt;

        announcement.Publish(TimeProvider.System);

        announcement.Status.Should().Be(AnnouncementStatus.Published);
        announcement.PublishAt.Should().Be(originalPublishAt);
        announcement.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    // --- Expire ---

    [Fact]
    public void Expire_SetsStatusToExpired()
    {
        Announcement announcement = CreateDraftAnnouncement();
        announcement.Publish(TimeProvider.System);

        announcement.Expire(TimeProvider.System);

        announcement.Status.Should().Be(AnnouncementStatus.Expired);
    }

    [Fact]
    public void Expire_SetsUpdatedTimestamp()
    {
        Announcement announcement = CreateDraftAnnouncement();
        DateTime before = DateTime.UtcNow;

        announcement.Expire(TimeProvider.System);

        announcement.UpdatedAt.Should().NotBeNull();
        announcement.UpdatedAt.Should().BeOnOrAfter(before);
    }

    // --- Archive ---

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        Announcement announcement = CreateDraftAnnouncement();
        announcement.Publish(TimeProvider.System);

        announcement.Archive(TimeProvider.System);

        announcement.Status.Should().Be(AnnouncementStatus.Archived);
    }

    [Fact]
    public void Archive_SetsUpdatedTimestamp()
    {
        Announcement announcement = CreateDraftAnnouncement();
        DateTime before = DateTime.UtcNow;

        announcement.Archive(TimeProvider.System);

        announcement.UpdatedAt.Should().NotBeNull();
        announcement.UpdatedAt.Should().BeOnOrAfter(before);
    }

    // --- Update ---

    [Fact]
    public void Update_WithValidData_UpdatesAllProperties()
    {
        Announcement announcement = CreateDraftAnnouncement();
        DateTime newExpiry = DateTime.UtcNow.AddDays(30);

        announcement.Update(
            "New Title", "New Content", AnnouncementType.Alert,
            AnnouncementTarget.Role, "admin", null, newExpiry,
            true, false, "https://new.url", "Click", "https://img.url",
            TimeProvider.System);

        announcement.Title.Should().Be("New Title");
        announcement.Content.Should().Be("New Content");
        announcement.Type.Should().Be(AnnouncementType.Alert);
        announcement.Target.Should().Be(AnnouncementTarget.Role);
        announcement.TargetValue.Should().Be("admin");
        announcement.ExpiresAt.Should().Be(newExpiry);
        announcement.IsPinned.Should().BeTrue();
        announcement.IsDismissible.Should().BeFalse();
        announcement.ActionUrl.Should().Be("https://new.url");
        announcement.ActionLabel.Should().Be("Click");
        announcement.ImageUrl.Should().Be("https://img.url");
    }

    [Fact]
    public void Update_SetsUpdatedTimestamp()
    {
        Announcement announcement = CreateDraftAnnouncement();
        DateTime before = DateTime.UtcNow;

        announcement.Update(
            "New Title", "New Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null,
            false, true, null, null, null, TimeProvider.System);

        announcement.UpdatedAt.Should().NotBeNull();
        announcement.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidTitle_ThrowsArgumentException(string? title)
    {
        Announcement announcement = CreateDraftAnnouncement();

        Action act = () => announcement.Update(
            title!, "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null,
            false, true, null, null, null, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidContent_ThrowsArgumentException(string? content)
    {
        Announcement announcement = CreateDraftAnnouncement();

        Action act = () => announcement.Update(
            "Title", content!, AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null,
            false, true, null, null, null, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }
}
