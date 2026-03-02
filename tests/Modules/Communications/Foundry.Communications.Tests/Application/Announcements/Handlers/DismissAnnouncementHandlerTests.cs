using Foundry.Communications.Application.Announcements.Commands.DismissAnnouncement;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class DismissAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IAnnouncementDismissalRepository _dismissalRepository;
    private readonly DismissAnnouncementHandler _handler;

    public DismissAnnouncementHandlerTests()
    {
        _announcementRepository = Substitute.For<IAnnouncementRepository>();
        _dismissalRepository = Substitute.For<IAnnouncementDismissalRepository>();
        _handler = new DismissAnnouncementHandler(_announcementRepository, _dismissalRepository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenDismissible_CreatesAndReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System, isDismissible: true);

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
    public async Task Handle_WhenAnnouncementNotFound_ReturnsNotFoundFailure()
    {
        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        DismissAnnouncementCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenNotDismissible_ReturnsValidationFailure()
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Alert, TimeProvider.System, isDismissible: false);

        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        DismissAnnouncementCommand command = new(announcement.Id.Value, Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotDismissible");
    }

    [Fact]
    public async Task Handle_WhenAlreadyDismissed_ReturnsSuccessWithoutAdding()
    {
        Guid userId = Guid.NewGuid();
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System, isDismissible: true);

        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);
        _dismissalRepository.ExistsAsync(Arg.Any<AnnouncementId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(true);

        DismissAnnouncementCommand command = new(announcement.Id.Value, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _dismissalRepository.DidNotReceive().AddAsync(Arg.Any<AnnouncementDismissal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotCheckDismissal()
    {
        _announcementRepository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        DismissAnnouncementCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        await _dismissalRepository.DidNotReceive().ExistsAsync(
            Arg.Any<AnnouncementId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }
}
