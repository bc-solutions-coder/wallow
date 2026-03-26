using System.Net;
using RichardSzalay.MockHttp;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Tests.Services;

public sealed class AuthApiClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly AuthApiClient _sut;

    public AuthApiClientTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AuthApi").Returns(httpClient);

        _sut = new AuthApiClient(factory);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "password", false));

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond(HttpStatusCode.Unauthorized, "application/json",
                """{"succeeded":false,"error":"invalid_credentials"}""");

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "wrong", false));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task LoginAsync_LockedOut_ReturnsLockedOutError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond((HttpStatusCode)423, "application/json",
                """{"succeeded":false,"error":"locked_out"}""");

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "password", false));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("locked_out");
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/register")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.RegisterAsync(
            new RegisterRequest("test@test.com", "Password1!", "Password1!"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/register")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"email_taken"}""");

        AuthResponse result = await _sut.RegisterAsync(
            new RegisterRequest("taken@test.com", "Password1!", "Password1!"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("email_taken");
    }

    [Fact]
    public async Task ForgotPasswordAsync_AnyEmail_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/forgot-password")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("any@test.com"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/verify-email*")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.VerifyEmailAsync("test@test.com", "valid-token");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidRequest_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/reset-password")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.ResetPasswordAsync(
            new ResetPasswordRequest("test@test.com", "valid-token", "NewPassword1!"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/reset-password")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"invalid_token"}""");

        AuthResponse result = await _sut.ResetPasswordAsync(
            new ResetPasswordRequest("test@test.com", "bad-token", "NewPassword1!"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_token");
    }

    [Fact]
    public async Task ValidateRedirectUriAsync_AllowedUri_ReturnsTrue()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/redirect-uri/validate*")
            .Respond("application/json", """{"allowed":true}""");

        bool result = await _sut.ValidateRedirectUriAsync("https://app.example.com/callback");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRedirectUriAsync_DisallowedUri_ReturnsFalse()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/redirect-uri/validate*")
            .Respond(HttpStatusCode.BadRequest, "application/json", """{"allowed":false}""");

        bool result = await _sut.ValidateRedirectUriAsync("https://evil.com/callback");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetExternalProvidersAsync_ProvidersExist_ReturnsList()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/external-providers")
            .Respond("application/json", """["Google","Microsoft"]""");

        List<string> result = await _sut.GetExternalProvidersAsync();

        result.Should().BeEquivalentTo("Google", "Microsoft");
    }

    [Fact]
    public async Task GetExternalProvidersAsync_ServerError_ReturnsEmptyList()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/external-providers")
            .Respond(HttpStatusCode.InternalServerError);

        List<string> result = await _sut.GetExternalProvidersAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMatchingOrganizationByDomainAsync_MatchFound_ReturnsOrgName()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/organization-domains/match*")
            .Respond("application/json", """{"orgName":"Acme Corp"}""");

        string? result = await _sut.GetMatchingOrganizationByDomainAsync("user@acme.com");

        result.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task GetMatchingOrganizationByDomainAsync_NoMatch_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/organization-domains/match*")
            .Respond(HttpStatusCode.NotFound);

        string? result = await _sut.GetMatchingOrganizationByDomainAsync("user@unknown.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RequestMembershipAsync_Success_ReturnsTrue()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/membership-requests")
            .Respond(HttpStatusCode.OK);

        bool result = await _sut.RequestMembershipAsync("acme.com");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestMembershipAsync_ServerError_ReturnsFalse()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/membership-requests")
            .Respond(HttpStatusCode.InternalServerError);

        bool result = await _sut.RequestMembershipAsync("acme.com");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendMagicLinkAsync_ValidEmail_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/passwordless/magic-link")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.SendMagicLinkAsync("test@test.com");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task SendMagicLinkAsync_Failure_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/passwordless/magic-link")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"rate_limited"}""");

        AuthResponse result = await _sut.SendMagicLinkAsync("test@test.com");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("rate_limited");
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_ValidToken_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/passwordless/magic-link/verify*")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.VerifyMagicLinkAsync("valid-magic-token");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_InvalidToken_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/passwordless/magic-link/verify*")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"invalid_token"}""");

        AuthResponse result = await _sut.VerifyMagicLinkAsync("bad-token");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_token");
    }

    [Fact]
    public async Task SendOtpAsync_ValidEmail_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/passwordless/otp")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.SendOtpAsync("test@test.com");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task SendOtpAsync_Failure_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/passwordless/otp")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"rate_limited"}""");

        AuthResponse result = await _sut.SendOtpAsync("test@test.com");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("rate_limited");
    }

    [Fact]
    public async Task VerifyOtpAsync_ValidCode_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/passwordless/otp/verify")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.VerifyOtpAsync("test@test.com", "123456");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyOtpAsync_InvalidCode_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/passwordless/otp/verify")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"invalid_code"}""");

        AuthResponse result = await _sut.VerifyOtpAsync("test@test.com", "000000");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_code");
    }

    [Fact]
    public async Task VerifyMfaChallengeAsync_ValidCode_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/mfa/verify")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.VerifyMfaChallengeAsync("challenge-token", "123456");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyMfaChallengeAsync_InvalidCode_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/mfa/verify")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"invalid_mfa_code"}""");

        AuthResponse result = await _sut.VerifyMfaChallengeAsync("challenge-token", "000000");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_mfa_code");
    }

    [Fact]
    public async Task UseBackupCodeAsync_ValidCode_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/mfa/verify")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.UseBackupCodeAsync("challenge-token", "backup-code");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task UseBackupCodeAsync_InvalidCode_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/mfa/verify")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"invalid_backup_code"}""");

        AuthResponse result = await _sut.UseBackupCodeAsync("challenge-token", "bad-code");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_backup_code");
    }

    [Fact]
    public async Task VerifyInvitationAsync_ValidToken_ReturnsDetails()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/invitations/verify/*")
            .Respond("application/json",
                """{"id":"00000000-0000-0000-0000-000000000001","email":"invited@test.com","status":"Pending","expiresAt":"2026-12-31T00:00:00+00:00","createdAt":"2026-01-01T00:00:00+00:00","acceptedByUserId":null}""");

        InvitationDetailsResponse? result = await _sut.VerifyInvitationAsync("valid-invite-token");

        result.Should().NotBeNull();
        result!.Email.Should().Be("invited@test.com");
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task VerifyInvitationAsync_InvalidToken_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/invitations/verify/*")
            .Respond(HttpStatusCode.NotFound);

        InvitationDetailsResponse? result = await _sut.VerifyInvitationAsync("bad-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvitationAsync_ValidToken_ReturnsTrue()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/invitations/*/accept")
            .Respond(HttpStatusCode.OK);

        bool result = await _sut.AcceptInvitationAsync("valid-invite-token");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AcceptInvitationAsync_InvalidToken_ReturnsFalse()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/invitations/*/accept")
            .Respond(HttpStatusCode.NotFound);

        bool result = await _sut.AcceptInvitationAsync("bad-token");

        result.Should().BeFalse();
    }
}
