using Foundry.Announcements.Application.Announcements.Commands.CreateAnnouncement;
using Foundry.Announcements.Application.Announcements.DTOs;
using Foundry.Announcements.Application.Announcements.Interfaces;
using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Tests.Application.Commands.CreateAnnouncement;

public class CreateAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository = Substitute.For<IAnnouncementRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly CreateAnnouncementHandler _handler;

    public CreateAnnouncementHandlerTests()
    {
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));
        _handler = new CreateAnnouncementHandler(_repository, _tenantContext, TimeProvider.System);
    }

    private static CreateAnnouncementCommand ValidCommand() => new(
        "Test Announcement",
        "This is test content",
        AnnouncementType.Feature,
        AnnouncementTarget.All,
        null,
        null,
        null,
        false,
        true,
        null,
        null,
        null);

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccessWithDto()
    {
        CreateAnnouncementCommand command = ValidCommand();

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be(command.Title);
        result.Value.Content.Should().Be(command.Content);
        result.Value.Type.Should().Be(command.Type);
        result.Value.Status.Should().Be(AnnouncementStatus.Draft);
        await _repository.Received(1).AddAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithScheduledPublishAt_ReturnsScheduledStatus()
    {
        CreateAnnouncementCommand command = ValidCommand() with { PublishAt = DateTime.UtcNow.AddDays(1) };

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AnnouncementStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_WithValidData_CallsRepositoryAdd()
    {
        CreateAnnouncementCommand command = ValidCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }
}
