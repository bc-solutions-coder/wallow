using Foundry.Communications.Application.Announcements.Commands.UpdateAnnouncement;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class UpdateAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository;
    private readonly UpdateAnnouncementHandler _handler;

    public UpdateAnnouncementHandlerTests()
    {
        _repository = Substitute.For<IAnnouncementRepository>();
        _handler = new UpdateAnnouncementHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenAnnouncementExists_UpdatesAndReturnsSuccess()
    {
        Announcement announcement = Announcement.Create("Old Title", "Old Content", AnnouncementType.Feature, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        UpdateAnnouncementCommand command = new(
            announcement.Id.Value, "New Title", "New Content",
            AnnouncementType.Alert, AnnouncementTarget.All,
            null, null, null, true, false, null, null, null);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New Title");
        result.Value.Content.Should().Be("New Content");
        result.Value.Type.Should().Be(AnnouncementType.Alert);
        result.Value.IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAnnouncementNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        UpdateAnnouncementCommand command = new(
            Guid.NewGuid(), "Title", "Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenAnnouncementExists_CallsUpdateOnRepository()
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        UpdateAnnouncementCommand command = new(
            announcement.Id.Value, "Updated", "Updated Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotCallUpdate()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        UpdateAnnouncementCommand command = new(
            Guid.NewGuid(), "Title", "Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }
}
