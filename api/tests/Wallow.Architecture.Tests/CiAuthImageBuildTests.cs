namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks CI workflow text for Dockerfile-based auth image builds and removal of the .NET publish path.
/// </summary>
public class CiAuthImageBuildTests
{
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _ciWorkflowPath = Path.Combine(
        _repoRoot,
        ".github",
        "workflows",
        "ci.yml");

    private const string BlazorAuthPublishTarget =
        "dotnet publish api/src/Wallow.Auth/Wallow.Auth.csproj";

    private const string AuthDockerfileBuild =
        "docker build -f apps/wallow-auth/Dockerfile";

    private const string BlazorAuthCssCleanup =
        "rm -f api/src/Wallow.Auth/wwwroot/css/app.css";



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
