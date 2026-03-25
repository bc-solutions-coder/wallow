using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ScimBearerAuthenticationHandlerGapTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ScimBearerAuthenticationHandlerGapTests()
    {
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(new TenantId(_tenantId));

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
        _dbContext.SetTenant(new TenantId(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private ScimBearerAuthenticationHandler CreateHandler(TimeProvider? timeProvider = null)
    {
        IOptionsMonitor<AuthenticationSchemeOptions> optionsMonitor =
            Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Get(Arg.Any<string>()).Returns(new AuthenticationSchemeOptions());

        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        ILogger logger = Substitute.For<ILogger>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);

        return new ScimBearerAuthenticationHandler(
            optionsMonitor,
            loggerFactory,
            UrlEncoder.Default,
            _dbContext,
            _tenantContext,
            timeProvider ?? TimeProvider.System);
    }

    private async Task InitializeHandler(ScimBearerAuthenticationHandler handler, HttpContext context)
    {
        AuthenticationScheme scheme = new("ScimBearer", "SCIM Bearer", typeof(ScimBearerAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_DisabledConfig_ReturnsFail()
    {
        TenantId tenantId = new(_tenantId);
        (ScimConfiguration config, string plainTextToken) = ScimConfiguration.Create(
            tenantId, Guid.NewGuid(), TimeProvider.System);
        // Config is created disabled by default — do NOT call Enable

        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = $"Bearer {plainTextToken}";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Invalid or expired SCIM token");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ExpiredToken_ReturnsFail()
    {
        // Use a fake TimeProvider that returns a time far in the future for IsTokenValid check
        FakeTimeProvider fakeTime = new(DateTimeOffset.UtcNow.AddYears(2));

        TenantId tenantId = new(_tenantId);
        (ScimConfiguration config, string plainTextToken) = ScimConfiguration.Create(
            tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);

        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        // Handler uses fakeTime which is 2 years ahead — token expires in 1 year
        ScimBearerAuthenticationHandler handler = CreateHandler(fakeTime);
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = $"Bearer {plainTextToken}";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Invalid or expired SCIM token");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShortToken_NoMatchingConfig_ReturnsFail()
    {
        // Token shorter than 8 chars exercises the short-token prefix branch
        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Bearer abc";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Invalid or expired SCIM token");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_CaseInsensitiveBearerPrefix_ReturnsSuccess()
    {
        TenantId tenantId = new(_tenantId);
        (ScimConfiguration config, string plainTextToken) = ScimConfiguration.Create(
            tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);

        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = $"bearer {plainTextToken}";
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidToken_SetsTenantIdClaim()
    {
        TenantId tenantId = new(_tenantId);
        (ScimConfiguration config, string plainTextToken) = ScimConfiguration.Create(
            tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);

        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = $"Bearer {plainTextToken}";
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.HasClaim("tenant_id", _tenantId.ToString()).Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NullRemoteIpAddress_StillReturnsFail()
    {
        // Exercises the log path with null RemoteIpAddress
        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Bearer unknowntoken123";
        // RemoteIpAddress is null by default on DefaultHttpContext
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid or expired SCIM token");
    }
}
