using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Domain.Announcements;

public class AnnouncementCreateTests
{
    private static readonly TenantId _testTenantId = TenantId.New();

    [Fact]
    public void Create_WithRequiredFields_ReturnsAnnouncementInDraftStatus()
    {
        Announcement announcement = Announcement.Create(_testTenantId, "New Feature", "We released a new feature", AnnouncementType.Feature, TimeProvider.System);

        announcement.Title.Should().Be("New Feature");
        announcement.Content.Should().Be("We released a new feature");
        announcement.Type.Should().Be(AnnouncementType.Feature);
        announcement.Target.Should().Be(AnnouncementTarget.All);
        announcement.Status.Should().Be(AnnouncementStatus.Draft);
        announcement.IsPinned.Should().BeFalse();
        announcement.IsDismissible.Should().BeTrue();
        announcement.TargetValue.Should().BeNull();
        announcement.PublishAt.Should().BeNull();
        announcement.ExpiresAt.Should().BeNull();
        announcement.ActionUrl.Should().BeNull();
        announcement.ActionLabel.Should().BeNull();
        announcement.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void Create_WithPublishAt_ReturnsAnnouncementInScheduledStatus()
    {
        DateTime publishAt = DateTime.UtcNow.AddDays(1);

        Announcement announcement = Announcement.Create(_testTenantId, "Upcoming Feature", "Coming soon", AnnouncementType.Feature, TimeProvider.System, publishAt: publishAt);

        announcement.Status.Should().Be(AnnouncementStatus.Scheduled);
        announcement.PublishAt.Should().Be(publishAt);
    }

    [Fact]
    public void Create_WithAllOptionalFields_SetsAllProperties()
    {
        DateTime publishAt = DateTime.UtcNow.AddDays(1);
        DateTime expiresAt = DateTime.UtcNow.AddDays(30);

        Announcement announcement = Announcement.Create(_testTenantId, "Maintenance Window", "Scheduled maintenance", AnnouncementType.Maintenance, TimeProvider.System, AnnouncementTarget.Tenant, "tenant-123", publishAt, expiresAt, isPinned: true, isDismissible: false, actionUrl: "https://example.com", actionLabel: "Learn More", imageUrl: "https://example.com/img.png");

        announcement.Target.Should().Be(AnnouncementTarget.Tenant);
        announcement.TargetValue.Should().Be("tenant-123");
        announcement.ExpiresAt.Should().Be(expiresAt);
        announcement.IsPinned.Should().BeTrue();
        announcement.IsDismissible.Should().BeFalse();
        announcement.ActionUrl.Should().Be("https://example.com");
        announcement.ActionLabel.Should().Be("Learn More");
        announcement.ImageUrl.Should().Be("https://example.com/img.png");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTitle_ThrowsArgumentException(string? title)
    {
        Action act = () => Announcement.Create(_testTenantId, title!, "Content", AnnouncementType.Feature, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidContent_ThrowsArgumentException(string? content)
    {
        Action act = () => Announcement.Create(_testTenantId, "Title", content!, AnnouncementType.Feature, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_SetsCreatedAtTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        Announcement announcement = Announcement.Create(_testTenantId, "Test", "Content", AnnouncementType.Feature, TimeProvider.System);

        announcement.CreatedAt.Should().BeOnOrAfter(before);
        announcement.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        Announcement first = Announcement.Create(_testTenantId, "Test1", "Content1", AnnouncementType.Feature, TimeProvider.System);
        Announcement second = Announcement.Create(_testTenantId, "Test2", "Content2", AnnouncementType.Update, TimeProvider.System);

        first.Id.Should().NotBe(second.Id);
    }
}
