using System.Reflection;

namespace Wallow.Architecture.Tests;

public class ModuleRegistrationTests
{


    [Theory]
    [InlineData("Notifications")]
    [InlineData("Announcements")]
    [InlineData("Identity")]
    [InlineData("Storage")]
    public void Module_ShouldProvide_AddModuleExtensionMethod(string moduleName)
    {
        string infrastructureAssemblyName = $"Wallow.{moduleName}.Infrastructure";

        Assembly infrastructureAssembly = Assembly.Load(infrastructureAssemblyName);

        Type? extensionType = infrastructureAssembly.GetTypes()
            .FirstOrDefault(t =>
                t.Name == $"{moduleName}ModuleExtensions" &&
                t.IsSealed &&
                t.IsAbstract);

        extensionType.Should().NotBeNull(
            $"{moduleName} module should have {moduleName}ModuleExtensions static class in Infrastructure");

        MethodInfo? addModuleMethod = extensionType.GetMethod(
            $"Add{moduleName}Module",
            BindingFlags.Public | BindingFlags.Static);

        addModuleMethod.Should().NotBeNull(
            $"{moduleName}ModuleExtensions should have Add{moduleName}Module method");

        addModuleMethod.IsStatic.Should().BeTrue();
        addModuleMethod.GetParameters()[0].ParameterType.Name.Should().Be("IServiceCollection");
        addModuleMethod.GetParameters()[1].ParameterType.Name.Should().Be("IConfiguration");
    }
}
