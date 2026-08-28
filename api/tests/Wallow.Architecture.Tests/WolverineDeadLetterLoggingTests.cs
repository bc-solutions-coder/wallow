using System.Text.Json;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Wallow does not write its own dead-letter log line, because Wolverine already does:
/// <c>WolverineRuntime</c> logs "Envelope {envelope} was moved to the error queue" at Error
/// (event id 108) with the exception attached, and the envelope rendering names the message
/// type — exactly what bead Wallow-qi90.2 asks for. The only thing this repo owns on that path
/// is the Serilog level override for the "Wolverine" source: raise it past Error in any
/// committed appsettings file and the sole terminal log of a dropped message vanishes, which is
/// how the SendEmailHandler incident stayed invisible. These tests pin every appsettings file
/// that carries the override to a level Error-level events pass through.
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
            // No Serilog section, or one that names no level reaching the Wolverine source —
            // this file inherits from appsettings.json, which the base-file case covers.
            return;
        }

        _errorPassingLevels.Should().Contain(
            effectiveLevel,
            "{0} sets the minimum level governing the \"Wolverine\" log source, and anything " +
            "above Error swallows the only log line a dead-lettered envelope produces",
            fileName);
    }

    /// <summary>
    /// The level governing the "Wolverine" source in one file: its explicit override when
    /// present, otherwise the file's own Default, otherwise null (nothing declared here).
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
