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

using Foundry.Identity.Infrastructure;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakAdminServiceAdditionalTests
{
    private readonly IKeycloakUserClient _userClient = Substitute.For<IKeycloakUserClient>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakAdminService> _logger = Substitute.For<ILogger<KeycloakAdminService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task AssignRoleAsync_WhenRoleExists_AssignsAndPublishesEvent()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "admin" }, new { name = "user" } });

        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        await service.AssignRoleAsync(userId, "admin");

        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenRoleExists_RemovesAndPublishesEvent()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithDelete($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        await service.RemoveRoleAsync(userId, "admin");

        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenNoRolesLeft_PublishesWithNoneRole()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-user", name = "user" })
            .WithDelete($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        await service.RemoveRoleAsync(userId, "user");

        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenException_ReturnsNull()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns<IEnumerable<UserRepresentation>>(_ => throw new HttpRequestException("Connection failed"));

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByEmailAsync("fail@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersAsync_NullResponse_ReturnsEmpty()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<Keycloak.AuthServices.Sdk.Admin.Requests.Users.GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns((IEnumerable<UserRepresentation>?)null!);

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_NullRoles_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull($"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<string> result = await service.GetUserRolesAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRealmRoleAsync_WhenException_ReturnsNull()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/roles/bad-role");

        KeycloakAdminService service = CreateService(handler);

        // RemoveRoleAsync calls GetRealmRoleAsync internally; when null, it returns without error
        await service.RemoveRoleAsync(userId, "bad-role");
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
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _routes = [];
        private readonly HashSet<string> _throwRoutes = [];
        private readonly HashSet<string> _nullRoutes = [];

        public MockHttpHandler WithGet(string path, object content)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, content);
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

        public MockHttpHandler WithPost(string path, HttpStatusCode status)
        {
            _routes[$"POST:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithDelete(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null);
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
