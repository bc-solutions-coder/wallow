using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

#pragma warning disable CA1024 // MemberData source methods cannot be properties

namespace Wallow.Architecture.Tests;

public class ApiVersioningTests
{
    public static IEnumerable<object[]> GetModuleNames()
    {
        foreach (string moduleName in TestConstants.AllModules)
        {
            yield return [moduleName];
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void Controllers_ShouldHaveApiVersionAttribute(string moduleName)
    {
        Assembly apiAssembly = Assembly.Load($"Wallow.{moduleName}.Api");

        IEnumerable<Type> controllers = Types.InAssembly(apiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .GetTypes();

        // OpenIddict OIDC controllers use standard OAuth2/OIDC routes, not API versioning
        string[] oidcControllers = ["AuthorizationController", "TokenController", "LogoutController", "UserinfoController"];

        foreach (Type controller in controllers)
        {
            if (oidcControllers.Contains(controller.Name, StringComparer.Ordinal))
            {
                continue;
            }

            bool hasApiVersion = controller
                .GetCustomAttributes(true)
                .Any(a => a.GetType().FullName == "Asp.Versioning.ApiVersionAttribute");

            hasApiVersion.Should().BeTrue(
                $"Controller {controller.Name} in {moduleName} module should have [ApiVersion] attribute from Asp.Versioning");
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void Controllers_ShouldHaveVersionedRouteTemplate(string moduleName)
    {
        Assembly apiAssembly = Assembly.Load($"Wallow.{moduleName}.Api");

        IEnumerable<Type> controllers = Types.InAssembly(apiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .GetTypes();

        // ScimController uses /scim/v2 per SCIM RFC 7644 — its own versioning scheme
        // OpenIddict OIDC controllers use standard OAuth2/OIDC routes (connect/*)
        string[] excludedControllers = ["ScimController", "AuthorizationController", "TokenController", "LogoutController", "UserinfoController"];
        IEnumerable<Type> filtered = controllers.Where(c => !excludedControllers.Contains(c.Name, StringComparer.Ordinal));

        foreach (Type controller in filtered)
        {
            RouteAttribute? routeAttribute = controller.GetCustomAttribute<RouteAttribute>();

            routeAttribute.Should().NotBeNull(
                $"Controller {controller.Name} in {moduleName} module should have a [Route] attribute");

            routeAttribute.Template.Should().Contain("v{version:apiVersion}",
                $"Controller {controller.Name} in {moduleName} module route template should contain 'v{{version:apiVersion}}'");
        }
    }
}
