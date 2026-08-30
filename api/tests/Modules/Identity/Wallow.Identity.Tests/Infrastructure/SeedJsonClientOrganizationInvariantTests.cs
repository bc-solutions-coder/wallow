using System.Text.Json;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Both shipped seed files must satisfy the client/organization invariant the seeder enforces at
/// boot (first-party => no organization; every other client => exactly one), so a fresh database
/// seeds without the boot-time rejection ever firing on the reference files. The production seed
/// is further pinned to its documented shape: first-party clients only, and no organization,
/// membership, or bootstrap admin, all of which first-run setup creates instead.
/// </summary>
public sealed class SeedJsonClientOrganizationInvariantTests
{
    private const string PlaceholderSecret = "injected-by-ClientSecrets-env-var";

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void DevSeed_SatisfiesTheClientOrganizationInvariant()
    {
        PreRegisteredClientOptions options = LoadClients(DevSeedPath());

        Action validate = options.Validate;

        validate.Should().NotThrow();
    }

    [Fact]
    public void DevSeed_BindsItsThirdPartyClientToAnOrganization()
    {
        PreRegisteredClientOptions options = LoadClients(DevSeedPath());

        options.Clients.Should().Contain(
            c => !c.FirstParty && c.IsBoundToOrganization,
            "the dev seed keeps one organization-bound third-party client so the consent flow stays exercised");
    }

    [Fact]
    public void ProductionSeed_SatisfiesTheClientOrganizationInvariant()
    {
        // Production ships secret-less and receives secrets from ClientSecrets__<clientId>; stand
        // one in so the public/confidential rule does not mask the organization invariant.
        PreRegisteredClientOptions options = LoadClients(ProductionSeedPath(), withPlaceholderSecrets: true);

        Action validate = options.Validate;

        validate.Should().NotThrow();
    }

    [Fact]
    public void ProductionSeed_HoldsOnlyFirstPartyClients()
    {
        PreRegisteredClientOptions options = LoadClients(ProductionSeedPath());

        options.Clients.Should().NotBeEmpty();
        options.Clients.Should().OnlyContain(
            c => c.FirstParty && !c.IsBoundToOrganization && c.SeedMembers.Count == 0 && c.SeedMemberRoles.Count == 0,
            "a third-party client is bound to an organization that only exists after first-run setup");
    }

    [Fact]
    public void ProductionSeed_DeclaresNoOrganizationOrBootstrapAdmin()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ProductionSeedPath()));

        document.RootElement.TryGetProperty("organizations", out _).Should().BeFalse(
            "the first organization comes from first-run setup, never the production seed");
        document.RootElement.TryGetProperty("admin", out _).Should().BeFalse(
            "the bootstrap administrator comes from first-run setup, never the production seed");
    }

    private static PreRegisteredClientOptions LoadClients(string seedPath, bool withPlaceholderSecrets = false)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(seedPath));

        PreRegisteredClientOptions options = new();
        foreach (JsonElement element in document.RootElement.GetProperty("clients").EnumerateArray())
        {
            PreRegisteredClientDefinition client = element.Deserialize<PreRegisteredClientDefinition>(_jsonOptions)
                ?? throw new InvalidOperationException($"{seedPath} holds a null client entry");

            if (withPlaceholderSecrets && !client.IsPublic && string.IsNullOrWhiteSpace(client.Secret))
            {
                client = client with { Secret = PlaceholderSecret };
            }

            options.Clients.Add(client);
        }

        return options;
    }

    private static string DevSeedPath()
    {
        return ExistingFile(Path.Combine(GetSolutionRoot(), "seed.json"));
    }

    private static string ProductionSeedPath()
    {
        return ExistingFile(Path.Combine(GetSolutionRoot(), "..", "docker", "seed.production.json"));
    }

    private static string ExistingFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        File.Exists(fullPath).Should().BeTrue($"seed file should exist at {fullPath}");
        return fullPath;
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
