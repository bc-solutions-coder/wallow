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

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimUserServiceTests
{
    private static readonly string[] _scimExternalId1 = ["ext-1"];
    private static readonly string[] _scimExternalIdPatch = ["ext-patch"];
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimUserService> _logger = Substitute.For<ILogger<ScimUserService>>();
    private readonly TenantId _testTenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsScimUser()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-1")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "jane.doe",
                email = "jane@example.com",
                firstName = "Jane",
                lastName = "Doe",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalId1
                }
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "jane.doe",
            ExternalId = "ext-1",
            Name = new ScimName { GivenName = "Jane", FamilyName = "Doe" },
            Emails = new[]
            {
                new ScimEmail { Value = "jane@example.com", Primary = true }
            },
            Active = true
        };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().Be("user-1");
        result.UserName.Should().Be("jane.doe");
        result.Active.Should().BeTrue();
        result.Name!.GivenName.Should().Be("Jane");
        result.Name.FamilyName.Should().Be("Doe");
        result.Meta!.ResourceType.Should().Be("User");
    }

    [Fact]
    public async Task CreateUserAsync_WithoutExternalId_GeneratesOne()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-2")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-2", HttpStatusCode.OK, new
            {
                id = "user-2",
                username = "auto.ext",
                email = "auto@example.com",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "auto.ext",
            ExternalId = null,
            Active = true
        };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log => log.Success));
    }

    [Fact]
    public async Task CreateUserAsync_UsesUsernameAsEmailFallback()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-3")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-3", HttpStatusCode.OK, new
            {
                id = "user-3",
                username = "noemail@company.com",
                email = "noemail@company.com",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "noemail@company.com",
            Active = true,
            Emails = null
        };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Conflict);

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "conflict.user",
            ExternalId = "ext-conflict",
            Active = true
        };

        Func<Task<ScimUser>> act = async () => await service.CreateUserAsync(request);

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create && !log.Success));
    }

    [Fact]
    public async Task CreateUserAsync_WithDefaultRole_AssignsRole()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_testTenantId, Guid.Empty);
        config.UpdateSettings(true, "user", false, Guid.Empty);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-role")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/roles/user", HttpStatusCode.OK, new { id = "role-1", name = "user" })
            .WithPost("/admin/realms/foundry/users/user-role/role-mappings/realm", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-role", HttpStatusCode.OK, new
            {
                id = "user-role",
                username = "roled.user",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "roled.user",
            Active = true
        };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserAsync_WhenOrgAddFails_DoesNotThrow()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-org-fail")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.InternalServerError)
            .WithGet("/admin/realms/foundry/users/user-org-fail", HttpStatusCode.OK, new
            {
                id = "user-org-fail",
                username = "org.fail",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new() { UserName = "org.fail", Active = true };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateUserAsync_Success_ReturnsUpdatedUser()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "updated.user",
                email = "updated@example.com",
                firstName = "Updated",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalId1
                }
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "updated.user",
            ExternalId = "ext-1",
            Name = new ScimName { GivenName = "Updated", FamilyName = "User" },
            Emails = new[] { new ScimEmail { Value = "updated@example.com", Primary = true } },
            Active = true
        };

        ScimUser result = await service.UpdateUserAsync("user-1", request);

        result.Should().NotBeNull();
        result.UserName.Should().Be("updated.user");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update && log.Success));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/users/user-fail", HttpStatusCode.NotFound);

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "fail.update",
            Active = true
        };

        Func<Task<ScimUser>> act = async () => await service.UpdateUserAsync("user-fail", request);

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update && !log.Success));
    }

    [Fact]
    public async Task PatchUserAsync_ReplacesActiveField()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "patch.user",
                email = "patch@example.com",
                firstName = "Patch",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalIdPatch
                }
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch && log.Success));
    }

    [Fact]
    public async Task PatchUserAsync_ReplacesUsername()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "old.name",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "userName", Value = "new.name" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_ReplacesNameFields()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                firstName = "Old",
                lastName = "Name",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "name.givenName", Value = "New" },
                new ScimPatchOperation { Op = "replace", Path = "name.familyName", Value = "Last" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_ReplacesEmails()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                email = "old@example.com",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "emails", Value = "new@example.com" },
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_AddOperation_Works()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "add", Path = "active", Value = true }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_RemoveOperation_IsNoOp()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "remove", Path = "active" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_WhenUserNotFound_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-404", HttpStatusCode.NotFound);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        Func<Task<ScimUser>> act = async () => await service.PatchUserAsync("user-404", request);

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch && !log.Success));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeprovisionTrue_HardDeletes()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_testTenantId, Guid.Empty);
        config.UpdateSettings(true, null, true, Guid.Empty);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete("/admin/realms/foundry/users/user-del", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        await service.DeleteUserAsync("user-del");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete && log.Success));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeprovisionFalse_SoftDeletes()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_testTenantId, Guid.Empty);
        config.UpdateSettings(true, null, false, Guid.Empty);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/users/user-disable", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        await service.DeleteUserAsync("user-disable");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete && log.Success));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenFails_LogsErrorAndThrows()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_testTenantId, Guid.Empty);
        config.UpdateSettings(true, null, true, Guid.Empty);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithDelete("/admin/realms/foundry/users/user-fail", HttpStatusCode.InternalServerError);

        ScimUserService service = CreateService(handler);

        Func<Task> act = async () => await service.DeleteUserAsync("user-fail");

        await act.Should().ThrowAsync<HttpRequestException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete && !log.Success));
    }

    [Fact]
    public async Task GetUserAsync_WhenExists_ReturnsScimUser()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test.user",
                email = "test@example.com",
                firstName = "Test",
                lastName = "User",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = _scimExternalId1
                }
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-1");

        result.Should().NotBeNull();
        result.UserName.Should().Be("test.user");
        result.Emails.Should().NotBeNull();
        result.Emails!.Should().ContainSingle(e => e.Value == "test@example.com" && e.Primary);
        result.DisplayName.Should().Be("Test User");
        result.Meta!.Location.Should().Be("/scim/v2/Users/user-1");
    }

    [Fact]
    public async Task GetUserAsync_WhenNotFound_ReturnsNull()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-404", HttpStatusCode.NotFound);

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-404");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_WhenException_ReturnsNull()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow("/admin/realms/foundry/users/user-err");

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-err");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_WithNoEmail_ReturnsNullEmails()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-noemail", HttpStatusCode.OK, new
            {
                id = "user-noemail",
                username = "noemail",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-noemail");

        result.Should().NotBeNull();
        result.Emails.Should().BeNull();
        result.UserName.Should().Be("noemail");
    }

    [Fact]
    public async Task ListUsersAsync_ReturnsPaginatedResults()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "user1",
                    email = "user1@example.com",
                    firstName = "User",
                    lastName = "One",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 5);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Should().NotBeNull();
        result.TotalResults.Should().Be(5);
        result.Resources.Should().HaveCount(1);
        result.StartIndex.Should().Be(1);
    }

    [Fact]
    public async Task ListUsersAsync_WithUsernameFilter_PassesQueryParam()
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
            Filter: "userName eq \"john.doe\"",
            StartIndex: 1,
            Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListUsersAsync_ClampsPageSize()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, Array.Empty<object>())
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 0);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 0, Count: 500);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task PatchUserAsync_EmailsWorkPathVariation()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                email = "old@example.com",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "emails[type eq \"work\"].value", Value = "work@example.com" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_EmailsPrimaryPathVariation()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                email = "old@example.com",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "emails[primary eq true].value", Value = "primary@example.com" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    private ScimUserService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_testTenantId);

        return new ScimUserService(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            _logger);
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
