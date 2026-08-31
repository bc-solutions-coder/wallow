using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class BackchannelLogoutNotifierTests
{
    private const string TestSid = "sid-under-test";
    private const string TestClientId = "rp-client";
    private static readonly Uri _issuer = new("https://id.example.com");
    private static readonly Uri _logoutUri = new("https://rp.example.com/bff/backchannel-logout");

    private readonly Guid _userId = Guid.NewGuid();
    private readonly ISsoClientSessionService _sessions = Substitute.For<ISsoClientSessionService>();
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048)) { KeyId = "test-signing-key" };

    private void SetRecipients(params BackchannelLogoutRecipient[] recipients) =>
        _sessions.ListBackchannelRecipientsAsync(TestSid, Arg.Any<CancellationToken>())
            .Returns(recipients);

#pragma warning disable CA2000 // The notifier takes ownership of the HttpClient
    private BackchannelLogoutNotifier CreateSut(
        HttpMessageHandler handler,
        BackchannelLogoutOptions? options = null,
        bool withSigningCredentials = true)
    {
        OpenIddictServerOptions serverOptions = new();
        if (withSigningCredentials)
        {
            serverOptions.SigningCredentials.Add(
                new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));
        }

        IOptionsMonitor<OpenIddictServerOptions> serverOptionsMonitor =
            Substitute.For<IOptionsMonitor<OpenIddictServerOptions>>();
        serverOptionsMonitor.CurrentValue.Returns(serverOptions);

        // AllowPrivateNetworkHosts skips DNS so unit tests never resolve real hosts, and a zero
        // retry delay keeps the single-retry path synchronous under the fake time provider.
        options ??= new BackchannelLogoutOptions { AllowPrivateNetworkHosts = true };
        options.RetryDelay = TimeSpan.Zero;

        return new BackchannelLogoutNotifier(
            new HttpClient(handler),
            _sessions,
            serverOptionsMonitor,
            Microsoft.Extensions.Options.Options.Create(options),
            _timeProvider,
            NullLogger<BackchannelLogoutNotifier>.Instance);
    }
#pragma warning restore CA2000

    private static string ExtractToken(string requestBody)
    {
        requestBody.Should().StartWith("logout_token=");
        return Uri.UnescapeDataString(requestBody["logout_token=".Length..]);
    }

    [Fact]
    public async Task NotifyAsync_PostsALogoutTokenCarryingTheSpecClaims()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, _logoutUri));
        using ScriptedHandler handler = new();
        BackchannelLogoutNotifier sut = CreateSut(handler);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        (Uri uri, string body) = handler.Requests.Should().ContainSingle().Subject;
        uri.Should().Be(_logoutUri);

        JsonWebToken token = new(ExtractToken(body));
        token.Typ.Should().Be("logout+jwt");
        token.Issuer.Should().Be(_issuer.AbsoluteUri);
        token.Audiences.Should().ContainSingle().Which.Should().Be(TestClientId);
        token.Subject.Should().Be(_userId.ToString());
        (token.ValidTo - token.IssuedAt).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(2));

        using JsonDocument payload = JsonDocument.Parse(Base64UrlEncoder.Decode(token.EncodedPayload));
        payload.RootElement.GetProperty("sid").GetString().Should().Be(TestSid);
        payload.RootElement.GetProperty("jti").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("events")
            .TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out JsonElement _)
            .Should().BeTrue();

        // A nonce is what lets relying parties tell a replayed id token from a logout token, so
        // the spec forbids it here.
        payload.RootElement.TryGetProperty("nonce", out JsonElement _).Should().BeFalse();
    }

    [Fact]
    public async Task NotifyAsync_SignsTheTokenWithTheServersKey()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, _logoutUri));
        using ScriptedHandler handler = new();
        BackchannelLogoutNotifier sut = CreateSut(handler);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        string token = ExtractToken(handler.Requests.Should().ContainSingle().Subject.Body);
        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = _issuer.AbsoluteUri,
                ValidAudience = TestClientId,
                IssuerSigningKey = _signingKey,
                ValidTypes = ["logout+jwt"],
            });

        result.IsValid.Should().BeTrue(because: result.Exception?.Message ?? "the token should validate");
    }

    [Fact]
    public async Task NotifyAsync_RetriesExactlyOnceAfterAFailedDelivery()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, _logoutUri));
        using ScriptedHandler handler = new(HttpStatusCode.InternalServerError);
        BackchannelLogoutNotifier sut = CreateSut(handler);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        handler.Requests.Should().HaveCount(2);

        // The token is minted once per recipient: the retry re-sends the same instruction, it
        // does not issue a new one.
        ExtractToken(handler.Requests[1].Body).Should().Be(ExtractToken(handler.Requests[0].Body));
    }

    [Fact]
    public async Task NotifyAsync_DoesNotRetryARejectedDelivery()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, _logoutUri));
        using ScriptedHandler handler = new(HttpStatusCode.BadRequest);
        BackchannelLogoutNotifier sut = CreateSut(handler);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        // A 4xx is the relying party rejecting this token — re-sending it cannot succeed.
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task NotifyAsync_GivesUpAfterTheSingleRetry()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, _logoutUri));
        using ScriptedHandler handler = new(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError);
        BackchannelLogoutNotifier sut = CreateSut(handler);

        Func<Task> act = () => sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        await act.Should().NotThrowAsync();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task NotifyAsync_SwallowsTransportFailuresAndStillRetries()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, _logoutUri));
        using CountingThrowingHandler handler = new(new HttpRequestException("connection refused"));
        BackchannelLogoutNotifier sut = CreateSut(handler);

        Func<Task> act = () => sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        await act.Should().NotThrowAsync();
        handler.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task NotifyAsync_NotifiesEveryRecipient()
    {
        Uri otherUri = new("https://rp-two.example.com/bff/backchannel-logout");
        SetRecipients(
            new BackchannelLogoutRecipient(TestClientId, _logoutUri),
            new BackchannelLogoutRecipient("rp-two", otherUri));
        using ScriptedHandler handler = new();
        BackchannelLogoutNotifier sut = CreateSut(handler);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        handler.Requests.Select(r => r.Uri).Should().BeEquivalentTo([_logoutUri, otherUri]);

        // Each relying party gets its own token: the audience is the pairwise claim.
        handler.Requests
            .Select(r => new JsonWebToken(ExtractToken(r.Body)).Audiences.Single())
            .Should().BeEquivalentTo([TestClientId, "rp-two"]);
    }

    [Fact]
    public async Task NotifyAsync_WithNoRecipients_SendsNothing()
    {
        SetRecipients();
        using ScriptedHandler handler = new();
        BackchannelLogoutNotifier sut = CreateSut(handler);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_WithoutAsymmetricSigningCredentials_SendsNothing()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, _logoutUri));
        using ScriptedHandler handler = new();
        BackchannelLogoutNotifier sut = CreateSut(handler, withSigningCredentials: false);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://127.0.0.1/backchannel-logout")]
    [InlineData("http://10.0.0.5/backchannel-logout")]
    [InlineData("http://192.168.1.20/backchannel-logout")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/backchannel-logout")]
    public async Task NotifyAsync_RefusesPrivateTargetsByDefault(string privateUri)
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, new Uri(privateUri)));
        using ScriptedHandler handler = new();
        BackchannelLogoutNotifier sut = CreateSut(
            handler, new BackchannelLogoutOptions { AllowPrivateNetworkHosts = false });

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_DeliversToPrivateTargetsWhenTheKnobIsOn()
    {
        SetRecipients(new BackchannelLogoutRecipient(TestClientId, new Uri("http://10.0.0.5/backchannel-logout")));
        using ScriptedHandler handler = new();
        BackchannelLogoutNotifier sut = CreateSut(handler);

        await sut.NotifyAsync(TestSid, _userId, _issuer, CancellationToken.None);

        handler.Requests.Should().ContainSingle();
    }

    /// <summary>
    /// Answers each request with the next scripted status code (200 once the script runs out) and
    /// records every request. Recipients fan out in parallel, so recording is locked.
    /// </summary>
    private sealed class ScriptedHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statusCodes = new(statusCodes);
        private readonly Lock _gate = new();

        public List<(Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            HttpStatusCode status;
            lock (_gate)
            {
                Requests.Add((request.RequestUri!, body));
                status = _statusCodes.Count > 0 ? _statusCodes.Dequeue() : HttpStatusCode.OK;
            }

            return new HttpResponseMessage(status);
        }
    }

    private sealed class CountingThrowingHandler(Exception exception) : HttpMessageHandler
    {
        private int _attempts;

        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            throw exception;
        }
    }
}
