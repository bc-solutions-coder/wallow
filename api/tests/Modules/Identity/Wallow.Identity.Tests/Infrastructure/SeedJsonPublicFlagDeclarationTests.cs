using System.Text.Json;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Every client entry shipped in seed.json must declare its public/confidential nature
/// explicitly, so a fork reading the file can never mistake "no secret written here"
/// (secrets are injected by env in production) for "this client is intentionally public".
/// </summary>
public sealed class SeedJsonPublicFlagDeclarationTests
{
    private const string PublicKey = "public";

    [Fact]
    public void SeedJson_EveryActiveClient_DeclaresThePublicFlag()
    {
        AssertEveryClientDeclaresPublic("clients");
    }

    [Fact]
    public void SeedJson_EveryProductionExampleClient_DeclaresThePublicFlag()
    {
        AssertEveryClientDeclaresPublic("_productionExampleClients");
    }

    [Fact]
    public void SeedJson_PublicFlags_AreBooleans()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));

        foreach (string arrayKey in new[] { "clients", "_productionExampleClients" })
        {
            foreach (JsonElement client in document.RootElement.GetProperty(arrayKey).EnumerateArray())
            {
                if (client.TryGetProperty(PublicKey, out JsonElement flag))
                {
                    flag.ValueKind.Should().BeOneOf(
                        [JsonValueKind.True, JsonValueKind.False],
                        $"the '{PublicKey}' flag must be a JSON boolean, not a string");
                }
            }
        }
    }

    [Fact]
    public void SeedJson_SecretlessClients_AreDeclaredPublic()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));

        foreach (JsonElement client in document.RootElement.GetProperty("clients").EnumerateArray())
        {
            string clientId = client.GetProperty("clientId").GetString() ?? "(unnamed)";
            bool hasSecret = client.TryGetProperty("secret", out JsonElement secret)
                && !string.IsNullOrWhiteSpace(secret.GetString());

            if (!hasSecret)
            {
                client.TryGetProperty(PublicKey, out JsonElement flag).Should().BeTrue(
                    $"seed client '{clientId}' has no secret and must declare \"{PublicKey}\": true");
                flag.ValueKind.Should().Be(
                    JsonValueKind.True,
                    $"seed client '{clientId}' has no secret, so it can only be a public client");
            }
        }
    }

    private static void AssertEveryClientDeclaresPublic(string arrayKey)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));

        JsonElement clients = document.RootElement.GetProperty(arrayKey);
        clients.GetArrayLength().Should().BeGreaterThan(0, $"seed.json should declare '{arrayKey}' entries");

        foreach (JsonElement client in clients.EnumerateArray())
        {
            string clientId = client.GetProperty("clientId").GetString() ?? "(unnamed)";

            client.TryGetProperty(PublicKey, out _).Should().BeTrue(
                $"'{arrayKey}' entry '{clientId}' must declare the \"{PublicKey}\" flag explicitly");
        }
    }

    private static string SeedPath()
    {
        string seedPath = Path.Combine(GetSolutionRoot(), "seed.json");
        File.Exists(seedPath).Should().BeTrue($"seed.json should exist at repo root ({seedPath})");
        return seedPath;
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
