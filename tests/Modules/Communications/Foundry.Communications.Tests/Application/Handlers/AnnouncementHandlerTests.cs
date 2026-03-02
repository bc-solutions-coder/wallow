using Foundry.Communications.Application.Announcements.Commands.ArchiveAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.CreateAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.PublishAnnouncement;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Contracts.Communications.Announcements.Events;
using Foundry.Shared.Kernel.Results;
using Wolverine;

namespace Foundry.Communications.Tests.Application.Handlers;

public class AnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly CreateAnnouncementHandler _createHandler;
    private readonly PublishAnnouncementHandler _publishHandler;
    private readonly ArchiveAnnouncementHandler _archiveHandler;

    public AnnouncementHandlerTests()
    {
        _repository = Substitute.For<IAnnouncementRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _createHandler = new CreateAnnouncementHandler(_repository, TimeProvider.System);
        _publishHandler = new PublishAnnouncementHandler(_repository, _messageBus, TimeProvider.System);
        _archiveHandler = new ArchiveAnnouncementHandler(_repository, TimeProvider.System);
    }

    // --- CreateAnnouncement ---

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccessWithDto()
    {
        CreateAnnouncementCommand command = new(
            "Test Announcement", "Content here",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        Result<AnnouncementDto> result = await _createHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Test Announcement");
        result.Value.Content.Should().Be("Content here");
        result.Value.Type.Should().Be(AnnouncementType.Feature);
        result.Value.Target.Should().Be(AnnouncementTarget.All);
        result.Value.Status.Should().Be(AnnouncementStatus.Draft);
        await _repository.Received(1).AddAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithPublishAt_SetsScheduledStatus()
    {
        DateTime publishAt = DateTime.UtcNow.AddHours(1);
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, publishAt, null, false, true, null, null, null);

        Result<AnnouncementDto> result = await _createHandler.Handle(command, CancellationToken.None);

        result.Value.Status.Should().Be(AnnouncementStatus.Scheduled);
    }

    [Fact]
    public async Task Create_WithAllOptionalFields_IncludesThem()
    {
        DateTime publishAt = DateTime.UtcNow.AddHours(1);
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Maintenance, AnnouncementTarget.Tenant,
            "tenant-123", publishAt, expiresAt, true, false,
            "https://example.com", "Click Here", "https://example.com/img.png");

        Result<AnnouncementDto> result = await _createHandler.Handle(command, CancellationToken.None);

        result.Value.TargetValue.Should().Be("tenant-123");
        result.Value.IsPinned.Should().BeTrue();
        result.Value.IsDismissible.Should().BeFalse();
        result.Value.ActionUrl.Should().Be("https://example.com");
        result.Value.ActionLabel.Should().Be("Click Here");
        result.Value.ImageUrl.Should().Be("https://example.com/img.png");
    }

    // --- PublishAnnouncement ---

    [Fact]
    public async Task Publish_WhenAnnouncementExists_ReturnsSuccessAndUpdatesRepository()
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        PublishAnnouncementCommand command = new(announcement.Id.Value);

        Result result = await _publishHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(announcement, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_WhenAnnouncementNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        PublishAnnouncementCommand command = new(Guid.NewGuid());

        Result result = await _publishHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Publish_SendsIntegrationEvent()
    {
        Announcement announcement = Announcement.Create("Test Title", "Test Content", AnnouncementType.Alert, TimeProvider.System, AnnouncementTarget.All, null, null, null, true, true);
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        PublishAnnouncementCommand command = new(announcement.Id.Value);

        await _publishHandler.Handle(command, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<AnnouncementPublishedEvent>(e =>
                e.AnnouncementId == announcement.Id.Value &&
                e.Title == "Test Title" &&
                e.Content == "Test Content" &&
                e.IsPinned));
    }

    [Fact]
    public async Task Publish_WhenNotFound_DoesNotPublishEvent()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        PublishAnnouncementCommand command = new(Guid.NewGuid());

        await _publishHandler.Handle(command, CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<AnnouncementPublishedEvent>());
    }

    // --- ArchiveAnnouncement ---

    [Fact]
    public async Task Archive_WhenAnnouncementExists_ArchivesAndReturnsSuccess()
    {
        Announcement announcement = Announcement.Create("Title", "Content", AnnouncementType.Feature, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        ArchiveAnnouncementCommand command = new(announcement.Id.Value);

        Result result = await _archiveHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(announcement, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archive_WhenAnnouncementNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        ArchiveAnnouncementCommand command = new(Guid.NewGuid());

        Result result = await _archiveHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Archive_WhenNotFound_DoesNotCallUpdate()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        ArchiveAnnouncementCommand command = new(Guid.NewGuid());

        await _archiveHandler.Handle(command, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    // --- Full Lifecycle ---

    [Fact]
    public async Task Lifecycle_CreateThenPublishThenArchive_TransitionsCorrectly()
    {
        CreateAnnouncementCommand createCommand = new(
            "Lifecycle Test", "Content",
            AnnouncementType.Update, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        Result<AnnouncementDto> createResult = await _createHandler.Handle(createCommand, CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Status.Should().Be(AnnouncementStatus.Draft);

        Announcement announcement = Announcement.Create("Lifecycle Test", "Content", AnnouncementType.Update, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        PublishAnnouncementCommand publishCommand = new(announcement.Id.Value);
        Result publishResult = await _publishHandler.Handle(publishCommand, CancellationToken.None);

        publishResult.IsSuccess.Should().BeTrue();

        ArchiveAnnouncementCommand archiveCommand = new(announcement.Id.Value);
        Result archiveResult = await _archiveHandler.Handle(archiveCommand, CancellationToken.None);

        archiveResult.IsSuccess.Should().BeTrue();
    }
}
