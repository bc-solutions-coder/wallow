using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ScimAuthenticationMiddlewareTests : IDisposable
{
    private readonly ILogger<ScimAuthenticationMiddleware> _logger;
    private readonly IdentityDbContext _dbContext;
    private readonly TenantContext _tenantContext;

    public ScimAuthenticationMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<ScimAuthenticationMiddleware>>();
        _tenantContext = new TenantContext();

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private ScimAuthenticationMiddleware CreateMiddleware(RequestDelegate? next = null)
        => new(next ?? (_ => Task.CompletedTask), _logger);

    [Fact]
    public async Task InvokeAsync_NonScimPath_PassesThroughWithoutAuthentication()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/api/users";
        bool nextCalled = false;
        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        nextCalled.Should().BeTrue();
        context.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Theory]
    [InlineData("/scim/v2/ServiceProviderConfig")]
    [InlineData("/scim/v2/Schemas")]
    [InlineData("/scim/v2/ResourceTypes")]
    [InlineData("/SCIM/V2/serviceproviderconfig")]
    public async Task InvokeAsync_DiscoveryEndpoint_BypassesAuthentication(string path)
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = path;
        bool nextCalled = false;
        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingAuthorizationHeader_Returns401()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Response.Body = new MemoryStream();

        await CreateMiddleware().InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.ContentType.Should().Contain("json");
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);
        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.ScimType.Should().Be("invalidCredentials");
        error.Detail.Should().Be("Missing Authorization header");
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Digest abc123")]
    [InlineData("InvalidScheme token123")]
    public async Task InvokeAsync_InvalidBearerScheme_Returns401(string authHeader)
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = authHeader;
        context.Response.Body = new MemoryStream();

        await CreateMiddleware().InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);
        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.Detail.Should().Be("Invalid authorization scheme. Use Bearer token.");
    }

    [Theory]
    [InlineData("Bearer ")]
    [InlineData("Bearer   ")]
    public async Task InvokeAsync_EmptyBearerToken_Returns401(string authHeader)
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = authHeader;
        context.Response.Body = new MemoryStream();

        await CreateMiddleware().InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);
        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.Detail.Should().Be("Empty Bearer token");
    }

    [Fact]
    public async Task InvokeAsync_BearerWithoutSpace_Returns401()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer";
        context.Response.Body = new MemoryStream();

        await CreateMiddleware().InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);
        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.Detail.Should().Be("Invalid authorization scheme. Use Bearer token.");
    }

    [Fact]
    public async Task InvokeAsync_TokenWithNoMatchingConfig_Returns401()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer unknowntoken12345";
        context.Response.Body = new MemoryStream();

        await CreateMiddleware().InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);
        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.ScimType.Should().Be("invalidCredentials");
        error.Detail.Should().Be("Invalid or expired SCIM token");
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_AuthenticatesAndSetsTenant()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        (ScimConfiguration config, string plainToken) = ScimConfiguration.Create(tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);
        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = $"Bearer {plainToken}";
        ClaimsPrincipal? capturedPrincipal = null;
        ScimAuthenticationMiddleware middleware = new(ctx =>
        {
            capturedPrincipal = ctx.User;
            return Task.CompletedTask;
        }, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(200);
        _tenantContext.TenantId.Should().Be(tenantId);
        _tenantContext.IsResolved.Should().BeTrue();
        capturedPrincipal.Should().NotBeNull();
        capturedPrincipal!.Identity!.IsAuthenticated.Should().BeTrue();
        capturedPrincipal.Identity.AuthenticationType.Should().Be("ScimBearer");
        List<Claim> claims = capturedPrincipal.Claims.ToList();
        claims.Should().Contain(c => c.Type == "scim_client" && c.Value == "true");
        claims.Should().Contain(c => c.Type == "auth_method" && c.Value == "scim_bearer");
        claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.Value.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_CallsNextMiddleware()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        (ScimConfiguration config, string plainToken) = ScimConfiguration.Create(tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);
        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = $"Bearer {plainToken}";
        bool nextCalled = false;
        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_DisabledConfig_Returns401()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        (ScimConfiguration config, string plainToken) = ScimConfiguration.Create(tenantId, Guid.NewGuid(), TimeProvider.System);
        // config is disabled by default — do NOT call Enable
        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = $"Bearer {plainToken}";
        context.Response.Body = new MemoryStream();

        await CreateMiddleware().InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_CaseInsensitiveBearerScheme_WorksCorrectly()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        (ScimConfiguration config, string plainToken) = ScimConfiguration.Create(tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);
        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = $"bearer {plainToken}";
        bool nextCalled = false;
        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WrongTokenForPrefix_Returns401()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        (ScimConfiguration config, string plainToken) = ScimConfiguration.Create(tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);
        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        // Use the correct prefix but tamper with the rest of the token
        string prefix = plainToken[..8];
        string tamperedToken = prefix + "TAMPERED_SUFFIX_THAT_WONT_MATCH";

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = $"Bearer {tamperedToken}";
        context.Response.Body = new MemoryStream();

        await CreateMiddleware().InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
