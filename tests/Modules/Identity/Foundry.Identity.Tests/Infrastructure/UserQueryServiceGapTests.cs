using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Tests.Common.Fakes;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class UserQueryServiceGapTests
{
    private readonly ILogger<UserQueryService> _logger = Substitute.For<ILogger<UserQueryService>>();
    private readonly HybridCache _cache = new NoOpHybridCache();

    // ── GetUserEmailAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetUserEmailAsync_WhenSuccess_ReturnsEmail()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new { email = "user@test.com" });

        UserQueryService service = CreateService(handler);

        string result = await service.GetUserEmailAsync(userId);

        result.Should().Be("user@test.com");
    }

    [Fact]
    public async Task GetUserEmailAsync_WhenNotSuccess_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/users/{userId}", HttpStatusCode.NotFound);

        UserQueryService service = CreateService(handler);

        string result = await service.GetUserEmailAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserEmailAsync_WhenNullEmail_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new { email = (string?)null });

        UserQueryService service = CreateService(handler);

        string result = await service.GetUserEmailAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserEmailAsync_WhenException_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/users/{userId}");

        UserQueryService service = CreateService(handler);

        string result = await service.GetUserEmailAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserEmailAsync_WhenServerError_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/users/{userId}", HttpStatusCode.InternalServerError);

        UserQueryService service = CreateService(handler);

        string result = await service.GetUserEmailAsync(userId);

        result.Should().BeEmpty();
    }

    // ── GetNewUsersCountAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetNewUsersCountAsync_WhenMembersApiNotSuccess_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/organizations/{tenantId}/members", HttpStatusCode.Forbidden);

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetNewUsersCountAsync_AllMembersOutsideRange_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        DateTime from = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        long beforeRange = new DateTimeOffset(from).ToUnixTimeMilliseconds() - 86400000;
        long afterRange = new DateTimeOffset(to).ToUnixTimeMilliseconds() + 86400000;

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = beforeRange },
                new { id = "u2", email = "u2@test.com", enabled = true, createdTimestamp = afterRange }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, from, to);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetNewUsersCountAsync_MultipleInRange_ReturnsCorrectCount()
    {
        Guid tenantId = Guid.NewGuid();
        DateTime from = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        long fromTs = new DateTimeOffset(from).ToUnixTimeMilliseconds();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = fromTs + 1000 },
                new { id = "u2", email = "u2@test.com", enabled = true, createdTimestamp = fromTs + 2000 },
                new { id = "u3", email = "u3@test.com", enabled = true, createdTimestamp = fromTs + 3000 }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, from, to);

        result.Should().Be(3);
    }

    // ── GetActiveUsersCountAsync ────────────────────────────────────────

    [Fact]
    public async Task GetActiveUsersCountAsync_WhenMembersApiNotSuccess_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/organizations/{tenantId}/members", HttpStatusCode.Forbidden);

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_EnabledButOldUser_NotCountedAsActive()
    {
        Guid tenantId = Guid.NewGuid();
        long oldTs = DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeMilliseconds();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = (long?)oldTs }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_DisabledRecentUser_NotCountedAsActive()
    {
        Guid tenantId = Guid.NewGuid();
        long recentTs = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeMilliseconds();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = false, createdTimestamp = (long?)recentTs }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_MixedUsers_ReturnsOnlyActiveCount()
    {
        Guid tenantId = Guid.NewGuid();
        long recentTs = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeMilliseconds();
        long oldTs = DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeMilliseconds();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = (long?)recentTs },
                new { id = "u2", email = "u2@test.com", enabled = true, createdTimestamp = (long?)oldTs },
                new { id = "u3", email = "u3@test.com", enabled = false, createdTimestamp = (long?)recentTs },
                new { id = "u4", email = "u4@test.com", enabled = true, createdTimestamp = (long?)null }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(2); // u1 (enabled+recent) and u4 (enabled+no timestamp)
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_EmptyMembersList_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", Array.Empty<object>());

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    // ── GetTotalUsersCountAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetTotalUsersCountAsync_WhenMembersApiNotSuccess_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/organizations/{tenantId}/members", HttpStatusCode.Forbidden);

        UserQueryService service = CreateService(handler);

        int result = await service.GetTotalUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalUsersCountAsync_EmptyList_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", Array.Empty<object>());

        UserQueryService service = CreateService(handler);

        int result = await service.GetTotalUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalUsersCountAsync_SingleMember_ReturnsOne()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com" }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetTotalUsersCountAsync(tenantId);

        result.Should().Be(1);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private UserQueryService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        return new UserQueryService(httpClientFactory, _cache, Options.Create(new KeycloakOptions()), _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _routes = new();
        private readonly HashSet<string> _throwRoutes = [];

        public MockHttpHandler WithGet(string path, object content)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, content);
            return this;
        }

        public MockHttpHandler WithGetStatus(string path, HttpStatusCode status)
        {
            _routes[$"GET:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithGetThrow(string path)
        {
            _throwRoutes.Add($"GET:{path}");
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            string key = $"{request.Method}:{path}";

            if (_throwRoutes.Contains(key))
            {
                throw new HttpRequestException("Simulated failure");
            }

            if (_routes.TryGetValue(key, out (HttpStatusCode Status, object? Content) route))
            {
                HttpResponseMessage response = new(route.Status);
                if (route.Content != null)
                {
                    response.Content = JsonContent.Create(route.Content);
                }
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            });
        }
    }
}
