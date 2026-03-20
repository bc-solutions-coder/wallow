using System.Reflection;

namespace Wallow.Architecture.Tests;

public class ModuleRegistrationTests
{
    private static readonly string[] _moduleNames = TestConstants.AllModules;

    [Fact]
    public void WallowModules_ShouldRegister_AllModules()
    {
        Assembly wallowModulesAssembly = Assembly.Load("Wallow.Api");
        Type? wallowModulesType = wallowModulesAssembly.GetType("Wallow.Api.WallowModules");

        wallowModulesType.Should().NotBeNull("WallowModules class should exist in Wallow.Api");

        MethodInfo? addModulesMethod = wallowModulesType.GetMethod(
            "AddWallowModules",
            BindingFlags.Public | BindingFlags.Static);

        addModulesMethod.Should().NotBeNull("AddWallowModules method should exist");

        string sourceCode = File.ReadAllText(
            Path.Combine(GetSolutionRoot(), "src/Wallow.Api/WallowModules.cs"));

        foreach (string moduleName in _moduleNames)
        {
            string addModuleCall = $"Add{moduleName}Module(configuration)";
            sourceCode.Should().Contain(addModuleCall,
                $"WallowModules.AddWallowModules should call {addModuleCall}");
        }
    }

    [Fact]
    public void WallowModules_ShouldInitialize_AllModulesWithDbContext()
    {
        string[] modulesWithDbContext =
        [
            "Billing", "Notifications", "Messaging", "Announcements",
            "Identity", "Storage"
        ];

        Assembly wallowModulesAssembly = Assembly.Load("Wallow.Api");
        Type? wallowModulesType = wallowModulesAssembly.GetType("Wallow.Api.WallowModules");

        wallowModulesType.Should().NotBeNull("WallowModules class should exist in Wallow.Api");

        MethodInfo? initializeModulesMethod = wallowModulesType.GetMethod(
            "InitializeWallowModulesAsync",
            BindingFlags.Public | BindingFlags.Static);

        initializeModulesMethod.Should().NotBeNull("InitializeWallowModulesAsync method should exist");

        string sourceCode = File.ReadAllText(
            Path.Combine(GetSolutionRoot(), "src/Wallow.Api/WallowModules.cs"));

        foreach (string moduleName in modulesWithDbContext)
        {
            string initializeModuleCall = $"Initialize{moduleName}ModuleAsync()";
            sourceCode.Should().Contain(initializeModuleCall,
                $"WallowModules.InitializeWallowModulesAsync should call {initializeModuleCall}");
        }
    }

    [Theory]
    [InlineData("Billing")]
    [InlineData("Notifications")]
    [InlineData("Messaging")]
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
        addModuleMethod.GetParameters().Should().HaveCount(2);
        addModuleMethod.GetParameters()[0].ParameterType.Name.Should().Be("IServiceCollection");
        addModuleMethod.GetParameters()[1].ParameterType.Name.Should().Be("IConfiguration");
    }

    [Theory]
    [InlineData("Billing")]
    [InlineData("Notifications")]
    [InlineData("Messaging")]
    [InlineData("Announcements")]
    [InlineData("Identity")]
    [InlineData("Storage")]
    public void Module_ShouldProvide_InitializeModuleExtensionMethod(string moduleName)
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

        MethodInfo? initializeModuleMethod = extensionType.GetMethod(
            $"Initialize{moduleName}ModuleAsync",
            BindingFlags.Public | BindingFlags.Static);

        initializeModuleMethod.Should().NotBeNull(
            $"{moduleName}ModuleExtensions should have Initialize{moduleName}ModuleAsync method");

        initializeModuleMethod.IsStatic.Should().BeTrue();
        initializeModuleMethod.GetParameters().Should().HaveCount(1);
        initializeModuleMethod.GetParameters()[0].ParameterType.Name.Should().Be("WebApplication");
    }

    [Fact]
    public void AllDiscoveredModules_ShouldBeRegistered_InWallowModules()
    {
        // Discover all Wallow.*.Infrastructure assemblies via loaded DLLs
        string[] infrastructureDlls = Directory
            .GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Wallow.*.Infrastructure.dll")
            .ToArray();

        infrastructureDlls.Should().NotBeEmpty("there should be Infrastructure assemblies in the output");

        List<string> discoveredModuleNames = [];

        foreach (string dll in infrastructureDlls)
        {
            Assembly assembly = Assembly.LoadFrom(dll);

            // Find static classes named {Module}ModuleExtensions with Add{Module}Module methods
            Type[] moduleExtensionTypes = assembly.GetTypes()
                .Where(t => t.IsSealed && t.IsAbstract && t.Name.EndsWith("ModuleExtensions", StringComparison.Ordinal))
                .ToArray();

            foreach (Type extensionType in moduleExtensionTypes)
            {
                string typeName = extensionType.Name;
                string moduleName = typeName.Replace("ModuleExtensions", "");

                MethodInfo? addMethod = extensionType.GetMethod(
                    $"Add{moduleName}Module",
                    BindingFlags.Public | BindingFlags.Static);

                if (addMethod != null)
                {
                    discoveredModuleNames.Add(moduleName);
                }
            }
        }

        discoveredModuleNames.Should().NotBeEmpty("reflection should discover at least one module");

        // Verify every discovered module is registered in WallowModules.cs
        string sourceCode = File.ReadAllText(
            Path.Combine(GetSolutionRoot(), "src/Wallow.Api/WallowModules.cs"));

        foreach (string moduleName in discoveredModuleNames)
        {
            string expectedCall = $"Add{moduleName}Module(configuration)";
            sourceCode.Should().Contain(expectedCall,
                $"module '{moduleName}' was discovered via reflection but is not registered in WallowModules.cs");
        }

        // Discovered modules via reflection should match the known module list
        discoveredModuleNames.Order().Should().BeEquivalentTo(
            _moduleNames.Order(),
            "discovered modules via reflection should match TestConstants.AllModules");
    }

    private static string GetSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Wallow.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Solution root not found");
    }
}
