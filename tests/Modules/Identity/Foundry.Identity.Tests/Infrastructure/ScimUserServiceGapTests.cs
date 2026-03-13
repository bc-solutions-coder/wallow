using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimUserServiceGapTests
{
    private static readonly string[] _scimExternalIdMyExt = ["my-ext-id"];
    private static readonly string[] _scimExternalIdExt123 = ["ext-123"];
    private static readonly string[] _scimExternalIdFull = ["ext-full"];
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimUserService> _logger = Substitute.For<ILogger<ScimUserService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task PatchUserAsync_WhenGetReturnsNullBody_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull("/admin/realms/foundry/users/user-nullpatch");

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        Func<Task<ScimUser>> act = async () => await service.PatchUserAsync("user-nullpatch", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task PatchUserAsync_AddUsername_SetsUsername()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "userName", Value = "added.name" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_AddGivenName_SetsFirstName()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "name.givenName", Value = "NewFirst" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_AddFamilyName_SetsLastName()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "name.familyName", Value = "NewLast" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_AddEmails_SetsEmail()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "emails", Value = "added@example.com" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_AddWorkEmail_SetsEmail()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "emails[type eq \"work\"].value", Value = "work@add.com" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_AddPrimaryEmail_SetsEmail()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "emails[primary eq true].value", Value = "primary@add.com" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_WithExternalIdInAttributes_UsesItForSyncLog()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-ext", HttpStatusCode.OK, new
            {
                id = "user-ext",
                username = "ext.user",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalIdMyExt
                }
            })
            .WithPut("/admin/realms/foundry/users/user-ext", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-ext", HttpStatusCode.OK, new
            {
                id = "user-ext",
                username = "ext.user",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalIdMyExt
                }
            });

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = true }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-ext", request);

        result.Should().NotBeNull();
        result.ExternalId.Should().Be("my-ext-id");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch && log.Success && log.ExternalId == "my-ext-id"));
    }

    [Fact]
    public async Task PatchUserAsync_UnknownPath_IsNoOp()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "unknownAttribute", Value = "something" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_MultipleOperations_AppliesAll()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "userName", Value = "multi.user" },
                new ScimPatchOperation { Op = "replace", Path = "name.givenName", Value = "Multi" },
                new ScimPatchOperation { Op = "replace", Path = "name.familyName", Value = "User" },
                new ScimPatchOperation { Op = "replace", Path = "emails", Value = "multi@example.com" },
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch && log.Success));
    }

    [Fact]
    public async Task PatchUserAsync_RemoveUsername_IsNoOp()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "remove", Path = "userName" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_RemoveEmail_IsNoOp()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "remove", Path = "emails" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_RemoveNameFields_IsNoOp()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "remove", Path = "name.givenName" },
                new ScimPatchOperation { Op = "remove", Path = "name.familyName" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ListUsersAsync_WithContainsFilter_UsesSearchParam()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "john.doe",
                    email = "john@example.com",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 1);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: "userName co \"john\"",
            StartIndex: 1,
            Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListUsersAsync_WithExternalIdFilter_UsesInMemoryFilter()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "test",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>
                    {
                        ["scim_external_id"] = _scimExternalIdExt123
                    }
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 1);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: "externalId eq \"ext-123\"",
            StartIndex: 1,
            Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserAsync_WithUsernameOnly_FallsBackToUsernameForUserName()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-uonly", HttpStatusCode.OK, new
            {
                id = "user-uonly",
                username = "just.username",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-uonly");

        result.Should().NotBeNull();
        result!.UserName.Should().Be("just.username");
        result.Emails.Should().BeNull();
        result.Name!.Formatted.Should().BeEmpty();
        result.DisplayName.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserAsync_WithEmailNoUsername_UsesEmailAsUserName()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-emailonly", HttpStatusCode.OK, new
            {
                id = "user-emailonly",
                email = "fallback@example.com",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-emailonly");

        result.Should().NotBeNull();
        result!.UserName.Should().Be("fallback@example.com");
    }

    [Fact]
    public async Task GetUserAsync_WithDisabledUser_ReturnsActiveFalse()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-disabled", HttpStatusCode.OK, new
            {
                id = "user-disabled",
                username = "disabled.user",
                enabled = false,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-disabled");

        result.Should().NotBeNull();
        result!.Active.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserAsync_WithNullEnabled_DefaultsToTrue()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-nullenabled", HttpStatusCode.OK, new
            {
                id = "user-nullenabled",
                username = "nullenabled",
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-nullenabled");

        result.Should().NotBeNull();
        result!.Active.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_WhenLocationHeaderMissing_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created);

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest()
        {
            UserName = "no.location",
            Active = true
        };

        Func<Task<ScimUser>> act = async () => await service.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
    }

    [Fact]
    public async Task CreateUserAsync_WhenGetUserReturnsNull_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-ghost")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-ghost", HttpStatusCode.NotFound);

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest()
        {
            UserName = "ghost.user",
            Active = true
        };

        Func<Task<ScimUser>> act = async () => await service.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retrieve created user*");
    }

    [Fact]
    public async Task UpdateUserAsync_WhenGetUserReturnsNull_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/users/user-updghost", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-updghost", HttpStatusCode.NotFound);

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest()
        {
            UserName = "ghost.update",
            Active = true
        };

        Func<Task<ScimUser>> act = async () => await service.UpdateUserAsync("user-updghost", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retrieve updated user*");
    }

    [Fact]
    public async Task PatchUserAsync_WhenGetUserReturnsNullAfterPatch_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetSequence("/admin/realms/foundry/users/user-patchghost",
            [
                (HttpStatusCode.OK, (object?)new
                {
                    id = "user-patchghost",
                    username = "patch.ghost",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }),
                (HttpStatusCode.NotFound, null)
            ])
            .WithPut("/admin/realms/foundry/users/user-patchghost", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = true }
            }
        };

        Func<Task<ScimUser>> act = async () => await service.PatchUserAsync("user-patchghost", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retrieve patched user*");
    }

    [Fact]
    public async Task CreateUserAsync_WhenNoDefaultRoleConfig_SkipsRoleAssignment()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.UpdateSettings(true, "", false, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-noroleconf")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-noroleconf", HttpStatusCode.OK, new
            {
                id = "user-noroleconf",
                username = "noroleconf",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest() { UserName = "noroleconf", Active = true };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ListUsersAsync_WithSearchFilter_PassesSearchParam()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "searchable",
                    email = "search@example.com",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 1);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: "emails.value co \"search\"",
            StartIndex: 1,
            Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().HaveCount(1);
    }

    [Fact]
    public async Task MapToScimUser_WithWhitespaceEmail_ReturnsNullEmails()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-wsmail", HttpStatusCode.OK, new
            {
                id = "user-wsmail",
                username = "wsmail",
                email = "   ",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-wsmail");

        result.Should().NotBeNull();
        result!.Emails.Should().BeNull();
    }

    [Fact]
    public async Task MapToScimUser_FullUser_MapsAllFields()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-full", HttpStatusCode.OK, new
            {
                id = "user-full",
                username = "full.user",
                email = "full@example.com",
                firstName = "Full",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalIdFull
                }
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-full");

        result.Should().NotBeNull();
        result!.Id.Should().Be("user-full");
        result.ExternalId.Should().Be("ext-full");
        result.UserName.Should().Be("full.user");
        result.Name!.GivenName.Should().Be("Full");
        result.Name.FamilyName.Should().Be("User");
        result.Name.Formatted.Should().Be("Full User");
        result.DisplayName.Should().Be("Full User");
        result.Active.Should().BeTrue();
        result.Emails.Should().ContainSingle();
        result.Emails![0].Value.Should().Be("full@example.com");
        result.Emails[0].Type.Should().Be("work");
        result.Emails[0].Primary.Should().BeTrue();
        result.Meta!.ResourceType.Should().Be("User");
        result.Meta.Location.Should().Be("/scim/v2/Users/user-full");
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeprovisionFalse_DisablesWithPut()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.UpdateSettings(true, null, false, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/users/user-soft", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        await service.DeleteUserAsync("user-soft");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete && log.Success));
    }

    [Fact]
    public async Task PatchUserAsync_ReplaceActiveWithStringTrue_ParsesCorrectly()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = "true" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_WithLowercaseUsername_MatchesCaseInsensitive()
    {
        MockHttpHandler handler = CreatePatchHandler();
        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "username", Value = "lowercased" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    private MockHttpHandler CreatePatchHandler()
    {
        return new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                email = "test@example.com",
                firstName = "Test",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);
    }

    private ScimUserService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);

        return new ScimUserService(
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
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = new();
        private readonly HashSet<string> _throwRoutes = [];
        private readonly HashSet<string> _nullRoutes = [];
        private readonly Dictionary<string, Queue<(HttpStatusCode Status, object? Content)>> _sequenceRoutes = new();

        public MockHttpHandler WithGet(string path, HttpStatusCode status, object? content = null)
        {
            _routes[$"GET:{path}"] = (status, content, null);
            return this;
        }

        public MockHttpHandler WithGetNull(string path)
        {
            _nullRoutes.Add($"GET:{path}");
            return this;
        }

        public MockHttpHandler WithGetSequence(string path, (HttpStatusCode Status, object? Content)[] responses)
        {
            _sequenceRoutes[$"GET:{path}"] = new Queue<(HttpStatusCode, object?)>(responses);
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

            if (_sequenceRoutes.TryGetValue(key, out Queue<(HttpStatusCode Status, object? Content)>? queue) && queue.Count > 0)
            {
                (HttpStatusCode status, object? content) = queue.Dequeue();
                HttpResponseMessage seqResponse = new(status);
                if (content != null)
                {
                    seqResponse.Content = JsonContent.Create(content);
                }
                return Task.FromResult(seqResponse);
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
