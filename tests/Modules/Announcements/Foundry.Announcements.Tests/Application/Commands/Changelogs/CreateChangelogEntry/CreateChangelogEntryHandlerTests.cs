using Foundry.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;
using Foundry.Announcements.Application.Changelogs.DTOs;
using Foundry.Announcements.Application.Changelogs.Interfaces;
using Foundry.Announcements.Domain.Changelogs.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Tests.Application.Commands.Changelogs.CreateChangelogEntry;

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
