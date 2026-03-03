using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework
#pragma warning disable CA1861 // Inline arrays in test data are intentional

namespace Foundry.Identity.Tests.Application.Handlers;

public class ScimSyncHandlerTests
{
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimService> _logger = Substitute.For<ILogger<ScimService>>();
    private readonly TenantId _testTenantId = TenantId.Create(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    [Fact]
    public async Task FullSync_CreateUser_ThenUpdateUser_LogsBothOperations()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("POST", "/admin/realms/foundry/users", HttpStatusCode.Created,
                locationHeader: "http://localhost/users/user-sync-1")
            .WithRoute("POST", "/admin/realms/foundry/organizations/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/members",
                HttpStatusCode.NoContent)
            .WithRoute("GET", "/admin/realms/foundry/users/user-sync-1", HttpStatusCode.OK, new
            {
                id = "user-sync-1",
                username = "sync.user",
                email = "sync@example.com",
                firstName = "Sync",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-sync-1" }
                }
            })
            .WithRoute("PUT", "/admin/realms/foundry/users/user-sync-1", HttpStatusCode.NoContent);

        ScimService service = CreateService(handler);

        ScimUserRequest createRequest = new()
        {
            UserName = "sync.user",
            ExternalId = "ext-sync-1",
            Name = new ScimName { GivenName = "Sync", FamilyName = "User" },
            Emails = new[] { new ScimEmail { Value = "sync@example.com", Primary = true } },
            Active = true
        };

        ScimUser created = await service.CreateUserAsync(createRequest);

        ScimUserRequest updateRequest = new()
        {
            UserName = "sync.user",
            ExternalId = "ext-sync-1",
            Name = new ScimName { GivenName = "Sync", FamilyName = "Updated" },
            Emails = new[] { new ScimEmail { Value = "sync@example.com", Primary = true } },
            Active = true
        };

        ScimUser updated = await service.UpdateUserAsync("user-sync-1", updateRequest);

        created.Id.Should().Be("user-sync-1");
        updated.Id.Should().Be("user-sync-1");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
    }

    [Fact]
    public async Task FullSync_CreateAndDeleteUser_LogsCreateAndDelete()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_testTenantId, Guid.Empty);
        config.UpdateSettings(true, null, true, Guid.Empty);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("POST", "/admin/realms/foundry/users", HttpStatusCode.Created,
                locationHeader: "http://localhost/users/user-del-1")
            .WithRoute("POST", "/admin/realms/foundry/organizations/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/members",
                HttpStatusCode.NoContent)
            .WithRoute("GET", "/admin/realms/foundry/users/user-del-1", HttpStatusCode.OK, new
            {
                id = "user-del-1",
                username = "del.user",
                email = "del@example.com",
                firstName = "Del",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-del-1" }
                }
            })
            .WithRoute("DELETE", "/admin/realms/foundry/users/user-del-1", HttpStatusCode.NoContent);

        ScimService service = CreateService(handler);

        ScimUserRequest createRequest = new()
        {
            UserName = "del.user",
            ExternalId = "ext-del-1",
            Emails = new[] { new ScimEmail { Value = "del@example.com", Primary = true } },
            Active = true
        };

        await service.CreateUserAsync(createRequest);
        await service.DeleteUserAsync("user-del-1");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create && log.Success));
        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete && log.Success));
    }

    [Fact]
    public async Task PartialSync_PatchActiveField_OnlyModifiesTargetAttribute()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("GET", "/admin/realms/foundry/users/user-patch-1", HttpStatusCode.OK, new
            {
                id = "user-patch-1",
                username = "patch.user",
                email = "patch@example.com",
                firstName = "Patch",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-patch-1" }
                }
            })
            .WithRoute("PUT", "/admin/realms/foundry/users/user-patch-1", HttpStatusCode.NoContent);

        ScimService service = CreateService(handler);

        ScimPatchRequest patchRequest = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-patch-1", patchRequest);

        result.Should().NotBeNull();
        result.Id.Should().Be("user-patch-1");
        result.UserName.Should().Be("patch.user");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
    }

    [Fact]
    public async Task PartialSync_PatchMultipleFields_AppliesAllOperations()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("GET", "/admin/realms/foundry/users/user-multi-1", HttpStatusCode.OK, new
            {
                id = "user-multi-1",
                username = "multi.user",
                email = "multi@example.com",
                firstName = "Multi",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-multi-1" }
                }
            })
            .WithRoute("PUT", "/admin/realms/foundry/users/user-multi-1", HttpStatusCode.NoContent);

        ScimService service = CreateService(handler);

        ScimPatchRequest patchRequest = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false },
                new ScimPatchOperation { Op = "replace", Path = "displayName", Value = "New Display" },
                new ScimPatchOperation { Op = "replace", Path = "userName", Value = "new.username" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-multi-1", patchRequest);

        result.Should().NotBeNull();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch &&
            log.Success));
    }

    [Fact]
    public async Task ConflictResolution_UpdateNonExistentUser_LogsFailureAndThrows()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("PUT", "/admin/realms/foundry/users/nonexistent-user", HttpStatusCode.NotFound);

        ScimService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "ghost.user",
            ExternalId = "ext-ghost",
            Active = true
        };

        Func<Task<ScimUser>> act = async () => await service.UpdateUserAsync("nonexistent-user", request);

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
            log.ResourceType == ScimResourceType.User &&
            !log.Success &&
            log.ErrorMessage != null));
    }

    [Fact]
    public async Task ConflictResolution_CreateDuplicateUser_LogsFailureAndThrows()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("POST", "/admin/realms/foundry/users", HttpStatusCode.Conflict);

        ScimService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "duplicate.user",
            ExternalId = "ext-dup-1",
            Emails = new[] { new ScimEmail { Value = "dup@example.com", Primary = true } },
            Active = true
        };

        Func<Task<ScimUser>> act = async () => await service.CreateUserAsync(request);

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.User &&
            !log.Success));
    }

    [Fact]
    public async Task ConflictResolution_DeleteAlreadyDeletedUser_LogsFailureAndThrows()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_testTenantId, Guid.Empty);
        config.UpdateSettings(true, null, true, Guid.Empty);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("DELETE", "/admin/realms/foundry/users/already-deleted", HttpStatusCode.NotFound);

        ScimService service = CreateService(handler);

        Func<Task> act = async () => await service.DeleteUserAsync("already-deleted");

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            !log.Success));
    }

    [Fact]
    public async Task ErrorRecovery_CreateUserFailsThenRetrySucceeds()
    {
        // First attempt fails
        MockKeycloakHttpHandler failHandler = new MockKeycloakHttpHandler()
            .WithRoute("POST", "/admin/realms/foundry/users", HttpStatusCode.ServiceUnavailable);

        ScimService failService = CreateService(failHandler);

        ScimUserRequest request = new()
        {
            UserName = "retry.user",
            ExternalId = "ext-retry-1",
            Emails = new[] { new ScimEmail { Value = "retry@example.com", Primary = true } },
            Active = true
        };

        Func<Task<ScimUser>> failAct = async () => await failService.CreateUserAsync(request);
        await failAct.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create && !log.Success));

        // Second attempt succeeds
        MockKeycloakHttpHandler successHandler = new MockKeycloakHttpHandler()
            .WithRoute("POST", "/admin/realms/foundry/users", HttpStatusCode.Created,
                locationHeader: "http://localhost/users/user-retry-1")
            .WithRoute("POST", "/admin/realms/foundry/organizations/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/members",
                HttpStatusCode.NoContent)
            .WithRoute("GET", "/admin/realms/foundry/users/user-retry-1", HttpStatusCode.OK, new
            {
                id = "user-retry-1",
                username = "retry.user",
                email = "retry@example.com",
                firstName = "Retry",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-retry-1" }
                }
            });

        ScimService successService = CreateService(successHandler);

        ScimUser result = await successService.CreateUserAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().Be("user-retry-1");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create && log.Success));
    }

    [Fact]
    public async Task ErrorRecovery_PatchUserNotFound_LogsErrorWithDetails()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("GET", "/admin/realms/foundry/users/missing-user", HttpStatusCode.NotFound);

        ScimService service = CreateService(handler);

        ScimPatchRequest patchRequest = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        Func<Task<ScimUser>> act = async () => await service.PatchUserAsync("missing-user", patchRequest);

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch &&
            !log.Success &&
            log.ErrorMessage != null));
    }

    [Fact]
    public async Task FullSync_GroupCreateUpdateDelete_LogsAllOperations()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("POST", "/admin/realms/foundry/groups", HttpStatusCode.Created,
                locationHeader: "http://localhost/groups/grp-sync-1")
            .WithRoute("GET", "/admin/realms/foundry/groups/grp-sync-1", HttpStatusCode.OK, new
            {
                id = "grp-sync-1",
                name = "Sync Group",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-grp-sync" }
                }
            })
            .WithRoute("GET", "/admin/realms/foundry/groups/grp-sync-1/members", HttpStatusCode.OK, Array.Empty<object>())
            .WithRoute("PUT", "/admin/realms/foundry/groups/grp-sync-1", HttpStatusCode.NoContent)
            .WithRoute("DELETE", "/admin/realms/foundry/groups/grp-sync-1", HttpStatusCode.NoContent);

        ScimService service = CreateService(handler);

        ScimGroupRequest createRequest = new()
        {
            DisplayName = "Sync Group",
            ExternalId = "ext-grp-sync"
        };

        ScimGroup created = await service.CreateGroupAsync(createRequest);
        created.Id.Should().Be("grp-sync-1");

        ScimGroupRequest updateRequest = new()
        {
            DisplayName = "Updated Sync Group",
            ExternalId = "ext-grp-sync"
        };

        ScimGroup updated = await service.UpdateGroupAsync("grp-sync-1", updateRequest);
        updated.Id.Should().Be("grp-sync-1");

        await service.DeleteGroupAsync("grp-sync-1");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));
    }

    [Fact]
    public async Task ErrorRecovery_GroupCreateFails_LogsErrorAndThrows()
    {
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithRoute("POST", "/admin/realms/foundry/groups", HttpStatusCode.InternalServerError);

        ScimService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Failing Group",
            ExternalId = "ext-fail-grp"
        };

        Func<Task<ScimGroup>> act = async () => await service.CreateGroupAsync(request);

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.Group &&
            !log.Success));
    }

    [Fact]
    public async Task GetSyncLogs_AfterMultipleOperations_ReturnsAllLogs()
    {
        List<ScimSyncLog> logs =
        [
            ScimSyncLog.Create(_testTenantId, ScimOperation.Create, ScimResourceType.User, "ext-1", "user-1", true),
            ScimSyncLog.Create(_testTenantId, ScimOperation.Update, ScimResourceType.User, "ext-1", "user-1", true),
            ScimSyncLog.Create(_testTenantId, ScimOperation.Delete, ScimResourceType.User, "ext-1", "user-1", true),
            ScimSyncLog.Create(_testTenantId, ScimOperation.Create, ScimResourceType.Group, "ext-grp-1", "grp-1", false, "Conflict")
        ];

        _syncLogRepository.GetRecentAsync(50, Arg.Any<CancellationToken>()).Returns(logs);

        ScimService service = CreateService();

        IReadOnlyList<ScimSyncLogDto> result = await service.GetSyncLogsAsync(50);

        result.Should().HaveCount(4);
        result.Count(l => l.Success).Should().Be(3);
        result.Count(l => !l.Success).Should().Be(1);
        result.First(l => !l.Success).ErrorMessage.Should().Be("Conflict");
        result.Select(l => l.Operation).Should().Contain(ScimOperation.Create);
        result.Select(l => l.Operation).Should().Contain(ScimOperation.Update);
        result.Select(l => l.Operation).Should().Contain(ScimOperation.Delete);
    }

    private ScimService CreateService(HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

        HttpClient keycloakClient = handler != null
            ? new HttpClient(handler)
            : new HttpClient(new MockKeycloakHttpHandler());
        keycloakClient.BaseAddress = new Uri("https://keycloak.test/");

        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(keycloakClient);

        _tenantContext.TenantId.Returns(_testTenantId);

        ScimUserService userService = new(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            Substitute.For<ILogger<ScimUserService>>());

        ScimGroupService groupService = new(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            Substitute.For<ILogger<ScimGroupService>>());

        return new ScimService(
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            userService,
            groupService,
            _logger);
    }

    private sealed class MockKeycloakHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = [];

        public MockKeycloakHttpHandler WithRoute(string method, string path, HttpStatusCode status,
            object? content = null, string? locationHeader = null)
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
                Content = new StringContent($"No mock configured for {key}")
            });
        }
    }
}
