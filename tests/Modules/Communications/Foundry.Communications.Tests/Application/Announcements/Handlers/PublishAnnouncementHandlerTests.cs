using Foundry.Communications.Application.Announcements.Commands.PublishAnnouncement;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Contracts.Communications.Announcements.Events;
using Foundry.Shared.Kernel.Results;
using Wolverine;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class PublishAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly PublishAnnouncementHandler _handler;

    public PublishAnnouncementHandlerTests()
    {
        _repository = Substitute.For<IAnnouncementRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _handler = new PublishAnnouncementHandler(_repository, _messageBus, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenAnnouncementExists_PublishesAndReturnsSuccess()
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        PublishAnnouncementCommand command = new(announcement.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(announcement, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnnouncementNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        PublishAnnouncementCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_PublishesIntegrationEvent()
    {
        Announcement announcement = Announcement.Create("Test Title", "Test Content", AnnouncementType.Alert, TimeProvider.System, AnnouncementTarget.All, null, null, null, true, true);

        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        PublishAnnouncementCommand command = new(announcement.Id.Value);

        await _handler.Handle(command, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<AnnouncementPublishedEvent>(e =>
                e.AnnouncementId == announcement.Id.Value &&
                e.Title == "Test Title" &&
                e.Content == "Test Content" &&
                e.IsPinned));
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotPublishEvent()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        PublishAnnouncementCommand command = new(Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<AnnouncementPublishedEvent>());
    }
}
