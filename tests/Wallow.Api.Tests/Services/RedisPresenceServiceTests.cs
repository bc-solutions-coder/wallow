using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Tests.Common.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Wallow.Api.Tests.Services;

[Trait("Category", "Integration")]
public class RedisPresenceServiceTests(RedisFixture fixture) : IClassFixture<RedisFixture>, IAsyncLifetime
{
    private static readonly Guid _testTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private ConnectionMultiplexer _multiplexer = null!;
    private RedisPresenceService _sut = null!;

    public async Task InitializeAsync()
    {
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString + ",allowAdmin=true");
        // Flush database between tests to ensure isolation
        IServer server = _multiplexer.GetServers()[0];
        await server.FlushDatabaseAsync();
        _sut = new RedisPresenceService(_multiplexer, NullLogger<RedisPresenceService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _multiplexer.DisposeAsync();
    }

    [Fact]
    public async Task TrackConnection_ShouldMakeUserOnline()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");

        bool isOnline = await _sut.IsUserOnlineAsync(_testTenantId, "user-1");
        isOnline.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveConnection_LastConnection_ShouldMakeUserOffline()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.RemoveConnectionAsync("conn-1");

        bool isOnline = await _sut.IsUserOnlineAsync(_testTenantId, "user-1");
        isOnline.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveConnection_OtherConnectionsExist_ShouldKeepUserOnline()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-2");

        await _sut.RemoveConnectionAsync("conn-1");

        bool isOnline = await _sut.IsUserOnlineAsync(_testTenantId, "user-1");
        isOnline.Should().BeTrue();
    }

    [Fact]
    public async Task SetPageContext_ShouldTrackUserOnPage()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.SetPageContextAsync(_testTenantId, "conn-1", "/dashboard");

        IReadOnlyList<UserPresence> users = await _sut.GetUsersOnPageAsync(_testTenantId, "/dashboard");
        users.Should().ContainSingle()
            .Which.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task GetUsersOnPage_ShouldReturnCorrectViewers()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.TrackConnectionAsync(_testTenantId, "user-2", "conn-2");
        await _sut.TrackConnectionAsync(_testTenantId, "user-3", "conn-3");

        await _sut.SetPageContextAsync(_testTenantId, "conn-1", "/tasks");
        await _sut.SetPageContextAsync(_testTenantId, "conn-2", "/tasks");
        await _sut.SetPageContextAsync(_testTenantId, "conn-3", "/dashboard");

        IReadOnlyList<UserPresence> tasksViewers = await _sut.GetUsersOnPageAsync(_testTenantId, "/tasks");
        tasksViewers.Should().HaveCount(2);
        tasksViewers.Select(u => u.UserId).Should().BeEquivalentTo(["user-1", "user-2"]);

        IReadOnlyList<UserPresence> dashboardViewers = await _sut.GetUsersOnPageAsync(_testTenantId, "/dashboard");
        dashboardViewers.Should().ContainSingle()
            .Which.UserId.Should().Be("user-3");
    }

    [Fact]
    public async Task GetUserIdByConnection_ShouldReturnCorrectUserId()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");

        string? userId = await _sut.GetUserIdByConnectionAsync("conn-1");
        userId.Should().Be("user-1");
    }

    [Fact]
    public async Task GetUserIdByConnection_UnknownConnection_ShouldReturnNull()
    {
        string? userId = await _sut.GetUserIdByConnectionAsync("unknown-conn");
        userId.Should().BeNull();
    }

    [Fact]
    public async Task GetOnlineUsers_ShouldReturnAllConnectedUsers()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-2");
        await _sut.TrackConnectionAsync(_testTenantId, "user-2", "conn-3");

        IReadOnlyList<UserPresence> onlineUsers = await _sut.GetOnlineUsersAsync(_testTenantId);
        onlineUsers.Should().HaveCount(2);

        UserPresence user1 = onlineUsers.Single(u => u.UserId == "user-1");
        user1.ConnectionIds.Should().BeEquivalentTo(["conn-1", "conn-2"]);

        UserPresence user2 = onlineUsers.Single(u => u.UserId == "user-2");
        user2.ConnectionIds.Should().BeEquivalentTo(["conn-3"]);
    }

    [Fact]
    public async Task SetPageContext_ShouldRemoveFromOldPage()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.SetPageContextAsync(_testTenantId, "conn-1", "/tasks");
        await _sut.SetPageContextAsync(_testTenantId, "conn-1", "/dashboard");

        IReadOnlyList<UserPresence> tasksViewers = await _sut.GetUsersOnPageAsync(_testTenantId, "/tasks");
        tasksViewers.Should().BeEmpty();

        IReadOnlyList<UserPresence> dashboardViewers = await _sut.GetUsersOnPageAsync(_testTenantId, "/dashboard");
        dashboardViewers.Should().ContainSingle()
            .Which.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task RemoveConnection_ShouldCleanUpPageContext()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.SetPageContextAsync(_testTenantId, "conn-1", "/tasks");

        await _sut.RemoveConnectionAsync("conn-1");

        IReadOnlyList<UserPresence> viewers = await _sut.GetUsersOnPageAsync(_testTenantId, "/tasks");
        viewers.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveConnection_UnknownConnection_DoesNotThrow()
    {
        Func<Task> act = () => _sut.RemoveConnectionAsync("non-existent-conn");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetOnlineUsers_WithPageContext_IncludesPageInfo()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.SetPageContextAsync(_testTenantId, "conn-1", "/dashboard");

        IReadOnlyList<UserPresence> onlineUsers = await _sut.GetOnlineUsersAsync(_testTenantId);

        UserPresence user = onlineUsers.Single();
        user.UserId.Should().Be("user-1");
        user.CurrentPages.Should().Contain("/dashboard");
    }

    [Fact]
    public async Task GetUsersOnPage_StaleConnection_IsSkipped()
    {
        await _sut.TrackConnectionAsync(_testTenantId, "user-1", "conn-1");
        await _sut.SetPageContextAsync(_testTenantId, "conn-1", "/tasks");

        // Remove the connection but not the page viewers set
        // This simulates a stale entry
        IDatabase db = _multiplexer.GetDatabase();
        await db.HashDeleteAsync($"presence:{_testTenantId}:conn2user", "conn-1");

        IReadOnlyList<UserPresence> viewers = await _sut.GetUsersOnPageAsync(_testTenantId, "/tasks");
        viewers.Should().BeEmpty();
    }

    [Fact]
    public async Task IsUserOnline_UnknownUser_ReturnsFalse()
    {
        bool isOnline = await _sut.IsUserOnlineAsync(_testTenantId, "unknown-user");

        isOnline.Should().BeFalse();
    }

    [Fact]
    public async Task GetOnlineUsers_NoUsers_ReturnsEmptyList()
    {
        IReadOnlyList<UserPresence> onlineUsers = await _sut.GetOnlineUsersAsync(_testTenantId);

        onlineUsers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersOnPage_NoViewers_ReturnsEmptyList()
    {
        IReadOnlyList<UserPresence> viewers = await _sut.GetUsersOnPageAsync(_testTenantId, "/empty-page");

        viewers.Should().BeEmpty();
    }
}
