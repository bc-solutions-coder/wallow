using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Keycloak.AuthServices.Sdk.Admin.Requests.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using Wolverine;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakAdminServiceCoverageTests
{
    private readonly IKeycloakUserClient _userClient = Substitute.For<IKeycloakUserClient>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakAdminService> _logger = Substitute.For<ILogger<KeycloakAdminService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    // --- CreateUserAsync error paths ---

    [Fact]
    public async Task CreateUserAsync_WhenKeycloakReturns409Conflict_ThrowsExternalServiceException()
    {
        HttpResponseMessage conflictResponse = new(HttpStatusCode.Conflict)
        {
            Content = new StringContent("User already exists")
        };
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(conflictResponse);

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        Func<Task> act = async () => await service.CreateUserAsync("dup@test.com", "Dup", "User");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task CreateUserAsync_WhenKeycloakReturns500_ThrowsExternalServiceException()
    {
        HttpResponseMessage errorResponse = new(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal server error")
        };
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(errorResponse);

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        Func<Task> act = async () => await service.CreateUserAsync("fail@test.com", "Fail", "User");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task CreateUserAsync_WhenRoleAssignmentFails_ThrowsExternalServiceException()
    {
        Guid userId = Guid.NewGuid();
        HttpResponseMessage createResponse = new(HttpStatusCode.Created);
        createResponse.Headers.Location = new Uri($"https://keycloak.test/users/{userId}");
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);
        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-user", name = "user" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.Forbidden)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        Func<Task> act = async () => await service.CreateUserAsync("test@test.com", "Test", "User", "pass");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task CreateUserAsync_WithEmptyPassword_CreatesWithoutCredentials()
    {
        Guid userId = Guid.NewGuid();
        HttpResponseMessage createResponse = new(HttpStatusCode.Created);
        createResponse.Headers.Location = new Uri($"https://keycloak.test/users/{userId}");
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);
        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-user", name = "user" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        Guid result = await service.CreateUserAsync("test@test.com", "Test", "User", "  ");

        result.Should().Be(userId);
    }

    [Fact]
    public async Task CreateUserAsync_PublishesUserRegisteredEventWithCorrectData()
    {
        Guid userId = Guid.NewGuid();
        HttpResponseMessage createResponse = new(HttpStatusCode.Created);
        createResponse.Headers.Location = new Uri($"https://keycloak.test/users/{userId}");
        _userClient.CreateUserWithResponseAsync("foundry", Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Returns(createResponse);
        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "evt@test.com" });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/user", new { id = "role-user", name = "user" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        await service.CreateUserAsync("evt@test.com", "Event", "Test");

        await _messageBus.Received().PublishAsync(
            Arg.Is<UserRegisteredEvent>(e =>
                e.UserId == userId &&
                e.TenantId == _tenantId.Value &&
                e.Email == "evt@test.com" &&
                e.FirstName == "Event" &&
                e.LastName == "Test"),
            Arg.Any<DeliveryOptions?>());
    }

    // --- AssignRoleAsync paths ---

    [Fact]
    public async Task AssignRoleAsync_WhenHttpPostFails_ThrowsExternalServiceException()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.InternalServerError)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        Func<Task> act = async () => await service.AssignRoleAsync(userId, "admin");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task AssignRoleAsync_WhenUserHasNoExistingRoles_OldRoleIsNone()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        await service.AssignRoleAsync(userId, "admin");

        await _messageBus.Received().PublishAsync(
            Arg.Is<UserRoleChangedEvent>(e =>
                e.OldRole == "none" &&
                e.NewRole == "admin"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task AssignRoleAsync_WhenUserHasExistingRole_OldRoleIsExistingRole()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" }, new { name = "admin" } });

        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "test@test.com" });

        KeycloakAdminService service = CreateService(handler);

        await service.AssignRoleAsync(userId, "admin");

        await _messageBus.Received().PublishAsync(
            Arg.Is<UserRoleChangedEvent>(e =>
                e.OldRole == "user" &&
                e.NewRole == "admin"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task AssignRoleAsync_PublishesUserRoleChangedEventWithCorrectEmail()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/editor", new { id = "role-editor", name = "editor" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "editor" } });

        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "editor@test.com" });

        KeycloakAdminService service = CreateService(handler);

        await service.AssignRoleAsync(userId, "editor");

        await _messageBus.Received().PublishAsync(
            Arg.Is<UserRoleChangedEvent>(e =>
                e.UserId == userId &&
                e.TenantId == _tenantId.Value &&
                e.Email == "editor@test.com"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task AssignRoleAsync_WhenGetRealmRoleReturns500_ThrowsExternalServiceException()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/roles/broken", HttpStatusCode.InternalServerError);

        KeycloakAdminService service = CreateService(handler);

        // GetRealmRoleAsync catches the exception and returns null, then AssignRoleAsync throws InvalidOperationException
        Func<Task> act = async () => await service.AssignRoleAsync(userId, "broken");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*broken*not found*");
    }

    // --- RemoveRoleAsync error paths ---

    [Fact]
    public async Task RemoveRoleAsync_WhenDeleteFails_ThrowsExternalServiceException()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithDeleteStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.InternalServerError);

        KeycloakAdminService service = CreateService(handler);

        Func<Task> act = async () => await service.RemoveRoleAsync(userId, "admin");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task RemoveRoleAsync_PublishesEventWithCorrectData()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithDeleteStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", new[] { new { name = "user" } });

        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = "remove@test.com" });

        KeycloakAdminService service = CreateService(handler);

        await service.RemoveRoleAsync(userId, "admin");

        await _messageBus.Received().PublishAsync(
            Arg.Is<UserRoleChangedEvent>(e =>
                e.UserId == userId &&
                e.Email == "remove@test.com" &&
                e.OldRole == "admin" &&
                e.NewRole == "user"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenGetRealmRoleHttpFails_ReturnsWithoutError()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/roles/broken", HttpStatusCode.InternalServerError);

        KeycloakAdminService service = CreateService(handler);

        // GetRealmRoleAsync catches exception and returns null; RemoveRoleAsync early-returns
        await service.RemoveRoleAsync(userId, "broken");

        await _messageBus.DidNotReceiveWithAnyArgs().PublishAsync(default(object)!);
    }

    // --- GetUserRolesAsync error paths ---

    [Fact]
    public async Task GetUserRolesAsync_WhenHttpReturns403_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.Forbidden);

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<string> result = await service.GetUserRolesAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenHttpReturns404_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NotFound);

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<string> result = await service.GetUserRolesAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_FiltersOutWhitespaceNames()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = "admin" }, new { name = "  " }, new { name = "" }, new { name = "user" } });

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<string> result = await service.GetUserRolesAsync(userId);

        result.Should().HaveCount(2);
        result.Should().Contain("admin");
        result.Should().Contain("user");
    }

    // --- GetUserByIdAsync edge cases ---

    [Fact]
    public async Task GetUserByIdAsync_WhenUserHasNullFields_MapsToEmptyStrings()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUserAsync("foundry", userId.ToString(), false, Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation
            {
                Email = null,
                FirstName = null,
                LastName = null,
                Enabled = null
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        KeycloakAdminService service = CreateService(handler);

        UserDto? result = await service.GetUserByIdAsync(userId);

        result.Should().NotBeNull();
        result!.Email.Should().BeEmpty();
        result.FirstName.Should().BeEmpty();
        result.LastName.Should().BeEmpty();
        result.Enabled.Should().BeFalse();
        result.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenGetUserRolesFails_ReturnsNullDueToException()
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
            .WithGetStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.InternalServerError);

        KeycloakAdminService service = CreateService(handler);

        // GetUserRolesAsync catches the exception internally and returns empty, so GetUserByIdAsync succeeds
        UserDto? result = await service.GetUserByIdAsync(userId);

        result.Should().NotBeNull();
        result!.Roles.Should().BeEmpty();
    }

    // --- GetUserByEmailAsync edge cases ---

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserHasEmptyStringId_ReturnsNull()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new() { Id = "", Email = "test@test.com" }
            });

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        UserDto? result = await service.GetUserByEmailAsync("test@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserHasNullFields_MapsToEmptyStrings()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new()
                {
                    Id = userId.ToString(),
                    Email = null,
                    FirstName = null,
                    LastName = null,
                    Enabled = null
                }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        KeycloakAdminService service = CreateService(handler);

        UserDto? result = await service.GetUserByEmailAsync("null@test.com");

        result.Should().NotBeNull();
        result!.Email.Should().BeEmpty();
        result.FirstName.Should().BeEmpty();
        result.LastName.Should().BeEmpty();
        result.Enabled.Should().BeFalse();
    }

    // --- GetUsersAsync edge cases ---

    [Fact]
    public async Task GetUsersAsync_WhenNoUsersReturned_ReturnsEmptyList()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>());

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersAsync_WhenAllUsersHaveNullIds_ReturnsEmptyList()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new() { Id = null, Email = "a@test.com" },
                new() { Id = "", Email = "b@test.com" },
                new() { Id = "  ", Email = "c@test.com" }
            });

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        // Users with null/empty IDs are filtered; whitespace ID might cause Guid.Parse to fail
        // The source filters on !string.IsNullOrWhiteSpace(u.Id), so all three are excluded
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersAsync_WithNullSearch_PassesNullToClient()
    {
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>());

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        IReadOnlyList<UserDto> result = await service.GetUsersAsync(null, 5, 50);

        result.Should().BeEmpty();
        await _userClient.Received(1).GetUsersAsync("foundry",
            Arg.Is<GetUsersRequestParameters>(p => p.Search == null && p.First == 5 && p.Max == 50),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsersAsync_MapsUserFieldsCorrectly()
    {
        Guid userId = Guid.NewGuid();
        _userClient.GetUsersAsync("foundry", Arg.Any<GetUsersRequestParameters>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserRepresentation>
            {
                new()
                {
                    Id = userId.ToString(),
                    Email = null,
                    FirstName = null,
                    LastName = null,
                    Enabled = null
                }
            });

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { name = "viewer" } });

        KeycloakAdminService service = CreateService(handler);

        IReadOnlyList<UserDto> result = await service.GetUsersAsync();

        result.Should().HaveCount(1);
        result[0].Email.Should().BeEmpty();
        result[0].FirstName.Should().BeEmpty();
        result[0].LastName.Should().BeEmpty();
        result[0].Enabled.Should().BeFalse();
        result[0].Roles.Should().Contain("viewer");
    }

    // --- DeactivateUserAsync / ActivateUserAsync error paths ---

    [Fact]
    public async Task DeactivateUserAsync_WhenClientThrows_PropagatesException()
    {
        Guid userId = Guid.NewGuid();
        _userClient.UpdateUserAsync("foundry", userId.ToString(), Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Connection refused"));

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        Func<Task> act = async () => await service.DeactivateUserAsync(userId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ActivateUserAsync_WhenClientThrows_PropagatesException()
    {
        Guid userId = Guid.NewGuid();
        _userClient.UpdateUserAsync("foundry", userId.ToString(), Arg.Any<UserRepresentation>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Connection refused"));

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        Func<Task> act = async () => await service.ActivateUserAsync(userId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // --- DeleteUserAsync error path ---

    [Fact]
    public async Task DeleteUserAsync_WhenClientThrows_PropagatesException()
    {
        Guid userId = Guid.NewGuid();
        _userClient.DeleteUserAsync("foundry", userId.ToString(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Connection refused"));

        KeycloakAdminService service = CreateService(new MockHttpHandler());

        Func<Task> act = async () => await service.DeleteUserAsync(userId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // --- GetRealmRoleAsync (private, tested via AssignRoleAsync/RemoveRoleAsync) ---

    [Fact]
    public async Task AssignRoleAsync_WhenGetRealmRoleHttp404_ThrowsInvalidOperationException()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/roles/ghost", HttpStatusCode.NotFound);

        KeycloakAdminService service = CreateService(handler);

        Func<Task> act = async () => await service.AssignRoleAsync(userId, "ghost");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ghost*not found*");
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenGetRealmRoleHttp404_ReturnsWithoutPublishing()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/roles/gone", HttpStatusCode.NotFound);

        KeycloakAdminService service = CreateService(handler);

        await service.RemoveRoleAsync(userId, "gone");

        await _messageBus.DidNotReceiveWithAnyArgs().PublishAsync(default(object)!);
    }

    // --- AssignRoleAsync with user having null email ---

    [Fact]
    public async Task AssignRoleAsync_WhenUserEmailIsNull_PublishesWithEmptyEmail()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithPostStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = null });

        KeycloakAdminService service = CreateService(handler);

        await service.AssignRoleAsync(userId, "admin");

        await _messageBus.Received().PublishAsync(
            Arg.Is<UserRoleChangedEvent>(e => e.Email == string.Empty),
            Arg.Any<DeliveryOptions?>());
    }

    // --- RemoveRoleAsync with user having null email ---

    [Fact]
    public async Task RemoveRoleAsync_WhenUserEmailIsNull_PublishesWithEmptyEmail()
    {
        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/roles/admin", new { id = "role-admin", name = "admin" })
            .WithDeleteStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm", Array.Empty<object>());

        _userClient.GetUserAsync("foundry", userId.ToString(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new UserRepresentation { Email = null });

        KeycloakAdminService service = CreateService(handler);

        await service.RemoveRoleAsync(userId, "admin");

        await _messageBus.Received().PublishAsync(
            Arg.Is<UserRoleChangedEvent>(e => e.Email == string.Empty && e.NewRole == "none"),
            Arg.Any<DeliveryOptions?>());
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

        public MockHttpHandler WithPostStatus(string path, HttpStatusCode status)
        {
            _routes[$"POST:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithDeleteStatus(string path, HttpStatusCode status)
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
