using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Tests.Common.Fakes;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000
// HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class UserQueryServiceAdditionalTests
{
    private readonly ILogger<UserQueryService> _logger = Substitute.For<ILogger<UserQueryService>>();
    private readonly HybridCache _cache = new NoOpHybridCache();

    [Fact]
    public async Task GetNewUsersCountAsync_WhenNullReturned_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/organizations/{tenantId}/members");

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_WhenNullReturned_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/organizations/{tenantId}/members");

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalUsersCountAsync_NullMembers_ReturnsZero()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/organizations/{tenantId}/members");

        UserQueryService service = CreateService(handler);

        int result = await service.GetTotalUsersCountAsync(tenantId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveUsersCountAsync_WithNullCreatedTimestamp_IncludesAsActive()
    {
        Guid tenantId = Guid.NewGuid();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = (long?)null }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetActiveUsersCountAsync(tenantId);

        // Enabled with no createdTimestamp: !HasValue is true, so passes filter
        result.Should().Be(1);
    }

    [Fact]
    public async Task GetNewUsersCountAsync_WithExactBoundaryTimestamps_HandlesCorrectly()
    {
        Guid tenantId = Guid.NewGuid();
        DateTime from = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        long fromTs = new DateTimeOffset(from).ToUnixTimeMilliseconds();
        long toTs = new DateTimeOffset(to).ToUnixTimeMilliseconds();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{tenantId}/members", new[]
            {
                // Exactly at from - should be included (>=)
                new { id = "u1", email = "u1@test.com", enabled = true, createdTimestamp = fromTs },
                // Exactly at to - should be excluded (<)
                new { id = "u2", email = "u2@test.com", enabled = true, createdTimestamp = toTs },
                // In between - should be included
                new { id = "u3", email = "u3@test.com", enabled = true, createdTimestamp = fromTs + 1000 }
            });

        UserQueryService service = CreateService(handler);

        int result = await service.GetNewUsersCountAsync(tenantId, from, to);

        result.Should().Be(2); // u1 and u3
    }

    [Fact]
    public async Task GetOrganizationMembersAsync_WhenNotSuccess_ReturnsNull()
    {
        Guid tenantId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/organizations/{tenantId}/members", HttpStatusCode.InternalServerError);

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
        private readonly HashSet<string> _nullRoutes = [];

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

        public MockHttpHandler WithGetNull(string path)
        {
            _nullRoutes.Add($"GET:{path}");
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            string key = $"{request.Method}:{path}";

            if (_nullRoutes.Contains(key))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                });
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
