using System.Reflection;

#pragma warning disable CA1024 // MemberData source methods cannot be properties

namespace Wallow.Architecture.Tests;

/// <summary>
/// Fails a non-inlinable Wolverine handler dependency at the offending type, in the default test
/// run, without Docker.
///
/// <para>Wolverine generates a handler adapter per message type and inlines the constructor calls
/// for that handler's dependencies. When it cannot inline one it falls back to a service locator,
/// and <c>Program.cs</c> sets <c>ServiceLocationPolicy.NotAllowed</c>, which turns that fallback
/// into an <c>InvalidServiceLocationException</c>. Codegen is lazy — it runs on the first message
/// of that type — so the exception surfaces inside a background envelope, three retries later, in
/// the dead-letter queue, behind an HTTP 200. That is how one constructor parameter on
/// SendEmailHandler killed every transactional email in the product and was found by four browser
/// specs rather than by this suite.</para>
///
/// <para><c>Wallow.Api.Tests.Integration.HandlerCodegenTests</c> is the precise check: it compiles
/// every discovered handler against the real container and reports exactly what the codegen could
/// not construct. It needs Testcontainers, so it is <c>Category=Integration</c> and runs in CI's
/// integration job, not in <c>./scripts/run-tests.sh</c>. These tests are the coarse half that
/// does run there — static, so they judge visibility rather than the resolved container, and they
/// name the offending type instead of the handler that failed to compile.</para>
/// </summary>
public class WolverineCodegenPolicyTests
{
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _apiProgramPath = Path.Combine(
        _repoRoot, "api", "src", "Wallow.Api", "Program.cs");

    /// <summary>
    /// The interfaces <c>Program.cs</c> exempts from the policy, and the entire justification for
    /// each: the registration is an opaque lambda factory or a framework factory the codegen
    /// cannot see through. The list grows one entry at a time in reaction to a production-shaped
    /// failure, so it is restated here — an addition has to be a deliberate edit in two places
    /// rather than a quiet third line in a config block.
    /// </summary>
    private static readonly string[] _expectedServiceLocationExemptions =
    [
        "IBootstrapAdminService",
        "IOrganizationService",
        "IServiceAccountService",
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
    /// A non-public implementation is the first half of the defect class: Wolverine cannot emit
    /// <c>new Foo(...)</c> for a type the generated assembly cannot see. The rule is deliberately
    /// coarser than the real failure — it judges every handler-reachable interface implementation,
    /// not just the ones a handler happens to depend on today — because the alternative is a rule
    /// that goes green until someone adds a constructor parameter, which is exactly the moment it
    /// is needed. The second half (opaque lambda registrations) is invisible to a static rule and
    /// is covered by HandlerCodegenTests.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void InfrastructureImplementations_OfApplicationInterfaces_ShouldBePublic(string moduleName)
    {
        Assembly applicationAssembly = Assembly.Load($"Wallow.{moduleName}.Application");
        Assembly infrastructureAssembly = Assembly.Load($"Wallow.{moduleName}.Infrastructure");

        // Shared.Contracts counts too: it is the one assembly modules reference across boundaries,
        // so an interface declared there is reachable from a handler in any of the seven.
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

        // BeEmpty reports only the first item, and these arrive in batches — name them all.
        violations.Should().BeEmpty(
            "Wolverine's generated handler code constructs its dependencies inline, so a handler " +
            "taking one of these interfaces fails codegen with InvalidServiceLocationException on " +
            $"the first message rather than at startup:\n- {string.Join("\n- ", violations)}\n");
    }

    /// <summary>
    /// Every exemption widens the policy for one interface across every handler, so the list is
    /// asserted rather than reviewed. Reads the source because the options are configured inside
    /// the API composition root, which needs EF, Redis and signing certificates to build — the
    /// same trade-off <see cref="AccessTokenAudienceTests"/> makes.
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
    /// ASP.NET's authorization handlers match Wolverine's discovery convention exactly — the class
    /// ends in "Handler" and <c>AuthorizationHandler&lt;T&gt;</c> exposes a public
    /// <c>HandleAsync(AuthorizationHandlerContext)</c> — so without this exclusion Wolverine builds
    /// a message chain for <c>AuthorizationHandlerContext</c> whose SignInManager and UserManager
    /// dependencies cannot be inlined. A type nothing ever sends then fails the codegen policy.
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
