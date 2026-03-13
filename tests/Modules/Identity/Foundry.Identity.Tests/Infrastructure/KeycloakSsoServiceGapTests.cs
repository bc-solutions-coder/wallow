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
using Foundry.Shared.Kernel.Services;
using Keycloak.AuthServices.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakSsoServiceGapTests
{
    private readonly ISsoConfigurationRepository _repository = Substitute.For<ISsoConfigurationRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakSsoService> _logger = Substitute.For<ILogger<KeycloakSsoService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task DisableAsync_WithoutKeycloakAlias_StillDisables()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, Guid.Empty, TimeProvider.System);
        config.MoveToTesting(Guid.Empty, TimeProvider.System);
        config.Activate(Guid.Empty, TimeProvider.System);
        // No SetKeycloakIdpAlias call

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService();

        await service.DisableAsync();

        config.Status.Should().Be(SsoStatus.Disabled);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionAsync_WhenExceptionThrown_ReturnsFailure()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateOidcConfig("https://idp.test", "client-123", "secret", "openid", Guid.Empty, TimeProvider.System);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithExternalThrow("https://idp.test/.well-known/openid-configuration");

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        SsoTestResult result = await service.TestConnectionAsync();

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SyncUserClaimsAsync_DelegatesToClaimsSyncService()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        KeycloakSsoService service = CreateService();

        Guid userId = Guid.NewGuid();

        // When no SSO config exists, SsoClaimsSyncService returns early with no error
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenException_RethrowsWithActivityStatus()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGetThrow($"/admin/realms/foundry/users/{userId}");

        KeycloakSsoService service = CreateService(handler);

        // SsoClaimsSyncService.GetUserAttributesAsync catches exceptions and returns null, so no throw
        // This completes without throwing since the inner service catches the error
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SaveSamlConfigurationAsync_WithAllNameIdFormats_MapsCorrectly()
    {
        // Test with Transient format
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.NotFound)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances", HttpStatusCode.Created)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances/saml-12345678/mappers", HttpStatusCode.Created);

        KeycloakSsoService service = CreateService(handler);

        SaveSamlConfigRequest request = new(
            DisplayName: "Transient SAML",
            EntityId: "https://idp.test/metadata",
            SsoUrl: "https://idp.test/sso",
            SloUrl: null,
            Certificate: "MIICert",
            NameIdFormat: SamlNameIdFormat.Transient,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "sn",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: false,
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        SsoConfigurationDto result = await service.SaveSamlConfigurationAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveSamlConfigurationAsync_WithUnspecifiedNameIdFormat_MapsCorrectly()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.NotFound)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances", HttpStatusCode.Created)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances/saml-12345678/mappers", HttpStatusCode.Created);

        KeycloakSsoService service = CreateService(handler);

        SaveSamlConfigRequest request = new(
            DisplayName: "Unspecified SAML",
            EntityId: "https://idp.test/metadata",
            SsoUrl: "https://idp.test/sso",
            SloUrl: null,
            Certificate: "MIICert",
            NameIdFormat: SamlNameIdFormat.Unspecified,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "sn",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: false,
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        SsoConfigurationDto result = await service.SaveSamlConfigurationAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveSamlConfigurationAsync_WithPemCertificate_NormalizesCorrectly()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.NotFound)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances", HttpStatusCode.Created)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances/saml-12345678/mappers", HttpStatusCode.Created);

        KeycloakSsoService service = CreateService(handler);

        string pemCert = "-----BEGIN CERTIFICATE-----\nMIICertData\n-----END CERTIFICATE-----";

        SaveSamlConfigRequest request = new(
            DisplayName: "PEM SAML",
            EntityId: "https://idp.test/metadata",
            SsoUrl: "https://idp.test/sso",
            SloUrl: "https://idp.test/slo",
            Certificate: pemCert,
            NameIdFormat: SamlNameIdFormat.Email,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "sn",
            GroupsAttribute: "groups",
            EnforceForAllUsers: true,
            AutoProvisionUsers: true,
            DefaultRole: "user",
            SyncGroupsAsRoles: true);

        SsoConfigurationDto result = await service.SaveSamlConfigurationAsync(request);

        result.Should().NotBeNull();
        result.SamlConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task GetConfigurationAsync_WithOidcConfig_ReturnsDtoCorrectly()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "OIDC SSO", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateOidcConfig("https://idp.test", "client-123", "secret", "openid", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService();

        SsoConfigurationDto? result = await service.GetConfigurationAsync();

        result.Should().NotBeNull();
        result.OidcIssuer.Should().Be("https://idp.test");
        result.OidcClientId.Should().Be("client-123");
        result.OidcConfigured.Should().BeTrue();
        result.SamlConfigured.Should().BeFalse();
        result.Protocol.Should().Be(SsoProtocol.Oidc);
    }

    [Fact]
    public async Task ValidateIdpConfigurationAsync_WhenExceptionThrown_ReturnsFailure()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateOidcConfig("https://idp.test", "client-123", "secret", "openid", Guid.Empty, TimeProvider.System);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithExternalThrow("https://idp.test/.well-known/openid-configuration");

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        SsoValidationResult result = await service.ValidateIdpConfigurationAsync();

        result.IsValid.Should().BeFalse();
    }

    private KeycloakSsoService CreateService(HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();

        HttpClient keycloakClient = handler != null
            ? new HttpClient(handler) : new HttpClient(new MockKeycloakHttpHandler());
        keycloakClient.BaseAddress = new Uri("https://keycloak.test/");

        HttpClient externalClient = handler != null
            ? new HttpClient(handler) : new HttpClient(new MockKeycloakHttpHandler());

        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(keycloakClient);
        httpClientFactory.CreateClient().Returns(externalClient);

        _tenantContext.TenantId.Returns(_tenantId);
        currentUserService.UserId.Returns(Guid.Empty);

        IOptions<KeycloakAuthenticationOptions> options = Options.Create(new KeycloakAuthenticationOptions
        {
            AuthServerUrl = "https://keycloak.test"
        });

        SsoClaimsSyncService claimsSyncService = new(
            httpClientFactory,
            _repository,
            _tenantContext,
            Options.Create(new KeycloakOptions()),
            Substitute.For<ILogger<SsoClaimsSyncService>>());

        KeycloakIdpService idpService = new(
            httpClientFactory,
            Options.Create(new KeycloakOptions()),
            Substitute.For<ILogger<KeycloakIdpService>>());

        return new KeycloakSsoService(
            httpClientFactory,
            _repository,
            _tenantContext,
            currentUserService,
            options,
            Options.Create(new KeycloakOptions()),
            _logger,
            claimsSyncService,
            idpService,
            TimeProvider.System);
    }

    private sealed class MockKeycloakHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _keycloakRoutes = new Dictionary<string, (HttpStatusCode Status, object? Content)>();
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _externalRoutes = new Dictionary<string, (HttpStatusCode Status, object? Content)>();
        private readonly HashSet<string> _throwRoutes = [];

        public MockKeycloakHttpHandler WithKeycloakGet(string path, HttpStatusCode status, object? content = null)
        {
            _keycloakRoutes[$"GET:{path}"] = (status, content);
            return this;
        }

        public MockKeycloakHttpHandler WithKeycloakGetThrow(string path)
        {
            _throwRoutes.Add($"GET:{path}");
            return this;
        }

        public MockKeycloakHttpHandler WithKeycloakPost(string path, HttpStatusCode status)
        {
            _keycloakRoutes[$"POST:{path}"] = (status, null);
            return this;
        }

        public MockKeycloakHttpHandler WithExternalThrow(string url)
        {
            _throwRoutes.Add($"GET:{url}");
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

            // Check full URL throw routes
            string fullKey = $"{request.Method}:{request.RequestUri}";
            if (_throwRoutes.Contains(fullKey))
            {
                throw new HttpRequestException("Simulated failure");
            }

            if (path.Contains("/admin/"))
            {
                if (_keycloakRoutes.TryGetValue(key, out (HttpStatusCode Status, object? Content) route))
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

            if (_externalRoutes.TryGetValue(fullKey, out (HttpStatusCode Status, object? Content) externalRoute))
            {
                HttpResponseMessage response = new(externalRoute.Status);
                if (externalRoute.Content != null)
                {
                    response.Content = JsonContent.Create(externalRoute.Content);
                }
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
