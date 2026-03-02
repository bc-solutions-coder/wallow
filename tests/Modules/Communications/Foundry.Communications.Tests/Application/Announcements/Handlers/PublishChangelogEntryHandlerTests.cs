using Foundry.Communications.Application.Announcements.Commands.PublishChangelogEntry;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class PublishChangelogEntryHandlerTests
{
    private readonly IChangelogRepository _repository;
    private readonly PublishChangelogEntryHandler _handler;

    public PublishChangelogEntryHandlerTests()
    {
        _repository = Substitute.For<IChangelogRepository>();
        _handler = new PublishChangelogEntryHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenEntryExists_PublishesAndReturnsSuccess()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ChangelogEntryId>(), Arg.Any<CancellationToken>())
            .Returns(entry);

        PublishChangelogEntryCommand command = new(entry.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        entry.IsPublished.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(entry, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEntryNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<ChangelogEntryId>(), Arg.Any<CancellationToken>())
            .Returns((ChangelogEntry?)null);

        PublishChangelogEntryCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotCallUpdate()
    {
        _repository.GetByIdAsync(Arg.Any<ChangelogEntryId>(), Arg.Any<CancellationToken>())
            .Returns((ChangelogEntry?)null);

        PublishChangelogEntryCommand command = new(Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<ChangelogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ChangelogEntryId>(), Arg.Any<CancellationToken>())
            .Returns(entry);

        PublishChangelogEntryCommand command = new(entry.Id.Value);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).GetByIdAsync(Arg.Any<ChangelogEntryId>(), cts.Token);
        await _repository.Received(1).UpdateAsync(entry, cts.Token);
    }
}
