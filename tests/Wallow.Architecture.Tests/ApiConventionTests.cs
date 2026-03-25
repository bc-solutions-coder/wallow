using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

#pragma warning disable CA1024 // MemberData source methods cannot be properties

namespace Wallow.Architecture.Tests;

public class ApiConventionTests
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
    public void ControllerConventions_ShouldBeFollowed(string moduleName)
    {
        Assembly apiAssembly = GetModuleAssembly(moduleName, "Api");

        IEnumerable<Type> controllers = Types.InAssembly(apiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .GetTypes();

        // OpenIddict OIDC controllers use standard OAuth2/OIDC patterns, not REST API conventions
        string[] oidcControllers = ["AuthorizationController", "TokenController", "LogoutController", "UserinfoController"];

        foreach (Type controller in controllers)
        {
            // Controllers should have "Controller" suffix
            controller.Name.Should().EndWith("Controller",
                $"Controller {controller.Name} in {moduleName} module should have 'Controller' suffix");

            // Skip OIDC controllers for API-specific conventions
            if (oidcControllers.Contains(controller.Name, StringComparer.Ordinal))
            {
                continue;
            }

            // Controllers should have [ApiController] attribute
            controller.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull(
                $"Controller {controller.Name} in {moduleName} module should have [ApiController] attribute");

            // Controllers should use constructor injection when they have instance state
            ConstructorInfo[] constructors = controller.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            constructors.Should().NotBeEmpty(
                $"Controller {controller.Name} in {moduleName} module should have at least one public constructor");

            foreach (ConstructorInfo constructor in constructors)
            {
                bool hasInjectedParameters = constructor.GetParameters().Length > 0;
                bool hasInstanceState = controller.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(f => !f.IsStatic);

                (hasInjectedParameters || !hasInstanceState).Should().BeTrue(
                    $"Controller {controller.Name} in {moduleName} module should use constructor injection when it has instance dependencies.");
            }

            // Action methods should return IActionResult or ActionResult variants
            IEnumerable<MethodInfo> actionMethods = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.DeclaringType == controller);

            foreach (MethodInfo method in actionMethods)
            {
                Type returnType = method.ReturnType;

                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    returnType = returnType.GetGenericArguments()[0];
                }

                bool isValidReturnType =
                    typeof(IActionResult).IsAssignableFrom(returnType) ||
                    returnType == typeof(ActionResult) ||
                    (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ActionResult<>));

                isValidReturnType.Should().BeTrue(
                    $"Action method {controller.Name}.{method.Name} in {moduleName} module should return IActionResult, ActionResult, or ActionResult<T>.");
            }
        }

        // Controllers should be in Controllers namespace
        TestResult namespaceResult = Types.InAssembly(apiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .Should()
            .ResideInNamespace($"Wallow.{moduleName}.Api.Controllers")
            .GetResult();

        namespaceResult.IsSuccessful.Should().BeTrue(
            $"All controllers in {moduleName} module should be in Controllers namespace. " +
            $"Failing types: {string.Join(", ", namespaceResult.FailingTypeNames ?? Array.Empty<string>())}");

        // Request contracts should be in Contracts namespace
        TestResult requestResult = Types.InAssembly(apiAssembly)
            .That()
            .HaveNameEndingWith("Request")
            .And()
            .DoNotResideInNamespace($"Wallow.{moduleName}.Api.Controllers")
            .Should()
            .ResideInNamespace($"Wallow.{moduleName}.Api.Contracts")
            .GetResult();

        requestResult.IsSuccessful.Should().BeTrue(
            $"All request types in {moduleName} module should be in Contracts namespace. " +
            $"Failing types: {string.Join(", ", requestResult.FailingTypeNames ?? Array.Empty<string>())}");

        // Response contracts should be in Contracts namespace
        TestResult responseResult = Types.InAssembly(apiAssembly)
            .That()
            .HaveNameEndingWith("Response")
            .And()
            .DoNotResideInNamespace($"Wallow.{moduleName}.Api.Controllers")
            .Should()
            .ResideInNamespace($"Wallow.{moduleName}.Api.Contracts")
            .GetResult();

        responseResult.IsSuccessful.Should().BeTrue(
            $"All response types in {moduleName} module should be in Contracts namespace. " +
            $"Failing types: {string.Join(", ", responseResult.FailingTypeNames ?? Array.Empty<string>())}");
    }

    private static Assembly GetModuleAssembly(string moduleName, string layer)
    {
        string assemblyName = $"Wallow.{moduleName}.{layer}";
        return Assembly.Load(assemblyName);
    }
}
