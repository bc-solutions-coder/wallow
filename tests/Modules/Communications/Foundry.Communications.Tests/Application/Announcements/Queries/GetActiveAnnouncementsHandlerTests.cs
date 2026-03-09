using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Queries.GetActiveAnnouncements;
using Foundry.Communications.Application.Announcements.Services;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Queries;

public class GetActiveAnnouncementsHandlerTests
{
    private readonly IAnnouncementTargetingService _targetingService;
    private readonly GetActiveAnnouncementsHandler _handler;

    public GetActiveAnnouncementsHandlerTests()
    {
        _targetingService = Substitute.For<IAnnouncementTargetingService>();
        _handler = new GetActiveAnnouncementsHandler(_targetingService);
    }

    [Fact]
    public async Task Handle_ReturnsAnnouncementsFromTargetingService()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        List<AnnouncementDto> expected =
        [
            new AnnouncementDto(
                Guid.NewGuid(), "Title", "Content",
                Communications.Domain.Announcements.Enums.AnnouncementType.Feature,
                Communications.Domain.Announcements.Enums.AnnouncementTarget.All,
                null, null, null, false, true, null, null, null,
                Communications.Domain.Announcements.Enums.AnnouncementStatus.Published,
                DateTime.UtcNow)
        ];

        _targetingService.GetActiveAnnouncementsForUserAsync(Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        GetActiveAnnouncementsQuery query = new(userId, tenantId, "Pro", ["Admin"]);

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_PassesCorrectUserContext()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        List<string> roles = ["Admin", "User"];

        _targetingService.GetActiveAnnouncementsForUserAsync(Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns([]);

        GetActiveAnnouncementsQuery query = new(userId, tenantId, "Enterprise", roles);

        await _handler.Handle(query, CancellationToken.None);

        await _targetingService.Received(1).GetActiveAnnouncementsForUserAsync(
            Arg.Is<UserContext>(ctx =>
                ctx.UserId.Value == userId &&
                ctx.TenantId.Value == tenantId &&
                ctx.PlanName == "Enterprise" &&
                ctx.Roles.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoAnnouncements_ReturnsEmptyList()
    {
        _targetingService.GetActiveAnnouncementsForUserAsync(Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns([]);

        GetActiveAnnouncementsQuery query = new(Guid.NewGuid(), Guid.NewGuid(), null, []);

        Result<IReadOnlyList<AnnouncementDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        _targetingService.GetActiveAnnouncementsForUserAsync(Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns([]);

        GetActiveAnnouncementsQuery query = new(Guid.NewGuid(), Guid.NewGuid(), null, []);

        await _handler.Handle(query, cts.Token);

        await _targetingService.Received(1).GetActiveAnnouncementsForUserAsync(
            Arg.Any<UserContext>(), cts.Token);
    }
}
