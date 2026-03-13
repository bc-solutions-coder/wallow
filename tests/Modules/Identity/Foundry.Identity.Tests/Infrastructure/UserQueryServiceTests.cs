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

public class UserQueryServiceTests
{
    private readonly ILogger<UserQueryService> _logger = Substitute.For<ILogger<UserQueryService>>();
    private readonly HybridCache _cache = new NoOpHybridCache();

    [Fact]
    public async Task GetNewUsersCountAsync_WithMembersInRange_ReturnsCount()
    {
        Guid tenantId = Guid.NewGuid();
        DateTime from = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        long fromTs = new DateTimeOffset(from).ToUnixTimeMilliseconds();
        _ = new DateTimeOffset(to).ToUnixTimeMilliseconds();
        long inRangeTs = fromTs + 86400000; // one day after start
        long beforeTs = fromTs - 86400000;

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = inRangeTs },
                new { id = "u2", email = "u2@test.com", enabled = true, createdTimestamp = beforeTs }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, from, to);

        result.Should().Be(1);
    }

    [Fact]
    public async Task GetNewUsersCountAsync_NoMembers_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", Array.Empty<object>());

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetNewUsersCountAsync_WhenException_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/organizations/{tenantId}/members");

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_WithEnabledRecentUsers_ReturnsCount()
    {
        Guid tenantId = Guid.NewGuid();
        long recentTs = DateTimeOffset.UtcNow.AddDays(-10).ToUnixTimeMilliseconds();
        long oldTs = DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeMilliseconds();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = recentTs },
                new { id = "u2", email = "u2@test.com", enabled = true, createdTimestamp = oldTs },
                new { id = "u3", email = "u3@test.com", enabled = false, createdTimestamp = recentTs }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(1); // Only u1: enabled + recent
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_NoMembers_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/organizations/{tenantId}/members", HttpStatusCode.NotFound);

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_WhenException_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/organizations/{tenantId}/members");

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalUsersCountAsync_ReturnsTotal()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com" },
                new { id = "u2", email = "u2@test.com" },
                new { id = "u3", email = "u3@test.com" }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetTotalUsersCountAsync(tenantId);

        result.Should().Be(3);
    }

    [Fact]
    public async Task GetTotalUsersCountAsync_WhenException_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/organizations/{tenantId}/members");

        UserQueryService service = CreateService(handler);

        int result = await service.GetTotalUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

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
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _routes = new Dictionary<string, (HttpStatusCode Status, object? Content)>();
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
