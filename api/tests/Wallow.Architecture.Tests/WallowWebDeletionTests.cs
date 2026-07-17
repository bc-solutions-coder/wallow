using System.Xml.Linq;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the terminal cutover of the Web side (bead Wallow-ffpq.3.11): the Blazor
/// <c>Wallow.Web</c> project, its two test projects, and every remaining reference to them
/// across the solution, build scripts, CI/deploy workflows, and docs must be gone once the
/// React <c>apps/wallow-web</c> app is the reachable dashboard. Verified by static inspection
/// of the source tree (never the live stack) so the deletion surface is asserted, not the app.
/// The AppHost side of the cutover is covered by <see cref="AppHostWebResourceTests"/>; the
/// dashboard E2E page-object readiness swap by <see cref="E2EWebReadinessSwapTests"/>. This
/// file adds the physical deletion, the solution/script/CI/docs reference removal, and the two
/// cleanup items folded into this bead (the stale bff-surface guard and the two residual
/// Blazor-era readiness calls in the organization E2E flow tests).
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
    private static readonly string _runE2eScriptPath = Path.Combine(_repoRoot, "scripts", "run-e2e.sh");
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

    private static readonly string _organizationFlowTestsPath = Path.Combine(
        _apiRoot,
        "tests",
        "Wallow.E2E.Tests",
        "Flows",
        "OrganizationFlowTests.cs");

    private static readonly string _organizationInvitationFlowTestsPath = Path.Combine(
        _apiRoot,
        "tests",
        "Wallow.E2E.Tests",
        "Flows",
        "OrganizationInvitationFlowTests.cs");

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

    [Fact]
    public void RunE2eScript_ShouldNotReference_BlazorWebProjectPath()
    {
        string source = File.ReadAllText(_runE2eScriptPath);

        source.Should().NotContain(
            "api/src/Wallow.Web",
            "run-e2e.sh must not publish or reference the deleted Blazor Wallow.Web project " +
            "(the dashboard image is now built from apps/wallow-web/Dockerfile)");
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

    // ---- Cleanup folded into this bead: residual Blazor readiness in org E2E flows ------

    [Fact]
    public void OrganizationFlowTests_ShouldNotCall_BlazorReadiness()
    {
        string source = File.ReadAllText(_organizationFlowTestsPath);

        source.Should().NotContain(
            "WaitForBlazorReadyAsync",
            "OrganizationFlowTests navigates the React wallow-web dashboard (Docker.WebBaseUrl), which " +
            "never emits [data-blazor-ready]; its inline readiness waits must swap to the React " +
            "readiness helper (WaitForWebReadyAsync), like the 7 dashboard page objects did in 3.10");
    }

    [Fact]
    public void OrganizationInvitationFlowTests_ShouldNotCall_BlazorReadiness()
    {
        string source = File.ReadAllText(_organizationInvitationFlowTestsPath);

        source.Should().NotContain(
            "WaitForBlazorReadyAsync",
            "OrganizationInvitationFlowTests navigates the React wallow-web dashboard (Docker.WebBaseUrl), " +
            "which never emits [data-blazor-ready]; its inline readiness wait must swap to the React " +
            "readiness helper (WaitForWebReadyAsync), like the 7 dashboard page objects did in 3.10");
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
