using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace Wallow.Api.Tests.Extensions;

/// <summary>
/// Pins the #150 requirement that registration-class rate limits apply to every org-surface
/// mutation and to organization create: each non-GET action on the org-surface controllers
/// must carry <c>[EnableRateLimiting("registration")]</c>.
/// </summary>
public sealed class RegistrationRateLimitPolicyCoverageTests
{
    private const string RegistrationPolicy = "registration";

    private static readonly string[] _orgSurfaceControllerNames =
    [
        "OrganizationClientsController",
        "OrganizationClientBrandingController",
    ];

    private static List<Type> DiscoverControllers(params string[] names)
    {
        List<Type> controllers = [];

        foreach (string assemblyPath in Directory.GetFiles(
            AppDomain.CurrentDomain.BaseDirectory, "Wallow.*.Api.dll"))
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            controllers.AddRange(assembly.GetTypes().Where(type =>
                typeof(ControllerBase).IsAssignableFrom(type)
                && !type.IsAbstract
                && names.Contains(type.Name, StringComparer.Ordinal)));
        }

        return controllers;
    }

    private static bool IsMutation(MethodInfo method)
    {
        List<HttpMethodAttribute> httpAttributes = method
            .GetCustomAttributes<HttpMethodAttribute>()
            .ToList();

        return httpAttributes.Count > 0
            && httpAttributes.Any(attribute => !attribute.HttpMethods.Contains("GET"));
    }

    private static bool HasRegistrationPolicy(MethodInfo method)
    {
        EnableRateLimitingAttribute? attribute =
            method.GetCustomAttribute<EnableRateLimitingAttribute>();
        return attribute?.PolicyName == RegistrationPolicy;
    }

    [Fact]
    public void OrgSurfaceControllers_AreDiscovered()
    {
        DiscoverControllers(_orgSurfaceControllerNames).Should().HaveCount(
            _orgSurfaceControllerNames.Length,
            "assembly discovery must find every org-surface controller, or the coverage "
            + "assertions below are vacuous");
    }

    [Fact]
    public void EveryOrgSurfaceMutation_CarriesTheRegistrationPolicy()
    {
        List<string> uncovered = [];

        foreach (Type controller in DiscoverControllers(_orgSurfaceControllerNames))
        {
            foreach (MethodInfo method in controller.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!IsMutation(method) || HasRegistrationPolicy(method))
                {
                    continue;
                }

                uncovered.Add($"{controller.Name}.{method.Name}");
            }
        }

        uncovered.Should().BeEmpty(
            "every org-surface mutation must be limited under the registration policy: {0}",
            string.Join(", ", uncovered));
    }

    [Fact]
    public void OrganizationCreate_CarriesTheRegistrationPolicy()
    {
        Type organizations = DiscoverControllers("OrganizationsController").Should()
            .ContainSingle().Subject;
        MethodInfo create = organizations.GetMethod("Create")!;

        create.Should().NotBeNull();
        HasRegistrationPolicy(create).Should().BeTrue(
            "organization create is registration-class and must be limited under the registration policy");
    }
}
