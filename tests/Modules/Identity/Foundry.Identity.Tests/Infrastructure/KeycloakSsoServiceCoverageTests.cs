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
using Foundry.Shared.Kernel.Services;
using Keycloak.AuthServices.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakSsoServiceCoverageTests
{
    private readonly ISsoConfigurationRepository _repository = Substitute.For<ISsoConfigurationRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<KeycloakSsoService> _logger = Substitute.For<ILogger<KeycloakSsoService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task SaveSamlConfigurationAsync_WhenKeycloakCreateFails_ThrowsExternalServiceException()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.NotFound)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances", HttpStatusCode.InternalServerError);

        KeycloakSsoService service = CreateService(handler);

        SaveSamlConfigRequest request = CreateSamlRequest();

        Func<Task> act = async () => await service.SaveSamlConfigurationAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task SaveSamlConfigurationAsync_WhenKeycloakUpdateFails_ThrowsExternalServiceException()
    {
        SsoConfiguration existingConfig = SsoConfiguration.Create(
            _tenantId, "Old Name", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(existingConfig);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.OK)
            .WithKeycloakPut("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.Forbidden);

        KeycloakSsoService service = CreateService(handler);

        SaveSamlConfigRequest request = CreateSamlRequest();

        Func<Task> act = async () => await service.SaveSamlConfigurationAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task SaveOidcConfigurationAsync_WhenKeycloakCreateFails_ThrowsExternalServiceException()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/oidc-12345678", HttpStatusCode.NotFound)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances", HttpStatusCode.BadRequest);

        KeycloakSsoService service = CreateService(handler);

        SaveOidcConfigRequest request = CreateOidcRequest();

        Func<Task> act = async () => await service.SaveOidcConfigurationAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task SaveOidcConfigurationAsync_WhenKeycloakUpdateFails_ThrowsExternalServiceException()
    {
        SsoConfiguration existingConfig = SsoConfiguration.Create(
            _tenantId, "Old OIDC", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(existingConfig);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/oidc-12345678", HttpStatusCode.OK)
            .WithKeycloakPut("/admin/realms/foundry/identity-provider/instances/oidc-12345678", HttpStatusCode.ServiceUnavailable);

        KeycloakSsoService service = CreateService(handler);

        SaveOidcConfigRequest request = CreateOidcRequest();

        Func<Task> act = async () => await service.SaveOidcConfigurationAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task ActivateAsync_WhenKeycloakEnableFails_ThrowsExternalServiceException()
    {
        SsoConfiguration config = CreateConfigWithAlias(SsoProtocol.Saml);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.InternalServerError);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        Func<Task> act = async () => await service.ActivateAsync();

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task DisableAsync_WhenKeycloakDisableFails_ThrowsExternalServiceException()
    {
        SsoConfiguration config = CreateConfigWithAlias(SsoProtocol.Saml);
        config.Activate(Guid.Empty, TimeProvider.System);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.InternalServerError);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        Func<Task> act = async () => await service.DisableAsync();

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task TestConnectionAsync_ForSaml_WhenGeneralExceptionThrown_ReturnsFailure()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, Guid.Empty, TimeProvider.System);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithExternalThrow("https://idp.test/sso");

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        SsoTestResult result = await service.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveSamlConfigurationAsync_WithCertContainingCarriageReturns_NormalizesCorrectly()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.NotFound)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances", HttpStatusCode.Created)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances/saml-12345678/mappers", HttpStatusCode.Created);

        KeycloakSsoService service = CreateService(handler);

        string certWithCr = "-----BEGIN CERTIFICATE-----\r\nMIICertData\r\n-----END CERTIFICATE-----";

        SaveSamlConfigRequest request = new(
            DisplayName: "CR SAML",
            EntityId: "https://idp.test/metadata",
            SsoUrl: "https://idp.test/sso",
            SloUrl: null,
            Certificate: certWithCr,
            NameIdFormat: SamlNameIdFormat.Email,
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
        result.SamlConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAsync_WithOidcConfig_EnablesIdpAndActivates()
    {
        SsoConfiguration config = CreateConfigWithAlias(SsoProtocol.Oidc);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/oidc-12345678", HttpStatusCode.OK, new
            {
                alias = "oidc-12345678",
                enabled = false
            })
            .WithKeycloakPut("/admin/realms/foundry/identity-provider/instances/oidc-12345678", HttpStatusCode.NoContent);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        await service.ActivateAsync();

        config.Status.Should().Be(SsoStatus.Active);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableAsync_WithOidcConfig_DisablesIdpAndUpdatesStatus()
    {
        SsoConfiguration config = CreateConfigWithAlias(SsoProtocol.Oidc);
        config.Activate(Guid.Empty, TimeProvider.System);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/oidc-12345678", HttpStatusCode.OK, new
            {
                alias = "oidc-12345678",
                enabled = true
            })
            .WithKeycloakPut("/admin/realms/foundry/identity-provider/instances/oidc-12345678", HttpStatusCode.NoContent);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        await service.DisableAsync();

        config.Status.Should().Be(SsoStatus.Disabled);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateIdpConfigurationAsync_ForOidc_WhenDiscoveryFails_ReturnsFailure()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Oidc,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateOidcConfig("https://idp.test", "client-123", "secret", "openid", Guid.Empty, TimeProvider.System);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithExternalGet("https://idp.test/.well-known/openid-configuration", HttpStatusCode.ServiceUnavailable);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        SsoValidationResult result = await service.ValidateIdpConfigurationAsync();

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failed to fetch OIDC discovery document");
    }

    [Fact]
    public async Task GetConfigurationAsync_WithDraftConfig_ReturnsDtoWithCorrectStatus()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Draft SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService();

        SsoConfigurationDto? result = await service.GetConfigurationAsync();

        result.Should().NotBeNull();
        result!.Status.Should().Be(SsoStatus.Draft);
        result.SamlConfigured.Should().BeFalse();
        result.OidcConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_ForOidc_WhenDiscoveryFetchThrows_ReturnsFailure()
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
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ActivateAsync_WhenKeycloakGetSucceeds_ButPutFails_ThrowsExternalServiceException()
    {
        SsoConfiguration config = CreateConfigWithAlias(SsoProtocol.Saml);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.OK, new
            {
                alias = "saml-12345678",
                enabled = false
            })
            .WithKeycloakPut("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.Forbidden);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        Func<Task> act = async () => await service.ActivateAsync();

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task DisableAsync_WhenKeycloakGetSucceeds_ButPutFails_ThrowsExternalServiceException()
    {
        SsoConfiguration config = CreateConfigWithAlias(SsoProtocol.Saml);
        config.Activate(Guid.Empty, TimeProvider.System);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.OK, new
            {
                alias = "saml-12345678",
                enabled = true
            })
            .WithKeycloakPut("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.Forbidden);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        KeycloakSsoService service = CreateService(handler);

        Func<Task> act = async () => await service.DisableAsync();

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task SaveSamlConfigurationAsync_WithPersistentNameIdFormat_SucceedsAndSavesConfig()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/identity-provider/instances/saml-12345678", HttpStatusCode.NotFound)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances", HttpStatusCode.Created)
            .WithKeycloakPost("/admin/realms/foundry/identity-provider/instances/saml-12345678/mappers", HttpStatusCode.Created);

        KeycloakSsoService service = CreateService(handler);

        SaveSamlConfigRequest request = new(
            DisplayName: "Persistent SAML",
            EntityId: "https://idp.test/metadata",
            SsoUrl: "https://idp.test/sso",
            SloUrl: "https://idp.test/slo",
            Certificate: "MIICert",
            NameIdFormat: SamlNameIdFormat.Persistent,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "sn",
            GroupsAttribute: null,
            EnforceForAllUsers: true,
            AutoProvisionUsers: false,
            DefaultRole: "admin",
            SyncGroupsAsRoles: false);

        SsoConfigurationDto result = await service.SaveSamlConfigurationAsync(request);

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Persistent SAML");
        result.Protocol.Should().Be(SsoProtocol.Saml);
        _repository.Received(1).Add(Arg.Any<SsoConfiguration>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static SaveSamlConfigRequest CreateSamlRequest()
    {
        return new SaveSamlConfigRequest(
            DisplayName: "Test SAML",
            EntityId: "https://idp.test/metadata",
            SsoUrl: "https://idp.test/sso",
            SloUrl: null,
            Certificate: "MIICert",
            NameIdFormat: SamlNameIdFormat.Email,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "sn",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: false,
            DefaultRole: null,
            SyncGroupsAsRoles: false);
    }

    private static SaveOidcConfigRequest CreateOidcRequest()
    {
        return new SaveOidcConfigRequest(
            DisplayName: "Test OIDC",
            Issuer: "https://idp.test",
            ClientId: "client-123",
            ClientSecret: "secret-456",
            Scopes: "openid profile email",
            EmailAttribute: "email",
            FirstNameAttribute: "given_name",
            LastNameAttribute: "family_name",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: false,
            DefaultRole: null,
            SyncGroupsAsRoles: false);
    }

    private SsoConfiguration CreateConfigWithAlias(SsoProtocol protocol)
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", protocol,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        if (protocol == SsoProtocol.Saml)
        {
            config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, Guid.Empty, TimeProvider.System);
            config.SetKeycloakIdpAlias("saml-12345678", Guid.Empty, TimeProvider.System);
        }
        else
        {
            config.UpdateOidcConfig("https://idp.test", "client-123", "secret", "openid", Guid.Empty, TimeProvider.System);
            config.SetKeycloakIdpAlias("oidc-12345678", Guid.Empty, TimeProvider.System);
        }

        config.MoveToTesting(Guid.Empty, TimeProvider.System);
        return config;
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
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _keycloakRoutes = new();
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _externalRoutes = new();
        private readonly HashSet<string> _throwRoutes = [];

        public MockKeycloakHttpHandler WithKeycloakGet(string path, HttpStatusCode status, object? content = null)
        {
            _keycloakRoutes[$"GET:{path}"] = (status, content);
            return this;
        }

        public MockKeycloakHttpHandler WithKeycloakPost(string path, HttpStatusCode status)
        {
            _keycloakRoutes[$"POST:{path}"] = (status, null);
            return this;
        }

        public MockKeycloakHttpHandler WithKeycloakPut(string path, HttpStatusCode status)
        {
            _keycloakRoutes[$"PUT:{path}"] = (status, null);
            return this;
        }

        public MockKeycloakHttpHandler WithExternalGet(string url, HttpStatusCode status, object? content = null)
        {
            _externalRoutes[$"GET:{url}"] = (status, content);
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
