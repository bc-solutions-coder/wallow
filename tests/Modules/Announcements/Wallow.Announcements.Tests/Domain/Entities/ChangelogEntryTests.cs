using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Announcements.Domain.Changelogs.Enums;
using Wallow.Announcements.Domain.Changelogs.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Announcements.Tests.Domain.Entities;

public class ChangelogEntryCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsUnpublishedEntry()
    {
        string version = "1.0.0";
        string title = "Initial Release";
        string content = "First release of the platform.";
        DateTime releasedAt = DateTime.UtcNow;

        ChangelogEntry entry = ChangelogEntry.Create(version, title, content, releasedAt, TimeProvider.System);

        entry.Version.Should().Be(version);
        entry.Title.Should().Be(title);
        entry.Content.Should().Be(content);
        entry.ReleasedAt.Should().Be(releasedAt);
        entry.IsPublished.Should().BeFalse();
        entry.Items.Should().BeEmpty();
    }

    [Fact]
    public void Create_SetsCreatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        entry.CreatedAt.Should().BeOnOrAfter(before);
        entry.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        ChangelogEntry first = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);
        ChangelogEntry second = ChangelogEntry.Create("1.0.1", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        first.Id.Should().NotBe(second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidVersion_ThrowsArgumentException(string? version)
    {
        Action act = () => ChangelogEntry.Create(version!, "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTitle_ThrowsArgumentException(string? title)
    {
        Action act = () => ChangelogEntry.Create("1.0.0", title!, "Content", DateTime.UtcNow, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidContent_ThrowsArgumentException(string? content)
    {
        Action act = () => ChangelogEntry.Create("1.0.0", "Title", content!, DateTime.UtcNow, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }
}

public class ChangelogEntryPublishTests
{
    [Fact]
    public void Publish_SetsIsPublishedTrue()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        entry.Publish(TimeProvider.System);

        entry.IsPublished.Should().BeTrue();
    }

    [Fact]
    public void Publish_SetsUpdatedTimestamp()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);
        DateTime before = DateTime.UtcNow;

        entry.Publish(TimeProvider.System);

        entry.UpdatedAt.Should().NotBeNull();
        entry.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Unpublish_SetsIsPublishedFalse()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.Publish(TimeProvider.System);

        entry.Unpublish(TimeProvider.System);

        entry.IsPublished.Should().BeFalse();
    }
}

public class ChangelogEntryUpdateTests
{
    [Fact]
    public void Update_WithValidData_UpdatesAllProperties()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);
        DateTime newDate = DateTime.UtcNow.AddDays(1);

        entry.Update("2.0.0", "New Title", "New Content", newDate, TimeProvider.System);

        entry.Version.Should().Be("2.0.0");
        entry.Title.Should().Be("New Title");
        entry.Content.Should().Be("New Content");
        entry.ReleasedAt.Should().Be(newDate);
    }

    [Fact]
    public void Update_SetsUpdatedTimestamp()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);
        DateTime before = DateTime.UtcNow;

        entry.Update("1.0.1", "New Title", "New Content", DateTime.UtcNow, TimeProvider.System);

        entry.UpdatedAt.Should().NotBeNull();
        entry.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidVersion_ThrowsArgumentException(string? version)
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        Action act = () => entry.Update(version!, "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }
}

public class ChangelogEntryItemTests
{
    [Fact]
    public void AddItem_AddsItemToCollection()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        entry.AddItem("Fixed a bug", ChangeType.Fix, TimeProvider.System);

        entry.Items.Should().HaveCount(1);
        entry.Items[0].Description.Should().Be("Fixed a bug");
        entry.Items[0].Type.Should().Be(ChangeType.Fix);
    }

    [Fact]
    public void AddItem_ReturnsCreatedItem()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        ChangelogItem item = entry.AddItem("New feature added", ChangeType.Feature, TimeProvider.System);

        item.Should().NotBeNull();
        item.Description.Should().Be("New feature added");
        item.Type.Should().Be(ChangeType.Feature);
        item.EntryId.Should().Be(entry.Id);
    }

    [Fact]
    public void AddItem_SetsUpdatedTimestamp()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);
        DateTime before = DateTime.UtcNow;

        entry.AddItem("Fix", ChangeType.Fix, TimeProvider.System);

        entry.UpdatedAt.Should().NotBeNull();
        entry.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void RemoveItem_RemovesItemFromCollection()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);
        ChangelogItem item = entry.AddItem("Item to remove", ChangeType.Fix, TimeProvider.System);

        entry.RemoveItem(item.Id, TimeProvider.System);

        entry.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_WhenItemNotFound_DoesNotThrow()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        Action act = () => entry.RemoveItem(ChangelogItemId.New(), TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddMultipleItems_AllAddedToCollection()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Title", "Content", DateTime.UtcNow, TimeProvider.System);

        entry.AddItem("Feature 1", ChangeType.Feature, TimeProvider.System);
        entry.AddItem("Fix 1", ChangeType.Fix, TimeProvider.System);
        entry.AddItem("Breaking change", ChangeType.Breaking, TimeProvider.System);

        entry.Items.Should().HaveCount(3);
    }
}

public class ChangelogItemCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsItem()
    {
        ChangelogEntryId entryId = ChangelogEntryId.New();
        string description = "Added new login feature";
        ChangeType type = ChangeType.Feature;

        ChangelogItem item = ChangelogItem.Create(entryId, description, type);

        item.EntryId.Should().Be(entryId);
        item.Description.Should().Be(description);
        item.Type.Should().Be(type);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        ChangelogEntryId entryId = ChangelogEntryId.New();

        ChangelogItem first = ChangelogItem.Create(entryId, "Description 1", ChangeType.Fix);
        ChangelogItem second = ChangelogItem.Create(entryId, "Description 2", ChangeType.Fix);

        first.Id.Should().NotBe(second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        Action act = () => ChangelogItem.Create(ChangelogEntryId.New(), description!, ChangeType.Fix);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_WithValidData_UpdatesProperties()
    {
        ChangelogItem item = ChangelogItem.Create(ChangelogEntryId.New(), "Original", ChangeType.Fix);

        item.Update("Updated description", ChangeType.Feature);

        item.Description.Should().Be("Updated description");
        item.Type.Should().Be(ChangeType.Feature);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        ChangelogItem item = ChangelogItem.Create(ChangelogEntryId.New(), "Valid", ChangeType.Fix);

        Action act = () => item.Update(description!, ChangeType.Feature);

        act.Should().Throw<ArgumentException>();
    }
}

public class AnnouncementDismissalCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsDismissal()
    {
        AnnouncementId announcementId = AnnouncementId.New();
        UserId userId = UserId.New();

        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcementId, userId, TimeProvider.System);

        dismissal.AnnouncementId.Should().Be(announcementId);
        dismissal.UserId.Should().Be(userId);
        dismissal.DismissedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        AnnouncementId announcementId = AnnouncementId.New();
        UserId userId = UserId.New();

        AnnouncementDismissal first = AnnouncementDismissal.Create(announcementId, userId, TimeProvider.System);
        AnnouncementDismissal second = AnnouncementDismissal.Create(announcementId, userId, TimeProvider.System);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_SetsDismissedAtToCurrentTime()
    {
        DateTime before = DateTime.UtcNow;

        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(
            AnnouncementId.New(), UserId.New(), TimeProvider.System);

        dismissal.DismissedAt.Should().BeOnOrAfter(before);
        dismissal.DismissedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }
}
