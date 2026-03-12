using Foundry.Announcements.Application.Announcements.DTOs;
using Foundry.Announcements.Application.Announcements.Interfaces;
using Foundry.Announcements.Application.Announcements.Queries.GetAllAnnouncements;
using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Tests.Application.Queries.GetAllAnnouncements;

public class GetAllAnnouncementsHandlerTests
{
    private readonly IAnnouncementRepository _repository = Substitute.For<IAnnouncementRepository>();
    private readonly GetAllAnnouncementsHandler _handler;

    public GetAllAnnouncementsHandlerTests()
    {
        _handler = new GetAllAnnouncementsHandler(_repository);
    }

    [Fact]
    public async Task Handle_ReturnsAllAnnouncements()
    {
        List<Announcement> announcements =
        [
            Announcement.Create(TenantId.Create(Guid.NewGuid()), "First", "Content 1", AnnouncementType.Feature, TimeProvider.System),
            Announcement.Create(TenantId.Create(Guid.NewGuid()), "Second", "Content 2", AnnouncementType.Update, TimeProvider.System)
        ];
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(announcements);

        GetAllAnnouncementsQuery query = new();

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Title.Should().Be("First");
        result.Value[1].Title.Should().Be("Second");
    }

    [Fact]
    public async Task Handle_WhenNoAnnouncements_ReturnsEmptyList()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Announcement>());

        GetAllAnnouncementsQuery query = new();

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
