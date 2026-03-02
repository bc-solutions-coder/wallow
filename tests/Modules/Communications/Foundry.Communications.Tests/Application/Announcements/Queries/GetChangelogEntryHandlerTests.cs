using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Application.Announcements.Queries.GetChangelogEntry;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Announcements.Queries;

public class GetChangelogByVersionHandlerTests
{
    private readonly IChangelogRepository _repository;
    private readonly GetChangelogByVersionHandler _handler;

    public GetChangelogByVersionHandlerTests()
    {
        _repository = Substitute.For<IChangelogRepository>();
        _handler = new GetChangelogByVersionHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenPublishedEntryExists_ReturnsSuccess()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.Publish(TimeProvider.System);

        _repository.GetByVersionAsync("1.0.0", Arg.Any<CancellationToken>())
            .Returns(entry);

        GetChangelogByVersionQuery query = new("1.0.0");

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("1.0.0");
        result.Value.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByVersionAsync("9.9.9", Arg.Any<CancellationToken>())
            .Returns((ChangelogEntry?)null);

        GetChangelogByVersionQuery query = new("9.9.9");

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenEntryIsNotPublished_ReturnsNotFound()
    {
        ChangelogEntry entry = ChangelogEntry.Create("1.0.0", "Release", "Content", DateTime.UtcNow, TimeProvider.System);
        // Not published

        _repository.GetByVersionAsync("1.0.0", Arg.Any<CancellationToken>())
            .Returns(entry);

        GetChangelogByVersionQuery query = new("1.0.0");

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        _repository.GetByVersionAsync("1.0.0", Arg.Any<CancellationToken>())
            .Returns((ChangelogEntry?)null);

        GetChangelogByVersionQuery query = new("1.0.0");

        await _handler.Handle(query, cts.Token);

        await _repository.Received(1).GetByVersionAsync("1.0.0", cts.Token);
    }
}

public class GetLatestChangelogHandlerTests
{
    private readonly IChangelogRepository _repository;
    private readonly GetLatestChangelogHandler _handler;

    public GetLatestChangelogHandlerTests()
    {
        _repository = Substitute.For<IChangelogRepository>();
        _handler = new GetLatestChangelogHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenLatestExists_ReturnsSuccess()
    {
        ChangelogEntry entry = ChangelogEntry.Create("2.0.0", "Latest", "Content", DateTime.UtcNow, TimeProvider.System);
        entry.Publish(TimeProvider.System);

        _repository.GetLatestPublishedAsync(Arg.Any<CancellationToken>())
            .Returns(entry);

        GetLatestChangelogQuery query = new();

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Handle_WhenNoLatest_ReturnsNotFound()
    {
        _repository.GetLatestPublishedAsync(Arg.Any<CancellationToken>())
            .Returns((ChangelogEntry?)null);

        GetLatestChangelogQuery query = new();

        Result<ChangelogEntryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        _repository.GetLatestPublishedAsync(Arg.Any<CancellationToken>())
            .Returns((ChangelogEntry?)null);

        GetLatestChangelogQuery query = new();

        await _handler.Handle(query, cts.Token);

        await _repository.Received(1).GetLatestPublishedAsync(cts.Token);
    }
}
