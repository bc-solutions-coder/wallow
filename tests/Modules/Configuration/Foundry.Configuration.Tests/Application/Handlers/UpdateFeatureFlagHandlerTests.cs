using Foundry.Configuration.Application.FeatureFlags.Commands.UpdateFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class UpdateFeatureFlagHandlerTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly UpdateFeatureFlagHandler _handler;

    public UpdateFeatureFlagHandlerTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _handler = new UpdateFeatureFlagHandler(_repository, _cache, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesAndReturnsSuccess()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("dark_mode", "Dark Mode", true, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);

        UpdateFeatureFlagCommand command = new(flag.Id.Value, "Dark Mode v2", "Updated desc", false, null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        flag.Name.Should().Be("Dark Mode v2");
        flag.Description.Should().Be("Updated desc");
        flag.DefaultEnabled.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(flag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFlagNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        UpdateFeatureFlagCommand command = new(Guid.NewGuid(), "Test", null, false, null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<FeatureFlag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithPercentageFlag_UpdatesRolloutPercentage()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 10, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);

        UpdateFeatureFlagCommand command = new(flag.Id.Value, "Rollout", null, true, RolloutPercentage: 50);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        flag.RolloutPercentage.Should().Be(50);
    }

    [Fact]
    public async Task Handle_WithBooleanFlag_IgnoresRolloutPercentage()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("bool_flag", "Bool Flag", true, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);

        UpdateFeatureFlagCommand command = new(flag.Id.Value, "Bool Flag", null, true, RolloutPercentage: 50);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        flag.RolloutPercentage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AfterSuccess_InvalidatesCache()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("cached_flag", "Cached", true, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);

        UpdateFeatureFlagCommand command = new(flag.Id.Value, "Cached Updated", null, false, null);

        await _handler.Handle(command, CancellationToken.None);

        await _cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k.Contains("cached_flag")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotInvalidateCache()
    {
        _repository.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        UpdateFeatureFlagCommand command = new(Guid.NewGuid(), "Test", null, false, null);

        await _handler.Handle(command, CancellationToken.None);

        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
