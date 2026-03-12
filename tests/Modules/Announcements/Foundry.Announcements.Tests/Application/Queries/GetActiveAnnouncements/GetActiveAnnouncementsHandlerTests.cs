using Foundry.Announcements.Application.Announcements.DTOs;
using Foundry.Announcements.Application.Announcements.Queries.GetActiveAnnouncements;
using Foundry.Announcements.Application.Announcements.Services;
using Foundry.Announcements.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Tests.Application.Queries.GetActiveAnnouncements;

public class GetActiveAnnouncementsHandlerTests
{
    private readonly IAnnouncementTargetingService _targetingService = Substitute.For<IAnnouncementTargetingService>();
    private readonly GetActiveAnnouncementsHandler _handler;

    public GetActiveAnnouncementsHandlerTests()
    {
        _handler = new GetActiveAnnouncementsHandler(_targetingService);
    }

    [Fact]
    public async Task Handle_ReturnsActiveAnnouncementsForUser()
    {
        List<AnnouncementDto> dtos =
        [
            new(Guid.NewGuid(), "Active", "Content", AnnouncementType.Feature, AnnouncementTarget.All,
                null, null, null, false, true, null, null, null, AnnouncementStatus.Published, DateTime.UtcNow)
        ];
        _targetingService.GetActiveAnnouncementsForUserAsync(Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(dtos);

        GetActiveAnnouncementsQuery query = new(Guid.NewGuid(), Guid.NewGuid(), null, []);

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Title.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_WhenNoActiveAnnouncements_ReturnsEmptyList()
    {
        _targetingService.GetActiveAnnouncementsForUserAsync(Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(new List<AnnouncementDto>());

        GetActiveAnnouncementsQuery query = new(Guid.NewGuid(), Guid.NewGuid(), null, []);

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
