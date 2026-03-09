using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Application.Announcements.Queries.GetAllAnnouncements;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Queries;

public class GetAllAnnouncementsHandlerTests
{
    private static readonly TenantId _testTenantId = TenantId.New();
    private readonly IAnnouncementRepository _repository;
    private readonly GetAllAnnouncementsHandler _handler;

    public GetAllAnnouncementsHandlerTests()
    {
        _repository = Substitute.For<IAnnouncementRepository>();
        _handler = new GetAllAnnouncementsHandler(_repository);
    }

    [Fact]
    public async Task Handle_ReturnsAllAnnouncements()
    {
        List<Announcement> announcements =
        [
            Announcement.Create(_testTenantId, "Title 1", "Content 1", AnnouncementType.Feature, TimeProvider.System),
            Announcement.Create(_testTenantId, "Title 2", "Content 2", AnnouncementType.Alert, TimeProvider.System)
        ];

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(announcements);

        GetAllAnnouncementsQuery query = new();

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenNoAnnouncements_ReturnsEmptyList()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        GetAllAnnouncementsQuery query = new();

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        Announcement announcement = Announcement.Create(_testTenantId, "Feature Title", "Feature Content", AnnouncementType.Feature, TimeProvider.System, AnnouncementTarget.Role, "Admin", null, null, true, false, "https://example.com", "Learn More", "https://example.com/img.png");

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([announcement]);

        GetAllAnnouncementsQuery query = new();

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        AnnouncementDto dto = result.Value[0];
        dto.Title.Should().Be("Feature Title");
        dto.Content.Should().Be("Feature Content");
        dto.Type.Should().Be(AnnouncementType.Feature);
        dto.Target.Should().Be(AnnouncementTarget.Role);
        dto.TargetValue.Should().Be("Admin");
        dto.IsPinned.Should().BeTrue();
        dto.IsDismissible.Should().BeFalse();
        dto.ActionUrl.Should().Be("https://example.com");
        dto.ActionLabel.Should().Be("Learn More");
        dto.ImageUrl.Should().Be("https://example.com/img.png");
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        GetAllAnnouncementsQuery query = new();

        await _handler.Handle(query, cts.Token);

        await _repository.Received(1).GetAllAsync(cts.Token);
    }
}
