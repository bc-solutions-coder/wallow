using System.Text.Json;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Verifies that the repo-root seed.json documents production-shaped example client
/// entries (confidential client, real-domain redirect URIs matching the reference
/// deployment) alongside the localhost entries, without altering actual dev seeding.
/// The examples live under a JSON-legal, underscore-prefixed documentation key so the
/// file stays standard JSON (no // comments) and SeedOptions binding ignores them.
/// </summary>
public sealed class SeedJsonProductionExampleTests
{
    private const string ExampleKey = "_productionExampleClients";
    private const string ExampleClientId = "bcordes-web-client";
    private const string ExampleRedirectUri = "https://bcordes.dev/bff/callback";
    private const string ExamplePostLogoutRedirectUri = "https://bcordes.dev/";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string SeedPath()
    {
        string seedPath = Path.Combine(GetSolutionRoot(), "seed.json");
        File.Exists(seedPath).Should().BeTrue($"seed.json should exist at repo root ({seedPath})");
        return seedPath;
    }

    [Fact]
    public void SeedJson_RemainsStandardJson_WithoutCommentSupport()
    {
        // Guards against the production examples being added as // line comments:
        // the config provider tolerates comments, but the existing SeedJsonBffClientTests
        // (and this test) parse with bare JsonDocument.Parse, which disallows them.
        Action parse = () => JsonDocument.Parse(File.ReadAllText(SeedPath()));

        parse.Should().NotThrow("seed.json must stay standard JSON so JsonDocument.Parse succeeds");
    }

    [Fact]
    public void SeedJson_DeclaresProductionExampleClients()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));

        document.RootElement.TryGetProperty(ExampleKey, out JsonElement examples)
            .Should().BeTrue($"seed.json should document production-shaped examples under '{ExampleKey}'");
        examples.ValueKind.Should().Be(JsonValueKind.Array);
        examples.GetArrayLength().Should().BeGreaterThan(0, "at least one production-shaped example is expected");
    }

    [Fact]
    public void ProductionExample_IsConfidential_WithRealDomainRedirects()
    {
        PreRegisteredClientDefinition example = GetProductionExampleClient();

        example.Secret.Should().NotBeNullOrEmpty("the production example must be a confidential client");
        example.IsPublic.Should().BeFalse("a confidential client is not public");
        example.RedirectUris.Should().Contain(ExampleRedirectUri);
        example.PostLogoutRedirectUris.Should().Contain(ExamplePostLogoutRedirectUri);
    }

    [Fact]
    public void ProductionExample_IsNotAnActiveSeedClient()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        JsonElement activeClients = document.RootElement.GetProperty("clients");

        List<PreRegisteredClientDefinition> active = [];
        foreach (JsonElement client in activeClients.EnumerateArray())
        {
            PreRegisteredClientDefinition? definition =
                client.Deserialize<PreRegisteredClientDefinition>(_jsonOptions);
            if (definition is not null)
            {
                active.Add(definition);
            }
        }

        active.Select(c => c.ClientId).Should()
            .NotContain(ExampleClientId, "the example must not become an active seed client");
        active.SelectMany(c => c.RedirectUris).Should()
            .OnlyContain(uri => uri.Contains("localhost", StringComparison.Ordinal),
                "active dev seeding must keep localhost-only redirect URIs");
    }

    private static PreRegisteredClientDefinition GetProductionExampleClient()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        JsonElement examples = document.RootElement.GetProperty(ExampleKey);

        foreach (JsonElement element in examples.EnumerateArray())
        {
            PreRegisteredClientDefinition? definition =
                element.Deserialize<PreRegisteredClientDefinition>(_jsonOptions);
            if (definition is not null && definition.ClientId == ExampleClientId)
            {
                return definition;
            }
        }

        throw new InvalidOperationException(
            $"seed.json '{ExampleKey}' must contain a '{ExampleClientId}' example client");
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
