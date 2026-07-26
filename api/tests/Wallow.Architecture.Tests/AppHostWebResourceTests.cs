using System.Xml.Linq;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the AppHost cutover from the Blazor Wallow.Web project resource to a Node.js
/// Aspire resource that runs apps/wallow-web. Mirrors AppHostAuthResourceTests (the wallow-auth
/// equivalent) and verifies by static inspection of the AppHost source, csproj, and
/// Directory.Packages.props, because exercising the real AppHost would attempt to start
/// containers and the whole dev stack.
/// </summary>
public class AppHostWebResourceTests
{
    private static readonly string _apiRoot = FindApiRoot();

    private static readonly string _appHostProgramPath = Path.Combine(
        _apiRoot,
        "src",
        "Wallow.AppHost",
        "Program.cs");

    private static readonly string _appHostCsprojPath = Path.Combine(
        _apiRoot,
        "src",
        "Wallow.AppHost",
        "Wallow.AppHost.csproj");

    private static readonly string _directoryPackagesPropsPath = Path.Combine(
        _apiRoot,
        "Directory.Packages.props");

    private readonly string _programSource;
    private readonly XDocument _csprojDocument;
    private readonly XDocument _propsDocument;

    public AppHostWebResourceTests()
    {
        _programSource = File.ReadAllText(_appHostProgramPath);
        _csprojDocument = XDocument.Load(_appHostCsprojPath);
        _propsDocument = XDocument.Load(_directoryPackagesPropsPath);
    }

    [Fact]
    public void Program_ShouldNotReference_BlazorWebProjectResource()
    {
        _programSource.Should().NotContain(
            "Projects.Wallow_Web",
            "the Blazor Wallow.Web project resource must be replaced by a Node resource so " +
            "deleting api/src/Wallow.Web does not break the AppHost");
    }

    [Fact]
    public void Program_ShouldRegister_NodeResourceForWallowWeb()
    {
        _programSource.Should().Contain(
            "AddJavaScriptApp(\"wallow-web\"",
            "apps/wallow-web must be registered as a JavaScript Aspire resource (AddJavaScriptApp " +
            "from Aspire.Hosting.JavaScript, running its pnpm dev script) in place of the Blazor Web project");
    }

    [Fact]
    public void Program_ShouldNameTheNodeResource_WallowWeb()
    {
        _programSource.Should().Contain(
            "\"wallow-web\"",
            "the Node resource must keep the resource name 'wallow-web' so downstream " +
            "references (WebUrl repoint) keep resolving");
    }

    [Fact]
    public void Program_ShouldPinTheNodeResource_ToDevPort3000()
    {
        _programSource.Should().Contain(
            "3000",
            "apps/wallow-web serves on dev port 3000 (per the port table in the root CLAUDE.md); " +
            "the endpoint must be pinned so WebUrl repointing resolves correctly");
    }

    [Fact]
    public void AppHostCsproj_ShouldNotReference_BlazorWebProject()
    {
        bool referencesBlazorWeb = _csprojDocument
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Any(include => include is not null &&
                include.Replace('\\', '/').Contains("Wallow.Web/Wallow.Web.csproj", StringComparison.OrdinalIgnoreCase));

        referencesBlazorWeb.Should().BeFalse(
            "Wallow.AppHost.csproj must not reference the Blazor Wallow.Web project once the " +
            "Node resource replaces it");
    }

    [Fact]
    public void AppHostCsproj_ShouldReference_AspireHostingJavaScript()
    {
        bool referencesJavaScript = _csprojDocument
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Any(include => string.Equals(include, "Aspire.Hosting.JavaScript", StringComparison.OrdinalIgnoreCase));

        referencesJavaScript.Should().BeTrue(
            "Wallow.AppHost.csproj must reference Aspire.Hosting.JavaScript to expose AddJavaScriptApp");
    }

    [Fact]
    public void DirectoryPackagesProps_ShouldDeclare_AspireHostingJavaScript()
    {
        XElement? entry = _propsDocument
            .Descendants("PackageVersion")
            .FirstOrDefault(element => string.Equals(
                element.Attribute("Include")?.Value,
                "Aspire.Hosting.JavaScript",
                StringComparison.OrdinalIgnoreCase));

        entry.Should().NotBeNull(
            "Directory.Packages.props must declare a PackageVersion for Aspire.Hosting.JavaScript");

        entry!.Attribute("Version")?.Value.Should().Be(
            "13.4.6",
            "Aspire.Hosting.JavaScript must be pinned in lockstep with the other Aspire.Hosting.* " +
            "packages at 13.4.6 so the whole Aspire stack stays on one release lineage");
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
}
