using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Infrastructure.Tests.Settings;

public class SettingsServiceExtensionsTests
{
    [Fact]
    public void AddSettings_RegistersSettingRegistryAsKeyedSingleton()
    {
        ServiceCollection services = new();
        services.AddDistributedMemoryCache();

        services.AddSettings<FakeDbContext, TestSettingRegistry>("test-module");

        ServiceProvider provider = services.BuildServiceProvider();
        ISettingRegistry? registry = provider.GetKeyedService<ISettingRegistry>("test-module");
        registry.Should().NotBeNull();
        registry!.ModuleName.Should().Be("test-settings");
    }

    [Fact]
    public void AddSettings_RegistryHasCorrectDefaults()
    {
        ServiceCollection services = new();
        services.AddDistributedMemoryCache();

        services.AddSettings<FakeDbContext, TestSettingRegistry>("test-module");

        ServiceProvider provider = services.BuildServiceProvider();
        ISettingRegistry? registry = provider.GetKeyedService<ISettingRegistry>("test-module");

        registry.Should().NotBeNull();
        registry!.IsCodeDefinedKey("feature.enabled").Should().BeTrue();
    }

    [Fact]
    public void AddSettings_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();
        services.AddDistributedMemoryCache();

        IServiceCollection result = services.AddSettings<FakeDbContext, TestSettingRegistry>("test-module");

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSettings_WithDifferentModuleKeys_RegistersSeparateRegistries()
    {
        ServiceCollection services = new();
        services.AddDistributedMemoryCache();

        services.AddSettings<FakeDbContext, TestSettingRegistry>("module-a");
        services.AddSettings<FakeDbContext, AnotherSettingRegistry>("module-b");

        ServiceProvider provider = services.BuildServiceProvider();
        ISettingRegistry? registryA = provider.GetKeyedService<ISettingRegistry>("module-a");
        ISettingRegistry? registryB = provider.GetKeyedService<ISettingRegistry>("module-b");

        registryA.Should().NotBeNull();
        registryB.Should().NotBeNull();
        registryA!.ModuleName.Should().Be("test-settings");
        registryB!.ModuleName.Should().Be("another-settings");
    }

    private sealed class TestSettingRegistry : SettingRegistryBase
    {
        public override string ModuleName => "test-settings";

        public static readonly SettingDefinition<bool> FeatureEnabled = new(
            Key: "feature.enabled",
            DefaultValue: false,
            Description: "Test feature flag");
    }

    private sealed class AnotherSettingRegistry : SettingRegistryBase
    {
        public override string ModuleName => "another-settings";
    }
}
