using Foundry.Communications.Application.Announcements.Commands.ArchiveAnnouncement;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class ArchiveAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository;
    private readonly ArchiveAnnouncementHandler _handler;

    public ArchiveAnnouncementHandlerTests()
    {
        _repository = Substitute.For<IAnnouncementRepository>();
        _handler = new ArchiveAnnouncementHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenAnnouncementExists_ArchivesAndReturnsSuccess()
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        ArchiveAnnouncementCommand command = new(announcement.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(announcement, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnnouncementNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        ArchiveAnnouncementCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotCallUpdate()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        ArchiveAnnouncementCommand command = new(Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        ArchiveAnnouncementCommand command = new(announcement.Id.Value);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).GetByIdAsync(Arg.Any<AnnouncementId>(), cts.Token);
        await _repository.Received(1).UpdateAsync(announcement, cts.Token);
    }
}
