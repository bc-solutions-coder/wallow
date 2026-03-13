using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakOrganizationServiceCoverageTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakOrganizationService> _logger = Substitute.For<ILogger<KeycloakOrganizationService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task CreateOrganizationAsync_HttpError_ThrowsExternalServiceException()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Forbidden);

        KeycloakOrganizationService service = CreateService(handler);

        Func<Task<Guid>> act = async () => await service.CreateOrganizationAsync("Test Org");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task CreateOrganizationAsync_WhitespaceOnlyDomain_TreatsAsNoDomain()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        Guid result = await service.CreateOrganizationAsync("Test Org", "   ");

        result.Should().Be(orgId);
    }

    [Fact]
    public async Task CreateOrganizationAsync_WithCreatorEmail_PublishesEventWithEmail()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        await service.CreateOrganizationAsync("Test Org", null, "creator@test.com");

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationCreatedEvent>(e => e.CreatorEmail == "creator@test.com"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task CreateOrganizationAsync_WithoutCreatorEmail_PublishesEventWithEmptyEmail()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        await service.CreateOrganizationAsync("Test Org");

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationCreatedEvent>(e => e.CreatorEmail == string.Empty),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_NullOrgBody_ReturnsNull()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationsAsync_EmptyList_ReturnsEmpty()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/organizations", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationsAsync_AllOrgsHaveNullId_ReturnsEmpty()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/organizations", new[]
            {
                new { id = (string?)null, name = "Bad 1", domains = Array.Empty<object>() },
                new { id = (string?)"", name = "Bad 2", domains = Array.Empty<object>() },
                new { id = (string?)" ", name = "Bad 3", domains = Array.Empty<object>() }
            });

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMemberAsync_HttpError_ThrowsExternalServiceException()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost($"/admin/realms/foundry/organizations/{orgId}/members", HttpStatusCode.Conflict);

        KeycloakOrganizationService service = CreateService(handler);

        Func<Task> act = async () => await service.AddMemberAsync(orgId, userId);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task RemoveMemberAsync_PublishesEventWithCorrectData()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new { email = "removed@test.com" })
            .WithDelete($"/admin/realms/foundry/organizations/{orgId}/members/{userId}", HttpStatusCode.NoContent);

        KeycloakOrganizationService service = CreateService(handler);

        await service.RemoveMemberAsync(orgId, userId);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationMemberRemovedEvent>(e =>
                e.OrganizationId == orgId &&
                e.UserId == userId &&
                e.Email == "removed@test.com"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task RemoveMemberAsync_HttpError_ThrowsExternalServiceException()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new { email = "user@test.com" })
            .WithDelete($"/admin/realms/foundry/organizations/{orgId}/members/{userId}", HttpStatusCode.NotFound);

        KeycloakOrganizationService service = CreateService(handler);

        Func<Task> act = async () => await service.RemoveMemberAsync(orgId, userId);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task RemoveMemberAsync_UserEmailFetchFails_StillUsesEmptyEmail()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/users/{userId}", HttpStatusCode.NotFound)
            .WithDelete($"/admin/realms/foundry/organizations/{orgId}/members/{userId}", HttpStatusCode.NoContent);

        KeycloakOrganizationService service = CreateService(handler);

        await service.RemoveMemberAsync(orgId, userId);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationMemberRemovedEvent>(e => e.Email == string.Empty),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task GetMembersAsync_NullResponse_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/organizations/{orgId}/members");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMembersAsync_AllMembersHaveNullId_ReturnsEmpty()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = (string?)null, email = "a@test.com", firstName = "A", lastName = "B", enabled = true },
                new { id = (string?)"", email = "b@test.com", firstName = "C", lastName = "D", enabled = true }
            });

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_NullResponse_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/users/{userId}/organizations");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetUserOrganizationsAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_AllOrgsHaveNullId_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/organizations", new[]
            {
                new { id = (string?)null, name = "Bad Org 1", domains = Array.Empty<object>() },
                new { id = (string?)"", name = "Bad Org 2", domains = Array.Empty<object>() }
            });

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetUserOrganizationsAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_Exception_ReturnsEmptyRoles()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = userId.ToString(), email = "user@test.com", firstName = "Test", lastName = "User", enabled = true }
            })
            .WithGetThrow($"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_FiltersOutNullAndEmptyRoleNames()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = userId.ToString(), email = "user@test.com", firstName = "Test", lastName = "User", enabled = true }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = "admin" }, new { name = string.Empty }, new { name = "  " }, new { name = "user" } });

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Roles.Should().HaveCount(2);
        result[0].Roles.Should().Contain("admin");
        result[0].Roles.Should().Contain("user");
    }

    [Fact]
    public async Task GetMembersCountAsync_NullResponse_ReturnsZero()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}", new
            {
                id = orgId.ToString(),
                name = "Test Org"
            })
            .WithGetNull($"/admin/realms/foundry/organizations/{orgId}/members");

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result!.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOrganizationsAsync_NullResponse_ReturnsEmpty()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull("/admin/realms/foundry/organizations");

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<OrganizationDto> result = await service.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMemberAsync_PublishesEventWithCorrectData()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost($"/admin/realms/foundry/organizations/{orgId}/members", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}", new { email = "member@test.com" });

        KeycloakOrganizationService service = CreateService(handler);

        await service.AddMemberAsync(orgId, userId);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationMemberAddedEvent>(e =>
                e.OrganizationId == orgId &&
                e.UserId == userId &&
                e.Email == "member@test.com" &&
                e.TenantId == _tenantId.Value),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task CreateOrganizationAsync_PublishesEventWithCorrectTenantAndOrg()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/organizations", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/organizations/{orgId}");

        KeycloakOrganizationService service = CreateService(handler);

        await service.CreateOrganizationAsync("My Org", "my.com", "admin@my.com");

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationCreatedEvent>(e =>
                e.OrganizationId == orgId &&
                e.Name == "My Org" &&
                e.Domain == "my.com" &&
                e.TenantId == _tenantId.Value),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task GetMembersAsync_MemberWithNullFields_DefaultsToEmptyStrings()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", new[]
            {
                new { id = userId.ToString(), email = (string?)null, firstName = (string?)null, lastName = (string?)null, enabled = (bool?)null }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Email.Should().BeEmpty();
        result[0].FirstName.Should().BeEmpty();
        result[0].LastName.Should().BeEmpty();
        result[0].Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_OrgWithNullName_DefaultsToEmptyString()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}", new
            {
                id = orgId.ToString(),
                name = (string?)null,
                domains = Array.Empty<object>()
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result!.Name.Should().BeEmpty();
        result.Domain.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_OrgWithNoDomains_ReturnsDomainNull()
    {
        Guid orgId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/organizations/{orgId}", new
            {
                id = orgId.ToString(),
                name = "No Domain Org"
            })
            .WithGet($"/admin/realms/foundry/organizations/{orgId}/members", Array.Empty<object>());

        KeycloakOrganizationService service = CreateService(handler);

        OrganizationDto? result = await service.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result!.Domain.Should().BeNull();
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
