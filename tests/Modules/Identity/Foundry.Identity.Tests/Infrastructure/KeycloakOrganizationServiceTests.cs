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

public class KeycloakOrganizationServiceTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakOrganizationService> _logger = Substitute.For<ILogger<KeycloakOrganizationService>>();
    private readonly TenantId _testTenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task CreateOrganizationAsync_Success_ReturnsOrgId()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        Guid result = await service.CreateOrganizationAsync("Test Org", "test.com");

        result.Should().Be(orgId);
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task CreateOrganizationAsync_MissingLocationHeader_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created);

        KeycloakOrganizationService service = CreateService(handler);

        Func<Task> act = async () => await service.CreateOrganizationAsync("Test Org");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenExists_ReturnsDto()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}", new
            {
                id = orgId.ToString(),
                name = "Org One",
                domains = new[] { new { name = "org.com" } }
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result.Name.Should().Be("Org One");
        result.Domain.Should().Be("org.com");
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenNotFound_ReturnsNull()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/organizations/{orgId}", HttpStatusCode.NotFound);

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenException_ReturnsNull()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationsAsync_ReturnsOrgList()
    {
        Guid orgId1 = Guid.NewGuid();
        Guid orgId2 = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/organizations", new[]
            {
                new { id = orgId1.ToString(), name = "Org 1", domains = Array.Empty<object>() },
                new { id = orgId2.ToString(), name = "Org 2", domains = Array.Empty<object>() }
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId1}/members", Array.Empty<object>())
            .WithGet($"/admin/realms/foundry/organizations/{orgId2}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync("search");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOrganizationsAsync_SkipsOrgsWithNullId()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/organizations", new[]
            {
                new { id = (string?)null, name = "Bad Org", domains = Array.Empty<object>() },
                new { id = (string?)orgId.ToString(), name = "Good Org", domains = Array.Empty<object>() }
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Good Org");
    }

    [Fact]
    public async Task GetOrganizationsAsync_WhenException_ReturnsEmpty()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow("/admin/realms/foundry/organizations");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMemberAsync_PublishesEvent()
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
    public async Task RemoveMemberAsync_CallsDelete()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete($"/admin/realms/foundry/organizations/{orgId}/members/{userId}", HttpStatusCode.NoContent);

        KeycloakOrganizationService service = CreateService(handler);

        // Should not throw
        await service.RemoveMemberAsync(orgId, userId);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsMemberList()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = userId.ToString(), email = "member@test.com", firstName = "Bob", lastName = "Smith", enabled = true }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("member@test.com");
    }

    [Fact]
    public async Task GetMembersAsync_WhenException_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/organizations/{orgId}/members");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_ReturnsOrgList()
    {
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/organizations", new[]
            {
                new { id = orgId.ToString(), name = "User Org", domains = Array.Empty<object>() }
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetUserOrganizationsAsync(userId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("User Org");
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_WhenException_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/users/{userId}/organizations");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetUserOrganizationsAsync(userId);

        result.Should().BeEmpty();
    }

    private KeycloakOrganizationService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_testTenantId);

        return new KeycloakOrganizationService(
            httpClientFactory,
            _messageBus,
            _tenantContext,
            _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = [];
        private readonly HashSet<string> _throwRoutes = [];

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

        public MockHttpHandler WithGetThrow(string path)
        {
            _throwRoutes.Add($"GET:{path}");
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string? locationHeader = null)
        {
            _routes[$"POST:{path}"] = (status, null, locationHeader);
            return this;
        }

        public MockHttpHandler WithDelete(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null, null);
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
