using Wallow.Announcements.Application.Announcements.Commands.DismissAnnouncement;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Tests.Application.Commands.DismissAnnouncement;

public class DismissAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _announcementRepository = Substitute.For<IAnnouncementRepository>();
    private readonly IAnnouncementDismissalRepository _dismissalRepository = Substitute.For<IAnnouncementDismissalRepository>();
    private readonly DismissAnnouncementHandler _handler;

    public DismissAnnouncementHandlerTests()
    {
        _handler = new DismissAnnouncementHandler(_announcementRepository, _dismissalRepository, TimeProvider.System);
    }

    private static Announcement CreateDismissibleAnnouncement()
    {
        return Announcement.Create(
            TenantId.Create(Guid.NewGuid()),
            "Test Announcement",
            "Test content",
            AnnouncementType.Feature,
            TimeProvider.System,
            isDismissible: true);
    }

    private static Announcement CreateNonDismissibleAnnouncement()
    {
        return Announcement.Create(
            TenantId.Create(Guid.NewGuid()),
            "Test Announcement",
            "Test content",
            AnnouncementType.Feature,
            TimeProvider.System,
            isDismissible: false);
    }

    [Fact]
    public async Task Handle_WithDismissibleAnnouncement_ReturnsSuccess()
    {
        Announcement announcement = CreateDismissibleAnnouncement();
        Guid userId = Guid.NewGuid();
        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);
        _dismissalRepository.ExistsAsync(Arg.Any<AnnouncementId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(false);

        DismissAnnouncementCommand command = new(announcement.Id.Value, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _dismissalRepository.Received(1).AddAsync(Arg.Any<AnnouncementDismissal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundError()
    {
        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        DismissAnnouncementCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Announcement.NotFound.NotFound");
    }

    [Fact]
    public async Task Handle_WhenNotDismissible_ReturnsValidationError()
    {
        Announcement announcement = CreateNonDismissibleAnnouncement();
        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        DismissAnnouncementCommand command = new(announcement.Id.Value, Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Announcement.NotDismissible");
    }

    [Fact]
    public async Task Handle_WhenAlreadyDismissed_ReturnsSuccessWithoutAdding()
    {
        Announcement announcement = CreateDismissibleAnnouncement();
        Guid userId = Guid.NewGuid();
        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);
        _dismissalRepository.ExistsAsync(Arg.Any<AnnouncementId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(true);

        DismissAnnouncementCommand command = new(announcement.Id.Value, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _dismissalRepository.DidNotReceive().AddAsync(Arg.Any<AnnouncementDismissal>(), Arg.Any<CancellationToken>());
    }
}
