using System.Text.Json;
using Wallow.Auth.Models;

namespace Wallow.Auth.Services;

public sealed class AuthApiClient(IHttpClientFactory httpClientFactory) : IAuthApiClient
{
    private const string BasePath = "api/v1/identity/auth";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/login", request, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
        {
            return response.IsSuccessStatusCode
                ? new AuthResponse(Succeeded: true)
                : new AuthResponse(Succeeded: false, Error: "unknown_error");
        }

        AuthResponse? body = JsonSerializer.Deserialize<AuthResponse>(content, JsonSerializerOptions.Web);

        if (response.IsSuccessStatusCode)
        {
            return body ?? new AuthResponse(Succeeded: true);
        }

        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/register", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/forgot-password", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/reset-password", request, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> VerifyEmailAsync(string email, string token, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        string encodedEmail = Uri.EscapeDataString(email);
        string encodedToken = Uri.EscapeDataString(token);
        HttpResponseMessage response = await client.GetAsync(
            $"{BasePath}/verify-email?email={encodedEmail}&token={encodedToken}", ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<bool> ValidateRedirectUriAsync(string uri, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        string encodedUri = Uri.EscapeDataString(uri);
        HttpResponseMessage response = await client.GetAsync(
            $"{BasePath}/redirect-uri/validate?uri={encodedUri}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        RedirectUriValidationResponse? body = await response.Content
            .ReadFromJsonAsync<RedirectUriValidationResponse>(ct);
        return body?.Allowed == true;
    }

    public async Task<List<string>> GetExternalProvidersAsync(CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.GetAsync($"{BasePath}/external-providers", ct);

        if (response.IsSuccessStatusCode)
        {
            List<string>? providers = await response.Content.ReadFromJsonAsync<List<string>>(ct);
            return providers ?? [];
        }

        return [];
    }

    public async Task<string?> GetMatchingOrganizationByDomainAsync(string email, CancellationToken ct = default)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("AuthApi");
            string encodedEmail = Uri.EscapeDataString(email);
            HttpResponseMessage response = await client.GetAsync(
                $"api/v1/identity/organization-domains/match?email={encodedEmail}", ct);

            if (response.IsSuccessStatusCode)
            {
                OrganizationDomainMatchResponse? body = await response.Content
                    .ReadFromJsonAsync<OrganizationDomainMatchResponse>(ct);
                return body?.OrgName;
            }

            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> RequestMembershipAsync(string emailDomain, CancellationToken ct = default)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("AuthApi");
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "api/v1/identity/membership-requests", new { EmailDomain = emailDomain }, ct);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<AuthResponse> SendMagicLinkAsync(string email, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{BasePath}/passwordless/magic-link", new { Email = email }, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> VerifyMagicLinkAsync(string token, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        string encodedToken = Uri.EscapeDataString(token);
        HttpResponseMessage response = await client.GetAsync(
            $"{BasePath}/passwordless/magic-link/verify?token={encodedToken}", ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> SendOtpAsync(string email, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{BasePath}/passwordless/otp", new { Email = email }, ct);

        if (response.IsSuccessStatusCode)
        {
            return new AuthResponse(Succeeded: true);
        }

        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> VerifyOtpAsync(string email, string code, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{BasePath}/passwordless/otp/verify", new { Email = email, Code = code }, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
        {
            return response.IsSuccessStatusCode
                ? new AuthResponse(Succeeded: true)
                : new AuthResponse(Succeeded: false, Error: "unknown_error");
        }

        AuthResponse? body = JsonSerializer.Deserialize<AuthResponse>(content, JsonSerializerOptions.Web);

        if (response.IsSuccessStatusCode)
        {
            return body ?? new AuthResponse(Succeeded: true);
        }

        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> VerifyMfaChallengeAsync(string challengeToken, string code, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{BasePath}/mfa/verify",
            new { ChallengeToken = challengeToken, Code = code, UseBackupCode = false }, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
        {
            return response.IsSuccessStatusCode
                ? new AuthResponse(Succeeded: true)
                : new AuthResponse(Succeeded: false, Error: "unknown_error");
        }

        AuthResponse? body = JsonSerializer.Deserialize<AuthResponse>(content, JsonSerializerOptions.Web);

        if (response.IsSuccessStatusCode)
        {
            return body ?? new AuthResponse(Succeeded: true);
        }

        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<AuthResponse> UseBackupCodeAsync(string challengeToken, string code, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{BasePath}/mfa/verify",
            new { ChallengeToken = challengeToken, Code = code, UseBackupCode = true }, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
        {
            return response.IsSuccessStatusCode
                ? new AuthResponse(Succeeded: true)
                : new AuthResponse(Succeeded: false, Error: "unknown_error");
        }

        AuthResponse? body = JsonSerializer.Deserialize<AuthResponse>(content, JsonSerializerOptions.Web);

        if (response.IsSuccessStatusCode)
        {
            return body ?? new AuthResponse(Succeeded: true);
        }

        return body ?? new AuthResponse(Succeeded: false, Error: "unknown_error");
    }

    public async Task<InvitationDetailsResponse?> VerifyInvitationAsync(string token, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        string encodedToken = Uri.EscapeDataString(token);
        HttpResponseMessage response = await client.GetAsync(
            $"api/v1/identity/invitations/verify/{encodedToken}", ct);

        if (response.IsSuccessStatusCode)
        {
            InvitationDetailsResponse? body = await response.Content
                .ReadFromJsonAsync<InvitationDetailsResponse>(ct);
            return body;
        }

        return null;
    }

    public async Task<bool> AcceptInvitationAsync(string token, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        string encodedToken = Uri.EscapeDataString(token);
        HttpResponseMessage response = await client.PostAsync(
            $"api/v1/identity/invitations/{encodedToken}/accept", null, ct);

        return response.IsSuccessStatusCode;
    }

    private sealed record RedirectUriValidationResponse(bool Allowed);
    private sealed record OrganizationDomainMatchResponse(string? OrgName);
}
