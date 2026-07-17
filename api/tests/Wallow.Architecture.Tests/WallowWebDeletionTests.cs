using System.Xml.Linq;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the terminal cutover of the Web side (bead Wallow-ffpq.3.11): the Blazor
/// <c>Wallow.Web</c> project, its two test projects, and every remaining reference to them
/// across the solution, build scripts, CI/deploy workflows, and docs must be gone once the
/// React <c>apps/wallow-web</c> app is the reachable dashboard. Verified by static inspection
/// of the source tree (never the live stack) so the deletion surface is asserted, not the app.
/// The AppHost side of the cutover is covered by <see cref="AppHostWebResourceTests"/>. The
/// auth-side deletion (Blazor Wallow.Auth plus the whole .NET E2E suite) is pinned by
/// <see cref="WallowAuthDeletionTests"/>.
/// </summary>
public class WallowWebDeletionTests
{
    private static readonly string _apiRoot = FindApiRoot();
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _blazorWebProjectDir = Path.Combine(_apiRoot, "src", "Wallow.Web");
    private static readonly string _blazorWebTestsDir = Path.Combine(_apiRoot, "tests", "Wallow.Web.Tests");
    private static readonly string _blazorWebComponentTestsDir = Path.Combine(
        _apiRoot,
        "tests",
        "Wallow.Web.Component.Tests");

    private static readonly string _solutionPath = Path.Combine(_apiRoot, "Wallow.slnx");
    private static readonly string _apiClaudeMdPath = Path.Combine(_apiRoot, "CLAUDE.md");

    private static readonly string _runTestsScriptPath = Path.Combine(_repoRoot, "scripts", "run-tests.sh");
    private static readonly string _ciWorkflowPath = Path.Combine(_repoRoot, ".github", "workflows", "ci.yml");
    private static readonly string _deployWorkflowPath = Path.Combine(
        _repoRoot,
        ".github",
        "workflows",
        "deploy.yml");

    private static readonly string _bffSurfaceTestPath = Path.Combine(
        _repoRoot,
        "apps",
        "wallow-web",
        "src",
        "bff-surface.test.ts");

    // ---- Physical deletion of the Blazor projects --------------------------------------

    [Fact]
    public void BlazorWebProject_DirectoryShouldNotExist()
    {
        Directory.Exists(_blazorWebProjectDir).Should().BeFalse(
            "api/src/Wallow.Web (the Blazor dashboard) must be deleted now that apps/wallow-web " +
            "is the reachable React dashboard");
    }

    [Fact]
    public void BlazorWebTestsProject_DirectoryShouldNotExist()
    {
        Directory.Exists(_blazorWebTestsDir).Should().BeFalse(
            "api/tests/Wallow.Web.Tests must be deleted with the Blazor Wallow.Web project it covers");
    }

    [Fact]
    public void BlazorWebComponentTestsProject_DirectoryShouldNotExist()
    {
        Directory.Exists(_blazorWebComponentTestsDir).Should().BeFalse(
            "api/tests/Wallow.Web.Component.Tests must be deleted with the Blazor Wallow.Web project it covers");
    }

    // ---- Solution file (Wallow.slnx) ---------------------------------------------------

    [Fact]
    public void Solution_ShouldNotReference_BlazorWebProjects()
    {
        List<string> projectPaths = XDocument.Load(_solutionPath)
            .Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .Where(path => path is not null)
            .Select(path => path!.Replace('\\', '/'))
            .ToList();

        projectPaths.Should().NotContain(
            "src/Wallow.Web/Wallow.Web.csproj",
            "Wallow.slnx must drop the Blazor Wallow.Web project once it is deleted");
        projectPaths.Should().NotContain(
            "tests/Wallow.Web.Tests/Wallow.Web.Tests.csproj",
            "Wallow.slnx must drop Wallow.Web.Tests once it is deleted");
        projectPaths.Should().NotContain(
            "tests/Wallow.Web.Component.Tests/Wallow.Web.Component.Tests.csproj",
            "Wallow.slnx must drop Wallow.Web.Component.Tests once it is deleted");
    }

    // ---- Build / test scripts ----------------------------------------------------------

    [Fact]
    public void RunTestsScript_ShouldNotDefine_BlazorWebShorthands()
    {
        string source = File.ReadAllText(_runTestsScriptPath);

        source.Should().NotContain(
            "Wallow.Web.Tests",
            "run-tests.sh's 'web' shorthand points at the deleted Wallow.Web.Tests project and must be removed");
        source.Should().NotContain(
            "Wallow.Web.Component.Tests",
            "run-tests.sh's 'web-components' shorthand points at the deleted Wallow.Web.Component.Tests project " +
            "and must be removed");
    }

    // ---- CI / deploy workflows ---------------------------------------------------------

    [Fact]
    public void CiWorkflow_ShouldNotReference_BlazorWebProjectPath()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().NotContain(
            "api/src/Wallow.Web",
            "ci.yml must not reference the deleted Blazor Wallow.Web project, including the " +
            "'rm -f api/src/Wallow.Web/wwwroot/css/app.css' Tailwind cleanup step that no longer applies");
    }

    [Fact]
    public void DeployWorkflow_ShouldNotReference_BlazorWebProjectPath()
    {
        string source = File.ReadAllText(_deployWorkflowPath);

        source.Should().NotContain(
            "api/src/Wallow.Web",
            "deploy.yml must not reference the deleted Blazor Wallow.Web project, including the " +
            "'rm -f api/src/Wallow.Web/wwwroot/css/app.css' Tailwind cleanup step that no longer applies");
    }

    // ---- Docs (api/CLAUDE.md project table) --------------------------------------------

    [Fact]
    public void ApiClaudeMd_ShouldNotDescribe_BlazorWebProject()
    {
        string source = File.ReadAllText(_apiClaudeMdPath);

        source.Should().NotContain(
            "| `Wallow.Web` |",
            "api/CLAUDE.md's solution-layout table must drop the Blazor Wallow.Web project row");
        source.Should().NotContain(
            "Wallow.Web.Tests",
            "api/CLAUDE.md's host/component tests line must drop the deleted Wallow.Web.Tests reference");
    }

    // ---- Cleanup folded into this bead: stale bff-surface guard -------------------------

    [Fact]
    public void BffSurfaceTest_ShouldNotGuard_OldH3Host()
    {
        // The Wallow-8w1h.2.2 guard pinned the OLD pre-migration h3 BFF host by reading
        // apps/wallow-web/server.ts; task 3.3 moved those routes into src/lib/bff-server.ts,
        // so the guard now reads the wrong file and fails. It must be removed, or rewritten to
        // guard the new host (never referencing ../server.ts as the route source). A deleted
        // file trivially satisfies this.
        if (!File.Exists(_bffSurfaceTestPath))
        {
            return;
        }

        string source = File.ReadAllText(_bffSurfaceTestPath);

        source.Should().NotContain(
            "../server.ts",
            "apps/wallow-web/src/bff-surface.test.ts guards the old h3 host in server.ts, whose BFF " +
            "routes moved to src/lib/bff-server.ts (task 3.3); the stale guard must be removed or " +
            "rewritten to read the new host so `pnpm test` is green");
    }

    private static string FindApiRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Directory.Packages.props")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find the api root containing Directory.Packages.props");
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
