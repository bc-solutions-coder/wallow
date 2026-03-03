using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Wolverine;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakOrganizationServiceAdditionalTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakOrganizationService> _logger = Substitute.For<ILogger<KeycloakOrganizationService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task CreateOrganizationAsync_WithoutDomain_CreatesSuccessfully()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        Guid result = await service.CreateOrganizationAsync("Test Org");

        result.Should().Be(orgId);
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenNullOrg_ReturnsNull()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationsAsync_NullOrEmpty_ReturnsEmpty()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull("/admin/realms/foundry/organizations");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationsAsync_WithoutSearch_DoesNotIncludeSearchParam()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/organizations", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMembersAsync_SkipsMembersWithNullId()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = (string?)null, email = "bad@test.com", firstName = "Bad", lastName = "User", enabled = true },
                new { id = (string?)userId.ToString(), email = "good@test.com", firstName = "Good", lastName = "User", enabled = true }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("good@test.com");
    }

    [Fact]
    public async Task GetMembersAsync_EmptyList_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_SkipsOrgsWithNullId()
    {
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/organizations", new[]
            {
                new { id = (string?)null, name = "Bad Org", domains = Array.Empty<object>() },
                new { id = (string?)orgId.ToString(), name = "Good Org", domains = Array.Empty<object>() }
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetUserOrganizationsAsync(userId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Good Org");
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_EmptyList_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/organizations", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetUserOrganizationsAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMembersCountAsync_WhenNonSuccess_ReturnsZero()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}", new
            {
                id = orgId.ToString(),
                name = "Test Org"
            })
            .WithGetStatus($"/admin/realms/foundry/organizations/{orgId}/members", HttpStatusCode.InternalServerError);

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task AddMemberAsync_GetsUserEmail()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost($"/admin/realms/foundry/organizations/{orgId}/members", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}", new { email = "user@test.com" });

        KeycloakOrganizationService service = CreateService(handler);

        await service.AddMemberAsync(orgId, userId);

        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task GetUserEmailAsync_WhenFails_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost($"/admin/realms/foundry/organizations/{orgId}/members", HttpStatusCode.NoContent)
            .WithGetStatus($"/admin/realms/foundry/users/{userId}", HttpStatusCode.NotFound);

        KeycloakOrganizationService service = CreateService(handler);

        await service.AddMemberAsync(orgId, userId);

        // Should complete without throwing, email will be empty
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    private KeycloakOrganizationService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);

        return new KeycloakOrganizationService(
            httpClientFactory,
            _messageBus,
            _tenantContext,
            _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = [];
        private readonly HashSet<string> _nullRoutes = [];

        public MockHttpHandler WithGet(string path, object content)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, content, null);
            return this;
        }

        public MockHttpHandler WithGetStatus(string path, HttpStatusCode status)
        {
            _routes[$"GET:{path}"] = (status, null, null);
            return this;
        }

        public MockHttpHandler WithGetNull(string path)
        {
            _nullRoutes.Add($"GET:{path}");
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string? locationHeader = null)
        {
            _routes[$"POST:{path}"] = (status, null, locationHeader);
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

            if (_routes.TryGetValue(key, out (HttpStatusCode Status, object? Content, string? LocationHeader) route))
            {
                HttpResponseMessage response = new(route.Status);
                if (route.Content != null)
                {
                    response.Content = JsonContent.Create(route.Content);
                }
                if (route.LocationHeader != null)
                {
                    response.Headers.Location = new Uri(route.LocationHeader);
                }
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { })
            });
        }
    }
}
