using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Testcontainers.Keycloak;

namespace Foundry.Tests.Common.Fixtures;

public sealed class KeycloakFixture : IAsyncLifetime
{
    private const string DefaultUsername = "admin";
    private const string DefaultPassword = "admin";

    private readonly KeycloakContainer _keycloak = new KeycloakBuilder("quay.io/keycloak/keycloak:26.0")
        .WithResourceMapping(
            new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "foundry-realm.json")),
            "/opt/keycloak/data/import/")
        .WithCommand("--import-realm")
        .Build();

    public string BaseUrl => _keycloak.GetBaseAddress();
    public string AdminUsername => DefaultUsername;
    public string AdminPassword => DefaultPassword;

    public string RealmName => "foundry";
    public string ClientId => "foundry-api";
    public string ClientSecret => "foundry-api-secret";

    public string TokenEndpoint => $"{BaseUrl}/realms/{RealmName}/protocol/openid-connect/token";

    public async Task InitializeAsync()
    {
        string realmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "foundry-realm.json");
        if (!File.Exists(realmPath))
        {
            throw new FileNotFoundException($"Realm file not found at: {realmPath}. BaseDirectory={AppDomain.CurrentDomain.BaseDirectory}, CWD={Directory.GetCurrentDirectory()}");
        }

        await _keycloak.StartAsync();

        // Verify realm was imported by checking the token endpoint
        using HttpClient http = new();
        HttpResponseMessage response = await http.GetAsync($"{BaseUrl}/realms/{RealmName}");
        if (!response.IsSuccessStatusCode)
        {
            (string stdout, _) = await _keycloak.GetLogsAsync();
            throw new InvalidOperationException(
                $"Realm '{RealmName}' not found (status={response.StatusCode}). " +
                $"RealmFile={realmPath}, Exists={File.Exists(realmPath)}.\n" +
                $"STDOUT (last 2000 chars):\n{stdout[Math.Max(0, stdout.Length - 2000)..]}");
        }
    }

    public async Task DisposeAsync() => await _keycloak.DisposeAsync();

    public async Task<string> GetServiceAccountTokenAsync(string clientId, string clientSecret)
    {
        using HttpClient httpClient = new HttpClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            })
        };

        HttpResponseMessage response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        TokenResponse? content = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return content?.AccessToken ?? throw new InvalidOperationException("Failed to retrieve access token");
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}
