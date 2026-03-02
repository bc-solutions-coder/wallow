using Foundry.Communications.Application.Announcements.Commands.CreateChangelogEntry;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Handlers;

public class CreateChangelogEntryMappingTests
{
    private readonly IChangelogRepository _repository;
    private readonly CreateChangelogEntryHandler _handler;

    public CreateChangelogEntryMappingTests()
    {
        _repository = Substitute.For<IChangelogRepository>();
        _handler = new CreateChangelogEntryHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_MapsIdCorrectly()
    {
        CreateChangelogEntryCommand command = new("1.0.0", "Release", "Content", DateTime.UtcNow);

        Result<ChangelogEntryDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_MapsCreatedAtCorrectly()
    {
        DateTime before = DateTime.UtcNow;
        CreateChangelogEntryCommand command = new("1.0.0", "Release", "Content", DateTime.UtcNow);

        Result<ChangelogEntryDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectDefaultPublishedState()
    {
        CreateChangelogEntryCommand command = new("1.0.0", "Release", "Content", DateTime.UtcNow);

        Result<ChangelogEntryDto> result = await _handler.Handle(command, CancellationToken.None);

        result.Value.IsPublished.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
    }
}
