using Foundry.Communications.Application.Announcements.Commands.CreateAnnouncement;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class CreateAnnouncementHandlerTests
{
    private readonly IAnnouncementRepository _repository;
    private readonly CreateAnnouncementHandler _handler;

    public CreateAnnouncementHandlerTests()
    {
        _repository = Substitute.For<IAnnouncementRepository>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());
        _handler = new CreateAnnouncementHandler(_repository, tenantContext, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        CreateAnnouncementCommand command = new(
            "Test Announcement", "Content here",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Test Announcement");
        result.Value.Content.Should().Be("Content here");
        result.Value.Type.Should().Be(AnnouncementType.Feature);
        result.Value.Target.Should().Be(AnnouncementTarget.All);
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsToRepository()
    {
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Alert, AnnouncementTarget.All,
            null, null, null, true, false, null, null, null);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<Announcement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithPinnedFlag_SetsIsPinned()
    {
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, true, true, null, null, null);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithOptionalFields_IncludesThem()
    {
        DateTime publishAt = DateTime.UtcNow.AddHours(1);
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Maintenance, AnnouncementTarget.Tenant,
            "tenant-123", publishAt, expiresAt, false, true,
            "https://example.com", "Click Here", "https://example.com/img.png");

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.TargetValue.Should().Be("tenant-123");
        result.Value.ActionUrl.Should().Be("https://example.com");
        result.Value.ActionLabel.Should().Be("Click Here");
        result.Value.ImageUrl.Should().Be("https://example.com/img.png");
    }

    [Fact]
    public async Task Handle_WithPublishAt_SetsScheduledStatus()
    {
        DateTime publishAt = DateTime.UtcNow.AddHours(1);
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, publishAt, null, false, true, null, null, null);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.Status.Should().Be(AnnouncementStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_WithoutPublishAt_SetsDraftStatus()
    {
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        Result<AnnouncementDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.Status.Should().Be(AnnouncementStatus.Draft);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        CreateAnnouncementCommand command = new(
            "Title", "Content",
            AnnouncementType.Feature, AnnouncementTarget.All,
            null, null, null, false, true, null, null, null);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).AddAsync(Arg.Any<Announcement>(), cts.Token);
    }
}
