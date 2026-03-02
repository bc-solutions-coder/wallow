# Fork Guide

How to fork Foundry, configure module options, and stay in sync with upstream changes.

---

## Overview

Foundry is designed as a base platform that teams fork and extend. Each fork becomes an independent product while retaining the ability to pull improvements from the upstream Foundry repository.

```
foundry (upstream)          your-product (fork)
    |                            |
    |-- main <----- PR -------- feature-branches
    |                            |
    |   generic improvements     |   product-specific code
    |   flow back via PR         |   lives only in fork
    |                            |
    v2.0 ---- git merge ------> fork pulls upstream
    v2.1 ---- git merge ------> fork pulls upstream
```

---

## Forking the Repository

### 1. Fork and clone

Use the GitHub UI to fork the repository, then clone your fork:

```bash
git clone git@github.com:your-org/YourProduct.git
cd YourProduct
```

### 2. Rename from Foundry to YourProduct

Every `Foundry.*` namespace, project name, and assembly reference must become `YourProduct.*`.

**Rename directories and project files:**

```bash
# Rename project directories
find src -type d -name 'Foundry.*' | while read dir; do
  mv "$dir" "$(echo "$dir" | sed 's/Foundry\./YourProduct./')"
done

# Rename .csproj files
find src tests -name 'Foundry.*.csproj' | while read f; do
  mv "$f" "$(echo "$f" | sed 's/Foundry\./YourProduct./')"
done

# Rename the solution file
mv Foundry.sln YourProduct.sln
```

**Replace namespace strings across all source files:**

```bash
find . \( -name '*.sln' -o -name '*.csproj' -o -name '*.cs' -o -name '*.json' \
       -o -name 'Dockerfile' -o -name '*.yml' -o -name '*.yaml' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' \
  -exec sed -i '' 's/Foundry\./YourProduct./g' {} +
```

Alternatively, use your IDE's global Find and Replace. JetBrains Rider handles this well with **Edit > Find and Replace in Files**.

### 3. Update configuration files

| File | What to change |
|------|---------------|
| `docker/.env` | `COMPOSE_PROJECT_NAME` |
| `docker/docker-compose.yml` | Network name, container prefixes |
| `appsettings.json` | Keycloak realm and resource names |
| `Dockerfile` | Solution file and DLL references |
| `.github/workflows/*.yml` | Database names, connection strings, deploy paths, image names |

### 4. Update Wolverine assembly scanning

In `Program.cs`, update the assembly prefix filter to match your new namespace:

```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("YourProduct.") == true))
{
    opts.Discovery.IncludeAssembly(assembly);
}
```

### 5. Build and verify

```bash
dotnet restore YourProduct.sln
dotnet build YourProduct.sln
dotnet test
```

Fix any remaining `Foundry` references the compiler surfaces.

---

## Configuring Modules

Foundry ships with five modules: Identity, Storage, Communications, Billing, and Configuration. All modules are enabled by default and can be toggled via configuration -- no source code changes required.

### Enabling and disabling modules

Modules are controlled by the `Foundry:Modules` section in `appsettings.json`. Each key maps to a module name with a boolean value. All default to `true` when omitted:

```json
{
  "Foundry": {
    "Modules": {
      "Identity": true,
      "Billing": true,
      "Communications": true,
      "Storage": true,
      "Configuration": true
    }
  }
}
```

To disable a module, set its value to `false`:

```json
{
  "Foundry": {
    "Modules": {
      "Billing": false,
      "Storage": false
    }
  }
}
```

This is wired in `FoundryModules.cs`, which checks configuration before registering each module:

```csharp
IConfigurationSection modules = configuration.GetSection("Foundry:Modules");

if (modules.GetValue("Identity", defaultValue: true))
    services.AddIdentityModule(configuration);

if (modules.GetValue("Billing", defaultValue: true))
    services.AddBillingModule(configuration);
```

When a module is disabled, its DI services, database migrations, API controllers, and Wolverine handlers are all excluded from the application.

### Module-specific configuration

Each module reads its own configuration section from `appsettings.json`. Common patterns:

```json
{
  "Email": {
    "Provider": "Smtp",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Sms": {
    "Provider": "Null"
  }
}
```

Provider implementations are swapped via DI registration in each module's Infrastructure layer. See `docs/claude/communications-channels.md` for the provider abstraction pattern.

### Environment-specific overrides

Use `appsettings.{Environment}.json` or environment variables to configure modules per deployment target:

```bash
# Disable billing in development
Foundry__Modules__Billing=false

# Configure email provider in production
Email__Provider=SendGrid
Email__SendGrid__ApiKey=your-key
```

### Adding your own modules

Follow the module creation guide at `docs/claude/module-creation.md`. Key steps:

1. Create four projects (Domain, Application, Infrastructure, Api)
2. Wire Clean Architecture project references
3. Register in `FoundryModules.cs`
4. Add to the solution file

---

## Adding Plugins and Extensions

Foundry includes a plugin system for product-specific extensions that load dynamically without modifying core code. Plugins are the recommended way to add fork-specific functionality because they don't create merge conflicts when syncing upstream.

### Plugin structure

A plugin is a .NET class library that implements `IFoundryPlugin` and ships with a `plugin.json` manifest:

```
plugins/
  your-plugin/
    plugin.json
    YourPlugin.dll
```

**Manifest (`plugin.json`):**

```json
{
  "id": "your-plugin",
  "name": "Your Plugin",
  "version": "1.0.0",
  "description": "Product-specific extension",
  "author": "Your Team",
  "minFoundryVersion": "0.2.0",
  "entryAssembly": "YourPlugin.dll",
  "dependencies": [],
  "requiredPermissions": ["storage:read", "messaging:send"],
  "exportedServices": []
}
```

**Plugin entry point:**

```csharp
public class YourPlugin : IFoundryPlugin
{
    public PluginManifest Manifest => // loaded from plugin.json

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register your DI services
    }

    public Task InitializeAsync(PluginContext context)
    {
        // Run startup logic
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        // Cleanup
        return Task.CompletedTask;
    }
}
```

### Plugin configuration

Configure the plugin system in `appsettings.json`:

```json
{
  "Plugins": {
    "PluginsDirectory": "plugins/",
    "AutoDiscover": true,
    "AutoEnable": false,
    "Permissions": {
      "your-plugin": ["storage:read", "messaging:send"]
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `PluginsDirectory` | `plugins/` | Directory to scan for plugin assemblies |
| `AutoDiscover` | `true` | Automatically discover plugins on startup |
| `AutoEnable` | `false` | Automatically load all discovered plugins |
| `Permissions` | `{}` | Per-plugin permission grants |

Plugins are loaded in an isolated `AssemblyLoadContext`, so they cannot interfere with core module assemblies.

### When to use plugins vs modules

| Use case | Approach |
|----------|----------|
| Generic capability useful across products | Module in core Foundry |
| Product-specific feature that only your fork needs | Plugin |
| Feature you want to develop in your fork and later contribute upstream | Start as a plugin, then convert to a module when contributing |

---

## Syncing Upstream Changes

### Initial setup (one-time)

```bash
git remote add upstream https://github.com/your-org/Foundry.git
git fetch upstream
```

### Pulling updates

```bash
git fetch upstream
git checkout main
git merge upstream/main
```

### Resolving conflicts

Conflicts typically occur in files where you renamed `Foundry` to `YourProduct`. The recommended workflow:

1. Accept the upstream version of the conflicted file
2. Re-apply the `Foundry -> YourProduct` replacement on that file
3. Review the diff to confirm the upstream logic change was preserved

For large upstream merges, consider cherry-picking specific commits:

```bash
git cherry-pick <commit-hash>
```

### Reducing merge friction

- **Avoid modifying shared projects** -- `Shared.Kernel` and `Shared.Contracts` are the highest-conflict areas. Extend them sparingly.
- **Keep product-specific logic in plugins or your own modules** -- not in core projects.
- **Merge upstream regularly** -- small, frequent merges are easier than large catch-up merges.
- **Prefer extending over modifying** -- when adding features to existing modules, add new files rather than editing existing ones where possible.
- **Track upstream-intended commits** -- prefix commits meant for contribution with `[foundry]` in the commit message for easy identification.

### Recommended sync cadence

| Stage | Cadence |
|-------|---------|
| Active upstream development | Weekly merge |
| Stable upstream, active fork development | Bi-weekly merge |
| Both stable | Monthly merge or on release tags |

---

## Contributing Changes Back Upstream

When you build something generic in your fork that would benefit the base platform, contribute it back via pull request.

### Workflow

1. **Build the feature in your fork** -- develop and validate it in your product context.
2. **Identify generic vs product-specific parts** -- separate business logic that is product-specific from infrastructure that is reusable.
3. **Re-implement generically in a clean branch off upstream/main:**

```bash
git fetch upstream
git checkout -b feat/my-feature upstream/main
# Implement the generic version
git push origin feat/my-feature
```

4. **Open a PR against the upstream repository** following Foundry's commit conventions (`feat:`, `fix:`, etc.).
5. **After the PR is merged**, sync upstream into your fork to replace your fork-specific version with the upstream one:

```bash
git fetch upstream
git checkout main
git merge upstream/main
git push origin main
```

### Guidelines for upstream contributions

- Remove all product-specific references, naming, and configuration.
- Follow the existing module patterns: Clean Architecture layers, strongly-typed IDs, Result pattern.
- Include tests matching the upstream coverage standards (90% minimum).
- Integration events go in `Shared.Contracts`. Domain logic stays within the module.
- Update documentation in `docs/` if adding a new module or significant feature.

---

## Checklist

- [ ] Fork created and cloned
- [ ] All `Foundry.*` references renamed to `YourProduct.*`
- [ ] Solution file renamed and project paths updated
- [ ] Docker Compose configuration updated
- [ ] Keycloak realm configuration updated
- [ ] CI/CD workflows updated
- [ ] Dockerfile updated
- [ ] Wolverine assembly scanning prefix updated
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] Upstream remote added for future syncing
- [ ] Module toggles configured in `appsettings.json`
- [ ] Plugin directory set up (if using plugins)
