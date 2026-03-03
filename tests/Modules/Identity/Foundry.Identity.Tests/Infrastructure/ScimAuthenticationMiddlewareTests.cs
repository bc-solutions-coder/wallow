using System.Security.Claims;
using System.Text.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimAuthenticationMiddlewareTests
{
    private readonly ILogger<ScimAuthenticationMiddleware> _logger;
    private readonly IScimService _scimService;
    private readonly TenantContext _tenantContext;

    public ScimAuthenticationMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<ScimAuthenticationMiddleware>>();
        _scimService = Substitute.For<IScimService>();
        _tenantContext = new TenantContext { TenantId = TenantId.Create(Guid.NewGuid()) };
    }

    [Fact]
    public async Task InvokeAsync_NonScimPath_PassesThroughWithoutAuthentication()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/api/users";
        bool nextCalled = false;

        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        nextCalled.Should().BeTrue();
        context.User.Identity?.IsAuthenticated.Should().BeFalse();
        await _scimService.DidNotReceive().ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/scim/v2/ServiceProviderConfig")]
    [InlineData("/scim/v2/Schemas")]
    [InlineData("/scim/v2/ResourceTypes")]
    [InlineData("/SCIM/V2/serviceproviderconfig")] // Case insensitive
    public async Task InvokeAsync_DiscoveryEndpoint_BypassesAuthentication(string path)
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = path;
        bool nextCalled = false;

        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        nextCalled.Should().BeTrue();
        await _scimService.DidNotReceive().ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MissingAuthorizationHeader_Returns401()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Response.Body = new MemoryStream();

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.ContentType.Should().Contain("json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);

        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.ScimType.Should().Be("invalidCredentials");
        error.Detail.Should().Be("Missing Authorization header");
        error.Schemas.Should().Contain("urn:ietf:params:scim:api:messages:2.0:Error");
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz")] // Basic auth
    [InlineData("Digest abc123")] // Digest auth
    [InlineData("InvalidScheme token123")] // Unknown scheme
    public async Task InvokeAsync_InvalidBearerScheme_Returns401(string authHeader)
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = authHeader;
        context.Response.Body = new MemoryStream();

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
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
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = authHeader;
        context.Response.Body = new MemoryStream();

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
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
        // Arrange - "Bearer" without trailing space fails the prefix check
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer";
        context.Response.Body = new MemoryStream();

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);

        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.Detail.Should().Be("Invalid authorization scheme. Use Bearer token.");
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_Returns401WithScimError()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer invalid_token_12345";
        context.Response.Body = new MemoryStream();

        _scimService.ValidateTokenAsync("invalid_token_12345", Arg.Any<CancellationToken>())
            .Returns(false);

        ScimAuthenticationMiddleware middleware = new(_ => Task.CompletedTask, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ScimError? error = await JsonSerializer.DeserializeAsync<ScimError>(context.Response.Body);

        error.Should().NotBeNull();
        error.Status.Should().Be(401);
        error.ScimType.Should().Be("invalidCredentials");
        error.Detail.Should().Be("Invalid or expired SCIM token");

        await _scimService.Received(1).ValidateTokenAsync("invalid_token_12345", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_CreatesClaimsPrincipalWithCorrectClaims()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        TenantContext tenantContext = new() { TenantId = TenantId.Create(tenantId) };
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer valid_token_xyz";

        _scimService.ValidateTokenAsync("valid_token_xyz", Arg.Any<CancellationToken>())
            .Returns(true);

        ClaimsPrincipal? capturedPrincipal = null;
        ScimAuthenticationMiddleware middleware = new(ctx =>
        {
            capturedPrincipal = ctx.User;
            return Task.CompletedTask;
        }, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, tenantContext);

        // Assert
        capturedPrincipal.Should().NotBeNull();
        capturedPrincipal!.Identity.Should().NotBeNull();
        capturedPrincipal.Identity!.IsAuthenticated.Should().BeTrue();
        capturedPrincipal.Identity.AuthenticationType.Should().Be("ScimBearer");

        List<Claim> claims = capturedPrincipal.Claims.ToList();
        claims.Should().Contain(c => c.Type == "scim_client" && c.Value == "true");
        claims.Should().Contain(c => c.Type == "auth_method" && c.Value == "scim_bearer");
        claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_CallsNextMiddleware()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer valid_token_abc";
        bool nextCalled = false;

        _scimService.ValidateTokenAsync("valid_token_abc", Arg.Any<CancellationToken>())
            .Returns(true);

        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200); // Default status, not modified to 401
    }

    [Fact]
    public async Task InvokeAsync_BearerWithExtraSpaces_ExtractsTokenCorrectly()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "Bearer   token_with_spaces   ";
        bool nextCalled = false;

        _scimService.ValidateTokenAsync("token_with_spaces", Arg.Any<CancellationToken>())
            .Returns(true);

        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        nextCalled.Should().BeTrue();
        await _scimService.Received(1).ValidateTokenAsync("token_with_spaces", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CaseInsensitiveBearerScheme_WorksCorrectly()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/scim/v2/Users";
        context.Request.Headers.Authorization = "bearer lowercase_bearer_token";
        bool nextCalled = false;

        _scimService.ValidateTokenAsync("lowercase_bearer_token", Arg.Any<CancellationToken>())
            .Returns(true);

        ScimAuthenticationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);

        // Act
        await middleware.InvokeAsync(context, _scimService, _tenantContext);

        // Assert
        nextCalled.Should().BeTrue();
        await _scimService.Received(1).ValidateTokenAsync("lowercase_bearer_token", Arg.Any<CancellationToken>());
    }
}
