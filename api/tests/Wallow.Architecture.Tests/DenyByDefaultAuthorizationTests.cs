using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks fallback-policy registration text and explicit authorization metadata on controller actions.
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
