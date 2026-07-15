using Wallow.Announcements.Application.Changelogs.Commands.PublishChangelogEntry;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Announcements.Domain.Changelogs.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Tests.Application.Commands.Changelogs.PublishChangelogEntry;

public class PublishChangelogEntryHandlerTests
{
    private readonly IChangelogRepository _repository = Substitute.For<IChangelogRepository>();
    private readonly PublishChangelogEntryHandler _handler;

    public PublishChangelogEntryHandlerTests()
    {
        _handler = new PublishChangelogEntryHandler(_repository, TimeProvider.System);
    }

    private static ChangelogEntry CreateEntry()
    {
        return ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidId_PublishesAndReturnsSuccess()
    {
        ChangelogEntry entry = CreateEntry();
        _repository.GetByIdAsync(Arg.Any<ChangelogEntryId>(), Arg.Any<CancellationToken>())
            .Returns(entry);

        PublishChangelogEntryCommand command = new(entry.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Any<ChangelogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<ChangelogEntryId>(), Arg.Any<CancellationToken>())
            .Returns((ChangelogEntry?)null);

        PublishChangelogEntryCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Changelog.NotFound");
    }
}
