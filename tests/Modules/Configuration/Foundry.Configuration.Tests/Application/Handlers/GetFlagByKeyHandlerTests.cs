using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetFlagByKey;
using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class GetFlagByKeyHandlerTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly GetFlagByKeyHandler _handler;

    public GetFlagByKeyHandlerTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _handler = new GetFlagByKeyHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenFlagExists_ReturnsSuccessWithDto()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("dark_mode", "Dark Mode", true, TimeProvider.System, "desc");

        _repository.GetByKeyAsync("dark_mode", Arg.Any<CancellationToken>())
            .Returns(flag);

        GetFlagByKeyQuery query = new("dark_mode");

        Result<FeatureFlagDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().Be("dark_mode");
        result.Value.Name.Should().Be("Dark Mode");
    }

    [Fact]
    public async Task Handle_WhenFlagNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByKeyAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        GetFlagByKeyQuery query = new("nonexistent");

        Result<FeatureFlagDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_MapsAllDtoFields()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout Feature", 75, TimeProvider.System, "75% rollout");

        _repository.GetByKeyAsync("rollout", Arg.Any<CancellationToken>())
            .Returns(flag);

        GetFlagByKeyQuery query = new("rollout");

        Result<FeatureFlagDto> result = await _handler.Handle(query, CancellationToken.None);

        FeatureFlagDto dto = result.Value;
        dto.Id.Should().Be(flag.Id.Value);
        dto.RolloutPercentage.Should().Be(75);
        dto.Description.Should().Be("75% rollout");
    }
}
