using System.Reflection;
using Wallow.Api.Extensions;

namespace Wallow.Api.Tests;

public class ServiceDefaultsWiringTests
{
    private static readonly Assembly _apiAssembly = typeof(ServiceCollectionExtensions).Assembly;

    [Fact]
    public void AddObservability_ShouldNotExist_OnServiceCollectionExtensions()
    {
        // The AddObservability method should be removed in favor of ServiceDefaults
        Type extensionsType = typeof(ServiceCollectionExtensions);

        MethodInfo? addObservabilityMethod = extensionsType.GetMethod(
            "AddObservability",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

        addObservabilityMethod.Should().BeNull(
            "AddObservability should be removed from ServiceCollectionExtensions — " +
            "observability is now provided by Wallow.ServiceDefaults");
    }

    [Fact]
    public void WallowApi_ShouldReference_ServiceDefaultsAssembly()
    {
        // Wallow.Api must reference Wallow.ServiceDefaults for centralized observability
        AssemblyName[] referencedAssemblies = _apiAssembly.GetReferencedAssemblies();

        bool referencesServiceDefaults = referencedAssemblies
            .Any(a => a.Name == "Wallow.ServiceDefaults");

        referencesServiceDefaults.Should().BeTrue(
            "Wallow.Api should reference Wallow.ServiceDefaults for centralized observability and health checks");
    }
}
