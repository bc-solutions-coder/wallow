using Wallow.Announcements.Application.Announcements.Commands.ArchiveAnnouncement;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Tests.Application.Commands.ArchiveAnnouncement;

public class ArchiveAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository = Substitute.For<IAnnouncementRepository>();
    private readonly ArchiveAnnouncementHandler _handler;

    public ArchiveAnnouncementHandlerTests()
    {
        _handler = new ArchiveAnnouncementHandler(_repository, TimeProvider.System);
    }

    private static Announcement CreateAnnouncement()
    {
        return Announcement.Create(
            TenantId.Create(Guid.NewGuid()),
            "Test Announcement",
            "Test content",
            AnnouncementType.Feature,
            TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidId_ArchivesAndReturnsSuccess()
    {
        Announcement announcement = CreateAnnouncement();
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);

        ArchiveAnnouncementCommand command = new(announcement.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        ArchiveAnnouncementCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Announcement.NotFound.NotFound");
    }
}
