using System.Reflection;
using System.Reflection.Emit;
using Wallow.Shared.Infrastructure.Plugins;
using Wallow.Shared.Kernel.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Wallow.Shared.Infrastructure.Tests.Plugins;

public class PluginLoaderTests
{
    private readonly PluginRegistry _registry = new();
    private readonly PluginLoader _sut;

    public PluginLoaderTests()
    {
        _sut = new PluginLoader(_registry, Options.Create(new PluginOptions()), NullLogger<PluginLoader>.Instance);
    }

    private static PluginManifest CreateManifest(
        string id = "test-plugin",
        string entryAssembly = "TestPlugin.dll") =>
        new(id, "Test Plugin", "1.0.0", "A test plugin", "Test Author", "1.0.0",
            entryAssembly, [], [], []);

    [Fact]
    public void LoadPlugin_NonExistentAssembly_ThrowsPluginLoadException()
    {
        PluginManifest manifest = CreateManifest(entryAssembly: "NonExistent.dll");
        _registry.Register(manifest);
        string basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Action act = () => _sut.LoadPlugin(manifest, basePath);

        act.Should().Throw<PluginLoadException>()
            .Which.PluginId.Should().Be("test-plugin");
    }

    [Fact]
    public void LoadPlugin_InvalidAssemblyFile_ThrowsPluginLoadException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string pluginDir = Path.Combine(tempDir, "bad-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            string assemblyPath = Path.Combine(pluginDir, "BadPlugin.dll");
            File.WriteAllText(assemblyPath, "this is not a valid assembly");

            PluginManifest manifest = CreateManifest(id: "bad-plugin", entryAssembly: "BadPlugin.dll");
            _registry.Register(manifest);

            Action act = () => _sut.LoadPlugin(manifest, tempDir);

            act.Should().Throw<PluginLoadException>()
                .Which.PluginId.Should().Be("bad-plugin");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPlugin_MissingPluginDirectory_ThrowsPluginLoadException()
    {
        string basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        PluginManifest manifest = CreateManifest(id: "missing-dir");
        _registry.Register(manifest);

        Action act = () => _sut.LoadPlugin(manifest, basePath);

        act.Should().Throw<PluginLoadException>()
            .Which.PluginId.Should().Be("missing-dir");
    }

    [Fact]
    public void LoadPlugin_NonExistentPath_WrapsExceptionInPluginLoadException()
    {
        PluginManifest manifest = CreateManifest(id: "wrap-test");
        _registry.Register(manifest);
        string basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Action act = () => _sut.LoadPlugin(manifest, basePath);

        act.Should().Throw<PluginLoadException>()
            .Which.InnerException.Should().NotBeNull();
    }

    [Fact]
    public void LoadPlugin_AssemblyWithNoPluginImplementation_ThrowsPluginLoadException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string pluginDir = Path.Combine(tempDir, "no-impl-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            // Create a minimal valid assembly with no IWallowPlugin implementation
            string assemblyPath = Path.Combine(pluginDir, "NoImpl.dll");
            CreateEmptyAssembly(assemblyPath, "NoImplAssembly");

            PluginManifest manifest = CreateManifest(id: "no-impl-plugin", entryAssembly: "NoImpl.dll");
            _registry.Register(manifest);

            Action act = () => _sut.LoadPlugin(manifest, tempDir);

            act.Should().Throw<PluginLoadException>()
                .Where(e => e.PluginId == "no-impl-plugin"
                    && e.Message.Contains("No IWallowPlugin implementation found"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPlugin_ManifestIdMismatch_ThrowsPluginLoadException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        // Manifest expects "wrong-id-plugin" but MismatchedManifestPlugin returns "mismatched-plugin-id"
        string pluginDir = Path.Combine(tempDir, "wrong-id-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            // Copy the test assembly which contains MismatchedManifestPlugin (ID: "mismatched-plugin-id")
            string sourceAssembly = typeof(MismatchedManifestPlugin).Assembly.Location;
            string targetAssembly = Path.Combine(pluginDir, "MismatchPlugin.dll");
            File.Copy(sourceAssembly, targetAssembly);

            PluginManifest manifest = CreateManifest(id: "wrong-id-plugin", entryAssembly: "MismatchPlugin.dll");
            _registry.Register(manifest);

            Action act = () => _sut.LoadPlugin(manifest, tempDir);

            act.Should().Throw<PluginLoadException>()
                .Where(e => e.PluginId == "wrong-id-plugin"
                    && e.Message.Contains("manifest ID mismatch"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void CreateEmptyAssembly(string outputPath, string assemblyName)
    {
        PersistedAssemblyBuilder ab = new(
            new AssemblyName(assemblyName), typeof(object).Assembly);
        ab.DefineDynamicModule(assemblyName);
        ab.Save(outputPath);
    }

    private static void CreateAssemblyWithMultiplePluginTypes(string outputPath)
    {
        // Build a dynamic assembly with two concrete IWallowPlugin implementations.
        // Both are minimal stubs — PluginLoader only needs GetTypes() to find them.
        Type pluginInterface = typeof(IWallowPlugin);
        Type manifestType = typeof(PluginManifest);
        Type pluginContextType = typeof(PluginContext);
        Assembly coreAssembly = typeof(object).Assembly;

        PersistedAssemblyBuilder ab = new(
            new AssemblyName("MultiPluginAssembly"), coreAssembly);
        ModuleBuilder mb = ab.DefineDynamicModule("MultiPluginAssembly");

        for (int i = 1; i <= 2; i++)
        {
            TypeBuilder tb = mb.DefineType(
                $"TestPlugin{i}",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(object),
                [pluginInterface]);

            // Manifest property backing field
            FieldBuilder manifestField = tb.DefineField(
                "_manifest", manifestType, FieldAttributes.Private | FieldAttributes.InitOnly);

            // Constructor: creates PluginManifest and stores it
            ConstructorBuilder ctor = tb.DefineConstructor(
                MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            ILGenerator ctorIl = ctor.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            // Create PluginManifest: new PluginManifest(id, name, version, desc, author, minVer, entry, deps, perms, svcs)
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldstr, $"plugin-{i}");           // Id
            ctorIl.Emit(OpCodes.Ldstr, $"Plugin {i}");           // Name
            ctorIl.Emit(OpCodes.Ldstr, "1.0.0");                 // Version
            ctorIl.Emit(OpCodes.Ldstr, "Test");                  // Description
            ctorIl.Emit(OpCodes.Ldstr, "Test");                  // Author
            ctorIl.Emit(OpCodes.Ldstr, "1.0.0");                 // MinWallowVersion
            ctorIl.Emit(OpCodes.Ldstr, "Test.dll");              // EntryAssembly
            // Empty lists for Dependencies, RequiredPermissions, ExportedServices
            ctorIl.Emit(OpCodes.Call, typeof(Array).GetMethod("Empty")!.MakeGenericMethod(typeof(PluginDependency)));
            ctorIl.Emit(OpCodes.Call, typeof(Array).GetMethod("Empty")!.MakeGenericMethod(typeof(string)));
            ctorIl.Emit(OpCodes.Call, typeof(Array).GetMethod("Empty")!.MakeGenericMethod(typeof(string)));
            ctorIl.Emit(OpCodes.Newobj, manifestType.GetConstructors()[0]);
            ctorIl.Emit(OpCodes.Stfld, manifestField);
            ctorIl.Emit(OpCodes.Ret);

            // Manifest property getter
            PropertyBuilder manifestProp = tb.DefineProperty(
                "Manifest", PropertyAttributes.None, manifestType, null);
            MethodBuilder getManifest = tb.DefineMethod(
                "get_Manifest",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName,
                manifestType, Type.EmptyTypes);
            ILGenerator getIl = getManifest.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, manifestField);
            getIl.Emit(OpCodes.Ret);
            manifestProp.SetGetMethod(getManifest);

            // AddServices(IServiceCollection, IConfiguration) — nop
            MethodBuilder addServices = tb.DefineMethod(
                "AddServices",
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(void),
                [typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection),
                 typeof(Microsoft.Extensions.Configuration.IConfiguration)]);
            ILGenerator addIl = addServices.GetILGenerator();
            addIl.Emit(OpCodes.Ret);

            // InitializeAsync(PluginContext) — return Task.CompletedTask
            MethodBuilder initAsync = tb.DefineMethod(
                "InitializeAsync",
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(Task),
                [pluginContextType]);
            ILGenerator initIl = initAsync.GetILGenerator();
            initIl.Emit(OpCodes.Call, typeof(Task).GetProperty("CompletedTask")!.GetGetMethod()!);
            initIl.Emit(OpCodes.Ret);

            // ShutdownAsync() — return Task.CompletedTask
            MethodBuilder shutdownAsync = tb.DefineMethod(
                "ShutdownAsync",
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(Task),
                Type.EmptyTypes);
            ILGenerator shutdownIl = shutdownAsync.GetILGenerator();
            shutdownIl.Emit(OpCodes.Call, typeof(Task).GetProperty("CompletedTask")!.GetGetMethod()!);
            shutdownIl.Emit(OpCodes.Ret);

            tb.CreateType();
        }

        ab.Save(outputPath);
    }

    [Fact]
    public void LoadPlugin_ValidPlugin_RegistersAndReturnsEntry()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string pluginDir = Path.Combine(tempDir, MismatchedManifestPlugin.PluginId);
        Directory.CreateDirectory(pluginDir);

        try
        {
            string sourceAssembly = typeof(MismatchedManifestPlugin).Assembly.Location;
            string targetAssembly = Path.Combine(pluginDir, "ValidPlugin.dll");
            File.Copy(sourceAssembly, targetAssembly);

            PluginManifest manifest = CreateManifest(
                id: MismatchedManifestPlugin.PluginId,
                entryAssembly: "ValidPlugin.dll");
            _registry.Register(manifest);

            PluginRegistryEntry result = _sut.LoadPlugin(manifest, tempDir);

            result.Should().NotBeNull();
            result.Instance.Should().NotBeNull();
            result.Instance.Should().BeAssignableTo<IWallowPlugin>();
            result.LoadContext.Should().NotBeNull();
            result.State.Should().Be(PluginLifecycleState.Installed);
            result.Manifest.Id.Should().Be(MismatchedManifestPlugin.PluginId);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPlugin_ValidPlugin_PluginInstanceHasCorrectManifestId()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string pluginDir = Path.Combine(tempDir, MismatchedManifestPlugin.PluginId);
        Directory.CreateDirectory(pluginDir);

        try
        {
            string sourceAssembly = typeof(MismatchedManifestPlugin).Assembly.Location;
            string targetAssembly = Path.Combine(pluginDir, "ValidPlugin.dll");
            File.Copy(sourceAssembly, targetAssembly);

            PluginManifest manifest = CreateManifest(
                id: MismatchedManifestPlugin.PluginId,
                entryAssembly: "ValidPlugin.dll");
            _registry.Register(manifest);

            PluginRegistryEntry result = _sut.LoadPlugin(manifest, tempDir);

            result.Instance!.Manifest.Id.Should().Be(MismatchedManifestPlugin.PluginId);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPlugin_AssemblyWithMultipleImplementations_ThrowsPluginLoadException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string pluginDir = Path.Combine(tempDir, "multi-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            string assemblyPath = Path.Combine(pluginDir, "MultiPlugin.dll");
            CreateAssemblyWithMultiplePluginTypes(assemblyPath);

            PluginManifest manifest = CreateManifest(id: "multi-plugin", entryAssembly: "MultiPlugin.dll");
            _registry.Register(manifest);

            Action act = () => _sut.LoadPlugin(manifest, tempDir);

            act.Should().Throw<PluginLoadException>()
                .Where(e => e.PluginId == "multi-plugin"
                    && e.Message.Contains("Multiple IWallowPlugin implementations found"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPlugin_InvalidAssembly_DoesNotLeaveRegistryInBadState()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string pluginDir = Path.Combine(tempDir, "bad-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            File.WriteAllText(Path.Combine(pluginDir, "Bad.dll"), "not an assembly");

            PluginManifest manifest = CreateManifest(id: "bad-plugin", entryAssembly: "Bad.dll");
            _registry.Register(manifest);

            try { _sut.LoadPlugin(manifest, tempDir); } catch (PluginLoadException) { }

            PluginRegistryEntry? entry = _registry.GetEntry("bad-plugin");
            entry!.Instance.Should().BeNull();
            entry.LoadContext.Should().BeNull();
            entry.State.Should().Be(PluginLifecycleState.Discovered);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
