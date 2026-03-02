# Fork Guide

How to fork Foundry, configure module options, and stay in sync with upstream changes.

---

## Overview

Foundry is designed as a base platform that teams fork and extend. Each fork becomes an independent product while retaining the ability to pull improvements from the upstream Foundry repository.

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

## Configuring Module Options

Foundry ships with five modules: Identity, Storage, Communications, Billing, and Configuration. Each can be enabled, disabled, or customized independently.

### Disabling a module

Remove the module's registration lines from `FoundryModules.cs`:

```csharp
// Comment out or remove:
// services.AddBillingModule(configuration);
// await app.InitializeBillingModuleAsync();
```

Then remove the project references from the solution file and the Api project's `.csproj`.

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

### Adding your own modules

Follow the module creation guide at `docs/claude/module-creation.md`. Key steps:

1. Create four projects (Domain, Application, Infrastructure, Api)
2. Wire Clean Architecture project references
3. Register in `FoundryModules.cs`
4. Add to the solution file

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

- Avoid modifying `Shared.Kernel` and `Shared.Contracts` unless necessary -- these are the highest-conflict areas
- Keep product-specific logic in your own modules, not in the shared/core projects
- Periodically merge upstream to avoid large conflict batches
- When adding features to existing modules, prefer extending over modifying existing code

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
