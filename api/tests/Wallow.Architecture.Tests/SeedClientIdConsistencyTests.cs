using System.Text.Json;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards OIDC clientId naming against drift between the files a fork operator has to keep in
/// sync by hand (bead Wallow-361m). Two independent drifts are pinned here.
///
/// <para><b>1. The fork's SPA client is documented under a name nobody seeds.</b>
/// The reference deployment's production seed (<c>docker/seed.production.json</c>, gitignored
/// per <c>.gitignore</c>'s <c>seed.*.json</c> rule) registers the bcordes.dev SPA as
/// <c>bcordes-dev-client</c>, and <c>api/src/Wallow.Api/appsettings.json</c> names the same id.
/// The two committed files that document that client —
/// <c>docker/docker-compose.production.yml</c>'s <c>ClientSecrets__&lt;clientId&gt;</c> map and
/// <c>docker/.env.production.example</c>'s clientId checklist — once called it
/// <c>bcordes-client</c>. An operator following the example values registers a client id that
/// does not match the one their SPA authenticates with, and the OIDC flow fails with no obvious
/// cause. The seeded value is canonical because it is already registered in the deployed
/// OpenIddict application table: renaming it needs a re-seed plus an SPA reconfiguration,
/// whereas the two drifted sites are inert config that costs nothing to correct. The compose
/// map is name-keyed (a <c>ClientSecrets__&lt;clientId&gt;</c> env var attaches its secret to the
/// seed client with that id), so a drifted key there is a secret that lands on no client — the
/// seeder aborts on a non-blank orphaned secret, but only at deploy time on a machine that sets
/// the variable; this pin catches the drift in CI.</para>
///
/// <para><b>2. The dev first-party list points at nothing.</b>
/// <c>Identity:FirstPartyClients</c> in <c>api/src/Wallow.Api/appsettings.json</c> lists
/// <c>bcordes-dev-client</c>, which is a <i>production</i> client — <c>api/seed.json</c> never
/// seeds it. First-party matching is by client id, so the dev list matches no client and consent
/// is never skipped locally. Every entry must therefore name a client <c>api/seed.json</c>
/// actually seeds. Whether an entry may be dropped outright is a separate question, settled by
/// the positional-override invariant in <see cref="PublicSeedClientRemovalTests"/>: entries may
/// be removed as long as <c>docker/docker-compose.test.yml</c>'s
/// <c>Identity__FirstPartyClients__&lt;index&gt;</c> overrides are renumbered to continue where
/// the shortened appsettings array ends.</para>
///
/// These are static source assertions, in the same style as
/// <see cref="TraceSamplerConfigurationTests" /> and <see cref="CiAuthImageBuildTests" />, because
/// the real failure only shows up in a deployed OIDC round trip. The production seed file itself
/// is gitignored and therefore cannot be asserted on from CI — the committed files that describe
/// it are the only enforceable surface.
/// </summary>
public class SeedClientIdConsistencyTests
{
    /// <summary>The id the reference deployment's production seed actually registers.</summary>
    private const string CanonicalForkClientId = "bcordes-dev-client";

    /// <summary>The stale id the committed deployment docs use for the same client.</summary>
    private const string DriftedForkClientId = "bcordes-client";

    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _productionComposePath = Path.Combine(
        _repoRoot,
        "docker",
        "docker-compose.production.yml");

    private static readonly string _testComposePath = Path.Combine(
        _repoRoot,
        "docker",
        "docker-compose.test.yml");

    private static readonly string _envProductionExamplePath = Path.Combine(
        _repoRoot,
        "docker",
        ".env.production.example");

    private static readonly string _apiAppSettingsPath = Path.Combine(
        _repoRoot,
        "api",
        "src",
        "Wallow.Api",
        "appsettings.json");

    private static readonly string _seedJsonPath = Path.Combine(_repoRoot, "api", "seed.json");

    // ---- production deployment annotations ---------------------------------------------

    [Fact]
    public void ProductionCompose_ClientSecretMap_ShouldKeyTheSeededForkClientId()
    {
        string source = File.ReadAllText(_productionComposePath);

        source.Should().Contain(
            $"ClientSecrets__{CanonicalForkClientId}:",
            "the ClientSecrets__<clientId> map in docker-compose.production.yml attaches each " +
            "injected secret to the seed.production.json client with that id; the SPA secret's key " +
            "must name '{0}', the id that file actually registers, or BCORDES_CLIENT_SECRET becomes " +
            "an orphaned secret that aborts the seeder at deploy time",
            CanonicalForkClientId);
    }

    [Fact]
    public void ProductionCompose_ClientSecretMap_ShouldNotNameTheDriftedClientId()
    {
        string source = File.ReadAllText(_productionComposePath);

        source.Should().NotContain(
            DriftedForkClientId,
            "'{0}' is not seeded anywhere; it is a stale name for '{1}'. Leaving it in the index " +
            "map keeps the three-way drift alive even after the canonical id is added",
            DriftedForkClientId,
            CanonicalForkClientId);
    }

    [Fact]
    public void EnvProductionExample_ClientIdChecklist_ShouldNameTheSeededForkClientId()
    {
        string source = File.ReadAllText(_envProductionExamplePath);

        source.Should().Contain(
            $"BCORDES_CLIENT_ID={CanonicalForkClientId}",
            ".env.production.example documents BCORDES_CLIENT_ID as a checklist of what must be " +
            "set inside seed.production.json for that client. It has to quote the id that file " +
            "really uses ('{0}'), since the whole point of the annotation is keeping the two files " +
            "in sync",
            CanonicalForkClientId);
    }

    [Fact]
    public void EnvProductionExample_ClientIdChecklist_ShouldNotNameTheDriftedClientId()
    {
        string source = File.ReadAllText(_envProductionExamplePath);

        source.Should().NotContain(
            DriftedForkClientId,
            "the checklist value must be corrected in place, not duplicated: a fork operator " +
            "copying '{0}' into seed.production.json gets a client id their SPA never asks for",
            DriftedForkClientId);
    }

    // ---- dev first-party client list ----------------------------------------------------

    // A "must not be empty" invariant used to live here, on the reasoning that
    // docker-compose.test.yml's positional overrides start at index 1 and so index 0 had to keep
    // a value. That is the wrong half of the constraint: what actually matters is that the
    // override indexes track the appsettings array's length, which
    // PublicSeedClientRemovalTests.TestCompose_FirstPartyClientOverrides_ShouldContinueWhereAppSettingsEnds
    // pins directly. Keeping the old test here would forbid deleting a client outright — exactly
    // what the public-client removal PublicSeedClientRemovalTests guards required.

    [Fact]
    public void DevFirstPartyClients_ShouldOnlyNameClientsThatSeedJsonSeeds()
    {
        IReadOnlyList<string> firstPartyClients = ReadDevFirstPartyClients();
        IReadOnlyList<string> seededClientIds = ReadSeededClientIds();

        firstPartyClients.Should().BeSubsetOf(
            seededClientIds,
            "Identity:FirstPartyClients matches by client id, so every entry in the dev " +
            "appsettings must be a client api/seed.json actually seeds. '{0}' is a production-only " +
            "id from seed.production.json — while it sits here the dev first-party list matches " +
            "nothing and the local OIDC flow always shows the consent screen. Seeded ids today: {1}",
            CanonicalForkClientId,
            string.Join(", ", seededClientIds));
    }

    [Fact]
    public void TestCompose_ShouldNotNameAProductionOnlyClientId()
    {
        string source = File.ReadAllText(_testComposePath);

        source.Should().NotContain(
            CanonicalForkClientId,
            "docker-compose.test.yml explains its Identity__FirstPartyClients__<index> overrides " +
            "by stating what appsettings already lists ahead of them. That comment names '{0}', a " +
            "production-only client; it must track whatever appsettings really declares, or the " +
            "next reader re-introduces the drift",
            CanonicalForkClientId);
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
