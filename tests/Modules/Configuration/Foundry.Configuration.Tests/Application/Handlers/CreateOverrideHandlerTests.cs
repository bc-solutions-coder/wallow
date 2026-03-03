using Foundry.Configuration.Application.FeatureFlags.Commands.CreateOverride;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class CreateOverrideHandlerTests
{
    private readonly IFeatureFlagRepository _flagRepo;
    private readonly IFeatureFlagOverrideRepository _overrideRepo;
    private readonly IDistributedCache _cache;
    private readonly CreateOverrideHandler _handler;
    private readonly Guid _callerTenantId;

    public CreateOverrideHandlerTests()
    {
        _flagRepo = Substitute.For<IFeatureFlagRepository>();
        _overrideRepo = Substitute.For<IFeatureFlagOverrideRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _callerTenantId = Guid.NewGuid();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(_callerTenantId));
        _handler = new CreateOverrideHandler(_flagRepo, _overrideRepo, _cache, TimeProvider.System, tenantContext);
    }

    [Fact]
    public async Task Handle_WithTenantOverride_CreatesSuccessfully()
    {
        Guid flagId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("dark_mode", "Dark Mode", true, TimeProvider.System);

        _flagRepo.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);
        _overrideRepo.GetOverrideAsync(Arg.Any<FeatureFlagId>(), _callerTenantId, null, Arg.Any<CancellationToken>())
            .Returns((FeatureFlagOverride?)null);

        CreateOverrideCommand command = new(flagId, TenantId: _callerTenantId, UserId: null, IsEnabled: true, Variant: null, ExpiresAt: null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _overrideRepo.Received(1).AddAsync(Arg.Any<FeatureFlagOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUserOverride_CreatesSuccessfully()
    {
        Guid flagId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("beta", "Beta", false, TimeProvider.System);

        _flagRepo.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);
        _overrideRepo.GetOverrideAsync(Arg.Any<FeatureFlagId>(), null, userId, Arg.Any<CancellationToken>())
            .Returns((FeatureFlagOverride?)null);

        CreateOverrideCommand command = new(flagId, TenantId: null, UserId: userId, IsEnabled: true, Variant: null, ExpiresAt: null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _overrideRepo.Received(1).AddAsync(Arg.Any<FeatureFlagOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithTenantAndUserOverride_CreatesSuccessfully()
    {
        Guid flagId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("feature", "Feature", true, TimeProvider.System);

        _flagRepo.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);
        _overrideRepo.GetOverrideAsync(Arg.Any<FeatureFlagId>(), _callerTenantId, userId, Arg.Any<CancellationToken>())
            .Returns((FeatureFlagOverride?)null);

        CreateOverrideCommand command = new(flagId, TenantId: _callerTenantId, UserId: userId, IsEnabled: false, Variant: null, ExpiresAt: null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _overrideRepo.Received(1).AddAsync(Arg.Any<FeatureFlagOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFlagNotFound_ReturnsNotFoundFailure()
    {
        Guid flagId = Guid.NewGuid();

        _flagRepo.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        CreateOverrideCommand command = new(flagId, TenantId: _callerTenantId, UserId: null, IsEnabled: true, Variant: null, ExpiresAt: null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _overrideRepo.DidNotReceive().AddAsync(Arg.Any<FeatureFlagOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoTenantOrUser_ReturnsValidationFailure()
    {
        Guid flagId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("key", "Name", true, TimeProvider.System);

        _flagRepo.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);

        CreateOverrideCommand command = new(flagId, TenantId: null, UserId: null, IsEnabled: true, Variant: null, ExpiresAt: null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Validation");
        await _overrideRepo.DidNotReceive().AddAsync(Arg.Any<FeatureFlagOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOverrideAlreadyExists_ReturnsConflictFailure()
    {
        Guid flagId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("key", "Name", true, TimeProvider.System);
        FeatureFlagOverride existing = FeatureFlagOverride.CreateForTenant(FeatureFlagId.Create(flagId), _callerTenantId, true, TimeProvider.System);

        _flagRepo.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);
        _overrideRepo.GetOverrideAsync(Arg.Any<FeatureFlagId>(), _callerTenantId, null, Arg.Any<CancellationToken>())
            .Returns(existing);

        CreateOverrideCommand command = new(flagId, TenantId: _callerTenantId, UserId: null, IsEnabled: true, Variant: null, ExpiresAt: null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Conflict");
    }

    [Fact]
    public async Task Handle_AfterSuccess_InvalidatesCache()
    {
        Guid flagId = Guid.NewGuid();
        FeatureFlag flag = FeatureFlag.CreateBoolean("cached_flag", "Cached", true, TimeProvider.System);

        _flagRepo.GetByIdAsync(Arg.Any<FeatureFlagId>(), Arg.Any<CancellationToken>())
            .Returns(flag);
        _overrideRepo.GetOverrideAsync(Arg.Any<FeatureFlagId>(), _callerTenantId, null, Arg.Any<CancellationToken>())
            .Returns((FeatureFlagOverride?)null);

        CreateOverrideCommand command = new(flagId, TenantId: _callerTenantId, UserId: null, IsEnabled: false, Variant: null, ExpiresAt: null);

        await _handler.Handle(command, CancellationToken.None);

        await _cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k.Contains("cached_flag")),
            Arg.Any<CancellationToken>());
    }
}
