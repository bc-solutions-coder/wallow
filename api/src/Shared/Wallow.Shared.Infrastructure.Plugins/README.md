# Wallow.Shared.Infrastructure.Plugins

The plugin system: discovery, isolated loading, and lifecycle.

- `PluginLoader`, `PluginAssemblyLoadContext`, `PluginManifestLoader` — discovery and isolated load
- `PluginRegistry` / `PluginRegistryEntry` — what is loaded
- `PluginLifecycleManager` — start/stop
- `PluginPermissionValidator` — permission checks against the manifest
- `PluginOptions`, `PluginServiceExtensions` — configuration and DI

`WallowModules.InitializeWallowPluginsAsync()` in the API host drives this at startup.
