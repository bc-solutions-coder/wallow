using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class WebPushPushProviderTests
{
    private const string Endpoint = "https://push.example.com/subscription";

    [Theory]
    [InlineData("old")]
    [InlineData("current")]
    public async Task SendAsync_UsesRegistrationSigningVersionAndEncryptsPayload(string registeredKey)
    {
        WebPushSigningKey old = SigningKey("old");
        WebPushSigningKey current = SigningKey("current");
        WebPushCredentials credentials = new("mailto:push@example.com", "current", [old, current]);
        DeviceRegistration device = Device(registeredKey);
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        WebPushPushProvider provider = new(http, JsonSerializer.Serialize(credentials), device);

        PushDeliveryResult result = await provider.SendAsync(Message(), Endpoint);

        result.Success.Should().BeTrue();
        handler.Authorization.Should().Contain("k=" + (registeredKey == "old" ? old.PublicKey : current.PublicKey));
        handler.ContentEncoding.Should().Be("aes128gcm");
        Encoding.UTF8.GetString(handler.Body!).Should().NotContain("Test Title").And.NotContain("Test Body");
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_RejectsRetiredOrMissingRegistrationSigningKey(bool retired)
    {
        WebPushSigningKey old = SigningKey("old") with { Retired = true, PrivateKey = null };
        WebPushSigningKey current = SigningKey("current");
        WebPushCredentials credentials = new("mailto:push@example.com", "current", retired ? [old, current] : [current]);
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        WebPushPushProvider provider = new(http, JsonSerializer.Serialize(credentials), Device("old"));
        PushDeliveryResult result = await provider.SendAsync(Message(), Endpoint);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        handler.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{malformed")]
    [InlineData("{\"subject\":\"mailto:push@example.com\",\"currentKeyId\":\"current\",\"keys\":[]}")]
    public async Task SendAsync_ReportsMissingOrMalformedCredentials(string credentials)
    {
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        WebPushPushProvider provider = new(http, credentials, Device("current"));
        PushDeliveryResult result = await provider.SendAsync(Message(), Endpoint);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_ReportsDuplicateSigningIdentifiersAsInvalidConfiguration()
    {
        WebPushSigningKey key = SigningKey("current");
        WebPushCredentials credentials = new("mailto:push@example.com", "current", [key, key]);
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        WebPushPushProvider provider = new(http, JsonSerializer.Serialize(credentials), Device("current"));
        PushDeliveryResult result = await provider.SendAsync(Message(), Endpoint);
        result.Success.Should().BeFalse();
        handler.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_ReportsMissingSubscriptionOrMismatchedEndpoint(bool missing)
    {
        WebPushSigningKey key = SigningKey("current");
        WebPushCredentials credentials = new("mailto:push@example.com", "current", [key]);
        DeviceRegistration device = missing
            ? DeviceRegistration.Register(UserId.New(), TenantId.New(), PushPlatform.WebPush, Endpoint, DateTimeOffset.UtcNow)
            : Device("current");
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        WebPushPushProvider provider = new(http, JsonSerializer.Serialize(credentials), device);
        PushDeliveryResult result = await provider.SendAsync(Message(), missing ? Endpoint : "https://push.example.com/other-subscription");
        result.Success.Should().BeFalse();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_UnconfiguredProviderReportsFailure()
    {
        PushDeliveryResult result = await new UnavailableWebPushProvider().SendAsync(Message(), Endpoint);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not configured");
    }

    private static PushMessage Message() => PushMessage.Create(TenantId.New(), UserId.New(), "Test Title", "Test Body", TimeProvider.System);

    private static DeviceRegistration Device(string keyId)
    {
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = browser.ExportParameters(false);
        WebPushSubscription subscription = new(Endpoint, new(Base64Url.EncodeToString([4, .. parameters.Q.X!, .. parameters.Q.Y!]),
            Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16))));
        return DeviceRegistration.RegisterWebPush(UserId.New(), TenantId.New(), subscription, keyId, DateTimeOffset.UtcNow);
    }

    private static WebPushSigningKey SigningKey(string id)
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = signer.ExportParameters(true);
        return new(id, Base64Url.EncodeToString([4, .. parameters.Q.X!, .. parameters.Q.Y!]), Base64Url.EncodeToString(parameters.D!), false);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? ContentEncoding { get; private set; }
        public byte[]? Body { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorization = request.Headers.Authorization?.Parameter;
            ContentEncoding = request.Content?.Headers.ContentEncoding.SingleOrDefault();
            Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return new(HttpStatusCode.Created);
        }
    }
}
