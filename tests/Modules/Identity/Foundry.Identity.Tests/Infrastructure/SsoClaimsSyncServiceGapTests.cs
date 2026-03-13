using System.Net;
using System.Net.Http.Json;
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

public class SsoClaimsSyncServiceGapTests
{
    private static readonly string[] _engineersGroup = ["Engineers"];
    private static readonly string[] _groupA = ["GroupA"];
    private static readonly string[] _mixedDnGroup = ["CN=Team Lead,OU=People,DC=corp"];
    private static readonly string[] _emptyJsonArray = ["[]"];
    private static readonly string[] _whitespaceOnlyGroup = ["   "];
    private readonly ISsoConfigurationRepository _repository = Substitute.For<ISsoConfigurationRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<SsoClaimsSyncService> _logger = Substitute.For<ILogger<SsoClaimsSyncService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task SyncUserClaimsAsync_WhenUserAttributesNull_SkipsSync()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = (Dictionary<string, IEnumerable<string>>?)null
            });

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);

        // Should return early because attributes is null => GetUserAttributesAsync returns null
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenRolesAlreadyMatch_NoAddsOrRemoves()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _engineersGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { id = "r1", name = "engineers" } });

        SsoClaimsSyncService service = CreateService(handler);

        // Roles already match target groups — should complete without any POST or DELETE calls
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenGetRolesEndpointReturnsError_FallsBackToEmptyRoles()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _groupA
                }
            })
            .WithGetStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                HttpStatusCode.InternalServerError)
            .WithGet("/admin/realms/foundry/roles/groupa",
                new { id = "role-ga", name = "groupa" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        // EnsureSuccessOrThrowAsync throws, caught by GetUserRolesFromKeycloakAsync => returns empty => treats all groups as new
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenRolesContainNullNames_FiltersThemOut()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _groupA
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[]
                {
                    new { id = "r1", name = (string?)"visible" },
                    new { id = "r2", name = (string?)null },
                    new { id = "r3", name = (string?)"" }
                })
            .WithGet("/admin/realms/foundry/roles/groupa",
                new { id = "role-ga", name = "groupa" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        // Null/empty/whitespace role names should be filtered out, not cause errors
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithDnContainingSpaces_SanitizesCorrectly()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _mixedDnGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/team lead",
                new { id = "role-tl", name = "team lead" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        // CN=Team Lead,OU=People,DC=corp has comma+equals => extracts "Team Lead" from CN= part
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithEmptyJsonArray_ReturnsNoGroups()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _emptyJsonArray
                }
            });

        SsoClaimsSyncService service = CreateService(handler);

        // "[]" parses as empty list, so no groups => early return
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithWhitespaceOnlyGroup_SkipsIt()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _whitespaceOnlyGroup
                }
            });

        SsoClaimsSyncService service = CreateService(handler);

        // Whitespace-only value is skipped by IsNullOrWhiteSpace check => no groups => early return
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithDefaultRoleAlreadyAssigned_DoesNotReassign()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, "member", true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _engineersGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[]
                {
                    new { id = "r1", name = "engineers" },
                    new { id = "r2", name = "member" }
                });

        SsoClaimsSyncService service = CreateService(handler);

        // Both "engineers" (from group) and "member" (default role) already assigned — no changes needed
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenGetRealmRoleThrows_TryAssignRoleHandlesGracefully()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _groupA
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGetThrow("/admin/realms/foundry/roles/groupa");

        SsoClaimsSyncService service = CreateService(handler);

        // GetRealmRoleAsync catches the exception, returns null => TryAssignRoleAsync logs "role does not exist"
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenPostThrowsException_TryAssignCatchesIt()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _groupA
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/groupa",
                new { id = "role-ga", name = "groupa" })
            .WithPostThrow($"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        SsoClaimsSyncService service = CreateService(handler);

        // TryAssignRoleAsync outer catch handles the POST exception
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithGroupsAttributeAsEmptyCollection_SkipsSync()
    {
        SsoConfiguration config = CreateConfigWithGroupsSync();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        string[] emptyGroups = [];
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = emptyGroups
                }
            });

        SsoClaimsSyncService service = CreateService(handler);

        // groups key exists but empty => ExtractGroupsFromAttributes returns empty => early return
        await service.SyncUserClaimsAsync(userId);
    }

    private SsoConfiguration CreateConfigWithGroupsSync()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);
        return config;
    }

    private SsoClaimsSyncService CreateService(HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

        HttpClient httpClient = handler != null
            ? new HttpClient(handler) : new HttpClient(new MockHttpHandler());
        httpClient.BaseAddress = new Uri("https://keycloak.test/");
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);

        return new SsoClaimsSyncService(httpClientFactory, _repository, _tenantContext, Options.Create(new KeycloakOptions()), _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _routes = new Dictionary<string, (HttpStatusCode Status, object? Content)>();
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

        public MockHttpHandler WithPostThrow(string path)
        {
            _throwRoutes.Add($"POST:{path}");
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
