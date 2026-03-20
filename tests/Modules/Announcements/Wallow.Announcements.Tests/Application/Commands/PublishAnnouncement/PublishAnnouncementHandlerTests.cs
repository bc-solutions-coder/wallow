using Wallow.Announcements.Application.Announcements.Commands.PublishAnnouncement;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Application.Announcements.Services;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Announcements.Tests.Application.Commands.PublishAnnouncement;

public class PublishAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository = Substitute.For<IAnnouncementRepository>();
    private readonly IAnnouncementTargetingService _targetingService = Substitute.For<IAnnouncementTargetingService>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly PublishAnnouncementHandler _handler;

    public PublishAnnouncementHandlerTests()
    {
        _handler = new PublishAnnouncementHandler(_repository, _targetingService, _bus, TimeProvider.System);
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
    public async Task Handle_WithValidId_PublishesAndReturnsSuccess()
    {
        Announcement announcement = CreateAnnouncement();
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns(announcement);
        _targetingService.ResolveTargetUsersAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        PublishAnnouncementCommand command = new(announcement.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<AnnouncementId>(), Arg.Any<CancellationToken>())
            .Returns((Announcement?)null);

        PublishAnnouncementCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Announcement.NotFound.NotFound");
    }
}
