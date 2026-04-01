using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.Api.Endpoints;
using Wallow.Api.Services;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Api.Tests.Endpoints;

public sealed class SseEndpointTests
{
    private readonly SseConnectionManager _connectionManager = Substitute.For<SseConnectionManager>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly ILogger<SseConnectionManager> _logger = Substitute.For<ILogger<SseConnectionManager>>();

    private static readonly Guid _testTenantGuid = TestConstants.TestTenantId;
    private static readonly string _testUserId = TestConstants.AdminUserId.ToString();

    public SseEndpointTests()
    {
        _lifetime.ApplicationStopping.Returns(CancellationToken.None);
    }

    [Fact]
    public async Task HandleSseConnection_TenantNotResolved_Returns400()
    {
        _tenantContext.IsResolved.Returns(false);
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(400);
        _connectionManager.DidNotReceive().AddConnection(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_ValidRequest_CallsAddConnectionWithCorrectParameters()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            _testUserId,
            _testTenantGuid,
            Arg.Is<HashSet<string>>(m => m.Contains("notifications") && m.Count == 1),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_OnDisconnect_CallsRemoveConnection()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).RemoveConnection(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleSseConnection_SubscribeParam_PopulatesModuleSet()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "billing,storage,identity",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Is<HashSet<string>>(m =>
                m.Contains("billing") &&
                m.Contains("storage") &&
                m.Contains("identity") &&
                m.Count == 3),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_NullSubscribeParam_PassesEmptyModuleSet()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            null,
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Is<HashSet<string>>(m => m.Count == 0),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_ExtractsUserIdFromNameIdentifierClaim()
    {
        SetupResolvedTenant();
        string expectedUserId = "specific-user-id";
        DefaultHttpContext httpContext = CreateHttpContext(expectedUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            expectedUserId,
            Arg.Any<Guid>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_FallsBackToSubClaim_WhenNameIdentifierMissing()
    {
        SetupResolvedTenant();
        string expectedUserId = "sub-claim-user-id";
        DefaultHttpContext httpContext = CreateHttpContextWithSubClaim(expectedUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            expectedUserId,
            Arg.Any<Guid>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_ExtractsTenantIdFromTenantContext()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            Arg.Any<string>(),
            _testTenantGuid,
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_ExtractsPermissionsFromClaims()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContextWithPermissionsAndRoles(
            _testUserId,
            ["inquiries.view", "billing.manage"],
            ["admin"]);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<HashSet<string>>(),
            Arg.Is<HashSet<string>>(p =>
                p.Contains("inquiries.view") &&
                p.Contains("billing.manage") &&
                p.Count == 2),
            Arg.Any<HashSet<string>>());
    }

    [Fact]
    public async Task HandleSseConnection_ExtractsRolesFromClaims()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContextWithPermissionsAndRoles(
            _testUserId,
            [],
            ["admin", "manager"]);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        _connectionManager.Received(1).AddConnection(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<HashSet<string>>(),
            Arg.Any<HashSet<string>>(),
            Arg.Is<HashSet<string>>(r =>
                r.Contains("admin") &&
                r.Contains("manager") &&
                r.Count == 2));
    }

    [Fact]
    public async Task HandleSseConnection_SendsHeartbeat_WhenNoMessages()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);
        MemoryStream responseBody = (MemoryStream)httpContext.Response.Body;

        _connectionManager.GetReader(Arg.Any<string>())
            .Returns(System.Threading.Channels.Channel.CreateUnbounded<Wallow.Shared.Contracts.Realtime.RealtimeEnvelope>().Reader);

        using CancellationTokenSource cts = new();

        Task sseTask = SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        // Wait long enough for at least one heartbeat (15s timer)
        await Task.Delay(TimeSpan.FromSeconds(16));
        await cts.CancelAsync();
        await sseTask;

        responseBody.Position = 0;
        using StreamReader reader = new(responseBody);
        string responseContent = await reader.ReadToEndAsync();
        responseContent.Should().Contain(": heartbeat");
    }

    [Fact]
    public async Task HandleSseConnection_SetsCorrectResponseHeaders()
    {
        SetupResolvedTenant();
        DefaultHttpContext httpContext = CreateHttpContext(_testUserId);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await SseEndpoint.HandleSseConnection(
            httpContext,
            "notifications",
            _connectionManager,
            _tenantContext,
            _lifetime,
            _logger,
            cts.Token);

        httpContext.Response.ContentType.Should().Be("text/event-stream");
        httpContext.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }

    private void SetupResolvedTenant()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_testTenantGuid));
    }

    private static DefaultHttpContext CreateHttpContext(string userId)
    {
        ClaimsIdentity identity = new("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal,
            Response = { Body = new MemoryStream() },
        };

        return httpContext;
    }

    private static DefaultHttpContext CreateHttpContextWithSubClaim(string userId)
    {
        ClaimsIdentity identity = new("test");
        identity.AddClaim(new Claim("sub", userId));
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal,
            Response = { Body = new MemoryStream() },
        };

        return httpContext;
    }

    private static DefaultHttpContext CreateHttpContextWithPermissionsAndRoles(
        string userId,
        string[] permissions,
        string[] roles)
    {
        ClaimsIdentity identity = new("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));

        foreach (string permission in permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal,
            Response = { Body = new MemoryStream() },
        };

        return httpContext;
    }
}
