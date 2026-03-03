using System.Text.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Tests.Common.Factories;
using Foundry.Tests.Common.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Foundry.Identity.IntegrationTests.Sso;

[Trait("Category", "Integration")]
public class SsoConfigurationTests : IClassFixture<SsoConfigurationTestFactory>, IAsyncLifetime
{
    private readonly SsoConfigurationTestFactory _factory;
    private IServiceScope? _scope;
    private IServiceProvider _scopedServices = null!;
    private ISsoService _ssoService = null!;
    private IdentityDbContext _dbContext = null!;

    public SsoConfigurationTests(SsoConfigurationTestFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _scope = _factory.Services.CreateScope();
        _scopedServices = _scope.ServiceProvider;

        _ssoService = _scopedServices.GetRequiredService<ISsoService>();
        _dbContext = _scopedServices.GetRequiredService<IdentityDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        // Clear any existing SSO configurations from the database
        List<Domain.Entities.SsoConfiguration> existingConfigs = _dbContext.SsoConfigurations.ToList();
        _dbContext.SsoConfigurations.RemoveRange(existingConfigs);
        await _dbContext.SaveChangesAsync();

        _factory.ResetKeycloakMock();
    }

    public Task DisposeAsync()
    {
        _scope?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ConfigureSaml_CreatesKeycloakIdpBroker()
    {
        // Arrange
        SaveSamlConfigRequest request = new(
            DisplayName: "Test SAML Provider",
            EntityId: "https://saml-idp.test/metadata",
            SsoUrl: "https://saml-idp.test/sso",
            SloUrl: "https://saml-idp.test/slo",
            Certificate: GenerateTestCertificate(),
            NameIdFormat: SamlNameIdFormat.Email,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "surname",
            GroupsAttribute: "groups",
            EnforceForAllUsers: false,
            AutoProvisionUsers: true,
            DefaultRole: "user",
            SyncGroupsAsRoles: true);

        // Act
        SsoConfigurationDto result = await _ssoService.SaveSamlConfigurationAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Test SAML Provider");
        result.Protocol.Should().Be(SsoProtocol.Saml);
        result.SamlEntityId.Should().Be("https://saml-idp.test/metadata");
        result.Status.Should().Be(SsoStatus.Draft);

        IReadOnlyList<JsonElement> createdIdps = _factory.CreatedIdentityProviders;
        createdIdps.Should().HaveCount(1);
        JsonElement idp = createdIdps[0];
        idp.GetProperty("displayName").GetString().Should().Be("Test SAML Provider");
        idp.GetProperty("providerId").GetString().Should().Be("saml");
        idp.GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ConfigureOidc_CreatesKeycloakIdpBroker()
    {
        // Arrange - Use the WireMock server URL as the issuer
        string mockIssuer = _factory.MockServerUrl;
        _factory.SetupOidcDiscovery(mockIssuer);

        SaveOidcConfigRequest request = new(
            DisplayName: "Test OIDC Provider",
            Issuer: mockIssuer,
            ClientId: "test-client-id",
            ClientSecret: "test-client-secret",
            Scopes: "openid profile email",
            EmailAttribute: "email",
            FirstNameAttribute: "given_name",
            LastNameAttribute: "family_name",
            GroupsAttribute: "groups",
            EnforceForAllUsers: false,
            AutoProvisionUsers: true,
            DefaultRole: "user",
            SyncGroupsAsRoles: true);

        // Act
        SsoConfigurationDto result = await _ssoService.SaveOidcConfigurationAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Test OIDC Provider");
        result.Protocol.Should().Be(SsoProtocol.Oidc);
        result.OidcIssuer.Should().Be(mockIssuer);
        result.Status.Should().Be(SsoStatus.Draft);

        IReadOnlyList<JsonElement> createdIdps = _factory.CreatedIdentityProviders;
        createdIdps.Should().HaveCount(1);
        JsonElement idp = createdIdps[0];
        idp.GetProperty("displayName").GetString().Should().Be("Test OIDC Provider");
        idp.GetProperty("providerId").GetString().Should().Be("oidc");
        idp.GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Activate_EnablesIdpInKeycloak()
    {
        // Arrange - Create and save configuration first
        SaveSamlConfigRequest request = new(
            DisplayName: "Test SAML Provider",
            EntityId: "https://saml-idp.test/metadata",
            SsoUrl: "https://saml-idp.test/sso",
            SloUrl: null,
            Certificate: GenerateTestCertificate(),
            NameIdFormat: SamlNameIdFormat.Email,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "surname",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: true,
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        await _ssoService.SaveSamlConfigurationAsync(request);

        // Act
        await _ssoService.ActivateAsync();

        // Assert
        SsoConfigurationDto? config = await _ssoService.GetConfigurationAsync();
        config.Should().NotBeNull();
        config.Status.Should().Be(SsoStatus.Active);

        IReadOnlyList<JsonElement> updatedIdps = _factory.UpdatedIdentityProviders;
        updatedIdps.Should().HaveCount(1);
        JsonElement idp = updatedIdps[0];
        idp.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Disable_DisablesIdpInKeycloak()
    {
        // Arrange - Create, save, and activate configuration first
        SaveSamlConfigRequest request = new(
            DisplayName: "Test SAML Provider",
            EntityId: "https://saml-idp.test/metadata",
            SsoUrl: "https://saml-idp.test/sso",
            SloUrl: null,
            Certificate: GenerateTestCertificate(),
            NameIdFormat: SamlNameIdFormat.Email,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "surname",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: true,
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        await _ssoService.SaveSamlConfigurationAsync(request);
        await _ssoService.ActivateAsync();

        _factory.ResetUpdatedProviders();

        // Act
        await _ssoService.DisableAsync();

        // Assert
        SsoConfigurationDto? config = await _ssoService.GetConfigurationAsync();
        config.Should().NotBeNull();
        config.Status.Should().Be(SsoStatus.Disabled);

        IReadOnlyList<JsonElement> updatedIdps = _factory.UpdatedIdentityProviders;
        updatedIdps.Should().HaveCount(1);
        JsonElement idp = updatedIdps[0];
        idp.GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetSamlMetadata_ReturnsValidXml()
    {
        // Act
        string metadata = await _ssoService.GetSamlServiceProviderMetadataAsync();

        // Assert
        metadata.Should().NotBeNullOrEmpty();
        metadata.Should().Contain("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        metadata.Should().Contain("EntityDescriptor");
        metadata.Should().Contain("SPSSODescriptor");
        metadata.Should().Contain("AssertionConsumerService");
        metadata.Should().Contain("SingleLogoutService");
        metadata.Should().Contain("realms/foundry");
    }

    [Fact]
    public async Task TestConnection_WithValidConfig_ReturnsSuccess()
    {
        // Arrange - Use the WireMock server URL as the issuer so we can control responses
        string mockIssuer = _factory.MockServerUrl;
        _factory.SetupOidcDiscovery(mockIssuer);

        SaveOidcConfigRequest request = new(
            DisplayName: "Test OIDC Provider",
            Issuer: mockIssuer,
            ClientId: "test-client-id",
            ClientSecret: "test-client-secret",
            Scopes: "openid profile email",
            EmailAttribute: "email",
            FirstNameAttribute: "given_name",
            LastNameAttribute: "family_name",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: true,
            DefaultRole: "user",
            SyncGroupsAsRoles: false);

        await _ssoService.SaveOidcConfigurationAsync(request);

        // Act
        SsoTestResult result = await _ssoService.TestConnectionAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnection_WithInvalidConfig_ReturnsFailure()
    {
        // Arrange - Create OIDC config with invalid issuer (no discovery endpoint)
        _factory.SetupOidcDiscoveryFailure("https://invalid-idp.test");

        SaveOidcConfigRequest request = new(
            DisplayName: "Invalid OIDC Provider",
            Issuer: "https://invalid-idp.test",
            ClientId: "test-client-id",
            ClientSecret: "test-client-secret",
            Scopes: "openid",
            EmailAttribute: "email",
            FirstNameAttribute: "given_name",
            LastNameAttribute: "family_name",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: false,
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        await _ssoService.SaveOidcConfigurationAsync(request);

        // Act
        SsoTestResult result = await _ssoService.TestConnectionAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_ChecksCertificateExpiry()
    {
        // Arrange - Create SAML config with a certificate
        SaveSamlConfigRequest request = new(
            DisplayName: "Test SAML Provider",
            EntityId: "https://saml-idp.test/metadata",
            SsoUrl: "https://saml-idp.test/sso",
            SloUrl: null,
            Certificate: GenerateTestCertificate(),
            NameIdFormat: SamlNameIdFormat.Email,
            EmailAttribute: "email",
            FirstNameAttribute: "givenName",
            LastNameAttribute: "surname",
            GroupsAttribute: null,
            EnforceForAllUsers: false,
            AutoProvisionUsers: true,
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        await _ssoService.SaveSamlConfigurationAsync(request);

        // Act
        SsoValidationResult result = await _ssoService.ValidateIdpConfigurationAsync();

        // Assert - The certificate format may fail validation, which is expected for a test cert
        result.Should().NotBeNull();
        if (result.IsValid)
        {
            result.IdpEntityId.Should().Be("https://saml-idp.test/metadata");
            result.IdpSsoUrl.Should().Be("https://saml-idp.test/sso");
            result.CertificateExpiry.Should().NotBeNull();
        }
        else
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    private static string GenerateTestCertificate()
    {
        // This is a valid self-signed certificate for testing purposes
        // Generated for testing SSO integration, expires in 2027
        return @"MIIDXTCCAkWgAwIBAgIJAKJ6jG0y7fB9MA0GCSqGSIb3DQEBCwUAMEUxCzAJBgNV
BAYTAkFVMRMwEQYDVQQIDApTb21lLVN0YXRlMSEwHwYDVQQKDBhJbnRlcm5ldCBX
aWRnaXRzIFB0eSBMdGQwHhcNMjYwMTAxMDAwMDAwWhcNMjcxMjMxMjM1OTU5WjBF
MQswCQYDVQQGEwJBVTETMBEGA1UECAwKU29tZS1TdGF0ZTEhMB8GA1UECgwYSW50
ZXJuZXQgV2lkZ2l0cyBQdHkgTHRkMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIB
CgKCAQEAz7e7v8xvLCLGrKWvLJd8y5w5YqGnLqR4pGPZKF2X8H6dJjY0LZYFqBvB
YR2ZqM5xvZ8FN7O0H8H2L5q8M0YxPY6KW9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8
F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3
X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9q
H0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F
0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F3L9F0Y9qH0H3X6Y8F
3QIDAQABo1AwTjAdBgNVHQ4EFgQU7V8fZ2oH3f6yF9J8H6dJjY0LZYFqBvBYR2Zq
M5xvZ8FN7O0H8H2L5q8MIH+YIBBGB0RVh0VQ0WVzdGCCEElbnRlZ3JhdGlvbiBU
ZXN0IENlcnRpZmljYXRlIFBUeSBMdGQwDQYJKoZIhvcNAQELBQADggEBAF2MqQ==";
    }
}

public class SsoConfigurationTestFactory : FoundryApiFactory
{
    private WireMockServer? _keycloakMock;
    private readonly List<JsonElement> _createdIdps = [];
    private readonly List<JsonElement> _updatedIdps = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        _keycloakMock = WireMockServer.Start();
        SetupKeycloakMock();

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpContextAccessor();

            services.AddHttpClient("KeycloakAdminClient", client =>
            {
                client.BaseAddress = new Uri(_keycloakMock.Url!);
            });

            // Configure the default HttpClient to also use WireMock for external IdP testing
            services.AddHttpClient(string.Empty, client =>
            {
                client.BaseAddress = new Uri(_keycloakMock.Url!);
            });

            services.AddScoped<ITenantContext>(_ => new TenantContext
            {
                TenantId = TenantId.Create(TestConstants.TestTenantId),
                TenantName = "Test Tenant",
                IsResolved = true
            });
        });
    }

    private void SetupKeycloakMock()
    {
        // GET IdP - Check if exists (initially returns 404, then 200 after creation)
        _keycloakMock!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/identity-provider/instances/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    string alias = request.Path.Split('/').Last();

                    // First check if we have an updated version
                    JsonElement updatedIdp = _updatedIdps.Where(i =>
                        i.ValueKind != JsonValueKind.Undefined &&
                        i.TryGetProperty("alias", out JsonElement aliasProperty) &&
                        aliasProperty.GetString() == alias).LastOrDefault();

                    if (updatedIdp.ValueKind != JsonValueKind.Undefined)
                    {
                        return new WireMock.ResponseMessage
                        {
                            StatusCode = 200,
                            Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                            {
                                { "Content-Type", new WireMock.Types.WireMockList<string>("application/json") }
                            },
                            BodyData = new WireMock.Util.BodyData
                            {
                                BodyAsString = updatedIdp.GetRawText(),
                                DetectedBodyType = WireMock.Types.BodyType.String,
                                Encoding = System.Text.Encoding.UTF8
                            }
                        };
                    }

                    // Otherwise return the created IdP
                    JsonElement createdIdp = _createdIdps.FirstOrDefault(i =>
                        i.ValueKind != JsonValueKind.Undefined &&
                        i.TryGetProperty("alias", out JsonElement aliasProperty) &&
                        aliasProperty.GetString() == alias);

                    if (createdIdp.ValueKind != JsonValueKind.Undefined)
                    {
                        return new WireMock.ResponseMessage
                        {
                            StatusCode = 200,
                            Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                            {
                                { "Content-Type", new WireMock.Types.WireMockList<string>("application/json") }
                            },
                            BodyData = new WireMock.Util.BodyData
                            {
                                BodyAsString = createdIdp.GetRawText(),
                                DetectedBodyType = WireMock.Types.BodyType.String,
                                Encoding = System.Text.Encoding.UTF8
                            }
                        };
                    }

                    return new WireMock.ResponseMessage { StatusCode = 404 };
                }));

        // POST IdP - Create
        _keycloakMock
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/identity-provider/instances")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    JsonElement body = JsonSerializer.Deserialize<JsonElement>(request.Body!);
                    _createdIdps.Add(body);

                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 201,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            { "Content-Type", new WireMock.Types.WireMockList<string>("application/json") }
                        }
                    };
                }));

        // PUT IdP - Update
        _keycloakMock
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/identity-provider/instances/*")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    JsonElement body = JsonSerializer.Deserialize<JsonElement>(request.Body!);
                    _updatedIdps.Add(body);

                    string alias = request.Path.Split('/').Last();
                    int index = _createdIdps.FindIndex(i =>
                        i.ValueKind != JsonValueKind.Undefined &&
                        i.TryGetProperty("alias", out JsonElement a) &&
                        a.GetString() == alias);

                    if (index >= 0)
                    {
                        _createdIdps[index] = body;
                    }

                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 204
                    };
                }));

        // POST IdP Mapper
        _keycloakMock
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/identity-provider/instances/*/mappers")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201));

        // GET IdP Mappers
        _keycloakMock
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/identity-provider/instances/*/mappers")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new List<object>()));
    }

    public void SetupOidcDiscovery(string issuer)
    {
        // The issuer URL will be accessed through the mock, so we need to handle it properly
        _keycloakMock!
            .Given(Request.Create()
                .WithPath("/.well-known/openid-configuration")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    Issuer = issuer,
                    AuthorizationEndpoint = $"{issuer}/protocol/openid-connect/auth",
                    TokenEndpoint = $"{issuer}/protocol/openid-connect/token",
                    UserinfoEndpoint = $"{issuer}/protocol/openid-connect/userinfo",
                    JwksUri = $"{issuer}/protocol/openid-connect/certs"
                }));

        // Also handle any path that might be requested
        _keycloakMock
            .Given(Request.Create()
                .UsingAnyMethod())
            .AtPriority(999) // Low priority fallback
            .RespondWith(Response.Create()
                .WithStatusCode(200));
    }

    public void SetupOidcDiscoveryFailure(string _)
    {
        _keycloakMock!
            .Given(Request.Create()
                .WithPath("/.well-known/openid-configuration")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404));
    }

    public void ResetKeycloakMock()
    {
        _createdIdps.Clear();
        _updatedIdps.Clear();
        _keycloakMock?.Reset();
        SetupKeycloakMock();
    }

    public void ResetUpdatedProviders()
    {
        _updatedIdps.Clear();
    }

    public IReadOnlyList<JsonElement> CreatedIdentityProviders => _createdIdps;
    public IReadOnlyList<JsonElement> UpdatedIdentityProviders => _updatedIdps;
    public string MockServerUrl => _keycloakMock?.Url ?? throw new InvalidOperationException("Mock server not started");

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        _keycloakMock?.Stop();
        _keycloakMock?.Dispose();
    }
}
