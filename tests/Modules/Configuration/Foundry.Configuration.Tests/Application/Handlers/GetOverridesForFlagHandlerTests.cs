using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetOverridesForFlag;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class GetOverridesForFlagHandlerTests
{
    private readonly IFeatureFlagOverrideRepository _repository;
    private readonly GetOverridesForFlagHandler _handler;
    private readonly Guid _callerTenantId;

    public GetOverridesForFlagHandlerTests()
    {
        _repository = Substitute.For<IFeatureFlagOverrideRepository>();
        _callerTenantId = Guid.NewGuid();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(_callerTenantId));
        _handler = new GetOverridesForFlagHandler(_repository, tenantContext);
    }

    [Fact]
    public async Task Handle_WithOverrides_ReturnsSuccessWithDtos()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        FeatureFlagOverride over1 = FeatureFlagOverride.CreateForTenant(flagId, _callerTenantId, true, TimeProvider.System);
        FeatureFlagOverride over2 = FeatureFlagOverride.CreateForTenant(flagId, _callerTenantId, false, TimeProvider.System);

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
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flagId, _callerTenantId, true, TimeProvider.System, "variant_a");

        _repository.GetOverridesForFlagAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlagOverride> { over });

        GetOverridesForFlagQuery query = new(flagId.Value);

        Result<IReadOnlyList<FeatureFlagOverrideDto>> result = await _handler.Handle(query, CancellationToken.None);

        FeatureFlagOverrideDto dto = result.Value[0];
        dto.FlagId.Should().Be(flagId.Value);
        dto.TenantId.Should().Be(_callerTenantId);
        dto.IsEnabled.Should().BeTrue();
        dto.Variant.Should().Be("variant_a");
    }

    [Fact]
    public async Task Handle_FiltersOutOverridesForOtherTenants()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid otherTenantId = Guid.NewGuid();
        FeatureFlagOverride callerOverride = FeatureFlagOverride.CreateForTenant(flagId, _callerTenantId, true, TimeProvider.System);
        FeatureFlagOverride otherOverride = FeatureFlagOverride.CreateForTenant(flagId, otherTenantId, false, TimeProvider.System);

        _repository.GetOverridesForFlagAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlagOverride> { callerOverride, otherOverride });

        GetOverridesForFlagQuery query = new(flagId.Value);

        Result<IReadOnlyList<FeatureFlagOverrideDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].TenantId.Should().Be(_callerTenantId);
    }
}
