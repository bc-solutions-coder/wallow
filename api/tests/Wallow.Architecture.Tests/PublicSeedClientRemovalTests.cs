using System.Text.Json;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks removal of the retired public seed client from selected files and alignment of indexed test redirect overrides.
/// </summary>
public class PublicSeedClientRemovalTests
{
    /// <summary>
    /// Retired public client ID excluded from the seed configuration.
    /// </summary>
    private const string RemovedPublicClientId = "wallow-dev-client";

    /// <summary>
    /// Confidential client used by the web BFF.
    /// </summary>
    private const string WebBffClientId = "wallow-web-client";

    /// <summary>
    /// Parameterized redirect override expected in test Compose configuration.
    /// </summary>
    private const string TestComposeRedirectUri = "http://localhost:${E2E_WEB_PORT:-5053}/bff/callback";

    /// <summary>
    /// Directories excluded from the recursive file scan.
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

    /// <summary>
    /// File extensions included in the scan.
    /// </summary>
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

    /// <summary>
    /// Repository subtrees included in the scan.
    /// </summary>
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

    private static readonly string _testComposePath = Path.Combine(
        _repoRoot,
        "docker",
        "docker-compose.test.yml");



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

        // Exclude this assertion file because its fixture contains the retired ID.
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
