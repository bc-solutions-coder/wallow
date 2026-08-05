using System.Reflection;

namespace Wallow.Architecture.Tests;

public class ModuleRegistrationTests
{
    // Four tests were deleted here when WallowModules became an IWallowModule registry.
    //
    // WallowModules_ShouldRegister_AllModules, WallowModules_ShouldInitialize_AllModulesWithDbContext
    // and AllDiscoveredModules_ShouldBeRegistered_InWallowModules all did File.ReadAllText over
    // src/Wallow.Api/WallowModules.cs and asserted that the text contained literal per-module call
    // strings. .claude/rules/TESTING.md bans reading application source from a spec outright, so they
    // were deleted rather than ported: a test calls a function and asserts what happens, and
    // constraining how code is written is a linter's job.
    //
    // Module_ShouldProvide_InitializeModuleExtensionMethod was not a source-text test, but it
    // reflected for Initialize{Module}ModuleAsync — seven methods that were all no-op
    // `return Task.FromResult(app);` and are now gone.
    //
    // What still proves modules register correctly is behavioural and lives in
    // Modules/ModuleToggleTests.cs, which invokes AddWallowModules and inspects the resulting
    // ServiceCollection.

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
