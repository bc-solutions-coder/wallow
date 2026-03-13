using Foundry.Announcements.Application.Announcements.DTOs;
using Foundry.Announcements.Application.Announcements.Interfaces;
using Foundry.Announcements.Application.Announcements.Services;
using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Announcements.Tests.Application.Services;

public class AnnouncementTargetingServiceTests
{
    private readonly IAnnouncementRepository _announcementRepository = Substitute.For<IAnnouncementRepository>();
    private readonly IAnnouncementDismissalRepository _dismissalRepository = Substitute.For<IAnnouncementDismissalRepository>();
    private readonly AnnouncementTargetingService _service;
    private static readonly TenantId _testTenantId = TenantId.New();

    public AnnouncementTargetingServiceTests()
    {
        _service = new AnnouncementTargetingService(_announcementRepository, _dismissalRepository, TimeProvider.System);
    }

    private static Announcement CreatePublishedAnnouncement(
        AnnouncementTarget target = AnnouncementTarget.All,
        string? targetValue = null,
        DateTime? expiresAt = null,
        bool isPinned = false,
        bool isDismissible = true)
    {
        Announcement announcement = Announcement.Create(
            _testTenantId, "Test Title", "Test Content", AnnouncementType.Feature, TimeProvider.System,
            target: target, targetValue: targetValue, expiresAt: expiresAt,
            isPinned: isPinned, isDismissible: isDismissible);
        announcement.Publish(TimeProvider.System);
        announcement.ClearDomainEvents();
        return announcement;
    }

    private static UserContext CreateUserContext(
        TenantId? tenantId = null,
        string? planName = null,
        IReadOnlyList<string>? roles = null)
    {
        return new UserContext(
            UserId.New(),
            tenantId ?? _testTenantId,
            planName,
            roles ?? []);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ReturnsPublishedAnnouncements()
    {
        Announcement published = CreatePublishedAnnouncement();
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { published });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(CreateUserContext());

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Test Title");
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ExcludesExpiredAnnouncements()
    {
        Announcement expired = CreatePublishedAnnouncement(expiresAt: DateTime.UtcNow.AddDays(-1));
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { expired });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(CreateUserContext());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_ExcludesDismissedDismissibleAnnouncements()
    {
        Announcement dismissible = CreatePublishedAnnouncement(isDismissible: true);
        UserId userId = UserId.New();
        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(dismissible.Id, userId, TimeProvider.System);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { dismissible });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal> { dismissal });

        UserContext userContext = new(userId, _testTenantId, null, []);
        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_DoesNotExcludeNonDismissibleDismissedAnnouncements()
    {
        Announcement nonDismissible = CreatePublishedAnnouncement(isDismissible: false);
        UserId userId = UserId.New();
        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(nonDismissible.Id, userId, TimeProvider.System);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { nonDismissible });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal> { dismissal });

        UserContext userContext = new(userId, _testTenantId, null, []);
        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetAll_ReturnsForAllUsers()
    {
        Announcement allTarget = CreatePublishedAnnouncement(target: AnnouncementTarget.All);
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { allTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(tenantId: TenantId.New()));

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetTenant_MatchesByTenantId()
    {
        TenantId tenantId = TenantId.New();
        Announcement tenantTarget = CreatePublishedAnnouncement(
            target: AnnouncementTarget.Tenant, targetValue: tenantId.Value.ToString());
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { tenantTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(tenantId: tenantId));

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetTenant_ExcludesDifferentTenant()
    {
        TenantId targetTenantId = TenantId.New();
        Announcement tenantTarget = CreatePublishedAnnouncement(
            target: AnnouncementTarget.Tenant, targetValue: targetTenantId.Value.ToString());
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { tenantTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(tenantId: TenantId.New()));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetPlan_MatchesByPlanName()
    {
        Announcement planTarget = CreatePublishedAnnouncement(
            target: AnnouncementTarget.Plan, targetValue: "pro");
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { planTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(planName: "pro"));

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetPlan_IsCaseInsensitive()
    {
        Announcement planTarget = CreatePublishedAnnouncement(
            target: AnnouncementTarget.Plan, targetValue: "Pro");
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { planTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(planName: "pro"));

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetPlan_ExcludesDifferentPlan()
    {
        Announcement planTarget = CreatePublishedAnnouncement(
            target: AnnouncementTarget.Plan, targetValue: "enterprise");
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { planTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(planName: "pro"));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetRole_MatchesByRole()
    {
        Announcement roleTarget = CreatePublishedAnnouncement(
            target: AnnouncementTarget.Role, targetValue: "admin");
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { roleTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(roles: ["admin", "user"]));

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_TargetRole_ExcludesMissingRole()
    {
        Announcement roleTarget = CreatePublishedAnnouncement(
            target: AnnouncementTarget.Role, targetValue: "superadmin");
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { roleTarget });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(
            CreateUserContext(roles: ["user"]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_OrdersPinnedFirst()
    {
        Announcement unpinned = CreatePublishedAnnouncement(isPinned: false);
        Announcement pinned = CreatePublishedAnnouncement(isPinned: true);
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { unpinned, pinned });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(CreateUserContext());

        result[0].IsPinned.Should().BeTrue();
        result[1].IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveTargetUsersAsync_ReturnsEmptyList()
    {
        Announcement announcement = CreatePublishedAnnouncement();

        IReadOnlyList<Guid> result = await _service.ResolveTargetUsersAsync(announcement);

        result.Should().BeEmpty();
    }
}
