using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public class ScimServiceTests
{
    private readonly IScimConfigurationRepository _scimRepository;
    private readonly IScimSyncLogRepository _syncLogRepository;
    private readonly ITenantContext _tenantContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ScimService _sut;
    private readonly TenantId _tenantId = new TenantId(Guid.NewGuid());

    public ScimServiceTests()
    {
        _scimRepository = Substitute.For<IScimConfigurationRepository>();
        _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero));

        // ScimUserService and ScimGroupService are sealed concrete types.
        // We construct real instances with mocked dependencies since ScimService config/token
        // methods don't touch them.
        ScimUserService userService = new ScimUserService(
            Substitute.For<UserManager<WallowUser>>(
                Substitute.For<IUserStore<WallowUser>>(), null!, null!, null!, null!, null!, null!, null!, null!),
            Substitute.For<RoleManager<WallowRole>>(
                Substitute.For<IRoleStore<WallowRole>>(), null!, null!, null!, null!),
            Substitute.For<IOrganizationService>(),
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            NullLogger<ScimUserService>.Instance,
            _timeProvider);

        ScimGroupService groupService = new ScimGroupService(
            Substitute.For<IOrganizationService>(),
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            NullLogger<ScimGroupService>.Instance,
            _timeProvider);

        _sut = new ScimService(
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            userService,
            groupService,
            NullLogger<ScimService>.Instance,
            _timeProvider);
    }

    [Fact]
    public async Task GetConfigurationAsync_NoConfig_ReturnsNull()
    {
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimConfigurationDto? result = await _sut.GetConfigurationAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConfigurationAsync_WithConfig_ReturnsMappedDto()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.Enable(Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        ScimConfigurationDto? result = await _sut.GetConfigurationAsync();

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue();
        result.ScimEndpointUrl.Should().Be("/scim/v2");
        result.AutoActivateUsers.Should().BeTrue();
    }

    [Fact]
    public async Task GetConfigurationAsync_MapsAllFields()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(false, "admin", true, Guid.Empty, _timeProvider);
        config.Enable(Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        ScimConfigurationDto? result = await _sut.GetConfigurationAsync();

        result.Should().NotBeNull();
        result!.AutoActivateUsers.Should().BeFalse();
        result.DefaultRole.Should().Be("admin");
        result.DeprovisionOnDelete.Should().BeTrue();
        result.TokenPrefix.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EnableScimAsync_NewConfig_CreatesAndReturnsToken()
    {
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);
        EnableScimRequest request = new EnableScimRequest(AutoActivateUsers: true, DefaultRole: "user", DeprovisionOnDelete: false);

        EnableScimResponse result = await _sut.EnableScimAsync(request);

        result.PlainTextToken.Should().NotBeNullOrEmpty();
        result.Configuration.Should().NotBeNull();
        result.Configuration.IsEnabled.Should().BeTrue();
        _scimRepository.Received(1).Add(Arg.Any<ScimConfiguration>());
        await _scimRepository.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableScimAsync_ExistingDisabledConfig_EnablesWithoutNewToken()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        EnableScimRequest request = new EnableScimRequest();

        EnableScimResponse result = await _sut.EnableScimAsync(request);

        result.PlainTextToken.Should().BeNull();
        result.Configuration.IsEnabled.Should().BeTrue();
        _scimRepository.DidNotReceive().Add(Arg.Any<ScimConfiguration>());
    }

    [Fact]
    public async Task DisableScimAsync_NoConfig_ThrowsInvalidOperationException()
    {
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        Func<Task> act = () => _sut.DisableScimAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SCIM configuration not found");
    }

    [Fact]
    public async Task DisableScimAsync_WithEnabledConfig_DisablesConfig()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.Enable(Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        await _sut.DisableScimAsync();

        config.IsEnabled.Should().BeFalse();
        await _scimRepository.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegenerateTokenAsync_NoConfig_ThrowsInvalidOperationException()
    {
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        Func<Task> act = () => _sut.RegenerateTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SCIM configuration not found");
    }

    [Fact]
    public async Task RegenerateTokenAsync_WithConfig_ReturnsNewToken()
    {
        (ScimConfiguration config, string originalToken) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.Enable(Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        string newToken = await _sut.RegenerateTokenAsync();

        newToken.Should().NotBeNullOrEmpty();
        newToken.Should().NotBe(originalToken);
        await _scimRepository.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateTokenAsync_NoConfig_ReturnsFalse()
    {
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        bool result = await _sut.ValidateTokenAsync("some-token");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_DisabledConfig_ReturnsFalse()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        bool result = await _sut.ValidateTokenAsync("some-token");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsFalse()
    {
        (ScimConfiguration config, string token) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.Enable(Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        _timeProvider.Advance(TimeSpan.FromDays(366));

        bool result = await _sut.ValidateTokenAsync(token);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_WrongToken_ReturnsFalse()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.Enable(Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        bool result = await _sut.ValidateTokenAsync("wrong-token");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetSyncLogsAsync_ReturnsMappedDtos()
    {
        ScimSyncLog log = ScimSyncLog.Create(
            _tenantId,
            ScimOperation.Create,
            ScimResourceType.User,
            "ext-1",
            "int-1",
            true,
            _timeProvider);

        _syncLogRepository.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ScimSyncLog> { log });

        IReadOnlyList<ScimSyncLogDto> result = await _sut.GetSyncLogsAsync();

        result.Should().HaveCount(1);
        result[0].ExternalId.Should().Be("ext-1");
        result[0].InternalId.Should().Be("int-1");
        result[0].Success.Should().BeTrue();
        result[0].Operation.Should().Be(ScimOperation.Create);
        result[0].ResourceType.Should().Be(ScimResourceType.User);
    }

    [Fact]
    public async Task GetSyncLogsAsync_EmptyLogs_ReturnsEmptyList()
    {
        _syncLogRepository.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ScimSyncLog>());

        IReadOnlyList<ScimSyncLogDto> result = await _sut.GetSyncLogsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSyncLogsAsync_PassesLimitToRepository()
    {
        _syncLogRepository.GetRecentAsync(50, Arg.Any<CancellationToken>())
            .Returns(new List<ScimSyncLog>());

        await _sut.GetSyncLogsAsync(50);

        await _syncLogRepository.Received(1).GetRecentAsync(50, Arg.Any<CancellationToken>());
    }
}
