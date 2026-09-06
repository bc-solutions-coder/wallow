using System.Text.RegularExpressions;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Source-level guard for the single problem writer. Every error body the API emits must pass
/// through <c>Wallow.Shared.Api.Problems</c> (the customizer adds <c>traceId</c>, fills
/// <c>code</c>, normalises validation keys, and forces the generic 5xx detail), so product code
/// must not build problem objects by hand, hand-roll a JSON error body, or call the bare
/// <c>Problem(statusCode: ...)</c> family. The allow-list names the writer path itself and the
/// two places that must build a <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> to hand to
/// the problem-details service because no controller is in scope.
/// </summary>
public sealed partial class ProblemWriterGuardTests
{
    private static readonly string _repoRoot = FindRepoRoot();
    private static readonly string _apiSourceRoot = Path.Combine(_repoRoot, "api", "src");

    /// <summary>Files allowed to construct problem objects or write bodies directly, relative to api/src.</summary>
    private static readonly HashSet<string> _writerPath = new(StringComparer.Ordinal)
    {
        "Shared/Wallow.Shared.Api/Problems/ProblemResult.cs",
        "Shared/Wallow.Shared.Api/Problems/ProblemDetailsServiceExtensions.cs",
        "Shared/Wallow.Shared.Api/Problems/WallowProblemDetailsWriter.cs",
        "Modules/Identity/Wallow.Identity.Infrastructure/Authorization/AuthProblemResponse.cs",
        "Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs",
    };

    /// <summary>Files allowed to write a non-error JSON or text body directly (health, SSE).</summary>
    private static readonly HashSet<string> _nonErrorBodies = new(StringComparer.Ordinal)
    {
        "Wallow.Api/Program.cs",
        "Wallow.Api/Endpoints/SseEndpoint.cs",
    };

    [Fact]
    public void ProblemObjects_AreConstructedOnlyOnTheWriterPath()
    {
        List<string> offenders = FindMatches(HandBuiltProblem(), _writerPath);

        offenders.Should().BeEmpty(
            "problem objects are built by ProblemResult, the IProblemDetailsService helpers, or the two " +
            "middleware writers on the allow-list; controllers return this.Problem(entry), " +
            "ValidationProblem(ModelState), or result.ToActionResult()");
    }

    [Fact]
    public void BareProblemCalls_AreNotUsed()
    {
        List<string> offenders = FindMatches(BareProblemCall(), []);

        offenders.Should().BeEmpty(
            "Problem(statusCode:/title:/detail:/type:/instance:) bypasses the catalog; " +
            "use this.Problem(ErrorCatalogEntry, detail?) so the body carries a catalogued code");
    }

    [Fact]
    public void ResponseBodies_AreNotWrittenDirectly()
    {
        List<string> offenders = FindMatches(DirectBodyWrite(), [.. _writerPath, .. _nonErrorBodies]);

        offenders.Should().BeEmpty(
            "error bodies are written by IProblemDetailsService.TryWriteAsync (via TryWriteProblemAsync); " +
            "a direct WriteAsJsonAsync/WriteAsync would skip the customizer and the traceId");
    }

    [Fact]
    public void TheAllowList_NamesOnlyFilesThatExist()
    {
        foreach (string relative in _writerPath.Concat(_nonErrorBodies))
        {
            File.Exists(Path.Combine(_apiSourceRoot, relative)).Should().BeTrue(
                "the allow-list entry {0} must track its file; remove the entry when the file goes", relative);
        }
    }

    private static List<string> FindMatches(Regex pattern, IEnumerable<string> allowed)
    {
        HashSet<string> allowList = allowed.ToHashSet(StringComparer.Ordinal);
        List<string> offenders = [];

        foreach (string file in EnumerateCSharpSources(_apiSourceRoot))
        {
            string relative = Path.GetRelativePath(_apiSourceRoot, file).Replace('\\', '/');
            if (allowList.Contains(relative))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                if (pattern.IsMatch(lines[index]))
                {
                    offenders.Add($"{relative}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        return offenders;
    }

    private static IEnumerable<string> EnumerateCSharpSources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "pnpm-workspace.yaml")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Could not locate the repository root (pnpm-workspace.yaml).");
    }

    // `new ProblemDetails` however the initializer follows (same line or the next), the
    // `Http`/`Validation` variants, and the target-typed `ProblemDetails x = new`. The trailing
    // word boundary keeps `new ProblemDetailsContext` out.
    [GeneratedRegex(@"\bnew\s+(Http|Validation)?ProblemDetails\b|\b(Http|Validation)?ProblemDetails\s+\w+\s*=\s*new\b")]
    private static partial Regex HandBuiltProblem();

    [GeneratedRegex(@"\bProblem\(\s*(statusCode|title|detail|type|instance)\s*:")]
    private static partial Regex BareProblemCall();

    [GeneratedRegex(@"\.WriteAsJsonAsync\(|\bResponse\.WriteAsync\(")]
    private static partial Regex DirectBodyWrite();
}
