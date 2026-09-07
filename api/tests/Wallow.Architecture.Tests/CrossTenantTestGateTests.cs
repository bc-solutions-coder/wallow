#pragma warning disable CA1024 // Keep callable MemberData factories.

namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks CI gate text and the CrossTenant trait on the explicitly listed isolation suites.
/// </summary>
public class CrossTenantTestGateTests
{
    /// <summary>
    /// Trait declaration expected in the listed test files.
    /// </summary>
    private const string TraitDeclaration = "[Trait(\"Category\", \"CrossTenant\")]";

    private const string GateFilter = "Category=CrossTenant";

    private const string GateJobId = "cross-tenant-tests";

    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _ciWorkflowPath = Path.Combine(
        _repoRoot,
        ".github",
        "workflows",
        "ci.yml");

    /// <summary>
    /// Isolation test files included in the trait check.
    /// </summary>
    private static readonly string[] _crossTenantTestFiles =
    [
        "api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/CrossOrgRoleIsolationTests.cs",
        "api/tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/OrganizationsControllerCrossTenantTests.cs",
        "api/tests/Modules/Storage/Wallow.Storage.Tests/Integration/CompiledQueryTenantFilterTests.cs",
        "api/tests/Wallow.Shared.Infrastructure.Tests/Persistence/TenantAwareDbContextTests.cs",
    ];



    [Fact]
    public void CiWorkflow_ShouldRun_OnPullRequests()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().Contain(
            "pull_request:",
            "R22 asks for a gate on every PR, so the workflow carrying it must be PR-triggered. " +
            "This holds today and is pinned so adding the gate to a workflow that only runs on " +
            "merge does not read as satisfying the requirement");
    }

    [Fact]
    public void CiWorkflow_ShouldDeclare_TheCrossTenantGateJob()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().Contain(
            $"  {GateJobId}:",
            "the cross-tenant suite must run as its own named CI job. Folded into the general " +
            "unit-test job it is invisible: a tenant-isolation regression reads as 'unit tests " +
            "failed', and a fork auditing the platform's controls cannot see the gate exists");
    }

    [Fact]
    public void CrossTenantGateJob_ShouldSelect_TestsByTheCrossTenantTrait()
    {
        string jobBlock = ReadJobBlock(GateJobId);

        jobBlock.Should().Contain(
            GateFilter,
            "the gate must select the cross-tenant suite by trait ('{0}'), not by naming test " +
            "projects. A project list goes stale the moment a module adds its first tenant-scoped " +
            "resource — which is exactly the change that most needs the gate",
            GateFilter);
    }

    [Fact]
    public void CrossTenantGateJob_ShouldNot_BeAdvisory()
    {
        string jobBlock = ReadJobBlock(GateJobId);

        jobBlock.Should().NotContain(
            "continue-on-error: true",
            "OWASP API1:2023 states the control as 'do not deploy changes that make the tests " +
            "fail'. A gate that reports without blocking is not that control");
    }



    [Theory]
    [MemberData(nameof(CrossTenantTestFiles))]
    public void CrossTenantTestClass_ShouldCarry_TheGateTrait(string relativePath)
    {
        string fullPath = Path.Combine(_repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(fullPath).Should().BeTrue(
            "{0} is one of the tenant-isolation suites the gate selects; if it moved, this list " +
            "and the gate moved out of sync",
            relativePath);

        File.ReadAllText(fullPath).Should().Contain(
            TraitDeclaration,
            "{0} asserts tenant isolation, so it must be inside the gate. A gate whose filter " +
            "matches nothing passes, which is the worst possible outcome for a control whose " +
            "whole purpose is to fail",
            relativePath);
    }

    public static IEnumerable<object[]> CrossTenantTestFiles()
    {
        foreach (string path in _crossTenantTestFiles)
        {
            yield return [path];
        }
    }



    /// <summary>
    /// Extracts a job block using the workflow two-space job indentation.
    /// </summary>
    private static string ReadJobBlock(string jobId)
    {
        string[] lines = File.ReadAllLines(_ciWorkflowPath);

        int start = Array.FindIndex(lines, line => string.Equals(line, $"  {jobId}:", StringComparison.Ordinal));
        if (start < 0)
        {
            return string.Empty;
        }

        int end = Array.FindIndex(
            lines,
            start + 1,
            line => line.Length > 2 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ' && line.EndsWith(':'));

        if (end < 0)
        {
            end = lines.Length;
        }

        return string.Join('\n', lines[start..end]);
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
