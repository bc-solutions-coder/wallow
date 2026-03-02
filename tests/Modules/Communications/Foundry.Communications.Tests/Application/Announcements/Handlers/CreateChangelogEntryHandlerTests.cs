using Foundry.Communications.Application.Announcements.Commands.CreateChangelogEntry;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class CreateChangelogEntryHandlerTests
{
    private readonly IChangelogRepository _repository;
    private readonly CreateChangelogEntryHandler _handler;

    public CreateChangelogEntryHandlerTests()
    {
        _repository = Substitute.For<IChangelogRepository>();
        _handler = new CreateChangelogEntryHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        DateTime releasedAt = DateTime.UtcNow;
        CreateChangelogEntryCommand command = new("1.0.0", "Initial Release", "Release notes", releasedAt);

        Result<ChangelogEntryDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("1.0.0");
        result.Value.Title.Should().Be("Initial Release");
        result.Value.Content.Should().Be("Release notes");
        result.Value.ReleasedAt.Should().Be(releasedAt);
        result.Value.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsToRepository()
    {
        CreateChangelogEntryCommand command = new("2.0.0", "Major Update", "Content", DateTime.UtcNow);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<ChangelogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsEmptyItemsList()
    {
        CreateChangelogEntryCommand command = new("1.0.0", "Release", "Content", DateTime.UtcNow);

        Result<ChangelogEntryDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        CreateChangelogEntryCommand command = new("1.0.0", "Release", "Content", DateTime.UtcNow);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).AddAsync(Arg.Any<ChangelogEntry>(), cts.Token);
    }
}
