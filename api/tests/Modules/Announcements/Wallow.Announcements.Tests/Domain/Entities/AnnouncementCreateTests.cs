using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Announcements.Tests.Domain.Entities;

public class AnnouncementCreateTests
{
    private static readonly TenantId _testTenantId = TenantId.New();

    [Fact]
    public void Create_WithValidData_ReturnsAnnouncementWithDraftStatus()
    {
        string title = "System Maintenance";
        string content = "Scheduled downtime this weekend.";
        AnnouncementType type = AnnouncementType.Maintenance;

        Announcement announcement = Announcement.Create(
            _testTenantId, title, content, type, TimeProvider.System);

        announcement.Title.Should().Be(title);
        announcement.Content.Should().Be(content);
        announcement.Type.Should().Be(type);
        announcement.Status.Should().Be(AnnouncementStatus.Draft);
        announcement.Target.Should().Be(AnnouncementTarget.All);
        announcement.IsPinned.Should().BeFalse();
        announcement.IsDismissible.Should().BeTrue();
    }

    [Fact]
    public void Create_WithPublishAt_ReturnsAnnouncementWithScheduledStatus()
    {
        DateTime publishAt = DateTime.UtcNow.AddDays(1);

        Announcement announcement = Announcement.Create(
            _testTenantId, "Title", "Content", AnnouncementType.Feature, TimeProvider.System,
            publishAt: publishAt);

        announcement.Status.Should().Be(AnnouncementStatus.Scheduled);
        announcement.PublishAt.Should().Be(publishAt);
    }

    [Fact]
    public void Create_WithAllOptionalParameters_SetsAllProperties()
    {
        DateTime publishAt = DateTime.UtcNow.AddDays(1);
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        Announcement announcement = Announcement.Create(
            _testTenantId, "Title", "Content", AnnouncementType.Alert, TimeProvider.System,
            target: AnnouncementTarget.Tenant,
            targetValue: "tenant-123",
            publishAt: publishAt,
            expiresAt: expiresAt,
            isPinned: true,
            isDismissible: false,
            actionUrl: "https://example.com",
            actionLabel: "Learn More",
            imageUrl: "https://example.com/image.png");

        announcement.Target.Should().Be(AnnouncementTarget.Tenant);
        announcement.TargetValue.Should().Be("tenant-123");
        announcement.PublishAt.Should().Be(publishAt);
        announcement.ExpiresAt.Should().Be(expiresAt);
        announcement.IsPinned.Should().BeTrue();
        announcement.IsDismissible.Should().BeFalse();
        announcement.ActionUrl.Should().Be("https://example.com");
        announcement.ActionLabel.Should().Be("Learn More");
        announcement.ImageUrl.Should().Be("https://example.com/image.png");
    }

    [Fact]
    public void Create_SetsCreatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        Announcement announcement = Announcement.Create(
            _testTenantId, "Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        announcement.CreatedAt.Should().BeOnOrAfter(before);
        announcement.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        Announcement first = Announcement.Create(
            _testTenantId, "Title", "Content", AnnouncementType.Feature, TimeProvider.System);
        Announcement second = Announcement.Create(
            _testTenantId, "Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_SetsTenantId()
    {
        TenantId tenantId = TenantId.New();

        Announcement announcement = Announcement.Create(
            tenantId, "Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        announcement.TenantId.Should().Be(tenantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTitle_ThrowsArgumentException(string? title)
    {
        Action act = () => Announcement.Create(
            _testTenantId, title!, "Content", AnnouncementType.Feature, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidContent_ThrowsArgumentException(string? content)
    {
        Action act = () => Announcement.Create(
            _testTenantId, "Title", content!, AnnouncementType.Feature, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }
}
