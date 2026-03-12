using Foundry.Announcements.Application.Announcements.Commands.UpdateAnnouncement;
using Foundry.Announcements.Application.Announcements.DTOs;
using Foundry.Announcements.Application.Announcements.Interfaces;
using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Announcements.Enums;
using Foundry.Announcements.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Tests.Application.Commands.UpdateAnnouncement;

public class UpdateAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository = Substitute.For<IAnnouncementRepository>();
    private readonly UpdateAnnouncementHandler _handler;

    public UpdateAnnouncementHandlerTests()
    {
        _handler = new UpdateAnnouncementHandler(_repository, TimeProvider.System);
    }

    private static UpdateAnnouncementCommand ValidCommand(Guid? id = null) => new(
        id ?? Guid.NewGuid(),
        "Updated Title",
        "Updated content",
        AnnouncementType.Update,
        AnnouncementTarget.All,
        null,
        null,
        null,
        false,
        true,
        null,
        null,
        null);

    private static Announcement CreateAnnouncement()
    {
        return Announcement.Create(
            TenantId.Create(Guid.NewGuid()),
            "Original Title",
            "Original content",
            AnnouncementType.Feature,
            TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccessWithUpdatedDto()
    {
        Announcement announcement = CreateAnnouncement();
        UpdateAnnouncementCommand command = ValidCommand(announcement.Id.Value);
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Updated Title");
        result.Value.Content.Should().Be("Updated content");
        await _repository.Received(1).UpdateAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundError()
    {
        UpdateAnnouncementCommand command = ValidCommand();
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Announcement.NotFound.NotFound");
    }
}
