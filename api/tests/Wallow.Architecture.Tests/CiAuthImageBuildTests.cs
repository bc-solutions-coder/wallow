namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the wallow-auth CI image-build cutover (bead Wallow-y49v): the CI and deploy
/// workflows must build the auth container from the pnpm/Node Dockerfile
/// (<c>apps/wallow-auth/Dockerfile</c>), NOT by <c>dotnet publish</c> of the Blazor
/// <c>api/src/Wallow.Auth/Wallow.Auth.csproj</c>. Once the Blazor Wallow.Auth project is
/// deleted (Wallow-vec7.5.3, which this bead blocks) the old publish legs 404 and CI breaks.
/// This mirrors the already-landed wallow-web image-build block that sits adjacent to each
/// Wallow.Auth reference in both workflows. Verified by static inspection of the workflow
/// YAML (the same source-inspection pattern as <see cref="WallowWebDeletionTests"/> and
/// <see cref="AppHostAuthResourceTests"/>) because the real acceptance signal is a CI run.
///
/// The two workflows differ in the local image tag they must produce, and each test encodes
/// the file it applies to:
/// <list type="bullet">
/// <item><c>ci.yml</c> must tag the auth image <c>wallow-auth-react:test</c> — the e2e job
/// loads it into the docker-compose.test.yml stack, whose <c>wallow-auth</c> service pins
/// <c>wallow-auth-react:test</c> (docker-compose.test.yml:176), exactly as the wallow-web
/// sibling uses <c>wallow-web-react:test</c>.</item>
/// <item><c>deploy.yml</c> mirrors its own wallow-web sibling, which keeps the bare
/// <c>:test</c> local tag so the <c>APP_IMAGE_MAP</c> push loop key stays stable; the tag
/// naming there is therefore not asserted, only the build-source cutover.</item>
/// </list>
/// </summary>
public class CiAuthImageBuildTests
{
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _ciWorkflowPath = Path.Combine(
        _repoRoot,
        ".github",
        "workflows",
        "ci.yml");

    private static readonly string _deployWorkflowPath = Path.Combine(
        _repoRoot,
        ".github",
        "workflows",
        "deploy.yml");

    private const string BlazorAuthPublishTarget =
        "dotnet publish api/src/Wallow.Auth/Wallow.Auth.csproj";

    private const string AuthDockerfileBuild =
        "docker build -f apps/wallow-auth/Dockerfile";

    private const string BlazorAuthCssCleanup =
        "rm -f api/src/Wallow.Auth/wwwroot/css/app.css";

    // ---- ci.yml -----------------------------------------------------------------------

    [Fact]
    public void CiWorkflow_ShouldNotPublish_BlazorAuthContainer()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().NotContain(
            BlazorAuthPublishTarget,
            "ci.yml must not build the auth image via 'dotnet publish' of the Blazor " +
            "Wallow.Auth project (both the amd64 and arm64 legs); once api/src/Wallow.Auth " +
            "is deleted that path 404s. The auth image now comes from apps/wallow-auth/Dockerfile");
    }

    [Fact]
    public void CiWorkflow_ShouldBuild_AuthImageFromPnpmDockerfile()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().Contain(
            AuthDockerfileBuild,
            "ci.yml must build the wallow-auth image from apps/wallow-auth/Dockerfile " +
            "(repo-root build context), mirroring the adjacent wallow-web block " +
            "'docker build -f apps/wallow-web/Dockerfile -t wallow-web-react:test .'");
    }

    [Fact]
    public void CiWorkflow_ShouldTag_AuthImageWithReactTestTag()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().Contain(
            "wallow-auth-react:test",
            "ci.yml's e2e job loads the auth image into the docker-compose.test.yml stack, " +
            "whose wallow-auth service pins 'wallow-auth-react:test' (docker-compose.test.yml:176); " +
            "the built/saved/loaded tag must match, as the wallow-web sibling does with wallow-web-react:test");
    }

    [Fact]
    public void CiWorkflow_ShouldNotTag_LegacyBlazorAuthTestImage()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().NotContain(
            "wallow-auth:test",
            "the legacy Blazor auth image tag 'wallow-auth:test' must be renamed to " +
            "'wallow-auth-react:test' throughout ci.yml (build + docker save list) so it matches the " +
            "already-cutover docker-compose.test.yml tag; the bare tag no longer has a producer");
    }

    [Fact]
    public void CiWorkflow_ShouldNotReference_BlazorAuthCssCleanup()
    {
        string source = File.ReadAllText(_ciWorkflowPath);

        source.Should().NotContain(
            BlazorAuthCssCleanup,
            "the pre-publish Tailwind cleanup step 'rm -f api/src/Wallow.Auth/wwwroot/css/app.css' " +
            "only applied to the Blazor publish path; with the pnpm Dockerfile build it is dead and must be removed");
    }

    // ---- deploy.yml -------------------------------------------------------------------

    [Fact]
    public void DeployWorkflow_ShouldNotPublish_BlazorAuthContainer()
    {
        string source = File.ReadAllText(_deployWorkflowPath);

        source.Should().NotContain(
            BlazorAuthPublishTarget,
            "deploy.yml must not build the auth image via 'dotnet publish' of the Blazor " +
            "Wallow.Auth project (both legs); the auth image now comes from apps/wallow-auth/Dockerfile, " +
            "mirroring the adjacent wallow-web build");
    }

    [Fact]
    public void DeployWorkflow_ShouldBuild_AuthImageFromPnpmDockerfile()
    {
        string source = File.ReadAllText(_deployWorkflowPath);

        source.Should().Contain(
            AuthDockerfileBuild,
            "deploy.yml must build the wallow-auth image from apps/wallow-auth/Dockerfile " +
            "(repo-root build context), mirroring the adjacent wallow-web block " +
            "'docker build -f apps/wallow-web/Dockerfile -t wallow-web:test .'");
    }

    [Fact]
    public void DeployWorkflow_ShouldNotReference_BlazorAuthCssCleanup()
    {
        string source = File.ReadAllText(_deployWorkflowPath);

        source.Should().NotContain(
            BlazorAuthCssCleanup,
            "the pre-publish Tailwind cleanup step 'rm -f api/src/Wallow.Auth/wwwroot/css/app.css' " +
            "only applied to the Blazor publish path; with the pnpm Dockerfile build it is dead and must be removed");
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
