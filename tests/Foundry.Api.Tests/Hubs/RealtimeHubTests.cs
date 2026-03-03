using System.Security.Claims;
using Foundry.Api.Hubs;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Tests.Common.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Api.Tests.Hubs;

public sealed class RealtimeHubTests : IDisposable
{
    private readonly IPresenceService _presenceService = Substitute.For<IPresenceService>();
    private readonly IRealtimeDispatcher _dispatcher = Substitute.For<IRealtimeDispatcher>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly RealtimeHub _hub;

    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();

    private static readonly Guid _tenantGuid = TestConstants.TestTenantId;

    public RealtimeHubTests()
    {
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantGuid));

        _hub = new RealtimeHub(
            _presenceService,
            _dispatcher,
            _tenantContext,
            NullLogger<RealtimeHub>.Instance)
        {
            Clients = _clients,
            Groups = _groups,
            Context = _context
        };
    }

    private void SetupAuthenticatedUser(string userId)
    {
        ClaimsIdentity identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim("organization", _tenantGuid.ToString()));
        ClaimsPrincipal principal = new ClaimsPrincipal(identity);
        _context.User.Returns(principal);
        _context.ConnectionId.Returns($"conn-{userId}");
    }

    private void SetupUserWithSubClaim(string userId)
    {
        ClaimsIdentity identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("sub", userId));
        ClaimsPrincipal principal = new ClaimsPrincipal(identity);
        _context.User.Returns(principal);
        _context.ConnectionId.Returns($"conn-{userId}");
    }

    private void SetupUnauthenticatedUser()
    {
        _context.User.Returns((ClaimsPrincipal?)null);
        _context.ConnectionId.Returns("conn-anon");
    }

    [Fact]
    public async Task OnConnectedAsync_WithAuthenticatedUser_TracksConnectionAndSendsPresence()
    {
        SetupAuthenticatedUser("user-1");

        await _hub.OnConnectedAsync();

        await _presenceService.Received(1).TrackConnectionAsync("user-1", "conn-user-1");
        await _dispatcher.Received(1).SendToAllAsync(
            Arg.Is<RealtimeEnvelope>(e => e.Module == "Presence" && e.Type == "UserOnline"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_WithSubClaim_TracksConnectionCorrectly()
    {
        SetupUserWithSubClaim("user-sub");

        await _hub.OnConnectedAsync();

        await _presenceService.Received(1).TrackConnectionAsync("user-sub", "conn-user-sub");
    }

    [Fact]
    public async Task OnConnectedAsync_WithNullUser_AbortsConnection()
    {
        SetupUnauthenticatedUser();

        await _hub.OnConnectedAsync();

        _context.Received(1).Abort();
        await _presenceService.DidNotReceive().TrackConnectionAsync(Arg.Any<string>(), Arg.Any<string>());
        await _dispatcher.DidNotReceive().SendToAllAsync(Arg.Any<RealtimeEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_WithNoUserIdClaims_AbortsConnection()
    {
        ClaimsIdentity identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.Email, "test@example.com"));
        ClaimsPrincipal principal = new ClaimsPrincipal(identity);
        _context.User.Returns(principal);
        _context.ConnectionId.Returns("conn-no-id");

        await _hub.OnConnectedAsync();

        _context.Received(1).Abort();
    }

    [Fact]
    public async Task OnDisconnectedAsync_UserGoesOffline_SendsOfflinePresence()
    {
        string connectionId = "conn-1";
        _context.ConnectionId.Returns(connectionId);
        _presenceService.GetUserIdByConnectionAsync(connectionId, Arg.Any<CancellationToken>())
            .Returns("user-1");
        _presenceService.IsUserOnlineAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(false);

        await _hub.OnDisconnectedAsync(null);

        await _presenceService.Received(1).RemoveConnectionAsync(connectionId, Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendToAllAsync(
            Arg.Is<RealtimeEnvelope>(e => e.Module == "Presence" && e.Type == "UserOffline"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_UserStillOnline_DoesNotSendOfflinePresence()
    {
        string connectionId = "conn-1";
        _context.ConnectionId.Returns(connectionId);
        _presenceService.GetUserIdByConnectionAsync(connectionId, Arg.Any<CancellationToken>())
            .Returns("user-1");
        _presenceService.IsUserOnlineAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(true);

        await _hub.OnDisconnectedAsync(null);

        await _presenceService.Received(1).RemoveConnectionAsync(connectionId, Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendToAllAsync(
            Arg.Is<RealtimeEnvelope>(e => e.Type == "UserOffline"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_UnknownConnection_DoesNotSendPresence()
    {
        string connectionId = "conn-unknown";
        _context.ConnectionId.Returns(connectionId);
        _presenceService.GetUserIdByConnectionAsync(connectionId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await _hub.OnDisconnectedAsync(null);

        await _presenceService.Received(1).RemoveConnectionAsync(connectionId, Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendToAllAsync(Arg.Any<RealtimeEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_StillCleansUp()
    {
        string connectionId = "conn-err";
        _context.ConnectionId.Returns(connectionId);
        _presenceService.GetUserIdByConnectionAsync(connectionId, Arg.Any<CancellationToken>())
            .Returns("user-err");
        _presenceService.IsUserOnlineAsync("user-err", Arg.Any<CancellationToken>())
            .Returns(false);

        await _hub.OnDisconnectedAsync(new InvalidOperationException("test error"));

        await _presenceService.Received(1).RemoveConnectionAsync(connectionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_AddsConnectionToGroup()
    {
        _context.ConnectionId.Returns("conn-1");
        string groupId = $"tenant:{_tenantGuid}:resource:123";

        await _hub.JoinGroup(groupId);

        await _groups.Received(1).AddToGroupAsync("conn-1", groupId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGroup_RemovesConnectionFromGroup()
    {
        _context.ConnectionId.Returns("conn-1");
        string groupId = $"tenant:{_tenantGuid}:resource:456";

        await _hub.LeaveGroup(groupId);

        await _groups.Received(1).RemoveFromGroupAsync("conn-1", groupId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_WithMatchingTenant_AddsToGroup()
    {
        _context.ConnectionId.Returns("conn-1");
        string groupId = $"tenant:{_tenantGuid}:resource:123";

        await _hub.JoinGroup(groupId);

        await _groups.Received(1).AddToGroupAsync("conn-1", groupId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_WithDifferentTenant_ThrowsHubException()
    {
        _context.ConnectionId.Returns("conn-1");
        Guid otherTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        string groupId = $"tenant:{otherTenantId}:resource:123";

        Func<Task> act = () => _hub.JoinGroup(groupId);

        await act.Should().ThrowAsync<HubException>().WithMessage("*tenant mismatch*");
    }

    [Fact]
    public async Task LeaveGroup_WithDifferentTenant_ThrowsHubException()
    {
        _context.ConnectionId.Returns("conn-1");
        Guid otherTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        string groupId = $"tenant:{otherTenantId}:resource:456";

        Func<Task> act = () => _hub.LeaveGroup(groupId);

        await act.Should().ThrowAsync<HubException>().WithMessage("*tenant mismatch*");
    }

    [Fact]
    public async Task JoinGroup_WithNonTenantPrefix_AllowsJoin()
    {
        _context.ConnectionId.Returns("conn-1");
        string groupId = "page:/dashboard";

        await _hub.JoinGroup(groupId);

        await _groups.Received(1).AddToGroupAsync("conn-1", groupId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_WithMalformedTenantGroup_ThrowsHubException()
    {
        _context.ConnectionId.Returns("conn-1");
        string groupId = "tenant:not-a-guid";

        Func<Task> act = () => _hub.JoinGroup(groupId);

        await act.Should().ThrowAsync<HubException>().WithMessage("*Invalid tenant group format*");
    }

    [Fact]
    public async Task UpdatePageContext_WithAuthenticatedUser_SetsContextAndSendsViewers()
    {
        SetupAuthenticatedUser("user-1");
        IClientProxy groupProxy = Substitute.For<IClientProxy>();
        _clients.Group("page:/dashboard").Returns(groupProxy);

        List<UserPresence> viewers = [new UserPresence("user-1", null, ["conn-user-1"], ["/dashboard"])];
        _presenceService.GetUsersOnPageAsync("/dashboard", Arg.Any<CancellationToken>())
            .Returns(viewers);

        await _hub.UpdatePageContext("/dashboard");

        await _presenceService.Received(1).SetPageContextAsync("conn-user-1", "/dashboard", Arg.Any<CancellationToken>());
        await _groups.Received(1).AddToGroupAsync("conn-user-1", "page:/dashboard", Arg.Any<CancellationToken>());
        await groupProxy.Received(1).SendCoreAsync(
            "ReceivePresence",
            Arg.Is<object?[]>(args => args.Length == 1 && MatchesEnvelopeType(args[0], "PageViewersUpdated")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePageContext_WithNullUser_ReturnsEarly()
    {
        SetupUnauthenticatedUser();

        await _hub.UpdatePageContext("/dashboard");

        await _presenceService.DidNotReceive().SetPageContextAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _hub.Dispose();
    }

    private static bool MatchesEnvelopeType(object? obj, string type)
        => obj is RealtimeEnvelope e && e.Type == type;
}
