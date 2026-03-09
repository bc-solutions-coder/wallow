using System.Text.Json;
using Foundry.Shared.Infrastructure.Plugins;
using Foundry.Shared.Kernel.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Foundry.Shared.Infrastructure.Tests.Plugins;

public class PluginServiceExtensionsTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    [Fact]
    public void AddFoundryPlugins_RegistersPluginRegistry()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddFoundryPlugins(config);

        ServiceProvider sp = services.BuildServiceProvider();
        PluginRegistry registry = sp.GetRequiredService<PluginRegistry>();
        registry.Should().NotBeNull();
    }

    [Fact]
    public void AddFoundryPlugins_RegistersPluginLoader()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddFoundryPlugins(config);

        ServiceProvider sp = services.BuildServiceProvider();
        PluginLoader loader = sp.GetRequiredService<PluginLoader>();
        loader.Should().NotBeNull();
    }

    [Fact]
    public void AddFoundryPlugins_RegistersPluginLifecycleManager()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddFoundryPlugins(config);
        services.AddLogging();

        ServiceProvider sp = services.BuildServiceProvider();
        PluginLifecycleManager manager = sp.GetRequiredService<PluginLifecycleManager>();
        manager.Should().NotBeNull();
    }

    [Fact]
    public void AddFoundryPlugins_RegistersPermissionValidator()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddFoundryPlugins(config);

        ServiceProvider sp = services.BuildServiceProvider();
        IPluginPermissionValidator validator = sp.GetRequiredService<IPluginPermissionValidator>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<PluginPermissionValidator>();
    }

    [Fact]
    public void AddFoundryPlugins_BindsPluginOptionsFromConfiguration()
    {
        Dictionary<string, string?> configData = new()
        {
            ["Plugins:PluginsDirectory"] = "/custom/plugins",
            ["Plugins:AutoDiscover"] = "true",
            ["Plugins:AutoEnable"] = "true"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        services.AddFoundryPlugins(config);

        ServiceProvider sp = services.BuildServiceProvider();
        PluginOptions options = sp.GetRequiredService<IOptions<PluginOptions>>().Value;

        options.PluginsDirectory.Should().Be("/custom/plugins");
        options.AutoDiscover.Should().BeTrue();
        options.AutoEnable.Should().BeTrue();
    }

    [Fact]
    public void AddFoundryPlugins_DefaultOptions_UsesDefaults()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddFoundryPlugins(config);

        ServiceProvider sp = services.BuildServiceProvider();
        PluginOptions options = sp.GetRequiredService<IOptions<PluginOptions>>().Value;

        options.PluginsDirectory.Should().Be("plugins/");
        options.AutoDiscover.Should().BeTrue();
        options.AutoEnable.Should().BeFalse();
    }

    [Fact]
    public void AddFoundryPlugins_AllServicesAreSingletons()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddFoundryPlugins(config);

        services.Should().Contain(s =>
            s.ServiceType == typeof(PluginRegistry)
            && s.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(s =>
            s.ServiceType == typeof(PluginLoader)
            && s.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(s =>
            s.ServiceType == typeof(PluginLifecycleManager)
            && s.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(s =>
            s.ServiceType == typeof(IPluginPermissionValidator)
            && s.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddFoundryPlugins_RegistersPluginOptionsConfiguration()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        services.AddFoundryPlugins(config);

        services.Should().Contain(s =>
            s.ServiceType == typeof(IConfigureOptions<PluginOptions>));
    }

    [Fact]
    public async Task InitializeFoundryPluginsAsync_AutoEnableFalse_SkipsLoadingPlugins()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"foundry-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            Dictionary<string, string?> configData = new()
            {
                ["Plugins:PluginsDirectory"] = tempDir,
                ["Plugins:AutoEnable"] = "false"
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(configData);
            builder.Services.AddFoundryPlugins(builder.Configuration);
            WebApplication app = builder.Build();

            Func<Task> act = () => app.InitializeFoundryPluginsAsync();

            await act.Should().NotThrowAsync();

            PluginRegistry registry = app.Services.GetRequiredService<PluginRegistry>();
            registry.GetAll().Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeFoundryPluginsAsync_AutoEnableTrue_NoManifests_CompletesWithoutError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"foundry-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            Dictionary<string, string?> configData = new()
            {
                ["Plugins:PluginsDirectory"] = tempDir,
                ["Plugins:AutoEnable"] = "true"
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(configData);
            builder.Services.AddFoundryPlugins(builder.Configuration);
            WebApplication app = builder.Build();

            Func<Task> act = () => app.InitializeFoundryPluginsAsync();

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeFoundryPluginsAsync_AutoEnableTrue_PluginLoadFails_DoesNotThrow()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"foundry-test-{Guid.NewGuid()}");
        string pluginDir = Path.Combine(tempDir, "failing-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            PluginManifest manifest = new(
                Id: "failing-plugin",
                Name: "Failing Plugin",
                Version: "1.0.0",
                Description: "A plugin that will fail to load",
                Author: "Test",
                MinFoundryVersion: "1.0.0",
                EntryAssembly: "NonExistent.dll",
                Dependencies: [],
                RequiredPermissions: [],
                ExportedServices: []);

            string manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "foundry-plugin.json"), manifestJson);

            Dictionary<string, string?> configData = new()
            {
                ["Plugins:PluginsDirectory"] = tempDir,
                ["Plugins:AutoEnable"] = "true"
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(configData);
            builder.Services.AddFoundryPlugins(builder.Configuration);
            WebApplication app = builder.Build();

            Func<Task> act = () => app.InitializeFoundryPluginsAsync();

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeFoundryPluginsAsync_AutoEnableTrue_DiscoversThenAttemptsLoad()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"foundry-test-{Guid.NewGuid()}");
        string pluginDir = Path.Combine(tempDir, "test-plugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            PluginManifest manifest = new(
                Id: "test-plugin",
                Name: "Test Plugin",
                Version: "1.0.0",
                Description: "A test plugin",
                Author: "Test",
                MinFoundryVersion: "1.0.0",
                EntryAssembly: "TestPlugin.dll",
                Dependencies: [],
                RequiredPermissions: [],
                ExportedServices: []);

            string manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "foundry-plugin.json"), manifestJson);

            Dictionary<string, string?> configData = new()
            {
                ["Plugins:PluginsDirectory"] = tempDir,
                ["Plugins:AutoEnable"] = "true"
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(configData);
            builder.Services.AddFoundryPlugins(builder.Configuration);
            WebApplication app = builder.Build();

            await app.InitializeFoundryPluginsAsync();

            PluginRegistry registry = app.Services.GetRequiredService<PluginRegistry>();
            PluginRegistryEntry? entry = registry.GetEntry("test-plugin");
            entry.Should().NotBeNull();
            entry.Manifest.Id.Should().Be("test-plugin");
            // Plugin was discovered but load failed (no real assembly), so state stays Discovered
            entry.State.Should().Be(PluginLifecycleState.Discovered);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeFoundryPluginsAsync_NonExistentDirectory_CompletesWithoutError()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"foundry-nonexistent-{Guid.NewGuid()}");

        Dictionary<string, string?> configData = new()
        {
            ["Plugins:PluginsDirectory"] = tempDir,
            ["Plugins:AutoEnable"] = "true"
        };

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(configData);
        builder.Services.AddFoundryPlugins(builder.Configuration);
        WebApplication app = builder.Build();

        Func<Task> act = () => app.InitializeFoundryPluginsAsync();

        await act.Should().NotThrowAsync();
    }
}
