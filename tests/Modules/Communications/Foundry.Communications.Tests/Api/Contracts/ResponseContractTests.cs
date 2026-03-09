using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Contracts.Email.Enums;
using Foundry.Communications.Api.Contracts.Email.Responses;
using Foundry.Communications.Api.Contracts.InApp.Responses;

namespace Foundry.Communications.Tests.Api.Contracts;

public class ResponseContractTests
{
    #region NotificationResponse

    [Fact]
    public void NotificationResponse_CreatesWithAllFields()
    {
        Guid id = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;
        DateTime readAt = DateTime.UtcNow.AddMinutes(-1);

        NotificationResponse response = new(id, userId, "TaskAssigned", "Title", "Message", true, readAt, createdAt, createdAt);

        response.Id.Should().Be(id);
        response.UserId.Should().Be(userId);
        response.Type.Should().Be("TaskAssigned");
        response.Title.Should().Be("Title");
        response.Message.Should().Be("Message");
        response.IsRead.Should().BeTrue();
        response.ReadAt.Should().Be(readAt);
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void NotificationResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        NotificationResponse response = new(Guid.NewGuid(), Guid.NewGuid(), "Type", "Title", "Msg", false, null, DateTime.UtcNow, null);

        response.ReadAt.Should().BeNull();
        response.UpdatedAt.Should().BeNull();
    }

    #endregion

    #region PagedNotificationResponse

    [Fact]
    public void PagedNotificationResponse_CreatesWithAllFields()
    {
        List<NotificationResponse> items =
        [
            new(Guid.NewGuid(), Guid.NewGuid(), "Type", "Title", "Msg", false, null, DateTime.UtcNow, null)
        ];

        PagedNotificationResponse response = new(items, 100, 1, 20, 5, false, true);

        response.Items.Should().HaveCount(1);
        response.TotalCount.Should().Be(100);
        response.PageNumber.Should().Be(1);
        response.PageSize.Should().Be(20);
        response.TotalPages.Should().Be(5);
        response.HasPreviousPage.Should().BeFalse();
        response.HasNextPage.Should().BeTrue();
    }

    #endregion

    #region UnreadCountResponse

    [Fact]
    public void UnreadCountResponse_CreatesWithCount()
    {
        UnreadCountResponse response = new(42);

        response.Count.Should().Be(42);
    }

    #endregion

    #region AnnouncementResponse

    [Fact]
    public void AnnouncementResponse_CreatesWithAllFields()
    {
        Guid id = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;

        AnnouncementResponse response = new(id, "Title", "Content", "Feature", true, false,
            "https://example.com", "Click", "https://img.example.com/img.png", createdAt);

        response.Id.Should().Be(id);
        response.Title.Should().Be("Title");
        response.Content.Should().Be("Content");
        response.Type.Should().Be("Feature");
        response.IsPinned.Should().BeTrue();
        response.IsDismissible.Should().BeFalse();
        response.ActionUrl.Should().Be("https://example.com");
        response.ActionLabel.Should().Be("Click");
        response.ImageUrl.Should().Be("https://img.example.com/img.png");
        response.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void AnnouncementResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        AnnouncementResponse response = new(Guid.NewGuid(), "Title", "Content", "Feature", false, true,
            null, null, null, DateTime.UtcNow);

        response.ActionUrl.Should().BeNull();
        response.ActionLabel.Should().BeNull();
        response.ImageUrl.Should().BeNull();
    }

    #endregion

    #region ChangelogEntryResponse

    [Fact]
    public void ChangelogEntryResponse_CreatesWithAllFields()
    {
        Guid id = Guid.NewGuid();
        DateTime releasedAt = DateTime.UtcNow;
        List<ChangelogItemResponse> items =
        [
            new(Guid.NewGuid(), "Added feature", "Feature")
        ];

        ChangelogEntryResponse response = new(id, "1.0.0", "Release 1.0", "Content", releasedAt, items);

        response.Id.Should().Be(id);
        response.Version.Should().Be("1.0.0");
        response.Title.Should().Be("Release 1.0");
        response.Content.Should().Be("Content");
        response.ReleasedAt.Should().Be(releasedAt);
        response.Items.Should().HaveCount(1);
    }

    [Fact]
    public void ChangelogEntryResponse_WithEmptyItems_CreatesCorrectly()
    {
        ChangelogEntryResponse response = new(Guid.NewGuid(), "1.0.0", "Release", "Content", DateTime.UtcNow, []);

        response.Items.Should().BeEmpty();
    }

    #endregion

    #region ChangelogItemResponse

    [Fact]
    public void ChangelogItemResponse_CreatesWithAllFields()
    {
        Guid id = Guid.NewGuid();

        ChangelogItemResponse response = new(id, "Fixed bug", "Fix");

        response.Id.Should().Be(id);
        response.Description.Should().Be("Fixed bug");
        response.Type.Should().Be("Fix");
    }

    #endregion

    #region EmailPreferenceResponse

    [Fact]
    public void EmailPreferenceResponse_CreatesWithAllFields()
    {
        Guid id = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;
        DateTime updatedAt = DateTime.UtcNow.AddMinutes(5);

        EmailPreferenceResponse response = new(id, userId, ApiNotificationType.TaskAssigned, true, createdAt, updatedAt);

        response.Id.Should().Be(id);
        response.UserId.Should().Be(userId);
        response.NotificationType.Should().Be(ApiNotificationType.TaskAssigned);
        response.IsEnabled.Should().BeTrue();
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void EmailPreferenceResponse_WithNullUpdatedAt_CreatesCorrectly()
    {
        EmailPreferenceResponse response = new(Guid.NewGuid(), Guid.NewGuid(), ApiNotificationType.BillingInvoice, false, DateTime.UtcNow, null);

        response.UpdatedAt.Should().BeNull();
    }

    #endregion
}
