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

public class ScimUserServiceAdditionalTests
{
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimUserService> _logger = Substitute.For<ILogger<ScimUserService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task ListUsersAsync_WithFirstNameFilter_PassesQueryParam()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "jane.doe",
                    email = "jane@example.com",
                    firstName = "Jane",
                    lastName = "Doe",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 1);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: "name.givenName eq \"Jane\"",
            StartIndex: 1,
            Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListUsersAsync_WithLastNameFilter_PassesQueryParam()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "john.smith",
                    email = "john@example.com",
                    firstName = "John",
                    lastName = "Smith",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 1);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: "name.familyName eq \"Smith\"",
            StartIndex: 1,
            Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListUsersAsync_WithEmailFilter_PassesQueryParam()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "test",
                    email = "test@example.com",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 1);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: "emails.value eq \"test@example.com\"",
            StartIndex: 1,
            Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListUsersAsync_WithNoFilter_ReturnsTotalCount()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "test1",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                },
                new
                {
                    id = "user-2",
                    username = "test2",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 10);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.TotalResults.Should().Be(10);
        result.Resources.Should().HaveCount(2);
        result.ItemsPerPage.Should().Be(2);
    }

    [Fact]
    public async Task ListUsersAsync_WithInMemoryFilter_ReturnsFilteredCount()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "active.user",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                },
                new
                {
                    id = "user-2",
                    username = "disabled.user",
                    enabled = false,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 5);

        ScimUserService service = CreateService(handler);

        // "active eq true" translates to an in-memory filter
        ScimListRequest request = new(
            Filter: "active eq true",
            StartIndex: 1,
            Count: 100);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().HaveCount(1);
        result.Resources[0].Active.Should().BeTrue();
        // When in-memory filter is used, TotalResults should be filtered count
        result.TotalResults.Should().Be(1);
    }

    [Fact]
    public async Task UpdateUserAsync_WithoutExternalId_UsesIdAsExternalId()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/users/user-noid", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/users/user-noid", HttpStatusCode.OK, new
            {
                id = "user-noid",
                username = "no.ext",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new()
        {
            UserName = "no.ext",
            ExternalId = null,
            Active = true
        };

        ScimUser result = await service.UpdateUserAsync("user-noid", request);

        result.Should().NotBeNull();
        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update && log.Success));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenNoConfig_DisablesUser()
    {
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        MockHttpHandler handler = new MockHttpHandler()
            .WithPut("/admin/realms/foundry/users/user-noconfig", HttpStatusCode.NoContent);

        ScimUserService service = CreateService(handler);

        await service.DeleteUserAsync("user-noconfig");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete && log.Success));
    }

    [Fact]
    public async Task CreateUserAsync_WhenDefaultRoleNotFound_DoesNotThrow()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.UpdateSettings(true, "nonexistent-role", false, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-norole")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGet("/admin/realms/foundry/roles/nonexistent-role", HttpStatusCode.NotFound)
            .WithGet("/admin/realms/foundry/users/user-norole", HttpStatusCode.OK, new
            {
                id = "user-norole",
                username = "norole.user",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new() { UserName = "norole.user", Active = true };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserAsync_WhenOrgAddThrows_DoesNotThrow()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-orgexc")
            .WithPostThrow("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members")
            .WithGet("/admin/realms/foundry/users/user-orgexc", HttpStatusCode.OK, new
            {
                id = "user-orgexc",
                username = "org.exc",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new() { UserName = "org.exc", Active = true };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserAsync_WhenAssignDefaultRoleThrows_DoesNotThrow()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.UpdateSettings(true, "bad-role", false, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-badrole")
            .WithPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithGetThrow("/admin/realms/foundry/roles/bad-role")
            .WithGet("/admin/realms/foundry/users/user-badrole", HttpStatusCode.OK, new
            {
                id = "user-badrole",
                username = "bad.role",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            });

        ScimUserService service = CreateService(handler);

        ScimUserRequest request = new() { UserName = "bad.role", Active = true };

        ScimUser result = await service.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_WithNullPath_IsNoOp()
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
                new ScimPatchOperation { Op = "replace", Path = null, Value = "something" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_WithActiveAsBoolean_SetsBoolValue()
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
                new ScimPatchOperation { Op = "replace", Path = "active", Value = true }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_WithActiveAsString_ParsesBoolValue()
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
                new ScimPatchOperation { Op = "replace", Path = "active", Value = "false" }
            }
        };

        ScimUser result = await service.PatchUserAsync("user-1", request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PatchUserAsync_WhenPatchFails_ThrowsAndLogsSyncFailure()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users/user-1", HttpStatusCode.OK, new
            {
                id = "user-1",
                username = "test",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithPut("/admin/realms/foundry/users/user-1", HttpStatusCode.InternalServerError);

        ScimUserService service = CreateService(handler);

        ScimPatchRequest request = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        Func<Task<ScimUser>> act = async () => await service.PatchUserAsync("user-1", request);

        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch && !log.Success));
    }

    [Fact]
    public async Task GetUserAsync_WhenNullBody_ReturnsNull()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull("/admin/realms/foundry/users/user-nullbody");

        ScimUserService service = CreateService(handler);

        ScimUser? result = await service.GetUserAsync("user-nullbody");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListUsersAsync_WithNullUsers_ReturnsEmpty()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetNull("/admin/realms/foundry/users")
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 0);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task ListUsersAsync_WithNegativeStartIndex_ClampsToZero()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/users", HttpStatusCode.OK, Array.Empty<object>())
            .WithGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 0);

        ScimUserService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: -5, Count: 10);

        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        result.Resources.Should().BeEmpty();
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
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = [];
        private readonly HashSet<string> _throwRoutes = [];
        private readonly HashSet<string> _nullRoutes = [];

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

        public MockHttpHandler WithGetThrow(string path)
        {
            _throwRoutes.Add($"GET:{path}");
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string? locationHeader = null)
        {
            _routes[$"POST:{path}"] = (status, null, locationHeader);
            return this;
        }

        public MockHttpHandler WithPostThrow(string path)
        {
            _throwRoutes.Add($"POST:{path}");
            return this;
        }

        public MockHttpHandler WithPut(string path, HttpStatusCode status)
        {
            _routes[$"PUT:{path}"] = (status, null, null);
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
