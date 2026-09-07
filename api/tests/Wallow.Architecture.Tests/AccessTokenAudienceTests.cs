namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks audience-validation registration text; token issuance is covered by TokenControllerAudienceTests.
/// </summary>
public class AccessTokenAudienceTests
{
    /// <summary>
    /// Expected API audience, independent of the production registration source.
    /// </summary>
    private const string ApiAudience = "wallow-api";

    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _identityInfrastructureSourcePath = Path.Combine(
        _repoRoot,
        "api",
        "src",
        "Modules",
        "Identity",
        "Wallow.Identity.Infrastructure",
        "Extensions",
        "IdentityInfrastructureExtensions.cs");

    private static readonly string _identitySourceRoot = Path.Combine(
        _repoRoot,
        "api",
        "src",
        "Modules",
        "Identity");

    [Fact]
    public void ValidationHandler_ShouldRequire_AnAudience()
    {
        string source = File.ReadAllText(_identityInfrastructureSourcePath);

        source.Should().Contain(
            "AddAudiences(",
            "the OpenIddict validation handler accepts any audience unless one is registered, so " +
            "AddValidation must call AddAudiences. Without it the issuance side stamping an " +
            "audience changes nothing — the API keeps honouring tokens minted for anything else");
    }

    [Fact]
    public void Identity_ShouldName_TheApiAudience()
    {
        List<string> filesNamingTheAudience = EnumerateCSharpSources(_identitySourceRoot)
            .Where(path => File.ReadAllText(path).Contains(ApiAudience, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(_repoRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        filesNamingTheAudience.Should().NotBeEmpty(
            "'{0}' is the audience the issuance side stamps on every access token and the " +
            "validation side must require. Nothing in the Identity module names it today, which " +
            "is the whole of finding R24",
            ApiAudience);

        filesNamingTheAudience.Should().HaveCountGreaterThanOrEqualTo(
            2,
            "the audience has to appear on both sides of the contract — where the token is issued " +
            "and where it is validated. A single mention means one of the two halves is missing " +
            "and tokens are still minted or accepted unrestricted. Named in: {0}",
            string.Join(", ", filesNamingTheAudience));
    }

    private static IEnumerable<string> EnumerateCSharpSources(string root)
        => Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

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
