using System.Text.Json;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks that declared Wolverine log thresholds in the listed settings files allow Error events.
/// </summary>
public class WolverineDeadLetterLoggingTests
{
    /// <summary>Serilog levels that let an Error-level event through. Absent: Fatal.</summary>
    private static readonly string[] _errorPassingLevels =
        ["Verbose", "Debug", "Information", "Warning", "Error"];

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.Production.json")]
    [InlineData("appsettings.Staging.json")]
    [InlineData("appsettings.Testing.json")]
    public void SerilogConfig_MustNotSilence_WolverinesDeadLetterErrorLog(string fileName)
    {
        string path = Path.Combine(FindRepoRoot(), "api", "src", "Wallow.Api", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        string? effectiveLevel = ResolveEffectiveWolverineLevel(document.RootElement);

        if (effectiveLevel is null)
        {
            // This file declares no threshold for the check to evaluate.
            return;
        }

        _errorPassingLevels.Should().Contain(
            effectiveLevel,
            "{0} sets the minimum level governing the \"Wolverine\" log source, and anything " +
            "above Error swallows the only log line a dead-lettered envelope produces",
            fileName);
    }

    /// <summary>
    /// Reads this file's Wolverine override, then its default threshold, or null if neither is declared.
    /// </summary>
    private static string? ResolveEffectiveWolverineLevel(JsonElement root)
    {
        if (!root.TryGetProperty("Serilog", out JsonElement serilog)
            || !serilog.TryGetProperty("MinimumLevel", out JsonElement minimumLevel))
        {
            return null;
        }

        if (minimumLevel.TryGetProperty("Override", out JsonElement overrides)
            && overrides.TryGetProperty("Wolverine", out JsonElement wolverineOverride))
        {
            return wolverineOverride.GetString();
        }

        return minimumLevel.TryGetProperty("Default", out JsonElement defaultLevel)
            ? defaultLevel.GetString()
            : null;
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
            "Could not locate the repository root (no pnpm-workspace.yaml found walking up from "
            + Directory.GetCurrentDirectory());
    }
}
