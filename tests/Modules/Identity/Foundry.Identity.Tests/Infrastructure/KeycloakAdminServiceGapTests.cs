using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakAdminServiceGapTests
{
    private readonly IKeycloakUserClient _userClient = Substitute.For<IKeycloakUserClient>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakAdminService> _logger = Substitute.For<ILogger<KeycloakAdminService>>();
    private readonly TenantId _testTenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task CreateUserAsync_WithPassword_CreatesWithCredentials()
    {
        Guid userId = Guid.NewGuid();
        HttpResponseMessage createResponse = new HttpResponseMessage(HttpStatusCode.Created);
        createResponse.Headers.Location = new Uri($"https://keycloak.test/users/{userId}");

        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-user", name = "user" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        Guid result = await service.CreateUserAsync("test@test.com", "Test", "User", "password123");

        result.Should().Be(userId);
        await _messageBus.Received(2).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task CreateUserAsync_WithoutPassword_CreatesWithoutCredentials()
    {
        Guid userId = Guid.NewGuid();
        HttpResponseMessage createResponse = new HttpResponseMessage(HttpStatusCode.Created);
        createResponse.Headers.Location = new Uri($"https://keycloak.test/users/{userId}");

        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-user", name = "user" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        Guid result = await service.CreateUserAsync("test@test.com", "Test", "User");

        result.Should().Be(userId);
    }

    [Fact]
    public async Task CreateUserAsync_MissingLocationHeader_ThrowsInvalidOperation()
    {
        HttpResponseMessage createResponse = new HttpResponseMessage(HttpStatusCode.Created);
        // No Location header

        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        Func<Task<Guid>> act = async () => await service.CreateUserAsync("test@test.com", "Test", "User");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenExists_ReturnsUserDto()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation
            {
                Email = "test@test.com",
                FirstName = "Test",
                LastName = "User",
                Enabled = true
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = "admin" } });

        KeycloakAdminService service = CreateService(handler);

        UserDto? result = await service.GetUserByIdAsync(userId);

        result.Should().NotBeNull();
        result.Email.Should().Be("test@test.com");
        result.FirstName.Should().Be("Test");
        result.LastName.Should().Be("User");
        result.Enabled.Should().BeTrue();
        result.Roles.Should().Contain("admin");
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenNotFound_ReturnsNull()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Returns((UserRepresentation?)null!);

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByIdAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenExists_ReturnsUserDto()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry",
            Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new UserRepresentation
                {
                    Id = userId.ToString(),
                    Email = "found@test.com",
                    FirstName = "Found",
                    LastName = "User",
                    Enabled = true
                }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        UserDto? result = await service.GetUserByEmailAsync("found@test.com");

        result.Should().NotBeNull();
        result.Email.Should().Be("found@test.com");
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenNoResults_ReturnsNull()
    {
        _userClient.GetUsersAsync("foundry",
            Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(),
            Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<UserRepresentation>());

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByEmailAsync("notfound@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserHasNullId_ReturnsNull()
    {
        _userClient.GetUsersAsync("foundry",
            Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new UserRepresentation { Id = null, Email = "test@test.com" }
            });

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByEmailAsync("test@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersAsync_WithSearch_ReturnsUserDtos()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry",
            Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new UserRepresentation
                {
                    Id = userId.ToString(),
                    Email = "search@test.com",
                    FirstName = "Search",
                    LastName = "User",
                    Enabled = true
                }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetUsersAsync("search", 0, 10);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("search@test.com");
    }

    [Fact]
    public async Task GetUsersAsync_SkipsUsersWithNullId()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry",
            Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new UserRepresentation { Id = null, Email = "bad@test.com" },
                new UserRepresentation { Id = "", Email = "empty@test.com" },
                new UserRepresentation { Id = userId.ToString(), Email = "good@test.com", Enabled = true }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>());

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("good@test.com");
    }

    [Fact]
    public async Task GetUsersAsync_WhenException_ReturnsEmpty()
    {
        _userClient.GetUsersAsync("foundry",
            Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(),
            Arg.Any<CancellationToken>())
            .Returns<IEnumerable<UserRepresentation>>(_ => throw new HttpRequestException("Connection failed"));

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeactivateUserAsync_CallsUpdateWithDisabled()
    {
        Guid userId = Guid.NewGuid();
        KeycloakAdminService service = CreateService(new MockHttpHandler());

        await service.DeactivateUserAsync(userId);

        await _userClient.Received(1).UpdateUserAsync("foundry", userId.ToString(),
            Arg.Is<UserRepresentation>(u => u.Enabled == false), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateUserAsync_CallsUpdateWithEnabled()
    {
        Guid userId = Guid.NewGuid();
        KeycloakAdminService service = CreateService(new MockHttpHandler());

        await service.ActivateUserAsync(userId);

        await _userClient.Received(1).UpdateUserAsync("foundry", userId.ToString(),
            Arg.Is<UserRepresentation>(u => u.Enabled == true), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserAsync_CallsDeleteOnClient()
    {
        Guid userId = Guid.NewGuid();
        KeycloakAdminService service = CreateService(new MockHttpHandler());

        await service.DeleteUserAsync(userId);

        await _userClient.Received(1).DeleteUserAsync("foundry", userId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRoleAsync_WhenRoleNotFound_ThrowsInvalidOperation()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/roles/missing", HttpStatusCode.NotFound);

        KeycloakAdminService service = CreateService(handler);

        Func<Task> act = async () => await service.AssignRoleAsync(userId, "missing");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*not found*");
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenException_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<string> result = await service.GetUserRolesAsync(userId);

        result.Should().BeEmpty();
    }

    private KeycloakAdminService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://keycloak.test/");
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_testTenantId);

        return new KeycloakAdminService(
            _userClient,
            httpClientFactory,
            _messageBus,
            _tenantContext,
            _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _routes = new();
        private readonly HashSet<string> _throwRoutes = new();

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

        public MockHttpHandler WithPost(string path, HttpStatusCode status)
        {
            _routes[$"POST:{path}"] = (status, null);
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
                HttpResponseMessage response = new HttpResponseMessage(route.Status);
                if (route.Content != null)
                {
                    response.Content = JsonContent.Create(route.Content);
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
