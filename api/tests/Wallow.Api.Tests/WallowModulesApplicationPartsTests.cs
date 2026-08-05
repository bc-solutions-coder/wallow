using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.ApiKeys.Api.Controllers;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Storage.Api.Controllers;

namespace Wallow.Api.Tests;

/// <summary>
/// The unit half of "a disabled module has no HTTP surface": whether the host's part manager still
/// carries the <see cref="ApplicationPart"/> of a module that is switched off.
/// </summary>
/// <remarks>
/// Deliberately keyed on assembly identity, not on a name or a namespace string — the parts are built
/// from real controller types in the two real module assemblies, so a test that passes here cannot be
/// passing because two strings happened to match. The enabled set is a stub module declared by this
/// test rather than a production module instance, so the assertion is about the method's own behaviour
/// and not about what the registry currently contains.
/// </remarks>
public sealed class WallowModulesApplicationPartsTests
{
    private static readonly Assembly _apiKeysApiAssembly = typeof(ApiKeysController).Assembly;
    private static readonly Assembly _storageApiAssembly = typeof(StorageController).Assembly;
    private static readonly Assembly _hostAssembly = typeof(WallowModules).Assembly;

    [Fact]
    public void RemoveDisabledModuleApiParts_RemovesTheApiAssemblyPartOfADisabledModule()
    {
        ApplicationPartManager manager = CreateManagerWithParts(
            _apiKeysApiAssembly, _storageApiAssembly);

        WallowModules.RemoveDisabledModuleApiParts(manager, [new StubModule("Storage")]);

        AssembliesOf(manager).Should().NotContain(
            _apiKeysApiAssembly,
            "ApiKeys is not in the enabled set, so its controllers must not reach the route table at all");
    }

    [Fact]
    public void RemoveDisabledModuleApiParts_KeepsTheApiAssemblyPartOfAnEnabledModule()
    {
        ApplicationPartManager manager = CreateManagerWithParts(
            _apiKeysApiAssembly, _storageApiAssembly);

        WallowModules.RemoveDisabledModuleApiParts(manager, [new StubModule("Storage")]);

        AssembliesOf(manager).Should().Contain(
            _storageApiAssembly,
            "an enabled module's HTTP surface must be untouched by the gate");
    }

    [Fact]
    public void RemoveDisabledModuleApiParts_LeavesAnAssemblyItDoesNotOwnAlone()
    {
        // The host's own assembly belongs to no module. Only assemblies the method explicitly claims
        // as a module's .Api assembly may ever be removal candidates.
        ApplicationPartManager manager = CreateManagerWithParts(_hostAssembly);

        WallowModules.RemoveDisabledModuleApiParts(manager, []);

        AssembliesOf(manager).Should().Contain(
            _hostAssembly,
            "an assembly that belongs to no module must survive even when no module is enabled");
    }

    [Fact]
    public void RemoveDisabledModuleApiParts_RemovesEveryOptionalModulePartWhenNothingIsEnabled()
    {
        ApplicationPartManager manager = CreateManagerWithParts(
            _apiKeysApiAssembly, _storageApiAssembly);

        WallowModules.RemoveDisabledModuleApiParts(manager, []);

        AssembliesOf(manager).Should().BeEmpty(
            "with no module enabled, no module's .Api assembly may remain an application part");
    }

    private static ApplicationPartManager CreateManagerWithParts(params Assembly[] assemblies)
    {
        ApplicationPartManager manager = new();

        foreach (Assembly assembly in assemblies)
        {
            manager.ApplicationParts.Add(new AssemblyPart(assembly));
        }

        return manager;
    }

    private static IEnumerable<Assembly> AssembliesOf(ApplicationPartManager manager) =>
        manager.ApplicationParts.OfType<AssemblyPart>().Select(part => part.Assembly);

    /// <summary>A module that exists only inside this test assembly; only its name is read.</summary>
    private sealed class StubModule(string name) : IWallowModule
    {
        public string Name => name;

        public bool IsCore => false;

        public IReadOnlyList<Assembly> HandlerAssemblies => [];

        public IReadOnlyList<Type> DbContextTypes => [];

        public string SchemaName => name.ToLowerInvariant();

        public IServiceCollection AddServices(
            IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment) => services;
    }
}
