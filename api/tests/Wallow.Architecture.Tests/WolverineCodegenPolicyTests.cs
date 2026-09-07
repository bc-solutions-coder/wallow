using System.Reflection;

#pragma warning disable CA1024 // Keep callable MemberData factories.

namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks dependency visibility and selected Wolverine registration text in the fast test tier.
/// HandlerCodegenTests exercises generation against the integration host.
/// </summary>
public class WolverineCodegenPolicyTests
{
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _apiProgramPath = Path.Combine(
        _repoRoot, "api", "src", "Wallow.Api", "Program.cs");

    /// <summary>
    /// Expected service-location exemptions, requiring an explicit test update when the host list changes.
    /// </summary>
    private static readonly string[] _expectedServiceLocationExemptions =
    [
        "IBootstrapAdminService",
        "IOpenIddictApplicationManager",
        "IOrganizationService",
        "ISetupStatusChecker",
        "ITenantContext",
        "ITenantContextSetter",
    ];

    public static IEnumerable<object[]> GetModuleNames()
    {
        foreach (string moduleName in TestConstants.AllModules)
        {
            yield return [moduleName];
        }
    }

    /// <summary>
    /// Checks public visibility of nonnested infrastructure implementations of application and shared interfaces.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void InfrastructureImplementations_OfApplicationInterfaces_ShouldBePublic(string moduleName)
    {
        Assembly applicationAssembly = Assembly.Load($"Wallow.{moduleName}.Application");
        Assembly infrastructureAssembly = Assembly.Load($"Wallow.{moduleName}.Infrastructure");

        // Shared interfaces can also be dependencies of module handlers.
        Assembly contractsAssembly = Assembly.Load("Wallow.Shared.Contracts");

        HashSet<Type> handlerReachableInterfaces =
        [
            .. applicationAssembly.GetTypes().Where(t => t.IsInterface),
            .. contractsAssembly.GetTypes().Where(t => t.IsInterface),
        ];

        List<string> violations =
        [
            .. infrastructureAssembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false })
                .Where(t => !t.IsPublic)
                .Where(t => t.GetInterfaces().Any(handlerReachableInterfaces.Contains))
                .Select(t => t.FullName!)
                .Order()
        ];

        // Include every violating type in the assertion message.
        violations.Should().BeEmpty(
            "Wolverine's generated handler code constructs its dependencies inline, so a handler " +
            "taking one of these interfaces fails codegen with InvalidServiceLocationException on " +
            $"the first message rather than at startup:\n- {string.Join("\n- ", violations)}\n");
    }

    /// <summary>
    /// Checks the service-location exemption list in the API composition source.
    /// </summary>
    [Fact]
    public void ServiceLocationExemptions_ShouldMatch_TheExpectedList()
    {
        string source = File.ReadAllText(_apiProgramPath);

        List<string> declared =
        [
            .. source
                .Split("AlwaysUseServiceLocationFor<", StringSplitOptions.None)
                .Skip(1)
                .Select(fragment => fragment[..fragment.IndexOf('>', StringComparison.Ordinal)])
                .Order()
        ];

        declared.Should().Equal(
            _expectedServiceLocationExemptions,
            "an interface exempted from ServiceLocationPolicy.NotAllowed stops being checked for " +
            "every handler that takes it, so the list is a deliberate decision rather than a " +
            "config detail");
    }

    /// <summary>
    /// Checks exclusion of ASP.NET authorization handlers from message discovery.
    /// </summary>
    [Fact]
    public void HandlerDiscovery_ShouldExclude_AspNetAuthorizationHandlers()
    {
        string source = File.ReadAllText(_apiProgramPath);

        source.Should().Contain(
            "Excludes.Implements<IAuthorizationHandler>()",
            "ASP.NET authorization handlers are not Wolverine message handlers");
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
