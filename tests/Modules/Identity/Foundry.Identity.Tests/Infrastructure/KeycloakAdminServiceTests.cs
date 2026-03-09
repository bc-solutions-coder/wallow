using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Keycloak.AuthServices.Sdk.Admin.Requests.Users;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Wolverine;

using Foundry.Identity.Infrastructure;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakAdminServiceTests
{
    private readonly IKeycloakUserClient _userClient = Substitute.For<IKeycloakUserClient>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakAdminService> _logger = Substitute.For<ILogger<KeycloakAdminService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsUserId()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-1", name = "user" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        HttpResponseMessage createResponse = new(HttpStatusCode.Created);
        createResponse.Headers.Location = new Uri($"https://keycloak.test/users/{userId}");
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);
        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        Guid result = await service.CreateUserAsync("test@test.com", "John", "Doe", "password123");

        result.Should().Be(userId);
        await _messageBus.ReceivedWithAnyArgs(2).PublishAsync(default(object));
    }

    [Fact]
    public async Task CreateUserAsync_WithoutPassword_CreatesUserWithoutCredentials()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-1", name = "user" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        HttpResponseMessage createResponse = new(HttpStatusCode.Created);
        createResponse.Headers.Location = new Uri($"https://keycloak.test/users/{userId}");
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);
        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        Guid result = await service.CreateUserAsync("test@test.com", "John", "Doe");

        result.Should().Be(userId);
    }

    [Fact]
    public async Task CreateUserAsync_MissingLocationHeader_Throws()
    {
        HttpResponseMessage createResponse = new(HttpStatusCode.Created);
        // No location header
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        Func<Task> act = async () => await service.CreateUserAsync("test@test.com", "John", "Doe");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
    }

    [Fact]
    public async Task GetUserByIdAsync_UserExists_ReturnsDto()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation
            {
                Email = "test@test.com",
                FirstName = "John",
                LastName = "Doe",
                Enabled = true
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        UserDto? result = await service.GetUserByIdAsync(userId);

        result.Should().NotBeNull();
        result.Email.Should().Be("test@test.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Enabled.Should().BeTrue();
        result.Roles.Should().Contain("user");
    }

    [Fact]
    public async Task GetUserByIdAsync_UserClientThrows_ReturnsNull()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Not found"));

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByIdAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_UserExists_ReturnsDto()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new() {
                    Id = userId.ToString(),
                    Email = "test@test.com",
                    FirstName = "Jane",
                    LastName = "Smith",
                    Enabled = true
                }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "admin" } });

        KeycloakAdminService service = CreateService(handler);

        UserDto? result = await service.GetUserByEmailAsync("test@test.com");

        result.Should().NotBeNull();
        result.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task GetUserByEmailAsync_NoUsers_ReturnsNull()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns([]);

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByEmailAsync("notfound@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_UserWithNullId_ReturnsNull()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new() { Id = null, Email = "test@test.com" }
            });

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByEmailAsync("test@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsUserList()
    {
        Guid userId1 = Guid.NewGuid();
        Guid userId2 = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new() { Id = userId1.ToString(), Email = "u1@test.com", FirstName = "U1", LastName = "L1", Enabled = true },
                new() { Id = userId2.ToString(), Email = "u2@test.com", FirstName = "U2", LastName = "L2", Enabled = false }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId1}/role-mappings/realm", new[] { new { name = "user" } })
            .WithGet($"/admin/realms/foundry/users/{userId2}/role-mappings/realm", Array.Empty<object>());

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetUsersAsync("search");

        result.Should().HaveCount(2);
        result[0].Email.Should().Be("u1@test.com");
        result[1].Email.Should().Be("u2@test.com");
    }

    [Fact]
    public async Task GetUsersAsync_SkipsUsersWithNullId()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new() { Id = null, Email = "bad@test.com" },
                new() { Id = userId.ToString(), Email = "good@test.com", Enabled = true }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("good@test.com");
    }

    [Fact]
    public async Task GetUsersAsync_WhenException_ReturnsEmpty()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Connection failed"));

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeactivateUserAsync_CallsUpdateWithEnabledFalse()
    {
        Guid userId = Guid.NewGuid();

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        await service.DeactivateUserAsync(userId);

        await _userClient.Received(1).UpdateUserAsync(
            "foundry",
            userId.ToString(),
            Arg.Is<UserRepresentation>(u => u.Enabled == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateUserAsync_CallsUpdateWithEnabledTrue()
    {
        Guid userId = Guid.NewGuid();

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        await service.ActivateUserAsync(userId);

        await _userClient.Received(1).UpdateUserAsync(
            "foundry",
            userId.ToString(),
            Arg.Is<UserRepresentation>(u => u.Enabled == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRoleAsync_WhenRoleNotFound_ThrowsInvalidOperationException()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNotFound($"/admin/realms/foundry/roles/nonexistent");

        KeycloakAdminService service = CreateService(handler);

        Func<Task> act = async () => await service.AssignRoleAsync(userId, "nonexistent");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nonexistent*not found*");
    }

    [Fact]
    public async Task DeleteUserAsync_CallsUserClientDelete()
    {
        Guid userId = Guid.NewGuid();

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        await service.DeleteUserAsync(userId);

        await _userClient.Received(1).DeleteUserAsync(
            "foundry",
            userId.ToString(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsRoleNames()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = (string?)"admin" }, new { name = (string?)"user" }, new { name = (string?)null } });

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<string> result = await service.GetUserRolesAsync(userId);

        result.Should().Contain("admin");
        result.Should().Contain("user");
        result.Should().NotContain("");
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenException_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetError($"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<string> result = await service.GetUserRolesAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenRoleNotFound_ReturnsWithoutError()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNotFound($"/admin/realms/foundry/roles/nonexistent");

        KeycloakAdminService service = CreateService(handler);

        // Should not throw
        await service.RemoveRoleAsync(userId, "nonexistent");
    }

    private KeycloakAdminService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);

        return new KeycloakAdminService(
            _userClient,
            httpClientFactory,
            _messageBus,
            _tenantContext,
            Options.Create(new KeycloakOptions()),
            _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _routes = new Dictionary<string, (HttpStatusCode Status, object? Content)>();

        public MockHttpHandler WithGet(string path, object content)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, content);
            return this;
        }

        public MockHttpHandler WithGetNotFound(string path)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.NotFound, null);
            return this;
        }

        public MockHttpHandler WithGetError(string path)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.InternalServerError, null);
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
                Content = JsonContent.Create(new { })
            });
        }
    }
}
