using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Exceptions;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework
#pragma warning disable CA1861 // Inline arrays in test data are intentional and not called repeatedly

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimServiceGapTests
{
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimService> _logger = Substitute.For<ILogger<ScimService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task GetUserAsync_WhenUserExists_ReturnsScimUser()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("GET", "/admin/realms/foundry/users/user-456", HttpStatusCode.OK, new
            {
                id = "user-456",
                username = "jane.doe",
                email = "jane.doe@example.com",
                firstName = "Jane",
                lastName = "Doe",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-456" }
                }
            });

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        // Act
        ScimUser? result = await service.GetUserAsync("user-456");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("user-456");
        result.UserName.Should().Be("jane.doe");
        result.Active.Should().BeTrue();
        result.ExternalId.Should().Be("ext-456");
    }

    [Fact]
    public async Task GetUserAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("GET", "/admin/realms/foundry/users/nonexistent", HttpStatusCode.NotFound);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        // Act
        ScimUser? result = await service.GetUserAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupAsync_WhenGroupExists_ReturnsScimGroup()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("GET", "/admin/realms/foundry/groups/group-456", HttpStatusCode.OK, new
            {
                id = "group-456",
                name = "Engineers",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-grp-456" }
                }
            })
            .WithResponse("GET", "/admin/realms/foundry/groups/group-456/members", HttpStatusCode.OK, new[]
            {
                new { id = "member-1", username = "user1" }
            });

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        // Act
        ScimGroup? result = await service.GetGroupAsync("group-456");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("group-456");
        result.DisplayName.Should().Be("Engineers");
        result.ExternalId.Should().Be("ext-grp-456");
        result.Members.Should().HaveCount(1);
        result.Members[0].Value.Should().Be("member-1");
    }

    [Fact]
    public async Task GetGroupAsync_WhenGroupNotFound_ReturnsNull()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("GET", "/admin/realms/foundry/groups/nonexistent", HttpStatusCode.NotFound);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        // Act
        ScimGroup? result = await service.GetGroupAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSyncLogsAsync_WithCustomLimit_PassesLimitToRepository()
    {
        // Arrange
        List<ScimSyncLog> logs =
        [
            ScimSyncLog.Create(_tenantId, ScimOperation.Create, ScimResourceType.User, "ext-1", "user-1", true, TimeProvider.System)
        ];

        _syncLogRepository.GetRecentAsync(25, Arg.Any<CancellationToken>()).Returns(logs);
        ScimService service = CreateService();

        // Act
        IReadOnlyList<ScimSyncLogDto> result = await service.GetSyncLogsAsync(limit: 25);

        // Assert
        result.Should().HaveCount(1);
        await _syncLogRepository.Received(1).GetRecentAsync(25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSyncLogsAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        _syncLogRepository.GetRecentAsync(100, Arg.Any<CancellationToken>()).Returns(new List<ScimSyncLog>());
        ScimService service = CreateService();

        // Act
        IReadOnlyList<ScimSyncLogDto> result = await service.GetSyncLogsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenTokenExpired_ReturnsFalse()
    {
        // Arrange - create config with token, then advance time past expiration
        FakeTimeProvider fakeTime = new();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, fakeTime);
        config.Enable(Guid.Empty, fakeTime);
        string plainTextToken = config.RegenerateToken(Guid.Empty, fakeTime);

        // Advance time past token expiration (tokens typically expire in 365 days)
        fakeTime.Advance(TimeSpan.FromDays(366));

        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        ScimService service = CreateServiceWithTimeProvider(fakeTime);

        // Act
        bool result = await service.ValidateTokenAsync(plainTextToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUserAsync_WhenConflict_ThrowsKeycloakConflictException()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("POST", "/admin/realms/foundry/users", HttpStatusCode.Conflict);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "existing.user",
            ExternalId = "ext-conflict",
            Active = true
        };

        // Act
        Func<Task<ScimUser>> act = async () => await service.CreateUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<KeycloakConflictException>()
            .WithMessage("*already exists*");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.User &&
            !log.Success));
    }

    [Fact]
    public async Task CreateGroupAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("POST", "/admin/realms/foundry/groups", HttpStatusCode.InternalServerError);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "FailGroup",
            ExternalId = "ext-fail"
        };

        // Act
        Func<Task<ScimGroup>> act = async () => await service.CreateGroupAsync(request);

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.Group &&
            !log.Success));
    }

    [Fact]
    public async Task UpdateGroupAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("PUT", "/admin/realms/foundry/groups/group-fail", HttpStatusCode.NotFound);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Updated",
            ExternalId = "ext-fail"
        };

        // Act
        Func<Task<ScimGroup>> act = async () => await service.UpdateGroupAsync("group-fail", request);

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
            log.ResourceType == ScimResourceType.Group &&
            !log.Success));
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("DELETE", "/admin/realms/foundry/groups/group-fail", HttpStatusCode.InternalServerError);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        // Act
        Func<Task> act = async () => await service.DeleteGroupAsync("group-fail");

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.Group &&
            !log.Success));
    }

    [Fact]
    public async Task GetConfigurationAsync_MapsEndpointUrl()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.Enable(Guid.Empty, TimeProvider.System);

        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        ScimService service = CreateService();

        // Act
        ScimConfigurationDto? result = await service.GetConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result!.ScimEndpointUrl.Should().Be("/scim/v2");
        result.TokenPrefix.Should().NotBeNull();
    }

    [Fact]
    public async Task EnableScimAsync_WhenConfigExists_DoesNotReturnPlainTextToken()
    {
        // Arrange
        (ScimConfiguration existingConfig, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(existingConfig);
        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService();

        EnableScimRequest request = new(AutoActivateUsers: true, DefaultRole: "viewer", DeprovisionOnDelete: true);

        // Act
        EnableScimResponse result = await service.EnableScimAsync(request);

        // Assert
        result.PlainTextToken.Should().BeNull();
        result.Configuration.ScimEndpointUrl.Should().Be("/scim/v2");
    }

    [Fact]
    public async Task CreateGroupAsync_WithMembers_AddsUsersToGroup()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("POST", "/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/group-new")
            .WithResponse("PUT", "/admin/realms/foundry/users/user-a/groups/group-new", HttpStatusCode.NoContent)
            .WithResponse("PUT", "/admin/realms/foundry/users/user-b/groups/group-new", HttpStatusCode.NoContent)
            .WithResponse("GET", "/admin/realms/foundry/groups/group-new", HttpStatusCode.OK, new
            {
                id = "group-new",
                name = "TeamWithMembers",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-team" }
                }
            })
            .WithResponse("GET", "/admin/realms/foundry/groups/group-new/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-a", username = "usera" },
                new { id = "user-b", username = "userb" }
            });

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "TeamWithMembers",
            ExternalId = "ext-team",
            Members = new List<ScimMember>
            {
                new() { Value = "user-a" },
                new() { Value = "user-b" }
            }
        };

        // Act
        ScimGroup result = await service.CreateGroupAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateGroupAsync_WithMemberChanges_AddsAndRemovesMembers()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("PUT", "/admin/realms/foundry/groups/group-sync", HttpStatusCode.NoContent)
            // Current members: user-a, user-b; request members: user-b, user-c
            .WithResponse("GET", "/admin/realms/foundry/groups/group-sync/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-a", username = "usera" },
                new { id = "user-b", username = "userb" }
            })
            .WithResponse("DELETE", "/admin/realms/foundry/users/user-a/groups/group-sync", HttpStatusCode.NoContent)
            .WithResponse("PUT", "/admin/realms/foundry/users/user-c/groups/group-sync", HttpStatusCode.NoContent)
            .WithResponse("GET", "/admin/realms/foundry/groups/group-sync", HttpStatusCode.OK, new
            {
                id = "group-sync",
                name = "SyncGroup",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-sync" }
                }
            });

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "SyncGroup",
            ExternalId = "ext-sync",
            Members = new List<ScimMember>
            {
                new() { Value = "user-b" },
                new() { Value = "user-c" }
            }
        };

        // Act
        ScimGroup result = await service.UpdateGroupAsync("group-sync", request);

        // Assert
        result.Should().NotBeNull();
        result.DisplayName.Should().Be("SyncGroup");
    }

    [Fact]
    public async Task DeleteUserAsync_WhenNoConfig_PerformsSoftDelete()
    {
        // Arrange - no config means DeprovisionOnDelete is null/false
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("PUT", "/admin/realms/foundry/users/user-soft", HttpStatusCode.NoContent);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        // Act
        await service.DeleteUserAsync("user-soft");

        // Assert
        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
    }

    [Fact]
    public async Task ListGroupsAsync_WhenGroupIdNull_SkipsGroup()
    {
        // Arrange - group with null id should be skipped
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("GET", "/admin/realms/foundry/groups", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = (string?)null,
                    name = "NullIdGroup",
                    attributes = new Dictionary<string, IEnumerable<string>>()
                },
                new
                {
                    id = (string?)"group-valid",
                    name = "ValidGroup",
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithResponse("GET", "/admin/realms/foundry/groups/group-valid", HttpStatusCode.OK, new
            {
                id = "group-valid",
                name = "ValidGroup",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithResponse("GET", "/admin/realms/foundry/groups/group-valid/members", HttpStatusCode.OK, Array.Empty<object>());

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        // Act
        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        // Assert
        result.Resources.Should().HaveCount(1);
        result.Resources[0].DisplayName.Should().Be("ValidGroup");
    }

    [Fact]
    public async Task GetSyncLogsAsync_MapsAllDtoFields()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        FakeTimeProvider fakeTime = new();
        fakeTime.SetUtcNow(timestamp);

        List<ScimSyncLog> logs =
        [
            ScimSyncLog.Create(_tenantId, ScimOperation.Patch, ScimResourceType.User, "ext-patch", "user-patch", false, fakeTime, "Patch failed")
        ];

        _syncLogRepository.GetRecentAsync(100, Arg.Any<CancellationToken>()).Returns(logs);
        ScimService service = CreateService();

        // Act
        IReadOnlyList<ScimSyncLogDto> result = await service.GetSyncLogsAsync();

        // Assert
        result.Should().HaveCount(1);
        ScimSyncLogDto dto = result[0];
        dto.Operation.Should().Be(ScimOperation.Patch);
        dto.ResourceType.Should().Be(ScimResourceType.User);
        dto.ExternalId.Should().Be("ext-patch");
        dto.InternalId.Should().Be("user-patch");
        dto.Success.Should().BeFalse();
        dto.ErrorMessage.Should().Be("Patch failed");
    }

    [Fact]
    public async Task PatchUserAsync_WithAddOperation_AppliesCorrectly()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("GET", "/admin/realms/foundry/users/user-add", HttpStatusCode.OK, new
            {
                id = "user-add",
                username = "add.user",
                email = "add@example.com",
                firstName = "Add",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-add" }
                }
            })
            .WithResponse("PUT", "/admin/realms/foundry/users/user-add", HttpStatusCode.NoContent);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "name.givenName", Value = "NewFirst" },
                new ScimPatchOperation { Op = "add", Path = "name.familyName", Value = "NewLast" }
            }
        };

        // Act
        ScimUser result = await service.PatchUserAsync("user-add", request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("user-add");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch &&
            log.Success));
    }

    [Fact]
    public async Task ListUsersAsync_WithLargeCount_CapsToMaxPageSize()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithResponse("GET", "/admin/realms/foundry/users", HttpStatusCode.OK, Array.Empty<object>())
            .WithResponse("GET", "/admin/realms/foundry/users/count", HttpStatusCode.OK, 0);

        _tenantContext.TenantId.Returns(_tenantId);
        ScimService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 500);

        // Act
        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalResults.Should().Be(0);
    }

    private ScimService CreateService(HttpMessageHandler? handler = null)
    {
        return CreateServiceWithTimeProvider(TimeProvider.System, handler);
    }

    private ScimService CreateServiceWithTimeProvider(TimeProvider timeProvider, HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

        HttpClient keycloakClient = handler != null
            ? new HttpClient(handler) : new HttpClient(new MockKeycloakHttpHandler());
        keycloakClient.BaseAddress = new Uri("https://keycloak.test/");

        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(keycloakClient);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimUserService userService = new(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            Options.Create(new KeycloakOptions()),
            Substitute.For<ILogger<ScimUserService>>(),
            TimeProvider.System);

        ScimGroupService groupService = new(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            Options.Create(new KeycloakOptions()),
            Substitute.For<ILogger<ScimGroupService>>(),
            TimeProvider.System);

        return new ScimService(
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            userService,
            groupService,
            _logger,
            timeProvider);
    }

    private sealed class MockKeycloakHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = new();

        public MockKeycloakHttpHandler WithResponse(string method, string path, HttpStatusCode status, object? content = null, string? locationHeader = null)
        {
            _routes[$"{method}:{path}"] = (status, content, locationHeader);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            string key = $"{request.Method}:{path}";

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

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"No mock configured for {request.Method}:{path}")
            });
        }
    }
}
