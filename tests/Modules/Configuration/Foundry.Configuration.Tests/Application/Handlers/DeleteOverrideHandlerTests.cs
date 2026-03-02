using Foundry.Configuration.Application.FeatureFlags.Commands.DeleteOverride;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class DeleteOverrideHandlerTests
{
    private readonly IFeatureFlagOverrideRepository _overrideRepo;
    private readonly IFeatureFlagRepository _flagRepo;
    private readonly IDistributedCache _cache;
    private readonly DeleteOverrideHandler _handler;

    public DeleteOverrideHandlerTests()
    {
        _overrideRepo = Substitute.For<IFeatureFlagOverrideRepository>();
        _flagRepo = Substitute.For<IFeatureFlagRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _handler = new DeleteOverrideHandler(_overrideRepo, _flagRepo, _cache);
    }

    [Fact]
    public async Task Handle_WithExistingOverride_DeletesAndReturnsSuccess()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("test_flag", "Test", true, TimeProvider.System);
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), true, TimeProvider.System);

        _overrideRepo.GetByIdAsync(Arg.Any<FeatureFlagOverrideId>(), Arg.Any<CancellationToken>())
            .Returns(over);
        _flagRepo.GetByIdAsync(over.FlagId, Arg.Any<CancellationToken>())
            .Returns(flag);

        DeleteOverrideCommand command = new(over.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _overrideRepo.Received(1).DeleteAsync(over, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOverrideNotFound_ReturnsNotFoundFailure()
    {
        _overrideRepo.GetByIdAsync(Arg.Any<FeatureFlagOverrideId>(), Arg.Any<CancellationToken>())
            .Returns((FeatureFlagOverride?)null);

        DeleteOverrideCommand command = new(Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _overrideRepo.DidNotReceive().DeleteAsync(Arg.Any<FeatureFlagOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFlagExists_InvalidatesCache()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("cached_flag", "Cached", true, TimeProvider.System);
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), true, TimeProvider.System);

        _overrideRepo.GetByIdAsync(Arg.Any<FeatureFlagOverrideId>(), Arg.Any<CancellationToken>())
            .Returns(over);
        _flagRepo.GetByIdAsync(over.FlagId, Arg.Any<CancellationToken>())
            .Returns(flag);

        DeleteOverrideCommand command = new(over.Id.Value);

        await _handler.Handle(command, CancellationToken.None);

        await _cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k.Contains("cached_flag")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFlagNotFound_DoesNotInvalidateCache()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("flag", "Flag", true, TimeProvider.System);
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flag.Id, Guid.NewGuid(), true, TimeProvider.System);

        _overrideRepo.GetByIdAsync(Arg.Any<FeatureFlagOverrideId>(), Arg.Any<CancellationToken>())
            .Returns(over);
        _flagRepo.GetByIdAsync(over.FlagId, Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        DeleteOverrideCommand command = new(over.Id.Value);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
