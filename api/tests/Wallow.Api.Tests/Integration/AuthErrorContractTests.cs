using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Kernel.Errors;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// The failure sweep over the auth and MFA routes: every way sign-in, registration, token
/// redemption and MFA can fail answers the unified problem contract with an <c>Auth.*</c>,
/// <c>Mfa.*</c> or shared code and the catalogued user-safe <c>detail</c>. Unknown accounts on
/// the credential paths collapse into <c>Auth.InvalidCredentials</c>, and the passwordless
/// throttle answers 429 <c>RateLimit.Exceeded</c> with a <c>Retry-After</c>.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class AuthErrorContractTests : IDisposable
{
    private const string AuthBase = "/v1/identity/auth";
    private const string MfaBase = "/v1/identity/mfa";
    private const string AdminEmail = "admin@wallow.test";
    private const string Password = "Password1!";

    private readonly HttpClient _client;
    private readonly ErrorCatalog _catalog;

    public AuthErrorContractTests(WallowApiFactory factory)
    {
        _client = factory.CreateClient();
        _catalog = factory.Services.GetRequiredService<ErrorCatalog>();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Login_With_Unknown_Email_Collapses_Into_Invalid_Credentials()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"{AuthBase}/login", new { email = $"{Guid.NewGuid():N}@nobody.test", password = Password, rememberMe = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, IdentityErrors.AuthInvalidCredentials);
    }

    [Fact]
    public async Task Login_Before_Confirming_Email_Returns_403_Email_Not_Confirmed()
    {
        string email = await RegisterFreshUserAsync();

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"{AuthBase}/login", new { email, password = Password, rememberMe = false });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(response, 403, IdentityErrors.AuthEmailNotConfirmed);
    }

    [Fact]
    public async Task Mfa_Verify_Without_A_Partial_Session_Returns_401_Session_Missing()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"{AuthBase}/mfa/verify", new { code = "000000", useBackupCode = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, IdentityErrors.MfaSessionMissing);
    }

    [Fact]
    public async Task External_Login_With_An_Unknown_Provider_Returns_400_Provider_Unsupported()
    {
        // An empty provider never reaches the action: the required query parameter fails model
        // validation first, which the shared sweep covers.
        HttpResponseMessage response = await _client.GetAsync($"{AuthBase}/external-login?provider=Nope&returnUrl=/");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(response, 400, IdentityErrors.AuthProviderUnsupported);
    }

    [Fact]
    public async Task Exchange_Ticket_With_Garbage_Returns_401_Ticket_Invalid()
    {
        HttpResponseMessage response = await _client.GetAsync($"{AuthBase}/exchange-ticket?ticket=not-a-ticket");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, IdentityErrors.AuthTicketInvalid);
    }

    [Fact]
    public async Task Register_With_Mismatched_Passwords_Returns_400_Passwords_Do_Not_Match()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"{AuthBase}/register",
            new { email = $"{Guid.NewGuid():N}@nobody.test", password = Password, confirmPassword = "Different1!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(response, 400, IdentityErrors.AuthPasswordsDoNotMatch);
    }

    [Fact]
    public async Task Register_With_A_Taken_Email_Returns_409_Email_Taken()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"{AuthBase}/register", new { email = AdminEmail, password = Password, confirmPassword = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(response, 409, IdentityErrors.AuthEmailTaken);
    }

    [Fact]
    public async Task Reset_Password_With_A_Bad_Token_Returns_400_Token_Invalid()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"{AuthBase}/reset-password", new { email = AdminEmail, token = "bad-token", newPassword = Password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(response, 400, IdentityErrors.AuthTokenInvalid);
    }

    [Fact]
    public async Task Verify_Email_With_A_Bad_Token_Returns_400_Token_Invalid()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{AuthBase}/verify-email?email={Uri.EscapeDataString(AdminEmail)}&token=bad-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(response, 400, IdentityErrors.AuthTokenInvalid);
    }

    [Fact]
    public async Task Magic_Link_Verify_With_Garbage_Returns_400_Token_Invalid()
    {
        HttpResponseMessage response = await _client.GetAsync($"{AuthBase}/passwordless/magic-link/verify?token=garbage");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(response, 400, IdentityErrors.AuthTokenInvalid);
    }

    [Fact]
    public async Task Otp_Verify_With_An_Unknown_Code_Returns_401_Otp_Invalid()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"{AuthBase}/passwordless/otp/verify",
            new { email = $"{Guid.NewGuid():N}@nobody.test", code = "000000", rememberMe = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, IdentityErrors.AuthOtpInvalid);
    }

    [Fact]
    public async Task Passwordless_Throttle_Returns_429_With_Retry_After()
    {
        // Sends for an unknown address succeed silently (no enumeration) but still spend the
        // per-address window, so the first request past the limit is the throttled one.
        string email = $"{Guid.NewGuid():N}@nobody.test";
        HttpResponseMessage response = await _client.PostAsJsonAsync($"{AuthBase}/passwordless/otp", new { email });
        for (int attempt = 0; attempt < 10 && response.StatusCode != HttpStatusCode.TooManyRequests; attempt++)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response = await _client.PostAsJsonAsync($"{AuthBase}/passwordless/otp", new { email });
        }

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
        await AssertProblemAsync(response, 429, SharedErrors.RateLimitExceeded);
    }

    [Fact]
    public async Task Mfa_Status_Unauthenticated_Returns_401_Problem()
    {
        HttpResponseMessage response = await _client.GetAsync($"{MfaBase}/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, SharedErrors.Unauthenticated);
    }

    [Fact]
    public async Task Mfa_Enroll_Without_A_Session_Returns_401_Session_Missing()
    {
        HttpResponseMessage response = await _client.PostAsync($"{MfaBase}/enroll/totp", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, IdentityErrors.MfaSessionMissing);
    }

    [Fact]
    public async Task Mfa_Enrollment_Token_Exchange_With_Garbage_Returns_401_Token_Invalid()
    {
        HttpResponseMessage response = await _client.PostAsync($"{MfaBase}/enroll/exchange-token?token=garbage", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, IdentityErrors.MfaEnrollmentTokenInvalid);
    }

    private async Task<string> RegisterFreshUserAsync()
    {
        string email = $"{Guid.NewGuid():N}@nobody.test";
        HttpResponseMessage registered = await _client.PostAsJsonAsync(
            $"{AuthBase}/register", new { email, password = Password, confirmPassword = Password });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        return email;
    }

    /// <summary>
    /// The shared contract assertion (<see cref="ProblemAssertions"/>) with this sweep's catalog,
    /// pinning <c>detail</c> to the entry's own sentence.
    /// </summary>
    private Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, int expectedStatus, ErrorCatalogEntry expected) =>
        response.AssertProblemAsync(_catalog, expectedStatus, expected);
}
