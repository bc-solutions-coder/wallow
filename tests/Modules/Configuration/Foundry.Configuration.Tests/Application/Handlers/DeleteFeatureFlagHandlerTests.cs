using Foundry.Configuration.Application.FeatureFlags.Commands.DeleteFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;
using Wolverine;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class DeleteFeatureFlagHandlerTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly IMessageBus _bus;
    private readonly DeleteFeatureFlagHandler _handler;

    public DeleteFeatureFlagHandlerTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _bus = Substitute.For<IMessageBus>();
        _handler = new DeleteFeatureFlagHandler(_repository, _cache, _bus);
    }

    [Fact]
    public async Task Handle_WithExistingFlag_DeletesAndReturnsSuccess()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("to_delete", "To Delete", true, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);

        DeleteFeatureFlagCommand command = new(flag.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(flag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        DeleteFeatureFlagCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<FeatureFlag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AfterSuccess_InvalidatesCacheAndPublishesEvent()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("cached_flag", "Cached", true, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);

        DeleteFeatureFlagCommand command = new(flag.Id.Value);

        await _handler.Handle(command, CancellationToken.None);

        await _cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k.Contains("cached_flag")),
            Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotInvalidateCacheOrPublish()
    {
        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        DeleteFeatureFlagCommand command = new(Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }
}
