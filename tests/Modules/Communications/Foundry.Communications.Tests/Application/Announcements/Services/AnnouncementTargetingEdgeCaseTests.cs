using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Application.Announcements.Services;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Application.Announcements.Services;

public class AnnouncementTargetingEdgeCaseTests
{
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IAnnouncementDismissalRepository _dismissalRepository;
    private readonly AnnouncementTargetingService _service;

    public AnnouncementTargetingEdgeCaseTests()
    {
        _announcementRepository = Substitute.For<IAnnouncementRepository>();
        _dismissalRepository = Substitute.For<IAnnouncementDismissalRepository>();
        _service = new AnnouncementTargetingService(_announcementRepository, _dismissalRepository, TimeProvider.System);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithEmptyPlanTargetValue_ExcludesAnnouncement()
    {
        Announcement announcement = CreatePublishedAnnouncement(
            "No Plan Target", AnnouncementTarget.Plan, targetValue: null);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            "Pro",
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithEmptyRoleTargetValue_ExcludesAnnouncement()
    {
        Announcement announcement = CreatePublishedAnnouncement(
            "No Role Target", AnnouncementTarget.Role, targetValue: null);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string> { "Admin" });

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithNullPlanName_ExcludesPlanTarget()
    {
        Announcement announcement = CreatePublishedAnnouncement(
            "Pro Plan Only", AnnouncementTarget.Plan, targetValue: "Pro");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithCaseInsensitivePlanMatch_IncludesAnnouncement()
    {
        Announcement announcement = CreatePublishedAnnouncement(
            "Pro Plan", AnnouncementTarget.Plan, targetValue: "Pro");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            "pro",
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithCaseInsensitiveRoleMatch_IncludesAnnouncement()
    {
        Announcement announcement = CreatePublishedAnnouncement(
            "Admin Only", AnnouncementTarget.Role, targetValue: "Admin");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string> { "admin" });

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithNonGuidTenantTargetValue_ExcludesAnnouncement()
    {
        Announcement announcement = CreatePublishedAnnouncement(
            "Invalid Tenant Target", AnnouncementTarget.Tenant, targetValue: "not-a-guid");

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_MapsAllDtoFields()
    {
        Announcement announcement = Announcement.Create(_testTenantId, "Test Title", "Test Content", AnnouncementType.Alert, TimeProvider.System, AnnouncementTarget.All, null, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7), true, true, "https://example.com", "Click Here", "https://example.com/image.png");
        announcement.Publish(TimeProvider.System);

        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement> { announcement });
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().HaveCount(1);
        AnnouncementDto dto = result[0];
        dto.Id.Should().Be(announcement.Id.Value);
        dto.Title.Should().Be("Test Title");
        dto.Content.Should().Be("Test Content");
        dto.Type.Should().Be(AnnouncementType.Alert);
        dto.Target.Should().Be(AnnouncementTarget.All);
        dto.IsPinned.Should().BeTrue();
        dto.IsDismissible.Should().BeTrue();
        dto.ActionUrl.Should().Be("https://example.com");
        dto.ActionLabel.Should().Be("Click Here");
        dto.ImageUrl.Should().Be("https://example.com/image.png");
        dto.Status.Should().Be(AnnouncementStatus.Published);
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_WithNoAnnouncements_ReturnsEmptyList()
    {
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement>());
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string>());

        IReadOnlyList<AnnouncementDto> result = await _service.GetActiveAnnouncementsForUserAsync(userContext);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAnnouncementsForUserAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        _announcementRepository.GetPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Announcement>());
        _dismissalRepository.GetByUserIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDismissal>());

        UserContext userContext = new(
            UserId.Create(Guid.NewGuid()),
            TenantId.Create(Guid.NewGuid()),
            null,
            new List<string>());

        await _service.GetActiveAnnouncementsForUserAsync(userContext, cts.Token);

        await _announcementRepository.Received(1).GetPublishedAsync(cts.Token);
    }

    private static readonly TenantId _testTenantId = TenantId.New();

    private static Announcement CreatePublishedAnnouncement(
        string title,
        AnnouncementTarget target,
        string? targetValue = null,
        DateTime? expiresAt = null)
    {
        Announcement announcement = Announcement.Create(_testTenantId, title, "Content", AnnouncementType.Feature, TimeProvider.System, target, targetValue, null, expiresAt);
        announcement.Publish(TimeProvider.System);
        return announcement;
    }
}
