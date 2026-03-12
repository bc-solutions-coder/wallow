using Foundry.Communications.Api.Controllers;
using Foundry.Communications.Domain.Announcements.Enums;

namespace Foundry.Communications.Tests.Api.Contracts;

public class RequestContractTests
{
    #region CreateAnnouncementRequest

    [Fact]
    public void CreateAnnouncementRequest_CreatesWithRequiredFields()
    {
        CreateAnnouncementRequest request = new("Title", "Content", AnnouncementType.Feature);

        request.Title.Should().Be("Title");
        request.Content.Should().Be("Content");
        request.Type.Should().Be(AnnouncementType.Feature);
        request.Target.Should().Be(AnnouncementTarget.All);
        request.TargetValue.Should().BeNull();
        request.PublishAt.Should().BeNull();
        request.ExpiresAt.Should().BeNull();
        request.IsPinned.Should().BeFalse();
        request.IsDismissible.Should().BeTrue();
        request.ActionUrl.Should().BeNull();
        request.ActionLabel.Should().BeNull();
        request.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void CreateAnnouncementRequest_CreatesWithAllFields()
    {
        DateTime publishAt = DateTime.UtcNow.AddDays(1);
        DateTime expiresAt = DateTime.UtcNow.AddDays(30);

        CreateAnnouncementRequest request = new("Title", "Content", AnnouncementType.Alert,
            AnnouncementTarget.Plan, "Pro", publishAt, expiresAt, true, false,
            "https://example.com", "Click", "https://img.example.com/image.png");

        request.Target.Should().Be(AnnouncementTarget.Plan);
        request.TargetValue.Should().Be("Pro");
        request.PublishAt.Should().Be(publishAt);
        request.ExpiresAt.Should().Be(expiresAt);
        request.IsPinned.Should().BeTrue();
        request.IsDismissible.Should().BeFalse();
        request.ActionUrl.Should().Be("https://example.com");
        request.ActionLabel.Should().Be("Click");
        request.ImageUrl.Should().Be("https://img.example.com/image.png");
    }

    #endregion

    #region UpdateAnnouncementRequest

    [Fact]
    public void UpdateAnnouncementRequest_CreatesWithAllFields()
    {
        DateTime publishAt = DateTime.UtcNow;
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        UpdateAnnouncementRequest request = new("Updated Title", "Updated Content", AnnouncementType.Maintenance,
            AnnouncementTarget.Tenant, "tenant-1", publishAt, expiresAt, true, false,
            "https://action.com", "Go", "https://img.com/img.png");

        request.Title.Should().Be("Updated Title");
        request.Content.Should().Be("Updated Content");
        request.Type.Should().Be(AnnouncementType.Maintenance);
        request.Target.Should().Be(AnnouncementTarget.Tenant);
        request.TargetValue.Should().Be("tenant-1");
        request.PublishAt.Should().Be(publishAt);
        request.ExpiresAt.Should().Be(expiresAt);
        request.IsPinned.Should().BeTrue();
        request.IsDismissible.Should().BeFalse();
        request.ActionUrl.Should().Be("https://action.com");
        request.ActionLabel.Should().Be("Go");
        request.ImageUrl.Should().Be("https://img.com/img.png");
    }

    #endregion

    #region CreateChangelogEntryRequest

    [Fact]
    public void CreateChangelogEntryRequest_CreatesWithAllFields()
    {
        DateTime releasedAt = DateTime.UtcNow;

        CreateChangelogEntryRequest request = new("1.0.0", "Release 1.0", "Content", releasedAt);

        request.Version.Should().Be("1.0.0");
        request.Title.Should().Be("Release 1.0");
        request.Content.Should().Be("Content");
        request.ReleasedAt.Should().Be(releasedAt);
    }

    #endregion
}
