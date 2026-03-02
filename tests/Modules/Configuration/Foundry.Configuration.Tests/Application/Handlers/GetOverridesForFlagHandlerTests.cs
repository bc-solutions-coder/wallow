using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetOverridesForFlag;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class GetOverridesForFlagHandlerTests
{
    private readonly IFeatureFlagOverrideRepository _repository;
    private readonly GetOverridesForFlagHandler _handler;

    public GetOverridesForFlagHandlerTests()
    {
        _repository = Substitute.For<IFeatureFlagOverrideRepository>();
        _handler = new GetOverridesForFlagHandler(_repository);
    }

    [Fact]
    public async Task Handle_WithOverrides_ReturnsSuccessWithDtos()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();
        FeatureFlagOverride over1 = FeatureFlagOverride.CreateForTenant(flagId, tenantId, true, TimeProvider.System);
        FeatureFlagOverride over2 = FeatureFlagOverride.CreateForUser(flagId, Guid.NewGuid(), false, TimeProvider.System);

        _repository.GetOverridesForFlagAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlagOverride> { over1, over2 });

        GetOverridesForFlagQuery query = new(flagId.Value);

        Result<IReadOnlyList<FeatureFlagOverrideDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithNoOverrides_ReturnsSuccessWithEmptyList()
    {
        _repository.GetOverridesForFlagAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlagOverride>());

        GetOverridesForFlagQuery query = new(Guid.NewGuid());

        Result<IReadOnlyList<FeatureFlagOverrideDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsDtoFieldsCorrectly()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flagId, tenantId, true, TimeProvider.System, "variant_a");

        _repository.GetOverridesForFlagAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlagOverride> { over });

        GetOverridesForFlagQuery query = new(flagId.Value);

        Result<IReadOnlyList<FeatureFlagOverrideDto>> result = await _handler.Handle(query, CancellationToken.None);

        FeatureFlagOverrideDto dto = result.Value[0];
        dto.FlagId.Should().Be(flagId.Value);
        dto.TenantId.Should().Be(tenantId);
        dto.IsEnabled.Should().BeTrue();
        dto.Variant.Should().Be("variant_a");
    }
}
