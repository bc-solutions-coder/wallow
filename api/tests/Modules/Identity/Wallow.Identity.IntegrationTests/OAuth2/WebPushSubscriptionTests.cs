using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

public sealed class WebPushSubscriptionTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "push-client-secret";
    private WebApplicationFactory<Program> _apiFactory = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _apiFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>())));
    }

    public override async Task DisposeAsync()
    {
        await _apiFactory.DisposeAsync();
        await base.DisposeAsync();
    }

    [Fact]
    public async Task PublicKey_OrdinaryMemberReceivesOnlyCurrentPublicMaterial()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient member = await MemberAsync(organization, application);
        using HttpResponseMessage unavailable = await member.GetAsync("/v1/push/web-push/public-key");
        unavailable.StatusCode.Should().Be(HttpStatusCode.Conflict);
        WebPushSigningKey key = SigningKey("v1");
        await ConfigureAsync(organization, "v1", key);
        using HttpResponseMessage response = await member.GetAsync("/v1/push/web-push/public-key");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("keyId").GetString().Should().Be("v1");
        body.GetProperty("publicKey").GetString().Should().Be(key.PublicKey);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(["keyId", "publicKey"]);
    }

    [Fact]
    public async Task Register_RetriesConvergeAndEndpointOwnershipIsIsolatedByOrganization()
    {
        (Guid organization, string application) = await OrganizationAsync();
        (Guid otherOrganization, string otherApplication) = await OrganizationAsync();
        await ConfigureAsync(organization, "v1", SigningKey("v1"));
        await ConfigureAsync(otherOrganization, "v1", SigningKey("v1"));
        using HttpClient owner = await MemberAsync(organization, application);
        using HttpClient peer = await MemberAsync(organization, application);
        using HttpClient outsider = await MemberAsync(otherOrganization, otherApplication);
        Registration request = Subscription();
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => RegisterAsync(owner, request)));
        Device original = (await DevicesAsync(owner)).Single();
        await RegisterAsync(owner, request);
        (await DevicesAsync(owner)).Should().ContainSingle().Which.Id.Should().Be(original.Id);
        using HttpResponseMessage conflict = await peer.PostAsJsonAsync("/v1/push/devices", request);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await RegisterAsync(outsider, request);
        (await DevicesAsync(outsider)).Should().ContainSingle();
        (await DevicesAsync(peer)).Should().BeEmpty();

        Registration replacement = Subscription(request.Subscription.Endpoint);
        await RegisterAsync(owner, replacement);
        Device replaced = (await DevicesAsync(owner)).Single();
        replaced.Id.Should().NotBe(original.Id);
        using HttpResponseMessage staleRemoval = await owner.DeleteAsync($"/v1/push/devices/{original.Id}");
        staleRemoval.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DevicesAsync(owner)).Should().ContainSingle().Which.Id.Should().Be(replaced.Id);
    }

    [Fact]
    public async Task Rotation_ExistingSubscriptionCanRetryRetainedKeyButCannotUseRetiredKey()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient owner = await MemberAsync(organization, application);
        WebPushSigningKey original = SigningKey("v1");
        await ConfigureAsync(organization, "v1", original);
        Registration existing = Subscription();
        await RegisterAsync(owner, existing);
        Guid originalId = (await DevicesAsync(owner)).Single().Id;
        WebPushSigningKey next = SigningKey("v2");
        await ConfigureAsync(organization, "v2", original, next);
        await RegisterAsync(owner, existing);
        (await DevicesAsync(owner)).Should().ContainSingle().Which.Id.Should().Be(originalId);
        using HttpResponseMessage obsolete = await owner.PostAsJsonAsync("/v1/push/devices", Subscription("https://push.example.com/new"));
        obsolete.IsSuccessStatusCode.Should().BeFalse();
        await RegisterAsync(owner, Subscription("https://push.example.com/current") with { SigningKeyId = "v2" });
        await ConfigureAsync(organization, "v2", original with { Retired = true, PrivateKey = null }, next);
        using HttpResponseMessage retired = await owner.PostAsJsonAsync("/v1/push/devices", existing);
        retired.IsSuccessStatusCode.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://push.example.com/sub")]
    [InlineData("https://127.0.0.1/sub")]
    [InlineData("https://localhost/sub")]
    [InlineData("https://push.example.com:444/sub")]
    public async Task Register_RejectsUnsafeEndpoint(string endpoint)
    {
        (Guid organization, string application) = await OrganizationAsync();
        await ConfigureAsync(organization, "v1", SigningKey("v1"));
        using HttpClient member = await MemberAsync(organization, application);
        using HttpResponseMessage response = await member.PostAsJsonAsync("/v1/push/devices", Subscription(endpoint));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_RejectsMalformedEncryptionMaterial()
    {
        (Guid organization, string application) = await OrganizationAsync();
        await ConfigureAsync(organization, "v1", SigningKey("v1"));
        using HttpClient member = await MemberAsync(organization, application);
        Registration request = Subscription();
        request = request with { Subscription = request.Subscription with { Keys = new SubscriptionKeys("invalid", "invalid") } };
        using HttpResponseMessage response = await member.PostAsJsonAsync("/v1/push/devices", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DevicesAsync(member)).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicKey_RequiresAuthenticatedOrganization()
    {
        using HttpClient anonymous = ApiClient(null);
        using HttpResponseMessage denied = await anonymous.GetAsync("/v1/push/web-push/public-key");
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        string application = $"push-first-party-{Guid.NewGuid():N}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(ScopedServices, application, ClientSecret, null, ["openid", "profile", "email"], firstParty: true);
        using HttpClient organizationless = await MemberAsync(null, application);
        using HttpResponseMessage forbidden = await organizationless.GetAsync("/v1/push/web-push/public-key");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task KeyManagement_OrdinaryBearerMemberCannotReadGenerateOrRetireKeys()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient member = await MemberAsync(organization, application);
        const string path = "/v1/admin/push/config/web-push/keys";
        using HttpResponseMessage list = await member.GetAsync(path);
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpResponseMessage generate = await member.PostAsJsonAsync(path, new { subject = "mailto:push@example.com" });
        generate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpResponseMessage retire = await member.DeleteAsync($"{path}/v1");
        retire.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task KeyManagement_TestAdministratorCanRotateAndIdempotentlyRetireKeys()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient member = await MemberAsync(organization, application);
        using HttpClient administrator = ApiClient("test-admin");
        administrator.DefaultRequestHeaders.Remove("X-Test-Auth-Skip");
        administrator.DefaultRequestHeaders.Add("X-Test-Tenant-Id", organization.ToString());
        const string path = "/v1/admin/push/config/web-push/keys";
        PublicSigningKey first = await GenerateKeyAsync(administrator, path);
        Registration existing = Subscription() with { SigningKeyId = first.KeyId };
        await RegisterAsync(member, existing);
        PublicSigningKey second = await GenerateKeyAsync(administrator, path);
        second.KeyId.Should().NotBe(first.KeyId);
        second.PublicKey.Should().NotBe(first.PublicKey);
        await RegisterAsync(member, existing);
        using HttpResponseMessage metadata = await administrator.GetAsync(path);
        metadata.StatusCode.Should().Be(HttpStatusCode.OK);
        string metadataJson = await metadata.Content.ReadAsStringAsync();
        metadataJson.Should().Contain(first.KeyId).And.Contain(second.KeyId);
        metadataJson.Should().NotContain("privateKey");
        for (int retry = 0; retry < 2; retry++)
        {
            using HttpResponseMessage retired = await administrator.DeleteAsync($"{path}/{first.KeyId}");
            retired.StatusCode.Should().Be(HttpStatusCode.NoContent, await retired.Content.ReadAsStringAsync());
        }
        using HttpResponseMessage discovery = await member.GetAsync("/v1/push/web-push/public-key");
        discovery.StatusCode.Should().Be(HttpStatusCode.OK);
        (await discovery.Content.ReadFromJsonAsync<PublicSigningKey>()).Should().Be(second);
        using HttpResponseMessage retiredRetry = await member.PostAsJsonAsync("/v1/push/devices", existing);
        retiredRetry.IsSuccessStatusCode.Should().BeFalse();
        using HttpResponseMessage retiredCurrent = await administrator.DeleteAsync($"{path}/{second.KeyId}");
        retiredCurrent.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage unavailable = await member.GetAsync("/v1/push/web-push/public-key");
        unavailable.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task KeyManagement_StaleRotationCannotResurrectRetiredPrivateKey()
    {
        (Guid organization, _) = await OrganizationAsync();
        await ConfigureAsync(organization, "v1", SigningKey("v1"));
        using IServiceScope stale = ConfigurationScope(organization);
        await stale.ServiceProvider.GetRequiredService<ITenantPushConfigurationRepository>().GetByPlatformAsync(PushPlatform.WebPush);
        using (IServiceScope retiring = ConfigurationScope(organization))
        {
            (await retiring.ServiceProvider.GetRequiredService<IWebPushConfiguration>().RetireAsync("v1", CancellationToken.None)).Should().BeTrue();
        }
        Func<Task> rotate = async () => await stale.ServiceProvider.GetRequiredService<IWebPushConfiguration>()
            .RotateAsync("mailto:push@example.com", CancellationToken.None);
        await rotate.Should().ThrowAsync<DbUpdateConcurrencyException>();
        using IServiceScope verification = ConfigurationScope(organization);
        WebPushCredentials credentials = await ReadCredentialsAsync(verification);
        credentials.CurrentKeyId.Should().BeNull();
        credentials.Keys.Should().ContainSingle().Which.Should().Match<WebPushSigningKey>(key => key.Id == "v1" && key.Retired && key.PrivateKey == null);
    }

    [Fact]
    public async Task KeyManagement_ConcurrentRotationCannotOverwriteWinningKey()
    {
        (Guid organization, _) = await OrganizationAsync();
        await ConfigureAsync(organization, "v1", SigningKey("v1"));
        using IServiceScope first = ConfigurationScope(organization);
        using IServiceScope second = ConfigurationScope(organization);
        await first.ServiceProvider.GetRequiredService<ITenantPushConfigurationRepository>().GetByPlatformAsync(PushPlatform.WebPush);
        await second.ServiceProvider.GetRequiredService<ITenantPushConfigurationRepository>().GetByPlatformAsync(PushPlatform.WebPush);
        WebPushPublicKey? winner = await first.ServiceProvider.GetRequiredService<IWebPushConfiguration>()
            .RotateAsync("mailto:push@example.com", CancellationToken.None);
        winner.Should().NotBeNull();
        Func<Task> rotate = async () => await second.ServiceProvider.GetRequiredService<IWebPushConfiguration>()
            .RotateAsync("mailto:push@example.com", CancellationToken.None);
        await rotate.Should().ThrowAsync<DbUpdateConcurrencyException>();
        using IServiceScope verification = ConfigurationScope(organization);
        WebPushCredentials credentials = await ReadCredentialsAsync(verification);
        credentials.CurrentKeyId.Should().Be(winner!.KeyId);
        credentials.Keys.Select(key => key.Id).Should().BeEquivalentTo(["v1", winner.KeyId]);
    }

    private IServiceScope ConfigurationScope(Guid organization)
    {
        IServiceScope scope = _apiFactory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(TenantId.Create(organization));
        scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().SetTenant(TenantId.Create(organization));
        return scope;
    }

    private static async Task<WebPushCredentials> ReadCredentialsAsync(IServiceScope scope)
    {
        TenantPushConfiguration? configuration = await scope.ServiceProvider.GetRequiredService<ITenantPushConfigurationRepository>()
            .GetByPlatformAsync(PushPlatform.WebPush);
        configuration.Should().NotBeNull();
        string decrypted = scope.ServiceProvider.GetRequiredService<IPushCredentialEncryptor>().Decrypt(configuration!.EncryptedCredentials);
        return WebPushCredentials.Parse(decrypted)!;
    }

    private static async Task<PublicSigningKey> GenerateKeyAsync(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(path, new { subject = "mailto:push@example.com" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(["keyId", "publicKey"]);
        return body.Deserialize<PublicSigningKey>(JsonSerializerOptions.Web)!;
    }

    private sealed record PublicSigningKey(string KeyId, string PublicKey);

    private async Task ConfigureAsync(Guid organization, string currentKey, params WebPushSigningKey[] keys)
    {
        using IServiceScope scope = _apiFactory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(TenantId.Create(organization));
        NotificationsDbContext context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        context.SetTenant(TenantId.Create(organization));
        IPushCredentialEncryptor encryptor = scope.ServiceProvider.GetRequiredService<IPushCredentialEncryptor>();
        string encrypted = encryptor.Encrypt(JsonSerializer.Serialize(new WebPushCredentials("mailto:push@example.test", currentKey, keys), JsonSerializerOptions.Web));
        TenantPushConfiguration? configuration = await context.TenantPushConfigurations.AsTracking().SingleOrDefaultAsync(item => item.Platform == PushPlatform.WebPush);
        if (configuration is null)
        {
            context.TenantPushConfigurations.Add(TenantPushConfiguration.Create(TenantId.Create(organization), PushPlatform.WebPush, encrypted, TimeProvider.System));
        }
        else
        {
            configuration.UpdateCredentials(encrypted, TimeProvider.System);
        }
        await context.SaveChangesAsync();
    }

    private static WebPushSigningKey SigningKey(string id)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = key.ExportParameters(true);
        return new WebPushSigningKey(id, Base64Url.EncodeToString([4, .. parameters.Q.X!, .. parameters.Q.Y!]), Base64Url.EncodeToString(parameters.D!), false);
    }

    private static Registration Subscription(string endpoint = "https://push.example.com/subscription")
    {
        using ECDiffieHellman key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = key.ExportParameters(false);
        return new Registration(2, null, new BrowserSubscription(endpoint, new SubscriptionKeys(
            Base64Url.EncodeToString([4, .. parameters.Q.X!, .. parameters.Q.Y!]),
            Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16))), null), "v1");
    }

    private static async Task RegisterAsync(HttpClient client, Registration request)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/v1/push/devices", request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Device[]> DevicesAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/v1/push/devices");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Device[]>())!;
    }

    private sealed record Registration(int Platform, string? Token, BrowserSubscription Subscription, string SigningKeyId);
    private sealed record BrowserSubscription(string Endpoint, SubscriptionKeys Keys, long? ExpirationTime);
    private sealed record SubscriptionKeys(string P256dh, string Auth);
    private sealed record Device(Guid Id);

    private async Task<(Guid Organization, string Application)> OrganizationAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        Guid owner = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, $"push-owner-{suffix}@example.test", Password);
        Guid organization = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(ScopedServices, $"Push {suffix}", owner);
        string application = $"app-push-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(ScopedServices, application, ClientSecret, organization, ["openid", "profile", "email"]);
        return (organization, application);
    }

    private async Task<HttpClient> MemberAsync(Guid? organization, string application)
    {
        string email = $"push-member-{Guid.NewGuid():N}@example.test";
        Guid user = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        if (organization is { } organizationId)
        {
            await AuthorizationCodeFlowHarness.EnrollMemberAsync(ScopedServices, organizationId, user, "user");
        }
        return await AuthenticateAsync(email, application);
    }

    private async Task<HttpClient> AuthenticateAsync(string email, string application)
    {
        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);
        AuthorizeOutcome authorize = await harness.AuthorizeAsync(application, "openid profile email");
        if (authorize.Code is null)
        {
            authorize = await harness.ConsentAsync(authorize, true);
        }

        authorize.Code.Should().NotBeNull(authorize.Body);
        TokenOutcome token = await harness.ExchangeCodeAsync(application, ClientSecret, authorize.Code!, authorize.CodeVerifier);
        token.StatusCode.Should().Be(HttpStatusCode.OK, token.Body);
        return ApiClient(token.RequireAccessToken());
    }

    private HttpClient ApiClient(string? accessToken)
    {
        HttpClient client = _apiFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Test-Auth-Skip", "true");
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return client;
    }

}
