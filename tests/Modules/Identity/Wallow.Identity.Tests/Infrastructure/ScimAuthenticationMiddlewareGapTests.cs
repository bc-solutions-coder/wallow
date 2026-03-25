using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ScimAuthenticationMiddlewareGapTests : IDisposable
{
    private readonly ILogger<ScimAuthenticationMiddleware> _logger = Substitute.For<ILogger<ScimAuthenticationMiddleware>>();
    private readonly TenantContext _tenantContext = new TenantContext();
    private readonly IdentityDbContext _dbContext;

    public ScimAuthenticationMiddlewareGapTests()
    {
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

    [Fact]
    public async Task InvokeAsync_ShortToken_Returns401WhenNoConfigMatches()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer abc";
        context.Response.Body = new MemoryStream();

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_ExpiredToken_Returns401()
    {
        FakeTimeProvider fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        (ScimConfiguration config, string plainToken) = ScimConfiguration.Create(tenantId, Guid.NewGuid(), fakeTime);
        config.Enable(Guid.NewGuid(), fakeTime);
        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        // Advance time past token expiry (tokens typically expire after some period)
        fakeTime.Advance(TimeSpan.FromDays(400));

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = $"Bearer {plainToken}";
        context.Response.Body = new MemoryStream();

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, fakeTime);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_NonScimPath_DoesNotSetTenantContext()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/users";
        bool nextCalled = false;

        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        nextCalled.Should().BeTrue();
        _tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_DiscoveryEndpoint_DoesNotRequireAuth()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/ResourceTypes";
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
    public async Task InvokeAsync_ScimEndpointNoAuth_DoesNotCallNext()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Groups";
        context.Response.Body = new MemoryStream();
        bool nextCalled = false;

        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_ScimEndpointInvalidScheme_ResponseContentTypeIsScimJson()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";
        context.Response.Body = new MemoryStream();

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.Response.ContentType.Should().Contain("json");
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_SetsClaimsIdentityWithScimBearerScheme()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        (ScimConfiguration config, string plainToken) = ScimConfiguration.Create(tenantId, Guid.NewGuid(), TimeProvider.System);
        config.Enable(Guid.NewGuid(), TimeProvider.System);
        _dbContext.ScimConfigurations.Add(config);
        await _dbContext.SaveChangesAsync();

        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = $"Bearer {plainToken}";

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        await middleware.InvokeAsync(context, _dbContext, _tenantContext, TimeProvider.System);

        context.User.Identity!.AuthenticationType.Should().Be("ScimBearer");
    }
}
