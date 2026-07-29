using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Deny-by-default function-level authorization (bead Wallow-pu6a.6.5, closing finding F13 and
/// guardrail R23 of the SDK review — one finding, not two: "No FallbackPolicy — a new controller
/// without [Authorize] is silently anonymous").
///
/// <para>Two independent layers have to hold, because either one alone fails open the moment
/// someone touches the other:</para>
/// <list type="number">
/// <item>The composition root must declare a fallback policy, so an endpoint that carries no
/// authorization metadata is denied rather than served. Today the deny-anonymous behaviour exists
/// only because <c>PermissionAuthorizationPolicyProvider.GetFallbackPolicyAsync</c> hardcodes it;
/// nothing at the registration site says so, and <c>AuthorizationOptions.FallbackPolicy</c> is
/// never set. A fork that swaps the custom policy provider — the documented way to add a
/// requirement — silently loses deny-by-default with no test failing.</item>
/// <item>Every controller action must still declare its own intent, so the fallback is a safety
/// net rather than the mechanism. An action relying on the fallback reads as "nobody decided",
/// which is indistinguishable from the bug.</item>
/// </list>
///
/// <para>The first assertion inspects source text rather than resolved options: the registration
/// lives in a private method inside a composition root that pulls in EF, Redis, and OpenIddict
/// certificates, so building it in a unit test costs more than it proves. The behavioural half —
/// that the policy provider returns the configured fallback — is a real test, in
/// <c>Wallow.Identity.Tests.Infrastructure.FallbackAuthorizationPolicyTests</c>. Source-text
/// inspection for wiring whose only true signal is a running host follows the pattern already set
/// by <see cref="CiAuthImageBuildTests"/> and <see cref="PublicSeedClientRemovalTests"/>.</para>
/// </summary>
public class DenyByDefaultAuthorizationTests
{
    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _identityAuthorizationSourcePath = Path.Combine(
        _repoRoot,
        "api",
        "src",
        "Modules",
        "Identity",
        "Wallow.Identity.Infrastructure",
        "Extensions",
        "IdentityInfrastructureExtensions.cs");

    [Fact]
    public void IdentityAuthorization_ShouldDeclare_AFallbackPolicy()
    {
        string source = File.ReadAllText(_identityAuthorizationSourcePath);

        source.Should().Contain(
            "options.FallbackPolicy",
            "AddIdentityAuthorization's AddAuthorization callback must set " +
            "AuthorizationOptions.FallbackPolicy to a policy requiring an authenticated user. " +
            "Deny-by-default currently survives only as a hardcoded return inside " +
            "PermissionAuthorizationPolicyProvider — invisible where authorization is configured, " +
            "and gone the moment a fork replaces that provider");
    }

    [Fact]
    public void EveryControllerAction_ShouldDeclare_ItsAuthorizationIntent()
    {
        List<string> undeclaredActions = [];

        foreach (string moduleName in TestConstants.AllModules)
        {
            Assembly apiAssembly = Assembly.Load($"Wallow.{moduleName}.Api");

            foreach (Type controller in apiAssembly.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
            {
                if (DeclaresAuthorizationIntent(controller))
                {
                    continue;
                }

                foreach (MethodInfo action in GetActionMethods(controller).Where(action => !DeclaresAuthorizationIntent(action)))
                {
                    undeclaredActions.Add($"{moduleName}: {controller.Name}.{action.Name}");
                }
            }
        }

        undeclaredActions.Should().BeEmpty(
            "an action with neither an [Authorize]-family attribute (including [HasPermission]) " +
            "nor [AllowAnonymous] on itself or its controller has had no authorization decision " +
            "made about it. The fallback policy keeps it from being anonymous, but 'nobody " +
            "decided' is the state F13 reports and it must not be reachable by writing an action " +
            "and forgetting. Undeclared today: {0}",
            string.Join(", ", undeclaredActions.Order(StringComparer.Ordinal)));
    }

    private static bool DeclaresAuthorizationIntent(MemberInfo member)
        => member.GetCustomAttributes(inherit: true).Any(attribute => attribute is IAuthorizeData or IAllowAnonymous);

    private static IEnumerable<MethodInfo> GetActionMethods(Type controller)
        => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName
                && method.DeclaringType == controller
                && method.GetCustomAttribute<NonActionAttribute>() is null);

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
