using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Notifications.Domain.Preferences.Entities;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Factories;
using Wolverine.Tracking;

namespace Wallow.Identity.IntegrationTests.OAuth2;

public sealed class PushDeviceOwnershipTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "push-client-secret";
    private readonly RecordingPushProvider _push = new();
    private readonly PushDeliveryGate _deliveryGate = new();
    private WebApplicationFactory<Program> _apiFactory = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _apiFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
            services.AddSingleton<IPushProviderFactory>(_push);
            services.AddSingleton(_deliveryGate);
            services.AddScoped<IPushMessageRepository, GatedPushMessageRepository>();
        }));
    }

    public override async Task DisposeAsync()
    {
        await _apiFactory.DisposeAsync();
        await base.DisposeAsync();
    }

    [Fact]
    public async Task RemoveDevice_OnlyItsOwnerInTheSameOrganizationCanRemoveIt()
    {
        (Guid organization, string application) = await OrganizationAsync();
        (Guid otherOrganization, string otherApplication) = await OrganizationAsync();
        using HttpClient owner = await MemberAsync(organization, application);
        using HttpClient peer = await MemberAsync(organization, application);
        using HttpClient outsider = await MemberAsync(otherOrganization, otherApplication);
        await RegisterAsync(owner, "owned-token");
        Device[] devices = await DevicesAsync(owner);
        Guid deviceId = devices.Single().Id;

        using HttpResponseMessage crossOrganization = await outsider.DeleteAsync($"/v1/push/devices/{deviceId}");
        crossOrganization.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpResponseMessage crossUser = await peer.DeleteAsync($"/v1/push/devices/{deviceId}");
        crossUser.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DevicesAsync(owner)).Should().ContainSingle();
        (await DevicesAsync(peer)).Should().BeEmpty();
        (await DevicesAsync(outsider)).Should().BeEmpty();

        using HttpResponseMessage removed = await owner.DeleteAsync($"/v1/push/devices/{deviceId}");
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage repeated = await owner.DeleteAsync($"/v1/push/devices/{deviceId}");
        repeated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DevicesAsync(owner)).Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterDevice_RetriesConvergeAndOwnershipChangesRequireRemoval()
    {
        (Guid organization, string application) = await OrganizationAsync();
        (Guid otherOrganization, string otherApplication) = await OrganizationAsync();
        using HttpClient owner = await MemberAsync(organization, application);
        using HttpClient peer = await MemberAsync(organization, application);
        using HttpClient outsider = await MemberAsync(otherOrganization, otherApplication);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RegisterAsync(owner, "shared-token")));
        Device original = (await DevicesAsync(owner)).Single();
        await RegisterAsync(owner, "shared-token");
        (await DevicesAsync(owner)).Should().ContainSingle().Which.Id.Should().Be(original.Id);
        using HttpResponseMessage conflict = await peer.PostAsJsonAsync("/v1/push/devices", new { platform = 0, token = "shared-token" });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DevicesAsync(peer)).Should().BeEmpty();
        await RegisterAsync(outsider, "shared-token");
        (await DevicesAsync(outsider)).Should().ContainSingle();

        using HttpResponseMessage removed = await owner.DeleteAsync($"/v1/push/devices/{original.Id}");
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await RegisterAsync(owner, "shared-token");
        Device reactivated = (await DevicesAsync(owner)).Single();
        using HttpResponseMessage removedAgain = await owner.DeleteAsync($"/v1/push/devices/{reactivated.Id}");
        removedAgain.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await RegisterAsync(peer, "shared-token");
        Device transferred = (await DevicesAsync(peer)).Single();
        using HttpResponseMessage staleRemoval = await owner.DeleteAsync($"/v1/push/devices/{transferred.Id}");
        staleRemoval.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpResponseMessage reclaim = await owner.PostAsJsonAsync("/v1/push/devices", new { platform = 0, token = "shared-token" });
        reclaim.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DevicesAsync(peer)).Should().ContainSingle();
        (await DevicesAsync(owner)).Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterDevice_ConcurrentOwnersCannotBothAcquireTheSameToken()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient first = await MemberAsync(organization, application);
        using HttpClient second = await MemberAsync(organization, application);
        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsJsonAsync("/v1/push/devices", new { platform = 0, token = "contested-token" }),
            second.PostAsJsonAsync("/v1/push/devices", new { platform = 0, token = "contested-token" }));
        try
        {
            responses.Select(response => response.StatusCode).Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        Device[] firstDevices = await DevicesAsync(first);
        Device[] secondDevices = await DevicesAsync(second);
        firstDevices.Concat(secondDevices).Should().ContainSingle();
    }

    [Fact]
    public async Task SendPush_UsesTheAuthenticatedUserEvenIfAnotherRecipientIsSupplied()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient sender = await MemberAsync(organization, application);
        using HttpClient peer = await MemberAsync(organization, application);
        await RegisterAsync(sender, "sender-device");
        await RegisterAsync(peer, "peer-device");
        Device senderDevice = (await DevicesAsync(sender)).Single();
        Device peerDevice = (await DevicesAsync(peer)).Single();
        (Guid otherOrganization, string otherApplication) = await OrganizationAsync();
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(ScopedServices, otherOrganization, senderDevice.UserId, "user");
        string email = AuthorizationCodeFlowHarness.ReadClaimValues(sender.DefaultRequestHeaders.Authorization!.Parameter!, "email").Single();
        using HttpClient otherSession = await AuthenticateAsync(email, otherApplication);
        await RegisterAsync(otherSession, "other-organization-device");
        await _apiFactory.Services.ExecuteAndWaitAsync(async () =>
        {
            using HttpResponseMessage response = await sender.PostAsJsonAsync("/v1/push/send", new
            {
                recipientId = peerDevice.UserId,
                title = "Self-test",
                body = "Controlled provider",
                notificationType = "Alert",
            });
            response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        });
        _push.Deliveries.Reader.TryRead(out RecordedPush? delivered).Should().BeTrue();
        delivered.Should().NotBeNull();
        delivered!.Recipient.Should().Be(senderDevice.UserId);
        delivered.Organization.Should().Be(organization);
        delivered.Token.Should().Be("sender-device");
        _push.Deliveries.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendPush_DoesNotDeliverAfterDeviceOwnershipChangesWhileQueued()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient sender = await MemberAsync(organization, application);
        using HttpClient nextOwner = await MemberAsync(organization, application);
        await RegisterAsync(sender, "handover-token");
        Device device = (await DevicesAsync(sender)).Single();
        _deliveryGate.Paused = true;
        await _apiFactory.Services.ExecuteAndWaitAsync(async () =>
        {
            try
            {
                using HttpResponseMessage sent = await sender.PostAsJsonAsync("/v1/push/send", new
                {
                    title = "Queued self-test",
                    body = "Must not reach the next owner",
                    notificationType = "Alert",
                });
                sent.StatusCode.Should().Be(HttpStatusCode.NoContent);
                await _deliveryGate.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                using HttpResponseMessage removed = await sender.DeleteAsync($"/v1/push/devices/{device.Id}");
                removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
                await RegisterAsync(nextOwner, "handover-token");
            }
            finally
            {
                _deliveryGate.Released.TrySetResult();
            }
        });
        _push.Deliveries.Reader.TryRead(out _).Should().BeFalse();
        (await DevicesAsync(nextOwner)).Should().ContainSingle();
    }

    [Fact]
    public async Task SendPush_RespectsTheSendersDisabledPreference()
    {
        (Guid organization, string application) = await OrganizationAsync();
        using HttpClient sender = await MemberAsync(organization, application);
        await RegisterAsync(sender, "disabled-device");
        Device device = (await DevicesAsync(sender)).Single();
        using (IServiceScope scope = _apiFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(TenantId.Create(organization));
            NotificationsDbContext context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            context.ChannelPreferences.Add(ChannelPreference.Create(device.UserId, ChannelType.Push, "*", TimeProvider.System, false));
            await context.SaveChangesAsync();
        }

        await _apiFactory.Services.ExecuteAndWaitAsync(async () =>
        {
            using HttpResponseMessage response = await sender.PostAsJsonAsync("/v1/push/send", new
            {
                title = "Self-test",
                body = "Disabled",
                notificationType = "Alert",
            });
            response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        });
        _push.Deliveries.Reader.TryRead(out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task PushOperations_RequireAuthentication(string? token)
    {
        using HttpClient client = ApiClient(token);
        await AssertOperationsDeniedAsync(client, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PushOperations_RequireAnOrganization()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string application = $"push-first-party-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(ScopedServices, application, ClientSecret, null, ["openid", "profile", "email"], firstParty: true);
        using HttpClient client = await MemberAsync(null, application);
        await AssertOperationsDeniedAsync(client, HttpStatusCode.Forbidden);
    }

    private static async Task AssertOperationsDeniedAsync(HttpClient client, HttpStatusCode expected)
    {
        using HttpResponseMessage register = await client.PostAsJsonAsync("/v1/push/devices", new { platform = 0, token = "denied" });
        register.StatusCode.Should().Be(expected);
        using HttpResponseMessage list = await client.GetAsync("/v1/push/devices");
        list.StatusCode.Should().Be(expected);
        using HttpResponseMessage remove = await client.DeleteAsync($"/v1/push/devices/{Guid.NewGuid()}");
        remove.StatusCode.Should().Be(expected);
        using HttpResponseMessage send = await client.PostAsJsonAsync("/v1/push/send", new { title = "Denied", body = "Denied", notificationType = "Alert" });
        send.StatusCode.Should().Be(expected);
    }

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

    private static async Task RegisterAsync(HttpClient client, string token)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/v1/push/devices", new { platform = 0, token });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Device[]> DevicesAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/v1/push/devices");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Device[]>())!;
    }

    private sealed record Device(Guid Id, Guid UserId, string Token);
}

public sealed record RecordedPush(Guid Recipient, Guid Organization, string Token);

public sealed class RecordingPushProvider : IPushProviderFactory, IPushProvider
{
    public Channel<RecordedPush> Deliveries { get; } = Channel.CreateUnbounded<RecordedPush>();

    public Task<IPushProvider> GetProviderAsync(DeviceRegistration device) => Task.FromResult<IPushProvider>(this);

    public Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default)
    {
        Deliveries.Writer.TryWrite(new RecordedPush(message.RecipientId.Value, message.TenantId.Value, deviceToken));
        return Task.FromResult(new PushDeliveryResult(true, null));
    }
}

public sealed class PushDeliveryGate
{
    public bool Paused { get; set; }
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        if (Paused)
        {
            Started.TrySetResult();
            await Released.Task.WaitAsync(cancellationToken);
        }
    }
}

public sealed class GatedPushMessageRepository(NotificationsDbContext context, ITenantContext tenantContext, PushDeliveryGate gate) : IPushMessageRepository
{
    private readonly PushMessageRepository _repository = new(context, tenantContext);

    public async Task<PushMessage?> GetByIdAsync(PushMessageId id, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public void Add(PushMessage message) => _repository.Add(message);
    public void Update(PushMessage message) => _repository.Update(message);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _repository.SaveChangesAsync(cancellationToken);
}
