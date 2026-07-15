using System.Text.Json;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Verifies that the repo-root seed.json declares the confidential
/// Authorization-Code + PKCE client the BFF uses to authenticate against Wallow.
/// </summary>
public sealed class SeedJsonBffClientTests
{
    private const string BffClientId = "bcordes-bff";
    private const string ExpectedRedirectUri = "http://localhost:3000/bff/callback";

    private static readonly string[] _expectedScopes =
    [
        "openid", "email", "profile", "roles", "offline_access",
        "inquiries.read", "inquiries.write",
        "notifications.read", "notifications.write"
    ];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static List<PreRegisteredClientDefinition> LoadSeededClients()
    {
        string seedPath = Path.Combine(GetSolutionRoot(), "seed.json");
        File.Exists(seedPath).Should().BeTrue($"seed.json should exist at repo root ({seedPath})");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(seedPath));
        JsonElement clients = document.RootElement.GetProperty("clients");

        List<PreRegisteredClientDefinition> definitions = [];
        foreach (JsonElement client in clients.EnumerateArray())
        {
            PreRegisteredClientDefinition? definition =
                client.Deserialize<PreRegisteredClientDefinition>(_jsonOptions);
            if (definition is not null)
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    private static PreRegisteredClientDefinition GetBffClient()
    {
        PreRegisteredClientDefinition? bff =
            LoadSeededClients().FirstOrDefault(c => c.ClientId == BffClientId);
        bff.Should().NotBeNull($"seed.json must contain a '{BffClientId}' client entry");
        return bff!;
    }

    [Fact]
    public void SeedJson_ContainsBffClient()
    {
        LoadSeededClients().Select(c => c.ClientId).Should().Contain(BffClientId);
    }

    [Fact]
    public void BffClient_IsConfidential()
    {
        PreRegisteredClientDefinition bff = GetBffClient();

        bff.Secret.Should().NotBeNullOrEmpty("the BFF client must be confidential (secret-driven)");
        bff.IsPublic.Should().BeFalse("a confidential client is not public");
    }

    [Fact]
    public void BffClient_IsAuthorizationCodeClient_NotServiceAccount()
    {
        PreRegisteredClientDefinition bff = GetBffClient();

        // The seeder routes 'sa-' prefixed clients to ClientCredentials only.
        // A non-'sa-' client with redirect URIs gets AuthorizationCode + RefreshToken + PKCE.
        bff.ClientId.Should().NotStartWith("sa-");
        bff.RedirectUris.Should().NotBeEmpty("auth-code clients require at least one redirect URI");
    }

    [Fact]
    public void BffClient_HasExpectedRedirectUri()
    {
        PreRegisteredClientDefinition bff = GetBffClient();

        bff.RedirectUris.Should().Contain(ExpectedRedirectUri);
    }

    [Fact]
    public void BffClient_HasExpectedScopes()
    {
        PreRegisteredClientDefinition bff = GetBffClient();

        bff.Scopes.Should().Contain(_expectedScopes);
    }

    private static string GetSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Wallow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Solution root not found");
    }
}
