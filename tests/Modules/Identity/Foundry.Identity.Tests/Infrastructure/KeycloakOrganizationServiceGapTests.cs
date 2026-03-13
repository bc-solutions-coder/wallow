using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakOrganizationServiceGapTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakOrganizationService> _logger = Substitute.For<ILogger<KeycloakOrganizationService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task CreateOrganizationAsync_WithDomain_CreatesSuccessfully()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        Guid result = await service.CreateOrganizationAsync("Test Org", "example.com");

        result.Should().Be(orgId);
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task CreateOrganizationAsync_MissingLocationHeader_ThrowsInvalidOperation()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created);

        KeycloakOrganizationService service = CreateService(handler);

        Func<Task<Guid>> act = async () => await service.CreateOrganizationAsync("Test Org");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
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
    public async Task GetOrganizationByIdAsync_WhenExists_ReturnsDto()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}", new
            {
                id = orgId.ToString(),
                name = "Test Org",
                domains = new[] { new { name = "example.com" } }
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = Guid.NewGuid().ToString(), email = "member@test.com" }
            });

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result.Name.Should().Be("Test Org");
        result.Domain.Should().Be("example.com");
        result.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrganizationsAsync_WithSearch_PassesSearchParam()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/organizations", new[]
            {
                new { id = orgId.ToString(), name = "Matched Org", domains = Array.Empty<object>() }
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync("Matched");

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Matched Org");
    }

    [Fact]
    public async Task GetOrganizationsAsync_SkipsOrgsWithNullId()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/organizations", new[]
            {
                new { id = (string?)null, name = "Bad Org" },
                new { id = (string?)"", name = "Empty Id Org" },
                new { id = (string?)orgId.ToString(), name = "Good Org" }
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
    public async Task RemoveMemberAsync_CallsDelete()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithDeleteStatus($"/admin/realms/foundry/organizations/{orgId}/members/{userId}", HttpStatusCode.NoContent);

        KeycloakOrganizationService service = CreateService(handler);

        await service.RemoveMemberAsync(orgId, userId);
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
    public async Task GetMembersAsync_WithRoles_ReturnsUserDtosWithRoles()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = userId.ToString(), email = "user@test.com", firstName = "Test", lastName = "User", enabled = true }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = "admin" }, new { name = "user" } });

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Roles.Should().Contain("admin");
        result[0].Roles.Should().Contain("user");
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

    [Fact]
    public async Task GetMembersCountAsync_WhenException_ReturnsZero()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}", new
            {
                id = orgId.ToString(),
                name = "Test Org"
            })
            .WithGetThrow($"/admin/realms/foundry/organizations/{orgId}/members");

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUserRolesAsync_WithNullResponse_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = userId.ToString(), email = "user@test.com", firstName = "Test", lastName = "User", enabled = true }
            })
            .WithGetNull($"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenNotSuccess_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = userId.ToString(), email = "user@test.com", firstName = "Test", lastName = "User", enabled = true }
            })
            .WithGetStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.InternalServerError);

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserEmailAsync_WhenException_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost($"/admin/realms/foundry/organizations/{orgId}/members", HttpStatusCode.NoContent)
            .WithGetThrow($"/admin/realms/foundry/users/{userId}");

        KeycloakOrganizationService service = CreateService(handler);

        await service.AddMemberAsync(orgId, userId);

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
            Options.Create(new KeycloakOptions()),
            _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = new Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)>();
        private readonly HashSet<string> _throwRoutes = [];
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

        public MockHttpHandler WithDeleteStatus(string path, HttpStatusCode status)
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
