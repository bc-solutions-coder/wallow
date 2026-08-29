using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the removal of the public <c>wallow-dev-client</c> OIDC seed client (bead
/// Wallow-pu6a.1.1, closing finding F1/R1 of the SDK review): a first-party client registered
/// with no secret authenticates on client id alone, so once a fork adds any other public client
/// the two are mutually spoofable. The remedy chosen was deletion, not conversion to a
/// confidential client, because every frontend the repo ships (<c>apps/wallow-web</c> via its
/// BFF, <c>apps/wallow-auth</c>) already authenticates through a client that holds a secret.
///
/// <para>Two classes of assertion live here. The first pins the deletion itself — the id is
/// gone from <c>api/seed.json</c>, from the dev first-party list, and from every tracked source,
/// config, and doc file — and is what fails until the client is actually removed. The second
/// pins the invariant the deletion is most likely to break silently: the environment overrides
/// in <c>docker/docker-compose.test.yml</c> address both the seeded client array and the
/// first-party client array <b>positionally</b>, so deleting an entry shifts every later index
/// down by one. An unshifted override does not error — it lands on the wrong client, or opens a
/// hole in the array — and the only symptom is an OIDC round trip that fails in the e2e stack.
/// Those two tests therefore pass today and must keep passing after the renumber.</para>
///
/// <para>Static source assertions, in the same style as <see cref="WallowWebDeletionTests"/> and
/// <see cref="SeedClientIdConsistencyTests"/>, because the real failure only surfaces in a
/// deployed OIDC round trip. Client-id naming drift between the fork's committed deployment docs
/// is a separate concern and stays in <see cref="SeedClientIdConsistencyTests"/>.</para>
/// </summary>
public class PublicSeedClientRemovalTests
{
    /// <summary>The public client id this bead deletes.</summary>
    private const string RemovedPublicClientId = "wallow-dev-client";

    /// <summary>
    /// The confidential client <c>apps/wallow-web</c>'s BFF uses. It sits after the removed
    /// client in <c>api/seed.json</c>, so it is the entry whose positional index shifts.
    /// </summary>
    private const string WebBffClientId = "wallow-web-client";

    /// <summary>
    /// The redirect URI docker-compose.test.yml overrides onto <see cref="WebBffClientId"/>.
    /// The host port is parameterized (Wallow-joo0), defaulting to 5053.
    /// </summary>
    private const string TestComposeRedirectUri = "http://localhost:${E2E_WEB_PORT:-5053}/bff/callback";

    /// <summary>
    /// Directories that are build output, dependencies, test artifacts, gitignored local
    /// scratch, or immutable historical records (plans, the beads tracker export), none of
    /// which are source a fork acts on, so none of which the deletion has to be true of.
    /// </summary>
    private static readonly HashSet<string> _prunedDirectories = new(StringComparer.Ordinal)
    {
        "node_modules",
        "bin",
        "obj",
        "dist",
        ".output",
        ".vite",
        "TestResults",
        "test-results",
        "playwright-report",
        "coverage",
        "plans",
        "beads-archive",
    };

    /// <summary>Text file extensions worth sweeping for a client id.</summary>
    private static readonly HashSet<string> _sweptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".json",
        ".yml",
        ".yaml",
        ".md",
        ".ts",
        ".tsx",
        ".js",
        ".jsx",
        ".sh",
        ".props",
        ".targets",
        ".csproj",
        ".slnx",
        ".example",
        ".http",
    };

    /// <summary>Roots that hold every tracked source, config, and doc file of the repo.</summary>
    private static readonly string[] _sweptRoots =
    [
        "api",
        "apps",
        "packages",
        "docker",
        "docs",
        "scripts",
        ".github",
    ];

    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _seedJsonPath = Path.Combine(_repoRoot, "api", "seed.json");

    private static readonly string _apiAppSettingsPath = Path.Combine(
        _repoRoot,
        "api",
        "src",
        "Wallow.Api",
        "appsettings.json");

    private static readonly string _testComposePath = Path.Combine(
        _repoRoot,
        "docker",
        "docker-compose.test.yml");

    // ---- the deletion itself -------------------------------------------------------------

    [Fact]
    public void SeedJson_ShouldNotSeed_TheRemovedPublicClient()
    {
        IReadOnlyList<string> seededClientIds = ReadSeededClientIds();

        seededClientIds.Should().NotContain(
            RemovedPublicClientId,
            "'{0}' is a public first-party client with no secret, so anything that learns the id " +
            "can impersonate it at the token endpoint. api/seed.json must stop registering it. " +
            "Seeded ids today: {1}",
            RemovedPublicClientId,
            string.Join(", ", seededClientIds));
    }

    [Fact]
    public void SeedJson_EveryClient_ShouldRegisterASecret()
    {
        List<string> secretlessClientIds = ReadSeededClients()
            .Where(client => string.IsNullOrWhiteSpace(client.Secret))
            .Select(client => client.ClientId)
            .ToList();

        secretlessClientIds.Should().BeEmpty(
            "a seeded client with no secret is registered as an OpenIddict public client, which " +
            "authenticates on client id alone; PreRegisteredClientDefinition infers public-ness " +
            "from exactly that missing secret. Every client api/seed.json ships must be " +
            "confidential. Secret-less today: {0}",
            string.Join(", ", secretlessClientIds));
    }

    [Fact]
    public void DevFirstPartyClients_ShouldNotName_TheRemovedPublicClient()
    {
        IReadOnlyList<string> firstPartyClients = ReadDevFirstPartyClients();

        firstPartyClients.Should().NotContain(
            RemovedPublicClientId,
            "Identity:FirstPartyClients in api/src/Wallow.Api/appsettings.json makes a client skip " +
            "the interactive consent screen. appsettings.Development.json declares no override, so " +
            "this array is the whole dev list; naming a deleted client leaves a registration that " +
            "matches nothing and silently grants consent-skip to an id a fork may later re-add");
    }

    [Fact]
    public void Repository_ShouldNotReference_TheRemovedPublicClientId()
    {
        List<string> offendingFiles = SweepSourceFiles()
            .Where(path => File.ReadAllText(path).Contains(RemovedPublicClientId, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(_repoRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        offendingFiles.Should().BeEmpty(
            "every reference to the deleted client has to go with it — a lingering mention in a " +
            "test claim, a compose comment, or the frontend-setup guide is what makes a fork " +
            "re-register it. Still referencing '{0}': {1}",
            RemovedPublicClientId,
            string.Join(", ", offendingFiles));
    }

    // ---- positional overrides that the deletion shifts ------------------------------------

    [Fact]
    public void TestCompose_SeederClientOverrides_ShouldTargetTheSeedJsonIndexOfTheWebBffClient()
    {
        IReadOnlyList<string> seededClientIds = ReadSeededClientIds();
        int webBffIndex = seededClientIds.ToList().IndexOf(WebBffClientId);

        webBffIndex.Should().BeGreaterThanOrEqualTo(
            0,
            "api/seed.json must keep seeding '{0}' — it is the confidential client apps/wallow-web's " +
            "BFF authenticates with",
            WebBffClientId);

        string source = File.ReadAllText(_testComposePath);

        source.Should().Contain(
            $"Clients__{webBffIndex}__RedirectUris__0: \"{TestComposeRedirectUri}\"",
            "the seeder binds SeedOptions.Clients straight from api/seed.json's array, so " +
            "docker-compose.test.yml's Clients__<index>__ overrides address seeded clients by " +
            "POSITION. '{0}' sits at index {1}; an override aimed at any other index silently " +
            "rewrites a different client's redirect URIs and the e2e login fails with no " +
            "configuration error to point at",
            WebBffClientId,
            webBffIndex);
    }

    [Fact]
    public void TestCompose_FirstPartyClientOverrides_ShouldContinueWhereAppSettingsEnds()
    {
        int appSettingsCount = ReadDevFirstPartyClients().Count;

        string source = File.ReadAllText(_testComposePath);

        Regex overrideIndex = new(
            @"Identity__FirstPartyClients__(\d+)\s*:",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        List<int> indexes = overrideIndex.Matches(source)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Order()
            .ToList();

        indexes.Should().NotBeEmpty(
            "docker-compose.test.yml must keep appending its BFF clients to the first-party list, " +
            "or the containerised OIDC flow stops at the consent screen and the e2e suite hangs");

        List<int> expected = Enumerable.Range(appSettingsCount, indexes.Count).ToList();

        indexes.Should().Equal(
            expected,
            "ASP.NET Core array configuration is positional: an override index below {0} silently " +
            "REPLACES an entry appsettings.json already declares, and a gap above it leaves an " +
            "index nothing fills. appsettings.json now lists {0} first-party client(s), so the " +
            "overrides must run {1}. Deleting a client without renumbering these is the exact " +
            "failure this pins",
            appSettingsCount,
            expected.Count == 0 ? "(none)" : string.Join(", ", expected));
    }

    // ---- helpers -------------------------------------------------------------------------

    private static List<string> SweepSourceFiles()
    {
        List<string> files = [];

        foreach (string root in _sweptRoots)
        {
            string rootPath = Path.Combine(_repoRoot, root);
            if (Directory.Exists(rootPath))
            {
                CollectSourceFiles(rootPath, files);
            }
        }

        // This guard spells the deleted id out in its own assertions and doc comment; excluding
        // it keeps the sweep from reporting itself as the offender forever.
        string selfFileName = $"{nameof(PublicSeedClientRemovalTests)}.cs";

        return files
            .Where(path => !string.Equals(Path.GetFileName(path), selfFileName, StringComparison.Ordinal))
            .ToList();
    }

    private static void CollectSourceFiles(string directory, List<string> files)
    {
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            if (_sweptExtensions.Contains(Path.GetExtension(file)))
            {
                files.Add(file);
            }
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            string name = Path.GetFileName(child);
            if (!_prunedDirectories.Contains(name) && !name.StartsWith('.'))
            {
                CollectSourceFiles(child, files);
            }
        }
    }

    private static List<string> ReadDevFirstPartyClients()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_apiAppSettingsPath));

        JsonElement identity = document.RootElement.GetProperty("Identity");
        JsonElement firstPartyClients = identity.GetProperty("FirstPartyClients");

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
        => ReadSeededClients().Select(client => client.ClientId).ToList();

    private static List<SeededClient> ReadSeededClients()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_seedJsonPath));

        JsonElement clients = document.RootElement.GetProperty("clients");

        List<SeededClient> seededClients = [];
        foreach (JsonElement client in clients.EnumerateArray())
        {
            if (!client.TryGetProperty("clientId", out JsonElement clientId))
            {
                continue;
            }

            string? id = clientId.GetString();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string? secret = client.TryGetProperty("secret", out JsonElement secretElement)
                ? secretElement.GetString()
                : null;

            seededClients.Add(new SeededClient(id, secret));
        }

        return seededClients;
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

    private sealed record SeededClient(string ClientId, string? Secret);
}
