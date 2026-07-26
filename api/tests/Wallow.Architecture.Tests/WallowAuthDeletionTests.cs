using System.Xml.Linq;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the terminal cutover of the Auth side (bead Wallow-vec7.5.3): the Blazor
/// <c>Wallow.Auth</c> project, its two test projects, the whole .NET Playwright E2E suite
/// (<c>Wallow.E2E.Tests</c>), and the <c>scripts/run-e2e.sh</c> driver must be gone once the
/// React <c>apps/wallow-auth</c> app is the reachable auth UI (per-app <c>@playwright/test</c>
/// suites replace the .NET E2E suite). Verified by static inspection of the source tree
/// (never the live stack), mirroring <see cref="WallowWebDeletionTests"/>. The AppHost side
/// is covered by <see cref="AppHostAuthResourceTests"/>; the CI/deploy image-build cutover by
/// <see cref="CiAuthImageBuildTests"/>.
/// </summary>
public class WallowAuthDeletionTests
{
    private static readonly string _apiRoot = FindApiRoot();
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _blazorAuthProjectDir = Path.Combine(_apiRoot, "src", "Wallow.Auth");
    private static readonly string _blazorAuthTestsDir = Path.Combine(_apiRoot, "tests", "Wallow.Auth.Tests");
    private static readonly string _blazorAuthComponentTestsDir = Path.Combine(
        _apiRoot,
        "tests",
        "Wallow.Auth.Component.Tests");
    private static readonly string _dotnetE2eTestsDir = Path.Combine(_apiRoot, "tests", "Wallow.E2E.Tests");

    private static readonly string _solutionPath = Path.Combine(_apiRoot, "Wallow.slnx");
    private static readonly string _runTestsScriptPath = Path.Combine(_repoRoot, "scripts", "run-tests.sh");
    private static readonly string _runE2eScriptPath = Path.Combine(_repoRoot, "scripts", "run-e2e.sh");
    private static readonly string _ciWorkflowPath = Path.Combine(_repoRoot, ".github", "workflows", "ci.yml");

    // ---- Physical deletion of the Blazor auth projects and the .NET E2E suite ----------

    [Fact]
    public void BlazorAuthProject_DirectoryShouldNotExist()
    {
        Directory.Exists(_blazorAuthProjectDir).Should().BeFalse(
            "api/src/Wallow.Auth (the Blazor auth app) must be deleted now that apps/wallow-auth " +
            "is the reachable React auth UI");
    }

    [Fact]
    public void BlazorAuthTestsProject_DirectoryShouldNotExist()
    {
        Directory.Exists(_blazorAuthTestsDir).Should().BeFalse(
            "api/tests/Wallow.Auth.Tests must be deleted with the Blazor Wallow.Auth project it covers");
    }

    [Fact]
    public void BlazorAuthComponentTestsProject_DirectoryShouldNotExist()
    {
        Directory.Exists(_blazorAuthComponentTestsDir).Should().BeFalse(
            "api/tests/Wallow.Auth.Component.Tests must be deleted with the Blazor Wallow.Auth " +
            "project it covers");
    }

    [Fact]
    public void DotnetE2eTestsProject_DirectoryShouldNotExist()
    {
        Directory.Exists(_dotnetE2eTestsDir).Should().BeFalse(
            "api/tests/Wallow.E2E.Tests (the .NET Playwright suite) must be deleted; per-app " +
            "@playwright/test suites replace it");
    }

    [Fact]
    public void RunE2eScript_ShouldNotExist()
    {
        File.Exists(_runE2eScriptPath).Should().BeFalse(
            "scripts/run-e2e.sh drove the deleted Wallow.E2E.Tests suite and must be deleted with it");
    }

    // ---- Solution file (Wallow.slnx) ---------------------------------------------------

    [Fact]
    public void Solution_ShouldNotReference_BlazorAuthOrE2eProjects()
    {
        List<string> projectPaths = XDocument.Load(_solutionPath)
            .Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .Where(path => path is not null)
            .Select(path => path!.Replace('\\', '/'))
            .ToList();

        projectPaths.Should().NotContain(
            "src/Wallow.Auth/Wallow.Auth.csproj",
            "Wallow.slnx must drop the Blazor Wallow.Auth project once it is deleted");
        projectPaths.Should().NotContain(
            "tests/Wallow.Auth.Tests/Wallow.Auth.Tests.csproj",
            "Wallow.slnx must drop Wallow.Auth.Tests once it is deleted");
        projectPaths.Should().NotContain(
            "tests/Wallow.Auth.Component.Tests/Wallow.Auth.Component.Tests.csproj",
            "Wallow.slnx must drop Wallow.Auth.Component.Tests once it is deleted");
        projectPaths.Should().NotContain(
            "tests/Wallow.E2E.Tests/Wallow.E2E.Tests.csproj",
            "Wallow.slnx must drop Wallow.E2E.Tests once it is deleted");
    }

    // ---- Build / test scripts ----------------------------------------------------------

    [Fact]
    public void RunTestsScript_ShouldNotDefine_BlazorAuthOrE2eShorthands()
    {
        string source = File.ReadAllText(_runTestsScriptPath);

        source.Should().NotContain(
            "Wallow.Auth.Tests",
            "run-tests.sh's 'auth' shorthand points at the deleted Wallow.Auth.Tests project " +
            "and must be removed");
        source.Should().NotContain(
            "Wallow.Auth.Component.Tests",
            "run-tests.sh's 'auth-components' shorthand points at the deleted " +
            "Wallow.Auth.Component.Tests project and must be removed");
        source.Should().NotContain(
            "run-e2e.sh",
            "run-tests.sh must not redirect to the deleted run-e2e.sh script");
    }

    // ---- CI workflow -------------------------------------------------------------------

    [Fact]
    public void CiWorkflow_ShouldNotReference_DotnetE2eSuite()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().NotContain(
            "Wallow.E2E.Tests",
            "ci.yml must not build or run the deleted .NET Wallow.E2E.Tests suite");
        source.Should().NotContain(
            "run-e2e.sh",
            "ci.yml must not point at the deleted run-e2e.sh script");
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
