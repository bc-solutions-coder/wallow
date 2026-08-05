using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.FeatureManagement;
using NSubstitute;
using StackExchange.Redis;
using Wallow.Api;
using Wallow.Modules.Registry;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Architecture.Tests.Modules;

/// <summary>
/// Pins what a <c>FeatureManagement:Modules.*</c> value is allowed to look like and what each shape
/// resolves to, now that <c>AddWallowModules</c> reads those flags straight off
/// <see cref="IConfiguration"/> instead of asking <see cref="IFeatureManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// The equivalence facts are the load-bearing ones. They resolve the enabled set twice for the same
/// configuration — once the way the host does it now, once through a live
/// <see cref="IFeatureManager"/> built from the very package the host used to ask — and require the
/// two answers to be identical. That is a real oracle rather than a restatement of the new code:
/// <c>Microsoft.FeatureManagement</c> is still referenced, so the old algorithm can still be run
/// side by side with the new one and would disagree the moment the new one drifted.
/// </para>
/// <para>
/// The malformed-shape facts cover the one place the two deliberately part company. A value that is
/// an object (<c>EnabledFor</c>/<c>RequirementType</c> — the shape a feature filter needs) or a
/// non-boolean scalar reads as a plain <see langword="false"/> off <see cref="IConfiguration"/>, and
/// a module that reads as disabled loses its DI registrations, its Wolverine handlers and its whole
/// HTTP surface. Silently. So the host refuses to start instead, naming the key.
/// </para>
/// </remarks>
public sealed class ModuleFlagShapeTests
{
    private const string StorageFlagKey = "FeatureManagement:Modules.Storage";

    /// <summary>
    /// Gets every shipped environment overlay, merged over the base <c>appsettings.json</c> exactly
    /// as the host merges it. These are the flag shapes that actually ship.
    /// </summary>
    public static TheoryData<string> ShippedOverlays =>
        new()
        {
            "appsettings.json",
            "appsettings.Development.json",
            "appsettings.Production.json",
            "appsettings.Staging.json",
            "appsettings.Testing.json",
        };

    /// <summary>
    /// Gets the flag-value spellings a provider can hand over for a scalar boolean: JSON's own
    /// <c>true</c>/<c>false</c> literals arrive title-cased, an environment variable or a command
    /// line arrives however it was typed.
    /// </summary>
    public static TheoryData<string, string, bool> ScalarSpellings =>
        new()
        {
            { "JSON boolean literal", "True", true },
            { "JSON boolean literal", "False", false },
            { "lowercase string", "true", true },
            { "lowercase string", "false", false },
            { "upper-case string", "TRUE", true },
            { "upper-case string", "FALSE", false },
            { "padded string", " true ", true },
        };

    /// <summary>
    /// Gets the values that are present but are not a scalar boolean: the object shape a
    /// <c>Microsoft.FeatureManagement</c> filter needs, and scalars that do not parse.
    /// </summary>
    public static TheoryData<string, string> MalformedFlagValues =>
        new()
        {
            { "", "tru" },
            { "", "1" },
            { "", "yes" },
            { "", "" },
            { ":EnabledFor:0:Name", "AlwaysOn" },
            { ":RequirementType", "All" },
        };

    [Theory]
    [MemberData(nameof(ShippedOverlays))]
    public void EnabledModules_MatchFeatureManager_ForEveryShippedConfiguration(string overlay)
    {
        IConfiguration configuration = BuildShippedConfiguration(overlay);

        IReadOnlyList<string> viaFeatureManager = ResolveViaFeatureManager(configuration);
        IReadOnlyList<string> viaHost = ResolveViaHost(configuration);

        viaHost.Should().BeEquivalentTo(
            viaFeatureManager,
            $"{overlay} must enable exactly the modules IFeatureManager enabled for it — reading the "
            + "same flags off IConfiguration instead of through the feature manager is only safe if "
            + "it lands on the same set");
    }

    [Theory]
    [MemberData(nameof(ScalarSpellings))]
    public void EnabledModules_MatchFeatureManager_ForEveryScalarSpelling(
        string spelling,
        string value,
        bool expectedEnabled)
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [StorageFlagKey] = value,
        });

        IReadOnlyList<string> viaFeatureManager = ResolveViaFeatureManager(configuration);
        IReadOnlyList<string> viaHost = ResolveViaHost(configuration);

        viaHost.Should().BeEquivalentTo(
            viaFeatureManager,
            $"a {spelling} flag value of '{value}' must resolve the same module set both ways");
        viaHost.Contains("Storage").Should().Be(
            expectedEnabled,
            $"a {spelling} flag value of '{value}' means the module is "
            + (expectedEnabled ? "on" : "off"));
    }

    [Fact]
    public void AbsentFlag_LeavesTheOptionalModuleOff_AndItsDbContextUnregistered()
    {
        // Nothing under FeatureManagement at all: every optional module's key is absent, which is
        // the one path the older toggle facts never asserted on by name.
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration([]);

        IReadOnlyList<IWallowModule> enabledModules = AddWallowModules(services, configuration);

        enabledModules.Select(module => module.Name).Should().NotContain(
            "Notifications",
            "an absent Modules.Notifications key must read as disabled, exactly as it did through "
            + "IFeatureManager, whose IgnoreMissingFeatures default answers no for a feature it has "
            + "no definition for");
        services.Should().NotContain(
            sd => sd.ServiceType == typeof(NotificationsDbContext),
            "a module the absent key left disabled must register nothing at all");
    }

    [Fact]
    public void EnvironmentVariableProvider_ReachesTheSameKey_AsTheJsonShape()
    {
        // The documented deployment override is FeatureManagement__Modules.Storage=false, which the
        // environment-variable provider normalises to the same colon-separated key the JSON files
        // produce — and hands over as a lowercase string, never a boolean.
        const string environmentVariable = "FeatureManagement__Modules.Storage";
        string? original = Environment.GetEnvironmentVariable(environmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, "false");
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [StorageFlagKey] = "true",
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
                })
                .AddEnvironmentVariables()
                .Build();

            configuration[StorageFlagKey].Should().Be(
                "false",
                "the environment-variable provider must win over the JSON value and arrive as a string");
            ResolveViaHost(configuration).Should().NotContain(
                "Storage",
                "an environment variable is how a deployment turns a module off, so its string value "
                + "must resolve the same way the JSON boolean does");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, original);
        }
    }

    [Theory]
    [MemberData(nameof(MalformedFlagValues))]
    public void MalformedFlagValue_FailsTheHost_InsteadOfSilentlyDisablingTheModule(
        string keySuffix,
        string value)
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [StorageFlagKey + keySuffix] = value,
        });

        Action addModules = () => AddWallowModules(services, configuration);

        addModules.Should().Throw<InvalidOperationException>(
                "a flag value that is present but is not a scalar boolean reads as false off "
                + "IConfiguration, and a module that reads as disabled loses its endpoints without a "
                + "word — the host must refuse to start instead")
            .WithMessage(
                $"*{StorageFlagKey}*",
                "the failure has to name the offending key, or a deployment cannot tell which flag "
                + "is wrong");
    }

    [Fact]
    public void FilterShapedFlagValue_FailsTheHost_RatherThanBeingReadAsDisabled()
    {
        // The exact shape a percentage rollout needs. IFeatureManager would evaluate the filter;
        // reading the same node off IConfiguration yields null, i.e. "off" — so this is the one
        // shape where a silent read would actively contradict what the config author asked for.
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [StorageFlagKey + ":EnabledFor:0:Name"] = "Percentage",
            [StorageFlagKey + ":EnabledFor:0:Parameters:Value"] = "50",
        });

        Action addModules = () => AddWallowModules(services, configuration);

        addModules.Should().Throw<InvalidOperationException>(
                "a module flag is an all-or-nothing startup switch over a module's DI, messaging and "
                + "HTTP registration, evaluated once with no request context for a filter to read")
            .WithMessage($"*{StorageFlagKey}*");
    }

    [Fact]
    public void CoreModuleFlag_IsNeverRead_WhateverShapeItHas()
    {
        // Identity is core, so its flag is inert. That short-circuit has to run BEFORE the shape
        // guard, or a stray core-module key would fail a host that does not even consult it.
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["FeatureManagement:Modules.Identity:EnabledFor:0:Name"] = "AlwaysOn",
        });

        IReadOnlyList<IWallowModule> enabledModules = AddWallowModules(services, configuration);

        enabledModules.Select(module => module.Name).Should().Contain(
            "Identity",
            "a core module is enabled by the registry, not by a flag, so no flag value of any shape "
            + "can turn it off or fail the host");
    }

    /// <summary>
    /// Resolves the enabled set the way <c>ResolveEnabledModules</c> used to: off a live
    /// <see cref="IFeatureManager"/>, built from the same package the host still references.
    /// </summary>
    private static IReadOnlyList<string> ResolveViaFeatureManager(IConfiguration configuration)
    {
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddFeatureManagement();

        using ServiceProvider provider = services.BuildServiceProvider();
        IFeatureManager featureManager = provider.GetRequiredService<IFeatureManager>();

        return
        [
            .. WallowModuleRegistry.All
                .Where(module =>
                    module.IsCore
                    || featureManager.IsEnabledAsync($"Modules.{module.Name}").GetAwaiter().GetResult())
                .Select(module => module.Name)
        ];
    }

    /// <summary>
    /// Resolves the enabled set the way the host does: whatever <c>AddWallowModules</c> returns.
    /// </summary>
    private static IReadOnlyList<string> ResolveViaHost(IConfiguration configuration)
    {
        ServiceCollection services = new();

        return [.. AddWallowModules(services, configuration).Select(module => module.Name)];
    }

    private static IReadOnlyList<IWallowModule> AddWallowModules(
        ServiceCollection services,
        IConfiguration configuration)
    {
        // Modules resolve IConnectionMultiplexer at registration time for Redis-backed services.
        IConnectionMultiplexer mockRedis = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(mockRedis);

        IHostEnvironment environment = new HostingEnvironment
        {
            EnvironmentName = Environments.Development,
        };

        return services.AddWallowModules(configuration, environment);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> flags)
    {
        Dictionary<string, string?> data = new(flags, StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        };

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    /// <summary>
    /// Loads the real, checked-in appsettings files through the same
    /// <see cref="ConfigurationBuilder"/> pipeline the host uses, so the flag shapes under test are
    /// the shapes that actually ship rather than a hand-written copy of them.
    /// </summary>
    private static IConfiguration BuildShippedConfiguration(string overlay)
    {
        string apiDirectory = Path.Combine(GetSolutionRoot(), "src", "Wallow.Api");

        ConfigurationBuilder builder = new();
        builder.AddJsonFile(Path.Combine(apiDirectory, "appsettings.json"), optional: false);
        if (!string.Equals(overlay, "appsettings.json", StringComparison.Ordinal))
        {
            builder.AddJsonFile(Path.Combine(apiDirectory, overlay), optional: false);
        }

        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        });

        return builder.Build();
    }

    private static string GetSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Wallow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Solution root not found");
    }
}
