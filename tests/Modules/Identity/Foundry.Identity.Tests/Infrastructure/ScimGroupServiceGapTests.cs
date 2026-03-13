using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
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
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimGroupServiceGapTests
{
    private static readonly string[] _extFullAttribute = ["ext-full"];
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimGroupService> _logger = Substitute.For<ILogger<ScimGroupService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    // --- GetGroupAsync direct tests ---

    [Fact]
    public async Task GetGroupAsync_WhenNotFound_ReturnsNull()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups/grp-missing", HttpStatusCode.NotFound);

        ScimGroupService service = CreateService(handler);

        ScimGroup? result = await service.GetGroupAsync("grp-missing", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupAsync_WhenDeserializationReturnsNull_ReturnsNull()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull("/admin/realms/foundry/groups/grp-null");

        ScimGroupService service = CreateService(handler);

        ScimGroup? result = await service.GetGroupAsync("grp-null", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupAsync_WhenExceptionThrown_LogsAndReturnsNull()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow("/admin/realms/foundry/groups/grp-throw");

        ScimGroupService service = CreateService(handler);

        ScimGroup? result = await service.GetGroupAsync("grp-throw", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupAsync_Success_MapsAllFields()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups/grp-full", HttpStatusCode.OK, new
            {
                id = "grp-full",
                name = "Full Group",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _extFullAttribute
                }
            })
            .WithGet("/admin/realms/foundry/groups/grp-full/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-a" },
                new { id = "user-b" }
            });

        ScimGroupService service = CreateService(handler);

        ScimGroup? result = await service.GetGroupAsync("grp-full", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be("grp-full");
        result.DisplayName.Should().Be("Full Group");
        result.ExternalId.Should().Be("ext-full");
        result.Members.Should().HaveCount(2);
        result.Members[0].Value.Should().Be("user-a");
        result.Members[0].Ref.Should().Be("/scim/v2/Users/user-a");
        result.Members[0].Type.Should().Be("User");
        result.Meta.Should().NotBeNull();
        result.Meta!.ResourceType.Should().Be("Group");
        result.Meta.Location.Should().Be("/scim/v2/Groups/grp-full");
    }

    [Fact]
    public async Task GetGroupAsync_WithNoAttributes_ReturnsNullExternalId()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups/grp-noattr", HttpStatusCode.OK, new
            {
                id = "grp-noattr",
                name = "No Attributes",
                attributes = (Dictionary<string, IEnumerable<string>>?)null
            })
            .WithGet("/admin/realms/foundry/groups/grp-noattr/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimGroup? result = await service.GetGroupAsync("grp-noattr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupAsync_WithNullName_ReturnsEmptyDisplayName()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups/grp-noname", HttpStatusCode.OK, new
            {
                id = "grp-noname",
                name = (string?)null,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-noname/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimGroup? result = await service.GetGroupAsync("grp-noname", CancellationToken.None);

        result.Should().NotBeNull();
        result!.DisplayName.Should().BeEmpty();
    }

    // --- GetGroupMembersAsync edge cases ---

    [Fact]
    public async Task GetGroupMembersAsync_WhenResponseNotSuccess_ReturnsEmptyMembers()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/grp-mem-fail")
            .WithGet("/admin/realms/foundry/groups/grp-mem-fail", HttpStatusCode.OK, new
            {
                id = "grp-mem-fail",
                name = "MemFail",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-mem-fail/members", HttpStatusCode.InternalServerError);

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "MemFail",
            ExternalId = "ext-mem-fail"
        };

        ScimGroup result = await service.CreateGroupAsync(request);

        result.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGroupMembersAsync_FiltersOutNullAndEmptyIds()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups/grp-filter", HttpStatusCode.OK, new
            {
                id = "grp-filter",
                name = "FilterGroup",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-filter/members", HttpStatusCode.OK, new object[]
            {
                new { id = "user-valid" },
                new { id = (string?)null },
                new { id = "" }
            });

        ScimGroupService service = CreateService(handler);

        ScimGroup? result = await service.GetGroupAsync("grp-filter", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Members.Should().HaveCount(1);
        result.Members[0].Value.Should().Be("user-valid");
    }

    // --- UpdateGroupAsync edge cases ---

    [Fact]
    public async Task UpdateGroupAsync_WithNullMembers_SkipsMemberSync()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-nomem", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-nomem", HttpStatusCode.OK, new
            {
                id = "grp-nomem",
                name = "NoMembers",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-nomem/members", HttpStatusCode.OK, new[]
            {
                new { id = "existing-user" }
            });

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "NoMembers",
            Members = null
        };

        ScimGroup result = await service.UpdateGroupAsync("grp-nomem", request);

        // The existing member should still be there since member sync was skipped
        result.Should().NotBeNull();
        result.Members.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateGroupAsync_WithoutExternalId_UsesGroupIdAsExternalId()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-noid", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-noid", HttpStatusCode.OK, new
            {
                id = "grp-noid",
                name = "FallbackId",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithGet("/admin/realms/foundry/groups/grp-noid/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "FallbackId",
            ExternalId = null
        };

        ScimGroup result = await service.UpdateGroupAsync("grp-noid", request);

        result.Should().NotBeNull();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.ExternalId == "grp-noid" &&
            log.Operation == ScimOperation.Update &&
            log.Success));
    }

    [Fact]
    public async Task UpdateGroupAsync_WithEmptyMembersList_RemovesAllMembers()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-clear", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-clear/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-1" },
                new { id = "user-2" }
            })
            .WithDelete("/admin/realms/foundry/users/user-1/groups/grp-clear", HttpStatusCode.NoContent)
            .WithDelete("/admin/realms/foundry/users/user-2/groups/grp-clear", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-clear", HttpStatusCode.OK, new
            {
                id = "grp-clear",
                name = "Cleared",
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "Cleared",
            Members = Array.Empty<ScimMember>()
        };

        ScimGroup result = await service.UpdateGroupAsync("grp-clear", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateGroupAsync_WhenGetGroupReturnsNull_ThrowsInvalidOperation()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-vanish", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-vanish", HttpStatusCode.NotFound)
            .WithGet("/admin/realms/foundry/groups/grp-vanish/members", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "Vanish"
        };

        Func<Task<ScimGroup>> act = async () => await service.UpdateGroupAsync("grp-vanish", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retrieve updated group*");
    }

    // --- CreateGroupAsync edge cases ---

    [Fact]
    public async Task CreateGroupAsync_WhenGetGroupReturnsNull_ThrowsInvalidOperation()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/grp-gone")
            .WithGet("/admin/realms/foundry/groups/grp-gone", HttpStatusCode.NotFound);

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "Gone",
            ExternalId = "ext-gone"
        };

        Func<Task<ScimGroup>> act = async () => await service.CreateGroupAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retrieve created group*");
    }

    [Fact]
    public async Task CreateGroupAsync_WhenMemberAddFails_LogsFailureAndThrows()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/grp-memfail")
            .WithPut("/admin/realms/foundry/users/bad-user/groups/grp-memfail", HttpStatusCode.NotFound);

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "MemFail",
            ExternalId = "ext-memfail",
            Members = new[]
            {
                new ScimMember { Value = "bad-user" }
            }
        };

        Func<Task<ScimGroup>> act = async () => await service.CreateGroupAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create && !log.Success));
    }

    // --- ListGroupsAsync edge cases ---

    [Fact]
    public async Task ListGroupsAsync_WhenDeserializationReturnsNull_ReturnsEmptyResponse()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull("/admin/realms/foundry/groups");

        ScimGroupService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        result.TotalResults.Should().Be(0);
        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task ListGroupsAsync_WithNegativeStartIndex_ClampsToZero()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups", HttpStatusCode.OK, Array.Empty<object>());

        ScimGroupService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: -5, Count: 10);

        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        result.Should().NotBeNull();
        result.Resources.Should().BeEmpty();
        result.StartIndex.Should().Be(-5);
    }

    [Fact]
    public async Task ListGroupsAsync_WhenKeycloakFails_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/groups", HttpStatusCode.InternalServerError);

        ScimGroupService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        Func<Task<ScimListResponse<ScimGroup>>> act = async () => await service.ListGroupsAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    // --- DeleteGroupAsync edge cases ---

    [Fact]
    public async Task DeleteGroupAsync_WhenSyncLogFails_OperationStillSucceeds()
    {
        _syncLogRepository.When(x => x.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("DB save failed"));

        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete("/admin/realms/foundry/groups/grp-dbfail", HttpStatusCode.NoContent);

        ScimGroupService service = CreateService(handler);

        // Should not throw — sync log failure is swallowed
        await service.DeleteGroupAsync("grp-dbfail");
    }

    // --- AddUserToGroupAsync / RemoveUserFromGroupAsync edge cases ---

    [Fact]
    public async Task UpdateGroupAsync_WhenRemoveMemberFails_LogsFailureAndThrows()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-rmfail", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-rmfail/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-stuck" }
            })
            .WithDelete("/admin/realms/foundry/users/user-stuck/groups/grp-rmfail", HttpStatusCode.InternalServerError);

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "RemoveFail",
            Members = Array.Empty<ScimMember>()
        };

        Func<Task<ScimGroup>> act = async () => await service.UpdateGroupAsync("grp-rmfail", request);

        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update && !log.Success));
    }

    [Fact]
    public async Task UpdateGroupAsync_RetainsExistingMembersNotInRemoveList()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/groups/grp-retain", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-retain/members", HttpStatusCode.OK, new[]
            {
                new { id = "user-keep" },
                new { id = "user-remove" }
            })
            .WithDelete("/admin/realms/foundry/users/user-remove/groups/grp-retain", HttpStatusCode.NoContent)
            .WithPut("/admin/realms/foundry/users/user-add/groups/grp-retain", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/groups/grp-retain", HttpStatusCode.OK, new
            {
                id = "grp-retain",
                name = "Retained",
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimGroupService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "Retained",
            Members = new[]
            {
                new ScimMember { Value = "user-keep" },
                new ScimMember { Value = "user-add" }
            }
        };

        ScimGroup result = await service.UpdateGroupAsync("grp-retain", request);

        result.Should().NotBeNull();
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
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader, bool IsNull)> _routes = new();
        private readonly HashSet<string> _throwRoutes = [];

        public MockHttpHandler WithGet(string path, HttpStatusCode status, object? content = null)
        {
            _routes[$"GET:{path}"] = (status, content, null, false);
            return this;
        }

        public MockHttpHandler WithGetNull(string path)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, null, null, true);
            return this;
        }

        public MockHttpHandler WithGetThrow(string path)
        {
            _throwRoutes.Add($"GET:{path}");
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string? locationHeader = null)
        {
            _routes[$"POST:{path}"] = (status, null, locationHeader, false);
            return this;
        }

        public MockHttpHandler WithPut(string path, HttpStatusCode status)
        {
            _routes[$"PUT:{path}"] = (status, null, null, false);
            return this;
        }

        public MockHttpHandler WithDelete(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null, null, false);
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

            if (_routes.TryGetValue(key, out (HttpStatusCode Status, object? Content, string? LocationHeader, bool IsNull) route))
            {
                HttpResponseMessage response = new(route.Status);
                if (route.IsNull)
                {
                    response.Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
                }
                else if (route.Content != null)
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
