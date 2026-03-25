using System.Text.Encodings.Web;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ScimBearerAuthenticationHandlerTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ScimBearerAuthenticationHandlerTests()
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

    private ScimBearerAuthenticationHandler CreateHandler()
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
            TimeProvider.System);
    }

    private async Task InitializeHandler(ScimBearerAuthenticationHandler handler, HttpContext context)
    {
        AuthenticationScheme scheme = new("ScimBearer", "SCIM Bearer", typeof(ScimBearerAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NoAuthorizationHeader_ReturnsNoResult()
    {
        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NonBearerScheme_ReturnsNoResult()
    {
        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_EmptyBearerToken_ReturnsFail()
    {
        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Bearer   ";
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Be("Empty Bearer token");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_InvalidToken_NoMatchingConfig_ReturnsFail()
    {
        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Bearer unknowntoken123";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Invalid or expired SCIM token");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidToken_ReturnsSuccess()
    {
        // Create a SCIM configuration with a known token
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
        result.Principal!.HasClaim("scim_client", "true").Should().BeTrue();
        result.Principal.HasClaim("auth_method", "scim_bearer").Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_TokenWithWrongHash_ReturnsFail()
    {
        // Create a SCIM configuration
        TenantId tenantId = new(_tenantId);
        (ScimConfiguration config, string _) = ScimConfiguration.Create(
            tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);

        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        // Use a token with the right prefix but wrong hash
        string tokenPrefix = config.TokenPrefix;
        string wrongToken = tokenPrefix + "wrongsuffixwrongsuffixwrongsuffixwrong";

        ScimBearerAuthenticationHandler handler = CreateHandler();
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = $"Bearer {wrongToken}";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        await InitializeHandler(handler, context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Invalid or expired SCIM token");
    }
}
