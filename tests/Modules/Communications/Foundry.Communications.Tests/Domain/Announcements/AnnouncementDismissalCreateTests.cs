using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Domain.Announcements;

public class AnnouncementDismissalCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsDismissalWithCorrectProperties()
    {
        AnnouncementId announcementId = AnnouncementId.New();
        UserId userId = UserId.New();
        DateTime before = DateTime.UtcNow;

        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcementId, userId, TimeProvider.System);

        dismissal.AnnouncementId.Should().Be(announcementId);
        dismissal.UserId.Should().Be(userId);
        dismissal.DismissedAt.Should().BeOnOrAfter(before);
        dismissal.DismissedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
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
}
