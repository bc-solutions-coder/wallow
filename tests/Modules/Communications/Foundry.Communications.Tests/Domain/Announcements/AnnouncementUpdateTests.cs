using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;

namespace Foundry.Communications.Tests.Domain.Announcements;

public class AnnouncementUpdateTests
{
    [Fact]
    public void Update_WithValidData_UpdatesAllProperties()
    {
        Announcement announcement = Announcement.Create("Original Title", "Original Content", AnnouncementType.Feature, TimeProvider.System);
        DateTime expiresAt = DateTime.UtcNow.AddDays(30);

        announcement.Update(
            "Updated Title",
            "Updated Content",
            AnnouncementType.Maintenance,
            AnnouncementTarget.Tenant,
            "tenant-456",
            null,
            expiresAt,
            true,
            false,
            "https://updated.com",
            "Click Here",
            "https://updated.com/img.png",
            TimeProvider.System);

        announcement.Title.Should().Be("Updated Title");
        announcement.Content.Should().Be("Updated Content");
        announcement.Type.Should().Be(AnnouncementType.Maintenance);
        announcement.Target.Should().Be(AnnouncementTarget.Tenant);
        announcement.TargetValue.Should().Be("tenant-456");
        announcement.PublishAt.Should().BeNull();
        announcement.ExpiresAt.Should().Be(expiresAt);
        announcement.IsPinned.Should().BeTrue();
        announcement.IsDismissible.Should().BeFalse();
        announcement.ActionUrl.Should().Be("https://updated.com");
        announcement.ActionLabel.Should().Be("Click Here");
        announcement.ImageUrl.Should().Be("https://updated.com/img.png");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidTitle_ThrowsArgumentException(string? title)
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        Action act = () => announcement.Update(
            title!, "Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidContent_ThrowsArgumentException(string? content)
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        Action act = () => announcement.Update(
            "Title", content!, AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_SetsUpdatedAtTimestamp()
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);
        DateTime before = DateTime.UtcNow;

        announcement.Update(
            "New Title", "New Content", AnnouncementType.Feature,
            AnnouncementTarget.All, null, null, null, false, true, null, null, null, TimeProvider.System);

        announcement.UpdatedAt.Should().NotBeNull();
        announcement.UpdatedAt.Should().BeOnOrAfter(before);
    }
}
