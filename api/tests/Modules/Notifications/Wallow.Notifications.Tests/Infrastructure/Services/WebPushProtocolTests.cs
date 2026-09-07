using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Infrastructure.Services;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class WebPushProtocolTests
{
    [Fact]
    public async Task Delivery_EncryptsWithSubscriptionKeysAndSignsVapidForPushService()
    {
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] auth = RandomNumberGenerator.GetBytes(16);
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        PushDeliveryResult result = await SendAsync(http, browser, signer, auth, "{\"title\":\"Hello browser\"}");

        result.Success.Should().BeTrue();
        handler.Encoding.Should().Be("aes128gcm");
        handler.Ttl.Should().Be("86400");
        Decrypt(handler.Body!, browser, auth).Should().Be("{\"title\":\"Hello browser\"}");
        handler.Authorization!.Scheme.Should().Be("vapid");
        string[] parameters = handler.Authorization.Parameter!.Split(',');
        string jwt = parameters.Single(value => value.TrimStart().StartsWith("t=", StringComparison.Ordinal)).Trim()[2..];
        parameters.Single(value => value.TrimStart().StartsWith("k=", StringComparison.Ordinal)).Trim()[2..]
            .Should().Be(Encode(PublicKey(signer.ExportParameters(false))));
        string[] parts = jwt.Split('.');
        signer.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation).Should().BeTrue();
        using JsonDocument claims = JsonDocument.Parse(Decode(parts[1]));
        claims.RootElement.GetProperty("aud").GetString().Should().Be("https://push.example.com");
        claims.RootElement.GetProperty("sub").GetString().Should().Be("mailto:push@example.com");
        long expiration = claims.RootElement.GetProperty("exp").GetInt64();
        expiration.Should().BeInRange(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData(201, true, false, false)]
    [InlineData(404, false, true, false)]
    [InlineData(410, false, true, false)]
    [InlineData(429, false, false, true)]
    [InlineData(503, false, false, true)]
    [InlineData(401, false, false, false)]
    [InlineData(403, false, false, false)]
    [InlineData(307, false, false, false)]
    public async Task Delivery_ClassifiesResponseWithoutExposingServiceBody(int status, bool accepted, bool expired, bool retryable)
    {
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using CaptureHandler handler = new((HttpStatusCode)status);
        using HttpClient http = new(handler);
        PushDeliveryResult result = await SendAsync(http, browser, signer, RandomNumberGenerator.GetBytes(16), "hello");
        result.Success.Should().Be(accepted);
        result.SubscriptionExpired.Should().Be(expired);
        result.Retryable.Should().Be(retryable);
        result.ErrorMessage.Should().NotContain("secret-service-body");
        result.RetryAfter.Should().Be(retryable ? TimeSpan.FromDays(1) : null);
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Delivery_RejectsOversizeUtf8BeforeSending()
    {
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        PushDeliveryResult result = await SendAsync(http, browser, signer, RandomNumberGenerator.GetBytes(16), new string('é', 1997));
        result.Success.Should().BeFalse();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Delivery_AcceptsMaximumPayloadWithoutTruncation()
    {
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] auth = RandomNumberGenerator.GetBytes(16);
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        string payload = new('x', 3993);
        PushDeliveryResult result = await SendAsync(http, browser, signer, auth, payload);
        result.Success.Should().BeTrue();
        handler.Body.Should().HaveCount(4096);
        Decrypt(handler.Body!, browser, auth).Should().Be(payload);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    public void Transport_AllowsPublicAddresses(string address) => WebPushTransport.IsPublicAddress(IPAddress.Parse(address)).Should().BeTrue();

    [Fact]
    public async Task Delivery_PreservesCallerCancellation()
    {
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Func<Task> send = () => new WebPushProtocol(http).SendAsync(new("https://push.example.com/sub", new("", "")), new("test", "", "", false), "", "", cancellation.Token);
        await send.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("http://push.example.com/sub")]
    [InlineData("https://push.example.com:8443/sub")]
    [InlineData("https://user:password@push.example.com/sub")]
    [InlineData("https://push.example.com/sub#fragment")]
    [InlineData("https://127.0.0.1/sub")]
    [InlineData("https://[::1]/sub")]
    [InlineData("https://[::ffff:127.0.0.1]/sub")]
    public async Task Delivery_RejectsUnsafeEndpointsBeforeHttp(string endpoint)
    {
        using CaptureHandler handler = new();
        using HttpClient http = new(handler);
        PushDeliveryResult result = await new WebPushProtocol(http).SendAsync(new(endpoint, new("", "")), new("test", "", "", false), "", "", CancellationToken.None);
        result.Success.Should().BeFalse();
        handler.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::")]
    public void Transport_BlocksNonPublicAddresses(string address) => WebPushTransport.IsPublicAddress(IPAddress.Parse(address)).Should().BeFalse();

    [Fact]
    public async Task Transport_ResolvesAndBlocksLocalhostWithoutConnecting()
    {
        using SocketsHttpHandler handler = WebPushTransport.CreateHandler();
        handler.AllowAutoRedirect.Should().BeFalse();
        handler.UseProxy.Should().BeFalse();
        using HttpClient http = new(handler);
        Func<Task> send = () => http.GetAsync("https://localhost/secret");
        await send.Should().ThrowAsync<HttpRequestException>().WithMessage("*public addresses*");
    }

    [Fact]
    public async Task Transport_ReturnsRedirectWithoutFollowingPrivateDestination()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        using SocketsHttpHandler handler = WebPushTransport.CreateHandler();
        int connections = 0;
        handler.ConnectCallback = async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref connections);
            TcpClient client = new();
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
                return client.GetStream();
            }
            catch
            {
                client.Dispose();
                throw;
            }
        };
        using HttpClient http = new(handler);
        Task<HttpResponseMessage> send = http.GetAsync("http://push.example.com/subscription", timeout.Token);
        using TcpClient accepted = await listener.AcceptTcpClientAsync(timeout.Token);
        await using NetworkStream stream = accepted.GetStream();
        using StreamReader reader = new(stream, leaveOpen: true);
        string? line;
        do
        {
            line = await reader.ReadLineAsync(timeout.Token);
        }
        while (!string.IsNullOrEmpty(line));

        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 307 Temporary Redirect\r\nLocation: http://169.254.169.254/secret\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, timeout.Token);
        using HttpResponseMessage result = await send;
        result.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        connections.Should().Be(1);
        listener.Pending().Should().BeFalse();
    }

    private static Task<PushDeliveryResult> SendAsync(HttpClient http, ECDiffieHellman browser, ECDsa signer, byte[] auth, string payload) =>
        new WebPushProtocol(http).SendAsync(
            new WebPushSubscription("https://push.example.com/subscription", new(Encode(PublicKey(browser.ExportParameters(false))), Encode(auth))),
            new WebPushSigningKey("test", Encode(PublicKey(signer.ExportParameters(false))), Encode(signer.ExportParameters(true).D!), false),
            "mailto:push@example.com", payload, CancellationToken.None);

    private static byte[] PublicKey(ECParameters parameters) => [4, .. parameters.Q.X!, .. parameters.Q.Y!];

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5390:Do not hard-code encryption key", Justification = "RFC8291 HKDF info labels are protocol constants, not encryption keys; all keys and secrets are generated per test.")]
    private static string Decrypt(byte[] record, ECDiffieHellman browser, byte[] auth)
    {
        byte[] salt = record[..16];
        record[20].Should().Be(65);
        byte[] senderPublic = record[21..86];
        using ECDiffieHellman sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = senderPublic[1..33], Y = senderPublic[33..65] },
        });
        byte[] shared = browser.DeriveRawSecretAgreement(sender.PublicKey);
        byte[] info = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. PublicKey(browser.ExportParameters(false)), .. senderPublic];
        byte[] ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, auth, info);
        byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        byte[] nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        byte[] plaintext = new byte[record.Length - 86 - 16];
        using AesGcm aes = new(key, 16);
        aes.Decrypt(nonce, record.AsSpan(86, plaintext.Length), record.AsSpan(record.Length - 16), plaintext);
        int delimiter = plaintext.Length - 1;
        while (plaintext[delimiter] == 0)
        {
            delimiter--;
        }

        plaintext[delimiter].Should().Be(2);
        return Encoding.UTF8.GetString(plaintext, 0, delimiter);
    }

    private sealed class CaptureHandler(HttpStatusCode status = HttpStatusCode.Created) : HttpMessageHandler
    {
        public string? Encoding { get; private set; }
        public string? Ttl { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public byte[]? Body { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Encoding = request.Content?.Headers.ContentEncoding.SingleOrDefault();
            Ttl = request.Headers.GetValues("TTL").Single();
            Authorization = request.Headers.Authorization;
            Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            HttpResponseMessage response = new(status) { Content = new StringContent("secret-service-body") };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromDays(10));
            return response;
        }
    }
}
