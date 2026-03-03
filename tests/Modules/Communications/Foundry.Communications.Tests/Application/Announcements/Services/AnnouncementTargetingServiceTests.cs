using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Application.Announcements.Services;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Application.Announcements.Services;

public class AnnouncementTargetingServiceTests
{
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IAnnouncementDismissalRepository _dismissalRepository;
    private readonly AnnouncementTargetingService _service;

    public AnnouncementTargetingServiceTests()
    {
        _announcementRepository = Substitute.For<IAnnouncementRepository>();
        _dismissalRepository = Substitute.For<IAnnouncementDismissalRepository>();
        _service = new AnnouncementTargetingService(_announcementRepository, _dismissalRepository, TimeProvider.System);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ReturnsPublishedNonExpiredAnnouncements()
    {
        Announcement published = CreatePublishedAnnouncement("Published", AnnouncementTarget.All);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { published });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = CreateUserContext();

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Published");
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ExcludesExpiredAnnouncements()
    {
        Announcement expired = CreatePublishedAnnouncement("Expired", AnnouncementTarget.All, expiresAt: DateTime.UtcNow.AddDays(-1));

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { expired });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = CreateUserContext();

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ExcludesDismissedAnnouncements()
    {
        Announcement announcement = CreatePublishedAnnouncement("Dismissible", AnnouncementTarget.All, isDismissible: true);

        UserId userId = UserId.Create(Guid.NewGuid());
        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcement.Id, userId, TimeProvider.System);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal> { dismissal });

        UserContext userContext = new(userId, TenantId.Create(Guid.NewGuid()), null, new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_IncludesNonDismissibleEvenIfDismissed()
    {
        Announcement announcement = CreatePublishedAnnouncement("Non-Dismissible", AnnouncementTarget.All, isDismissible: false);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = CreateUserContext();

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_FiltersByTenantTarget()
    {
        Guid tenantId = Guid.NewGuid();
        Announcement tenantAnnouncement = CreatePublishedAnnouncement(
            "For Tenant", AnnouncementTarget.Tenant, targetValue: tenantId.ToString());

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { tenantAnnouncement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext matchingContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(tenantId),
            null,
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(matchingContext);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ExcludesNonMatchingTenantTarget()
    {
        Announcement tenantAnnouncement = CreatePublishedAnnouncement(
            "For Other Tenant", AnnouncementTarget.Tenant, targetValue: Guid.NewGuid().ToString());

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { tenantAnnouncement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = CreateUserContext();

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_FiltersByPlanTarget()
    {
        Announcement planAnnouncement = CreatePublishedAnnouncement(
            "For Pro Plan", AnnouncementTarget.Plan, targetValue: "Pro");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { planAnnouncement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext matchingContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            "Pro",
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(matchingContext);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ExcludesNonMatchingPlanTarget()
    {
        Announcement planAnnouncement = CreatePublishedAnnouncement(
            "For Enterprise", AnnouncementTarget.Plan, targetValue: "Enterprise");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { planAnnouncement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            "Free",
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_FiltersByRoleTarget()
    {
        Announcement roleAnnouncement = CreatePublishedAnnouncement(
            "For Admins", AnnouncementTarget.Role, targetValue: "Admin");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { roleAnnouncement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext matchingContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string> { "Admin", "User" });

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(matchingContext);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ExcludesNonMatchingRoleTarget()
    {
        Announcement roleAnnouncement = CreatePublishedAnnouncement(
            "For Admins", AnnouncementTarget.Role, targetValue: "Admin");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { roleAnnouncement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string> { "User" });

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_OrdersPinnedFirst()
    {
        Announcement pinned = CreatePublishedAnnouncement("Pinned", AnnouncementTarget.All, isPinned: true);
        Announcement notPinned = CreatePublishedAnnouncement("Not Pinned", AnnouncementTarget.All, isPinned: false);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { notPinned, pinned });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = CreateUserContext();

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Pinned");
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithEmptyTargetValue_ExcludesTenantTarget()
    {
        Announcement announcement = CreatePublishedAnnouncement(
            "No Target Value", AnnouncementTarget.Tenant, targetValue: null);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = CreateUserContext();

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    private static readonly TenantId _testTenantId = TenantId.New();

    private static Announcement CreatePublishedAnnouncement(
        string title,
        AnnouncementTarget target,
        string? targetValue = null,
        DateTime? expiresAt = null,
        bool isPinned = false,
        bool isDismissible = true)
    {
        Announcement announcement = Announcement.Create(_testTenantId, title, "Content", AnnouncementType.Feature, TimeProvider.System, target, targetValue, null, expiresAt, isPinned, isDismissible);
        announcement.Publish(TimeProvider.System);
        return announcement;
    }

    private static UserContext CreateUserContext()
    {
        return new UserContext(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string>());
    }
}
