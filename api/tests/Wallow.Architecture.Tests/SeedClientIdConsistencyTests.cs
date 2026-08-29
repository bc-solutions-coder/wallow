using System.Text.Json;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards OIDC clientId naming against drift between the files an operator has to keep in sync
/// by hand (bead Wallow-361m). The original drift this pinned — a fork-specific SPA client
/// documented under one id in <c>docker/.env.production.example</c> and seeded under another —
/// was retired along with the fork client itself: the production compose stack now injects a
/// secret only for the platform's own dashboard client, and additional production clients are
/// created through the organizations UI rather than seeded. What remains is the invariant that
/// still bites locally.
///
/// <para><b>The dev first-party list must point at seeded clients.</b>
/// <c>Identity:FirstPartyClients</c> in <c>api/src/Wallow.Api/appsettings.json</c> matches by
/// client id, so an entry naming a client <c>api/seed.json</c> never seeds matches nothing —
/// consent is silently never skipped locally, and the only symptom is an OIDC round trip that
/// behaves differently from production. Whether an entry may be dropped outright is a separate
/// question, settled by the positional-override invariant in
/// <see cref="PublicSeedClientRemovalTests"/>: entries may be removed as long as
/// <c>docker/docker-compose.test.yml</c>'s <c>Identity__FirstPartyClients__&lt;index&gt;</c>
/// overrides are renumbered to continue where the shortened appsettings array ends.</para>
///
/// This is a static source assertion, in the same style as
/// <see cref="TraceSamplerConfigurationTests" /> and <see cref="CiAuthImageBuildTests" />, because
/// the real failure only shows up in a live OIDC round trip.
/// </summary>
public class SeedClientIdConsistencyTests
{
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _apiAppSettingsPath = Path.Combine(
        _repoRoot,
        "api",
        "src",
        "Wallow.Api",
        "appsettings.json");

    private static readonly string _seedJsonPath = Path.Combine(_repoRoot, "api", "seed.json");

    [Fact]
    public void DevFirstPartyClients_ShouldOnlyNameClientsThatSeedJsonSeeds()
    {
        IReadOnlyList<string> firstPartyClients = ReadDevFirstPartyClients();
        IReadOnlyList<string> seededClientIds = ReadSeededClientIds();

        firstPartyClients.Should().BeSubsetOf(
            seededClientIds,
            "Identity:FirstPartyClients matches by client id, so every entry in the dev " +
            "appsettings must be a client api/seed.json actually seeds — an unseeded id matches " +
            "nothing and the local OIDC flow always shows the consent screen. Seeded ids today: {0}",
            string.Join(", ", seededClientIds));
    }

    // ---- helpers -------------------------------------------------------------------------

    private static List<string> ReadDevFirstPartyClients()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_apiAppSettingsPath));

        document.RootElement.TryGetProperty("Identity", out JsonElement identity)
            .Should().BeTrue("api/src/Wallow.Api/appsettings.json must declare an Identity section");

        identity.TryGetProperty("FirstPartyClients", out JsonElement firstPartyClients)
            .Should().BeTrue("the Identity section must declare FirstPartyClients");

        List<string> clientIds = [];
        foreach (JsonElement entry in firstPartyClients.EnumerateArray())
        {
            string? clientId = entry.GetString();
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                clientIds.Add(clientId);
            }
        }

        return clientIds;
    }

    private static List<string> ReadSeededClientIds()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_seedJsonPath));

        JsonElement clients = document.RootElement.GetProperty("clients");

        List<string> clientIds = [];
        foreach (JsonElement client in clients.EnumerateArray())
        {
            if (client.TryGetProperty("clientId", out JsonElement clientId))
            {
                string? value = clientId.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    clientIds.Add(value);
                }
            }
        }

        return clientIds;
    }

    private static string FindRepoRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "pnpm-workspace.yaml")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find the repo root containing pnpm-workspace.yaml");
    }
}
