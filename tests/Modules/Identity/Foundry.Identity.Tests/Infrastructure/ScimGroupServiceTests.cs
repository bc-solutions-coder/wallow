using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Shared.Kernel.Domain;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

using Foundry.Identity.Infrastructure;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimGroupServiceTests
{
    private static readonly string[] _scimExternalIdAttribute = ["ext-grp-1"];
    private static readonly string[] _scimExtAttribute1 = ["ext-1"];
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimGroupService> _logger = Substitute.For<ILogger<ScimGroupService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task CreateGroupAsync_Success_ReturnsScimGroup()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/grp-1")
            .WithGet("/admin/realms/foundry/groups/grp-1", HttpStatusCode.OK, new
            {
                id = "grp-1",
                name = "Engineering",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalIdAttribute
                }
            })
            .WithGet("/admin/realms/foundry/groups/grp-1/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Engineering",
            ExternalId = "ext-grp-1"
        };

        ScimGroup result = await service.CreateGroupAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().Be("grp-1");
        result.DisplayName.Should().Be("Engineering");
        result.ExternalId.Should().Be("ext-grp-1");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));
    }

    [Fact]
    public async Task CreateGroupAsync_WithMembers_AddsUsersToGroup()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/grp-2")
            .WithPut("/admin/realms/foundry/users/user-1/groups/grp-2", HttpStatusCode.NoContent)
            .WithPut("/admin/realms/foundry/users/user-2/groups/grp-2", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-2", HttpStatusCode.OK, new
            {
                id = "grp-2",
                name = "Team",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-2/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-1" },
                new { id = "user-2" }
            });

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Team",
            Members = new[]
            {
                new ScimMember { Value = "user-1" },
                new ScimMember { Value = "user-2" }
            }
        };

        ScimGroup result = await service.CreateGroupAsync(request);

        result.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateGroupAsync_WithoutExternalId_GeneratesOne()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/grp-3")
            .WithGet("/admin/realms/foundry/groups/grp-3", HttpStatusCode.OK, new
            {
                id = "grp-3",
                name = "NoExt",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-3/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "NoExt",
            ExternalId = null
        };

        ScimGroup result = await service.CreateGroupAsync(request);

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("NoExt");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log => log.Success));
    }

    [Fact]
    public async Task CreateGroupAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.BadRequest);

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Fail Group",
            ExternalId = "ext-fail"
        };

        Func<Task<ScimGroup>> act = async () => await service.CreateGroupAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.Group &&
            !log.Success));
    }

    [Fact]
    public async Task CreateGroupAsync_MissingLocationHeader_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created);

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "No Location",
            ExternalId = "ext-no-loc"
        };

        Func<Task<ScimGroup>> act = async () => await service.CreateGroupAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
    }

    [Fact]
    public async Task UpdateGroupAsync_Success_ReturnsUpdatedGroup()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-1", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-1/members", HttpStatusCode.OK, Array.Empty<object>())
            .WithGet("/admin/realms/foundry/groups/grp-1", HttpStatusCode.OK, new
            {
                id = "grp-1",
                name = "Updated Team",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExtAttribute1
                }
            });

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Updated Team",
            ExternalId = "ext-1"
        };

        ScimGroup result = await service.UpdateGroupAsync("grp-1", request);

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Updated Team");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
            log.Success));
    }

    [Fact]
    public async Task UpdateGroupAsync_WithMemberChanges_AddsAndRemovesMembers()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-1", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-1/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-old" }
            })
            .WithDelete("/admin/realms/foundry/users/user-old/groups/grp-1", HttpStatusCode.NoContent)
            .WithPut("/admin/realms/foundry/users/user-new/groups/grp-1", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-1", HttpStatusCode.OK, new
            {
                id = "grp-1",
                name = "Team",
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Team",
            Members = new[]
            {
                new ScimMember { Value = "user-new" }
            }
        };

        ScimGroup result = await service.UpdateGroupAsync("grp-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateGroupAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-1", HttpStatusCode.InternalServerError);

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "Fail Update"
        };

        Func<Task<ScimGroup>> act = async () => await service.UpdateGroupAsync("grp-1", request);

        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update && !log.Success));
    }

    [Fact]
    public async Task DeleteGroupAsync_Success_LogsSync()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete("/admin/realms/foundry/groups/grp-1", HttpStatusCode.NoContent);

        ScimGroupService service = CreateService(handler);

        await service.DeleteGroupAsync("grp-1");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete("/admin/realms/foundry/groups/grp-1", HttpStatusCode.NotFound);

        ScimGroupService service = CreateService(handler);

        Func<Task> act = async () => await service.DeleteGroupAsync("grp-1");

        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete && !log.Success));
    }

    [Fact]
    public async Task ListGroupsAsync_ReturnsPaginatedGroups()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups", HttpStatusCode.OK, new[]
            {
                new { id = "grp-1", name = "Group 1" },
                new { id = "grp-2", name = "Group 2" }
            })
            .WithGet("/admin/realms/foundry/groups/grp-1", HttpStatusCode.OK, new
            {
                id = "grp-1",
                name = "Group 1",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-1/members", HttpStatusCode.OK, Array.Empty<object>())
            .WithGet("/admin/realms/foundry/groups/grp-2", HttpStatusCode.OK, new
            {
                id = "grp-2",
                name = "Group 2",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-2/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        result.Should().NotBeNull();
        result.TotalResults.Should().Be(2);
        result.Resources.Should().HaveCount(2);
        result.StartIndex.Should().Be(1);
    }

    [Fact]
    public async Task ListGroupsAsync_SkipsGroupsWithNullId()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups", HttpStatusCode.OK, new[]
            {
                new { id = (string?)null, name = "Bad Group" },
                new { id = (string?)"grp-1", name = "Good Group" }
            })
            .WithGet("/admin/realms/foundry/groups/grp-1", HttpStatusCode.OK, new
            {
                id = "grp-1",
                name = "Good Group",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-1/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        result.Resources.Should().HaveCount(1);
        result.Resources[0].DisplayName.Should().Be("Good Group");
    }

    [Fact]
    public async Task ListGroupsAsync_ClampsPaginationValues()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 0, Count: 500);

        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        result.Resources.Should().BeEmpty();
        result.TotalResults.Should().Be(0);
    }

    [Fact]
    public async Task GetGroupAsync_WhenNotFound_ReturnsNull_InListResult()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups", HttpStatusCode.OK, new[]
            {
                new { id = "grp-404", name = "Gone" }
            })
            .WithGet("/admin/realms/foundry/groups/grp-404", HttpStatusCode.NotFound);

        ScimGroupService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGroupMembersAsync_WhenFails_ReturnsEmptyList()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/grp-err")
            .WithGet("/admin/realms/foundry/groups/grp-err", HttpStatusCode.OK, new
            {
                id = "grp-err",
                name = "ErrorMembers",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGetThrow("/admin/realms/foundry/groups/grp-err/members");

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new()
        {
            DisplayName = "ErrorMembers",
            ExternalId = "ext-err"
        };

        ScimGroup result = await service.CreateGroupAsync(request);

        result.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task LogSyncAsync_WhenSyncLogFails_DoesNotThrow()
    {
        _syncLogRepository.When(x => x.Add(Arg.Any<ScimSyncLog>())).Do(_ => throw new InvalidOperationException("DB error"));

        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete("/admin/realms/foundry/groups/grp-ok", HttpStatusCode.NoContent);

        ScimGroupService service = CreateService(handler);

        // Should not throw even though sync logging fails
        await service.DeleteGroupAsync("grp-ok");
    }

    [Fact]
    public async Task LogSyncAsync_UpdatesScimConfiguration_WhenConfigExists()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete("/admin/realms/foundry/groups/grp-sync", HttpStatusCode.NoContent);

        ScimGroupService service = CreateService(handler);

        await service.DeleteGroupAsync("grp-sync");

        await _scimRepository.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ScimGroupService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);

        return new ScimGroupService(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            Options.Create(new KeycloakOptions()),
            _logger,
            TimeProvider.System);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = [];
        private readonly HashSet<string> _throwRoutes = [];

        public MockHttpHandler WithGet(string path, HttpStatusCode status, object? content = null)
        {
            _routes[$"GET:{path}"] = (status, content, null);
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string? locationHeader = null)
        {
            _routes[$"POST:{path}"] = (status, null, locationHeader);
            return this;
        }

        public MockHttpHandler WithPut(string path, HttpStatusCode status)
        {
            _routes[$"PUT:{path}"] = (status, null, null);
            return this;
        }

        public MockHttpHandler WithDelete(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null, null);
            return this;
        }

        public MockHttpHandler WithGetThrow(string path)
        {
            _throwRoutes.Add($"GET:{path}");
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

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
