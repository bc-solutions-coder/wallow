using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Sso;

[CollectionDefinition("SsoConfiguration")]
public class SsoTestCollection : ICollectionFixture<SsoConfigurationTestFactory>;

[Collection("SsoConfiguration")]
[Trait("Category", "Integration")]
public class SsoConfigurationTests : IAsyncLifetime
{
    private readonly SsoConfigurationTestFactory _factory;
    private IServiceScope? _scope;
    private ISsoService _ssoService = null!;
    private IdentityDbContext _dbContext = null!;

    public SsoConfigurationTests(SsoConfigurationTestFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _scope = _factory.Services.CreateScope();
        IServiceProvider scopedServices = _scope.ServiceProvider;

        _ssoService = scopedServices.GetRequiredService<ISsoService>();
        _dbContext = scopedServices.GetRequiredService<IdentityDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        await _dbContext.SsoConfigurations.ExecuteDeleteAsync();

        _factory.ResetIdpMock();
    }

    public Task DisposeAsync()
    {
        _scope?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ConfigureOidc_CreatesIdpBroker()
    {
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

        SsoConfigurationDto result = await _ssoService.SaveOidcConfigurationAsync(request);

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Test OIDC Provider");
        result.Protocol.Should().Be(SsoProtocol.Oidc);
        result.OidcIssuer.Should().Be(mockIssuer);
        result.Status.Should().Be(SsoStatus.Draft);
    }

    [Fact]
    public async Task Activate_EnablesIdp()
    {
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
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        _ = await _ssoService.SaveOidcConfigurationAsync(request);
        await _ssoService.ActivateAsync();

        SsoConfigurationDto? config = await _ssoService.GetConfigurationAsync();
        config.Should().NotBeNull();
        config!.Status.Should().Be(SsoStatus.Active);
    }

    [Fact]
    public async Task Disable_DisablesIdp()
    {
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
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        _ = await _ssoService.SaveOidcConfigurationAsync(request);
        await _ssoService.ActivateAsync();

        await _ssoService.DisableAsync();

        SsoConfigurationDto? config = await _ssoService.GetConfigurationAsync();
        config.Should().NotBeNull();
        config!.Status.Should().Be(SsoStatus.Disabled);
    }

    [Fact]
    public async Task TestConnection_WithValidConfig_ReturnsSuccess()
    {
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

        _ = await _ssoService.SaveOidcConfigurationAsync(request);

        SsoTestResult result = await _ssoService.TestConnectionAsync();

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnection_WithInvalidConfig_ReturnsFailure()
    {
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

        _ = await _ssoService.SaveOidcConfigurationAsync(request);

        SsoTestResult result = await _ssoService.TestConnectionAsync();

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_ReturnsResult()
    {
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
            DefaultRole: null,
            SyncGroupsAsRoles: false);

        _ = await _ssoService.SaveOidcConfigurationAsync(request);

        SsoValidationResult result = await _ssoService.ValidateIdpConfigurationAsync();

        result.Should().NotBeNull();
    }
}

public class SsoConfigurationTestFactory : WallowApiFactory
{
    private WireMockServer? _idpMock;
    private readonly List<JsonElement> _createdIdps = [];
    private readonly List<JsonElement> _updatedIdps = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        _idpMock = WireMockServer.Start();
        SetupIdpMock();

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpContextAccessor();

            services.AddHttpClient(string.Empty, client =>
            {
                client.BaseAddress = new Uri(_idpMock.Url!);
            });

            services.AddScoped<ITenantContext>(_ =>
            {
                TenantContext tc = new();
                tc.SetTenant(TenantId.Create(TestConstants.TestTenantId), "Test Tenant");
                return tc;
            });
        });
    }

    private void SetupIdpMock()
    {
        _idpMock!
            .Given(Request.Create()
                .WithPath("/admin/realms/wallow/identity-provider/instances/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    string alias = request.Path.Split('/').Last();

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

        _idpMock
            .Given(Request.Create()
                .WithPath("/admin/realms/wallow/identity-provider/instances")
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

        _idpMock
            .Given(Request.Create()
                .WithPath("/admin/realms/wallow/identity-provider/instances/*")
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

        _idpMock
            .Given(Request.Create()
                .WithPath("/admin/realms/wallow/identity-provider/instances/*/mappers")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201));

        _idpMock
            .Given(Request.Create()
                .WithPath("/admin/realms/wallow/identity-provider/instances/*/mappers")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(Array.Empty<object>()));
    }

    public void SetupOidcDiscovery(string issuer)
    {
        _idpMock!
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

        _idpMock
            .Given(Request.Create()
                .UsingAnyMethod())
            .AtPriority(999)
            .RespondWith(Response.Create()
                .WithStatusCode(200));
    }

    public void SetupOidcDiscoveryFailure(string _)
    {
        _idpMock!
            .Given(Request.Create()
                .WithPath("/.well-known/openid-configuration")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404));
    }

    public void ResetIdpMock()
    {
        _createdIdps.Clear();
        _updatedIdps.Clear();
        _idpMock?.Reset();
        SetupIdpMock();
    }

    public void ResetUpdatedProviders()
    {
        _updatedIdps.Clear();
    }

    public IReadOnlyList<JsonElement> CreatedIdentityProviders => _createdIdps;
    public IReadOnlyList<JsonElement> UpdatedIdentityProviders => _updatedIdps;
    public string MockServerUrl => _idpMock?.Url ?? throw new InvalidOperationException("Mock server not started");

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        _idpMock?.Stop();
        _idpMock?.Dispose();
    }
}
