using Foundry.Configuration.Application.FeatureFlags.Commands.CreateFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Enums;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class CreateFeatureFlagHandlerTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly CreateFeatureFlagHandler _handler;

    public CreateFeatureFlagHandlerTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _handler = new(_repository, _cache, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithBooleanFlag_CreatesAndReturnsId()
    {
        CreateFeatureFlagCommand command = new(
            Key: "dark_mode",
            Name: "Dark Mode",
            Description: "Enable dark mode",
            FlagType: FlagType.Boolean,
            DefaultEnabled: true,
            RolloutPercentage: null,
            Variants: null,
            DefaultVariant: null);

        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        Result<FeatureFlagDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        await _repository.Received(1).AddAsync(Arg.Any<FeatureFlag>(), Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(Arg.Is<string>(k => k.Contains("dark_mode")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateKey_ReturnsConflictFailure()
    {
        FeatureFlag existing = FeatureFlag.CreateBoolean("dark_mode", "Dark Mode", true, TimeProvider.System);

        CreateFeatureFlagCommand command = new(
            Key: "dark_mode",
            Name: "Dark Mode v2",
            Description: null,
            FlagType: FlagType.Boolean,
            DefaultEnabled: false,
            RolloutPercentage: null,
            Variants: null,
            DefaultVariant: null);

        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<FeatureFlagDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Conflict");
        await _repository.DidNotReceive().AddAsync(Arg.Any<FeatureFlag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithPercentageFlag_CreatesWithRollout()
    {
        CreateFeatureFlagCommand command = new(
            Key: "new_ui",
            Name: "New UI",
            Description: "Gradual rollout",
            FlagType: FlagType.Percentage,
            DefaultEnabled: false,
            RolloutPercentage: 25,
            Variants: null,
            DefaultVariant: null);

        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        Result<FeatureFlagDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AddAsync(
            Arg.Is<FeatureFlag>(f => f.FlagType == FlagType.Percentage && f.RolloutPercentage == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithVariantFlag_CreatesWithVariants()
    {
        List<VariantWeightDto> variants =
        [
            new("control", 50),
            new("treatment", 50)
        ];

        CreateFeatureFlagCommand command = new(
            Key: "ab_test",
            Name: "A/B Test",
            Description: null,
            FlagType: FlagType.Variant,
            DefaultEnabled: false,
            RolloutPercentage: null,
            Variants: variants,
            DefaultVariant: "control");

        _repository.GetByKeyAsync(command.Key, Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        Result<FeatureFlagDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AddAsync(
            Arg.Is<FeatureFlag>(f => f.FlagType == FlagType.Variant && f.Variants.Count == 2),
            Arg.Any<CancellationToken>());
    }
}
