using Wallow.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;
using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Tests.Application.Commands.Changelogs.CreateChangelogEntry;

public class CreateChangelogEntryHandlerTests
{
    private readonly IChangelogRepository _repository = Substitute.For<IChangelogRepository>();
    private readonly CreateChangelogEntryHandler _handler;

    public CreateChangelogEntryHandlerTests()
    {
        _handler = new CreateChangelogEntryHandler(_repository, TimeProvider.System);
    }

    private static CreateChangelogEntryCommand ValidCommand() => new(
        "1.0.0",
        "Initial Release",
        "First release of the platform",
        DateTime.UtcNow);

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccessWithDto()
    {
        CreateChangelogEntryCommand command = ValidCommand();

        Result<ChangelogEntryDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(command.Version);
        result.Value.Title.Should().Be(command.Title);
        result.Value.Content.Should().Be(command.Content);
        result.Value.IsPublished.Should().BeFalse();
        await _repository.Received(1).AddAsync(Arg.Any<ChangelogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidData_CallsRepositoryAdd()
    {
        CreateChangelogEntryCommand command = ValidCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<ChangelogEntry>(), Arg.Any<CancellationToken>());
    }
}
