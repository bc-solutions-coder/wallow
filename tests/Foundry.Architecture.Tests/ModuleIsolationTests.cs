using System.Reflection;
using NetArchTest.Rules;

#pragma warning disable CA1024 // MemberData source methods cannot be properties

namespace Foundry.Architecture.Tests;

public class ModuleIsolationTests
{
    private static readonly string[] _layers = ["Domain", "Application", "Infrastructure", "Api"];

    public static IEnumerable<object[]> GetModuleLayerCombinations()
    {
        foreach (string moduleName in TestConstants.AllModules)
        {
            foreach (string layer in _layers)
            {
                yield return [moduleName, layer];
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleLayerCombinations))]
    public void ModuleLayer_ShouldNotReference_AnyOtherModule(string sourceModule, string sourceLayer)
    {
        Assembly sourceAssembly = GetModuleAssembly(sourceModule, sourceLayer);
        Types types = Types.InAssembly(sourceAssembly);

        foreach (string targetModule in TestConstants.AllModules)
        {
            if (targetModule == sourceModule)
            {
                continue;
            }

            TestResult result = types
                .ShouldNot()
                .HaveDependencyOn($"Foundry.{targetModule}")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{sourceModule}.{sourceLayer} should not reference {targetModule}. " +
                $"Modules must communicate only via Shared.Contracts events. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    private static Assembly GetModuleAssembly(string moduleName, string layer)
    {
        string assemblyName = $"Foundry.{moduleName}.{layer}";
        return Assembly.Load(assemblyName);
    }
}
