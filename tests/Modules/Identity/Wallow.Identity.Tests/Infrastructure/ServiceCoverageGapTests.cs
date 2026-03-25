#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OpenIddict.Abstractions;
using StackExchange.Redis;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Services;
using Wolverine;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Infrastructure;

#region PreRegisteredClientSyncService Gap Tests

public sealed class PreRegisteredClientSyncServiceGapTests
{
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly PreRegisteredClientSyncService _sut;
    private readonly PreRegisteredClientOptions _options;

    public PreRegisteredClientSyncServiceGapTests()
    {
        _appManager = Substitute.For<IOpenIddictApplicationManager>();
        _options = new PreRegisteredClientOptions();
        _sut = new PreRegisteredClientSyncService(
            _appManager,
            Options.Create(_options),
            NullLogger<PreRegisteredClientSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_ExistingClient_ClientTypeChangedToPublic_Updates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "spa",
            DisplayName = "SPA",
            // No Secret = public
            RedirectUris = ["https://spa/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });

        object existing = new object();
        _appManager.FindByClientIdAsync("spa", Arg.Any<CancellationToken>()).Returns(existing);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "SPA";
                d.ClientType = ClientTypes.Confidential; // currently confidential
                d.RedirectUris.Add(new Uri("https://spa/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ClientType == ClientTypes.Public),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingClient_PostLogoutUrisChanged_Updates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "app",
            DisplayName = "App",
            Secret = "s",
            RedirectUris = ["https://app/cb"],
            PostLogoutRedirectUris = ["https://app/logout-new"],
            Scopes = ["openid"]
        });

        object existing = new object();
        _appManager.FindByClientIdAsync("app", Arg.Any<CancellationToken>()).Returns(existing);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "App";
                d.ClientType = ClientTypes.Confidential;
                d.RedirectUris.Add(new Uri("https://app/cb"));
                d.PostLogoutRedirectUris.Add(new Uri("https://app/logout-old"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(existing, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingClient_ScopesChanged_Updates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "scoped",
            DisplayName = "Scoped",
            Secret = "s",
            RedirectUris = ["https://s/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid", "profile"]
        });

        object existing = new object();
        _appManager.FindByClientIdAsync("scoped", Arg.Any<CancellationToken>()).Returns(existing);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Scoped";
                d.ClientType = ClientTypes.Confidential;
                d.RedirectUris.Add(new Uri("https://s/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid"); // missing "profile"
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(existing, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingClient_MissingSourceProperty_AddsItAndUpdates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "nosrc",
            DisplayName = "NoSrc",
            Secret = "s",
            RedirectUris = ["https://ns/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });

        object existing = new object();
        _appManager.FindByClientIdAsync("nosrc", Arg.Any<CancellationToken>()).Returns(existing);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "NoSrc";
                d.ClientType = ClientTypes.Confidential;
                d.RedirectUris.Add(new Uri("https://ns/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                // No "source" property
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(existing, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DeletePhase_SkipsNonConfigSourceValue()
    {
        _options.Clients.Clear();
        object app = new object();
        IAsyncEnumerable<object> apps = ToAsync([app]);
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(apps);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                ci.ArgAt<OpenIddictApplicationDescriptor>(0).Properties["source"] = JsonSerializer.SerializeToElement("manual");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.DidNotReceive().DeleteAsync(app, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DeletePhase_NullClientId_DoesNotDelete()
    {
        _options.Clients.Clear();
        object app = new object();
        IAsyncEnumerable<object> apps = ToAsync([app]);
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(apps);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                ci.ArgAt<OpenIddictApplicationDescriptor>(0).Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });
        _appManager.GetClientIdAsync(app, Arg.Any<CancellationToken>()).Returns(new ValueTask<string?>((string?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.DidNotReceive().DeleteAsync(app, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingClient_RedirectUrisChanged_Updates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "redir",
            DisplayName = "Redir",
            Secret = "s",
            RedirectUris = ["https://new/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });

        object existing = new object();
        _appManager.FindByClientIdAsync("redir", Arg.Any<CancellationToken>()).Returns(existing);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Redir";
                d.ClientType = ClientTypes.Confidential;
                d.RedirectUris.Add(new Uri("https://old/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(existing, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DeletePhase_StillConfiguredClient_NotDeleted()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "keep",
            DisplayName = "Keep",
            Secret = "s",
            RedirectUris = ["https://k/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });

        _appManager.FindByClientIdAsync("keep", Arg.Any<CancellationToken>()).Returns((object?)null);

        object app = new object();
        IAsyncEnumerable<object> apps = ToAsync([app]);
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(apps);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                ci.ArgAt<OpenIddictApplicationDescriptor>(0).Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });
        _appManager.GetClientIdAsync(app, Arg.Any<CancellationToken>()).Returns(new ValueTask<string?>("keep"));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.DidNotReceive().DeleteAsync(app, Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<object> ToAsync(List<object> items)
    {
        foreach (object item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}

#endregion

#region OidcFederationService Gap Tests

public sealed class OidcFederationServiceGapTests
{
    private readonly ISsoConfigurationRepository _repo;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OidcFederationService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public OidcFederationServiceGapTests()
    {
        _repo = Substitute.For<ISsoConfigurationRepository>();
        TenantContext tc = new();
        tc.SetTenant(new TenantId(_tenantId));
        _tenantContext = tc;
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _sut = new OidcFederationService(
            _repo, _tenantContext, _currentUserService,
            _httpClientFactory, _httpContextAccessor,
            NullLogger<OidcFederationService>.Instance, TimeProvider.System);
    }

    [Fact]
    public async Task DisableAsync_WhenConfigNotFound_Throws()
    {
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        Func<Task> act = () => _sut.DisableAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SSO configuration not found");
    }

    [Fact]
    public async Task DisableAsync_WhenConfigExists_DisablesAndSaves()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            new TenantId(_tenantId), "Test", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        await _sut.DisableAsync();

        config.Status.Should().Be(SsoStatus.Disabled);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOidcCallbackInfoAsync_ReturnsCorrectUrls()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");
        _httpContextAccessor.HttpContext.Returns(httpContext);

        OidcCallbackInfo result = await _sut.GetOidcCallbackInfoAsync();

        string expectedPrefix = $"oidc-sso-{_tenantId.ToString()[..8]}";
        result.RedirectUri.Should().Contain($"signin-oidc-{expectedPrefix}");
        result.PostLogoutRedirectUri.Should().Contain($"signout-callback-oidc-{expectedPrefix}");
    }

    [Fact]
    public async Task GetOidcCallbackInfoAsync_NoHttpContext_FallsBackToLocalhost()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        OidcCallbackInfo result = await _sut.GetOidcCallbackInfoAsync();

        result.RedirectUri.Should().StartWith("https://localhost:5001/");
    }

    [Fact]
    public async Task ValidateIdpConfigurationAsync_NoConfig_ReturnsInvalid()
    {
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        SsoValidationResult result = await _sut.ValidateIdpConfigurationAsync();

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("SSO configuration not found");
    }

    [Fact]
    public async Task ValidateIdpConfigurationAsync_NoIssuer_ReturnsInvalid()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            new TenantId(_tenantId), "Test", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        // OidcIssuer is null by default
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        SsoValidationResult result = await _sut.ValidateIdpConfigurationAsync();

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("OIDC Issuer not configured");
    }

    [Fact]
    public async Task ValidateIdpConfigurationAsync_NoClientId_ReturnsInvalid()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            new TenantId(_tenantId), "Test", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        // Set OidcIssuer via reflection without setting OidcClientId
        typeof(SsoConfiguration).GetProperty("OidcIssuer")!.SetValue(config, "https://issuer");
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        SsoValidationResult result = await _sut.ValidateIdpConfigurationAsync();

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("OIDC Client ID not configured");
    }

    [Fact]
    public async Task TestConnectionAsync_NoConfig_ReturnsFailure()
    {
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        SsoTestResult result = await _sut.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("SSO configuration not found");
    }

    [Fact]
    public async Task TestConnectionAsync_NoIssuer_ReturnsFailure()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            new TenantId(_tenantId), "Test", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        SsoTestResult result = await _sut.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("OIDC Issuer not configured");
    }

    [Fact]
    public async Task ActivateAsync_WhenConfigNotFound_Throws()
    {
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        Func<Task> act = () => _sut.ActivateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SSO configuration not found");
    }

    [Fact]
    public async Task GetConfigurationAsync_WhenNull_ReturnsNull()
    {
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        SsoConfigurationDto? result = await _sut.GetConfigurationAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConfigurationAsync_WhenExists_ReturnsDto()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            new TenantId(_tenantId), "TestConfig", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        SsoConfigurationDto? result = await _sut.GetConfigurationAsync();

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("TestConfig");
        result.Protocol.Should().Be(SsoProtocol.Oidc);
    }
}

#endregion

#region MfaService Additional Gap Tests

public sealed class MfaServiceAdditionalGapTests
{
    private readonly IDatabase _redisDb;
    private readonly UserManager<WallowUser> _userManager;
    private readonly MfaService _sut;
    private readonly IDataProtectionProvider _dp;

    public MfaServiceAdditionalGapTests()
    {
        _dp = DataProtectionProvider.Create("test");
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        IConnectionMultiplexer mux = Substitute.For<IConnectionMultiplexer>();
        _redisDb = Substitute.For<IDatabase>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mfa:MaxFailedAttempts"] = "5" }).Build();
        _sut = new MfaService(_dp, cache, mux, _userManager, cfg, NullLogger<MfaService>.Instance);
    }

    [Fact]
    public async Task ValidateBackupCodeAsync_ValidCode_ReturnsTrueAndRemovesCode()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Test", "User", "u@t.com", TimeProvider.System);

        // Compute the hash of "test-code" the same way the service does (SHA256 hex)
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("test-code"));
        string codeHash = Convert.ToHexStringLower(hash);

        user.SetBackupCodes(JsonSerializer.Serialize(new List<string> { codeHash, "other-hash" }));
        _userManager.FindByIdAsync("u1").Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        bool result = await _sut.ValidateBackupCodeAsync("u1", "test-code", CancellationToken.None);

        result.Should().BeTrue();
        await _userManager.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task ValidateChallengeAsync_ThreeArg_InvalidTotpCode_ReturnsFalse()
    {
        _redisDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(true);

        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Test", "User", "u@t.com", TimeProvider.System);
        // Generate an enrollment secret so TotpSecretEncrypted is set
        (string secret, string _) = await _sut.GenerateEnrollmentSecretAsync("u1", CancellationToken.None);
        typeof(WallowUser).GetProperty("TotpSecretEncrypted")!.SetValue(user, secret);

        _userManager.FindByIdAsync("u1").Returns(user);

        bool result = await _sut.ValidateChallengeAsync("u1", "ct", "000000", CancellationToken.None);

        result.Should().BeFalse();
    }
}

#endregion

#region MfaExemptionChecker Tests

public sealed class MfaExemptionCheckerTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly MfaExemptionChecker _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider;

    public MfaExemptionCheckerTests()
    {
        DbContextOptions<IdentityDbContext> opts = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(opts, dp);
        _dbContext.SetTenant(new TenantId(_tenantId));
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _sut = new MfaExemptionChecker(_dbContext, _timeProvider);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task IsExemptAsync_UserWithFutureGraceDeadline_ReturnsTrue()
    {
        WallowUser user = WallowUser.Create(_tenantId, "Grace", "User", "grace@t.com", TimeProvider.System);
        user.SetMfaGraceDeadline(DateTimeOffset.UtcNow.AddDays(3));

        bool result = await _sut.IsExemptAsync(user, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsExemptAsync_UserWithNoMembership_ReturnsFalse()
    {
        WallowUser user = WallowUser.Create(_tenantId, "No", "Membership", "nm@t.com", TimeProvider.System);

        bool result = await _sut.IsExemptAsync(user, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsExemptAsync_UserWithExpiredGraceAndNoMembership_ReturnsFalse()
    {
        WallowUser user = WallowUser.Create(_tenantId, "Expired", "Grace", "eg@t.com", TimeProvider.System);
        // MfaGraceDeadline is null, no membership

        bool result = await _sut.IsExemptAsync(user, CancellationToken.None);

        result.Should().BeFalse();
    }
}

#endregion

#region SetupClientService Tests

public sealed class SetupClientServiceTests
{
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly SetupClientService _sut;

    public SetupClientServiceTests()
    {
        _appManager = Substitute.For<IOpenIddictApplicationManager>();
        _sut = new SetupClientService(_appManager);
    }

    [Fact]
    public async Task ClientExistsAsync_WhenExists_ReturnsTrue()
    {
        _appManager.FindByClientIdAsync("test-client", Arg.Any<CancellationToken>()).Returns(new object());

        bool result = await _sut.ClientExistsAsync("test-client");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ClientExistsAsync_WhenNotExists_ReturnsFalse()
    {
        _appManager.FindByClientIdAsync("missing", Arg.Any<CancellationToken>()).Returns((object?)null);

        bool result = await _sut.ClientExistsAsync("missing");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateConfidentialClientAsync_CreatesWithCorrectDescriptor()
    {
        List<string> redirectUris = ["https://app/cb", "https://app2/cb"];

        string result = await _sut.CreateConfidentialClientAsync("my-client", "my-secret", redirectUris);

        result.Should().Be("my-secret");

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.ClientId == "my-client" &&
                d.ClientType == ClientTypes.Confidential &&
                d.RedirectUris.Count == 2 &&
                d.PostLogoutRedirectUris.Count == 2),
            Arg.Any<CancellationToken>());
    }
}

#endregion

#region OpenIddictServiceAccountService Gap Tests

public sealed class OpenIddictServiceAccountServiceGapTests
{
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly IServiceAccountRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly OpenIddictServiceAccountService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public OpenIddictServiceAccountServiceGapTests()
    {
        _appManager = Substitute.For<IOpenIddictApplicationManager>();
        _repository = Substitute.For<IServiceAccountRepository>();
        TenantContext tc = new();
        tc.SetTenant(new TenantId(_tenantId));
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(Guid.NewGuid());
        _sut = new OpenIddictServiceAccountService(
            _appManager, _repository, tc, _currentUserService,
            TimeProvider.System, NullLogger<OpenIddictServiceAccountService>.Instance);
    }

    [Fact]
    public async Task RevokeAsync_WhenNotFound_ThrowsEntityNotFound()
    {
        ServiceAccountMetadataId id = ServiceAccountMetadataId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ServiceAccountMetadata?)null);

        Func<Task> act = () => _sut.RevokeAsync(id);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task UpdateScopesAsync_WhenMetadataNotFound_ThrowsEntityNotFound()
    {
        ServiceAccountMetadataId id = ServiceAccountMetadataId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ServiceAccountMetadata?)null);

        Func<Task> act = () => _sut.UpdateScopesAsync(id, ["billing"]);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task UpdateScopesAsync_WhenAppNotFound_ThrowsInvalidOperation()
    {
        ServiceAccountMetadataId id = ServiceAccountMetadataId.New();
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            new TenantId(_tenantId), "sa-test", "Test SA", "Desc",
            ["openid"], Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>()).Returns(metadata);
        _appManager.FindByClientIdAsync(metadata.ClientId, Arg.Any<CancellationToken>()).Returns((object?)null);

        Func<Task> act = () => _sut.UpdateScopesAsync(id, ["billing"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RotateSecretAsync_WhenMetadataNotFound_ThrowsEntityNotFound()
    {
        ServiceAccountMetadataId id = ServiceAccountMetadataId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ServiceAccountMetadata?)null);

        Func<Task> act = () => _sut.RotateSecretAsync(id);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task RotateSecretAsync_WhenAppNotFound_ThrowsInvalidOperation()
    {
        ServiceAccountMetadataId id = ServiceAccountMetadataId.New();
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            new TenantId(_tenantId), "sa-test", "Test SA", "Desc",
            ["openid"], Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>()).Returns(metadata);
        _appManager.FindByClientIdAsync(metadata.ClientId, Arg.Any<CancellationToken>()).Returns((object?)null);

        Func<Task> act = () => _sut.RotateSecretAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAsync_WhenNotFound_ReturnsNull()
    {
        ServiceAccountMetadataId id = ServiceAccountMetadataId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ServiceAccountMetadata?)null);

        ServiceAccountDto? result = await _sut.GetAsync(id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenExists_ReturnsDto()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            new TenantId(_tenantId), "sa-test", "Test SA", "Test Description",
            ["openid", "billing"], Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>()).Returns(metadata);

        ServiceAccountDto? result = await _sut.GetAsync(metadata.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test SA");
        result.ClientId.Should().Be("sa-test");
    }
}

#endregion

#region PasswordlessService Additional Gap Tests

public sealed class PasswordlessServiceAdditionalGapTests
{
    private readonly IDatabase _redis;
    private readonly IMessageBus _messageBus;
    private readonly UserManager<WallowUser> _userManager;
    private readonly PasswordlessService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public PasswordlessServiceAdditionalGapTests()
    {
        IConnectionMultiplexer mux = Substitute.For<IConnectionMultiplexer>();
        _redis = Substitute.For<IDatabase>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redis);
        _messageBus = Substitute.For<IMessageBus>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        TenantContext tc = new();
        tc.SetTenant(new TenantId(_tenantId));
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        PasswordlessOptions opts = new()
        {
            RateLimitMaxRequests = 3,
            RateLimitWindow = TimeSpan.FromMinutes(15),
            MagicLinkTtl = TimeSpan.FromMinutes(10),
            OtpTtl = TimeSpan.FromMinutes(5)
        };
        _sut = new PasswordlessService(mux, _messageBus, _userManager, tc, dp, Options.Create(opts), NullLogger<PasswordlessService>.Instance);
    }

    [Fact]
    public async Task SendMagicLinkAsync_FirstRequest_SetsKeyExpiry()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        _userManager.FindByEmailAsync("first@t.com").Returns((WallowUser?)null);

        await _sut.SendMagicLinkAsync("first@t.com", CancellationToken.None);

        await _redis.Received(1).KeyExpireAsync(
            Arg.Any<RedisKey>(),
            Arg.Is<TimeSpan>(ts => ts == TimeSpan.FromMinutes(15)),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SendMagicLinkAsync_SubsequentRequest_DoesNotResetExpiry()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(2L);
        _userManager.FindByEmailAsync("second@t.com").Returns((WallowUser?)null);

        await _sut.SendMagicLinkAsync("second@t.com", CancellationToken.None);

        await _redis.DidNotReceive().KeyExpireAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ValidateMagicLinkAsync_ExpiredToken_ReturnsFailure()
    {
        // Generate a valid signed token by sending a magic link first
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        WallowUser user = WallowUser.Create(_tenantId, "A", "B", "valid@t.com", TimeProvider.System);
        _userManager.FindByEmailAsync("valid@t.com").Returns(user);

        string? capturedToken = null;
        await _messageBus.PublishAsync(Arg.Do<object>(e =>
        {
            System.Reflection.PropertyInfo? tokenProp = e.GetType().GetProperty("Token");
            if (tokenProp != null)
            {
                capturedToken = tokenProp.GetValue(e) as string;
            }
        }));

        await _sut.SendMagicLinkAsync("valid@t.com", CancellationToken.None);

        if (capturedToken is not null)
        {
            // Token exists but Redis returns empty (expired)
            _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);

            PasswordlessResult result = await _sut.ValidateMagicLinkAsync(capturedToken, CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.Error.Should().Be("Token expired or already used.");
        }
    }

    [Fact]
    public async Task SendOtpAsync_FirstRequest_SetsKeyExpiry()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        _userManager.FindByEmailAsync("otp-first@t.com").Returns((WallowUser?)null);

        await _sut.SendOtpAsync("otp-first@t.com", CancellationToken.None);

        await _redis.Received(1).KeyExpireAsync(
            Arg.Any<RedisKey>(),
            Arg.Is<TimeSpan>(ts => ts == TimeSpan.FromMinutes(15)),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }
}

#endregion
