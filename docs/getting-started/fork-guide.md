# Fork Guide

How to fork Wallow, configure modules, add new functionality, and stay in sync with upstream changes.

---

## Overview

Wallow is designed as a base platform that teams fork and extend. Each fork becomes an independent product while retaining the ability to pull improvements from the upstream Wallow repository.

```
wallow (upstream)          your-product (fork)
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

## Prerequisites

- .NET 10 SDK
- Node 24 (see `.nvmrc`) and pnpm 11.24.0 — the React apps and every shared package live in a pnpm workspace, and Approach A's very first customization step edits a file inside it
- Docker and Docker Compose
- PostgreSQL (via Docker or standalone)
- Git

---

## Trademark and Attribution

See the `NOTICE` file in the repository root. "Wallow" is a trademark of BC Solutions Ltd. If you distribute a derivative product under a different name, you must:

- Remove or replace the Wallow name and branding (see Approach B below)
- Retain the `NOTICE` file and MIT license attribution
- Not use "Wallow" in your product name without written permission

---

## Approach A (Recommended): Keep Namespaces, Customize via Config

The simplest fork strategy is to **keep all `Wallow.*` namespaces unchanged** and customize the user-facing product identity through configuration only:

1. **Fork and clone** the repository
2. **Edit `packages/styles/branding.json`** to set your product name, icon, tagline, landing-page toggle, and theme colors
3. **Edit `api/src/Wallow.Api/appsettings.json`** to configure connection strings, the SMTP sender name, and the OpenTelemetry service name
4. **Edit `api/seed.json`** to set your bootstrap tenant, roles, and admin account
5. **Set up the merge driver** so upstream merges don't overwrite your config (see "Merge Driver Setup" below)

This approach has **zero risk of silent failures** and gives you the easiest upstream sync path. All fork identity in the React apps -- page titles, auth screens, theme colors -- is resolved from `packages/styles/branding.json` by `packages/styles`, so no source changes are needed. See the [Configuration Guide](configuration.md) for the full key reference.

### Merge Driver Setup

The repository ships a `.gitattributes` that marks fork-owned files with `merge=ours`. Activate the merge driver:

```bash
git config merge.ours.driver true
```

The entries it covers are:

| Pattern | What it protects |
|---------|------------------|
| `appsettings*.json` | Every app settings file, in every project |
| `branding.json` | Fork branding and theme (`packages/styles/branding.json`) |
| `docker/.env` | Your local Compose credentials |
| `docker/.env.example` | Fork-specific additions to the example env |
| `seed.json` | Bootstrap tenant, roles, and admin (`api/seed.json`) |

These files keep your fork's version during upstream merges. Anything outside this list -- including `CLAUDE.md` and `.claude/**` -- merges normally, so expect to resolve conflicts there yourself.

#### The protection is unconditional -- diff these files after every sync

`merge=ours` does not weigh the two sides. It resolves the file to your fork's version and reports
success: no conflict markers, no merge message, nothing in `git status`. That is exactly what you
want for the *values* in these files -- your connection strings, your bootstrap admin, your palette
-- but it applies just as unconditionally to upstream changes to a file's *shape*: a key upstream
renames, a key upstream deletes, a block upstream adds. Your fork keeps whatever it had, and the
merge looks clean.

So make "diff the protected files against upstream" a step in every sync. After merging:

```bash
git ls-files | git check-attr --stdin merge | grep ': merge: ours$' | sed 's/: merge: ours$//' \
  | xargs git diff upstream/main --
```

That asks git itself which tracked paths carry `merge=ours` -- so it stays correct if the pattern
list grows, and it expands `appsettings*.json` to every environment overlay -- then diffs each one
against upstream's copy. `docker/.env` never appears because it is untracked by design; diff it
against `docker/.env.example` instead. Everything the command prints is either a deliberate fork
customization or drift you have not reconciled yet, and only you can tell those apart, so read the
output rather than automate it. Reconciling is manual by definition: hand-apply the upstream key
changes you want, and leave your own values in place.

**Worked example -- the `Modules.*` flag cleanup.** Upstream deleted two `FeatureManagement` keys
from `appsettings.json` and `appsettings.Production.json` -- `Modules.Identity`, which never did
anything because Identity is a core module and is registered regardless, and
`Modules.Configuration`, which named a module that has never existed -- along with a dead top-level
`Wallow:Modules` block that nothing read. A fork that merges that change keeps all three. They are
inert, so the application behaves identically; they are also a lie about which modules exist, and
`Wallow.Architecture.Tests` now asserts otherwise. Its `ModuleFeatureFlagTests` loads the real
checked-in `appsettings*.json` files and fails if the declared `Modules.*` set is anything but
exactly the non-core modules in `WallowModuleRegistry.All`, or if a second `Wallow:Modules` block is
present. An unreconciled fork therefore gets a red test suite with no compile error and nothing
pointing back at the merge that caused it. Delete the stale keys from your copy and it goes green;
the flags that legitimately remain are described under [Configuring Modules](#configuring-modules).

The same trap bites `branding.json` in the other direction -- a fork whose palette predates a token
upstream *added* never receives it -- which is why new theme tokens ship with a fallback. See the
[Configuration Guide](configuration.md).

---

## Approach B (Advanced): Full Namespace Rename

If you need to remove all `Wallow` references from source code (e.g., for white-label distribution), follow this approach. **Be aware that namespace renaming can cause silent failures** — see the checklist below.

### 1. Fork and clone

```bash
git clone git@github.com:your-org/YourProduct.git
cd YourProduct
```

The .NET side of the repository lives entirely under `api/`: the solution is `api/Wallow.slnx`, projects are under `api/src/`, and test projects under `api/tests/`. The commands below assume you run them from the repository root.

### 2. Rename the solution file

```bash
mv api/Wallow.slnx api/YourProduct.slnx
```

### 3. Rename namespaces across the codebase

Every `Wallow.*` namespace, project name, and assembly reference must become `YourProduct.*`.

**Rename directories and project files:**

```bash
# Rename project directories (deepest first, so parents stay valid mid-loop)
find api/src api/tests -depth -type d -name 'Wallow.*' | while read dir; do
  mv "$dir" "$(echo "$dir" | sed 's/Wallow\./YourProduct./')"
done

# Rename .csproj files
find api/src api/tests -name 'Wallow.*.csproj' | while read f; do
  mv "$f" "$(echo "$f" | sed 's/Wallow\./YourProduct./')"
done
```

**Replace namespace strings in all source files:**

```bash
find api \( -name '*.slnx' -o -name '*.csproj' -o -name '*.props' -o -name '*.cs' \
       -o -name '*.json' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' \
  -exec sed -i '' 's/Wallow\./YourProduct./g' {} +

# Catch standalone "Wallow" references (log messages, display names, etc.)
# Review these manually — some may be intentional:
grep -rl '"Wallow"' api --include='*.cs' --include='*.json' \
  --exclude-dir=bin --exclude-dir=obj
```

Alternatively, use your IDE's global Find and Replace. JetBrains Rider handles this well with **Edit > Find and Replace in Files**.

### 4. Update the solution file references

Open `api/YourProduct.slnx` and verify all project paths point to the renamed `.csproj` files. The `sed` pass above should handle this, but confirm with:

```bash
grep 'Wallow\.' api/YourProduct.slnx
```

Should return nothing.

### 5. Update configuration and build files

| File | What to change |
|------|---------------|
| `docker/.env` | `COMPOSE_PROJECT_NAME` |
| `docker/docker-compose.yml` | Network name, container prefixes |
| `docker/docker-compose.production.yml` | Image names (`ghcr.io/<org>/<image>`), container names |
| `api/src/Wallow.Api/appsettings.json` | `OpenTelemetry:ServiceName`, `Smtp:DefaultFromName` |
| `packages/styles/branding.json` | `appName`, `tagline`, `appIcon` |
| `api/Directory.Build.props`, `api/Directory.Packages.props` | Any hardcoded product name or assembly prefix |
| `.github/workflows/*.yml` | Database names, connection strings, deploy paths, image names |

**There is no root `Dockerfile`.** The .NET images are produced by the .NET SDK container tooling -- `dotnet publish /t:PublishContainer` in `.github/workflows/ci.yml` and `deploy.yml` -- driven by the `<ContainerRepository>` properties in `api/src/Wallow.Api/Wallow.Api.csproj`, `Wallow.MigrationService.csproj`, and `Wallow.SeederService.csproj`. Rename those properties rather than editing a Dockerfile. The only Dockerfiles in the repository build the React apps (`apps/wallow-web/Dockerfile`, `apps/wallow-auth/Dockerfile`) and supporting infrastructure images (`docker/docs/`, `docker/images/*/`).

### 6. Rename the frontend workspace (optional)

Renaming the .NET namespaces does not touch the pnpm workspace. If you also want to re-scope the TypeScript packages, change the `name` fields in each `packages/*/package.json` and `apps/*/package.json` from `@bc-solutions-coder/*` to your own scope, update the matching `workspace:*` dependency keys, update `.npmrc` for your registry, and re-run `pnpm install` to regenerate the lockfile.

### 7. Handler discovery — nothing to rename

Wolverine's handler discovery has **no assembly-name prefix to update**. `Program.cs` builds its
discovery list from `enabledModules.SelectMany(module => module.HandlerAssemblies)`, and each module
declares its own assemblies with `typeof(...)` anchors inside its `IWallowModule` implementation
(`api/src/Modules/{Module}/*.Infrastructure/Modules/{Module}Module.cs`). Renaming namespaces moves
those anchors and the discovery list follows automatically — a rename that broke one would be a
compile error, not a silently empty scan.

### 8. Build and verify

```bash
dotnet restore api/YourProduct.slnx
dotnet build api/YourProduct.slnx
./scripts/run-tests.sh
```

Fix any remaining `Wallow` references the compiler surfaces.

### Silent Failure Checklist

These components reference `"Wallow"` as a string literal and will **fail silently** if you rename namespaces but miss them:

| Component | File | What breaks |
|-----------|------|-------------|
| **ModuleEnricher** | `api/src/Wallow.Api/Logging/ModuleEnricher.cs` | Log enrichment stops tagging module names |
| **OpenTelemetry ServiceName** | `api/src/Wallow.Api/appsettings*.json` → `OpenTelemetry:ServiceName` | Traces/metrics report wrong service name |
| **Diagnostics ActivitySource** | `api/src/Shared/Wallow.Shared.Kernel/Diagnostics.cs` | Custom traces stop appearing (`new ActivitySource("Wallow")`) |
| **SMTP DefaultFromName** | `api/src/Wallow.Api/appsettings.json` → `Smtp:DefaultFromName` | Emails show "Wallow" as sender |
| **branding.json** | `packages/styles/branding.json` → `appName` | React app titles and auth screens show "Wallow" |
| **Email templates** | `SimpleEmailTemplateService` in the Notifications module (`api/src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/`) | Email bodies may contain hardcoded product name |
| **Container repositories** | `<ContainerRepository>` in `Wallow.Api.csproj`, `Wallow.MigrationService.csproj`, `Wallow.SeederService.csproj` | Published images keep the upstream image names |

After renaming, search for remaining literal references:

```bash
grep -r '"Wallow"' --include='*.cs' --include='*.json' \
  --exclude-dir=bin --exclude-dir=obj --exclude-dir=node_modules .
```

---

## Frontend Authentication Policy: BFF-only

Every frontend in this repository authenticates through a **Backend-For-Frontend**: the browser
never holds an access token, a confidential server-side client runs the authorization-code flow,
and the token set lives in a sealed `httpOnly` session cookie. Your fork inherits that default,
so it is worth stating plainly what kind of rule it is.

**It is a policy choice, not a standards mandate.** The IETF's *OAuth 2.0 for Browser-Based
Applications* is still an Internet-Draft (`draft-ietf-oauth-browser-based-apps`) in the RFC
Editor queue, with no RFC number assigned — treat any doc or commit message claiming otherwise
as wrong. That draft *ranks* three architectures in decreasing order of security rather than
mandating one:

1. **Backend-For-Frontend** — "strongly recommended for business applications, sensitive
   applications, and applications that handle personal data"
2. **Token-mediating backend** — the server holds the tokens but the browser drives the calls
3. **Browser-based public client** — tokens in the browser, PKCE only

Wallow adopts tier 1 for everything and does not ship the other two. That is a layer of policy
on top of the ranking, and it is the right default for a fork-first platform: every downstream
deployment inherits whatever this repository chooses, so the choice should be the one that is
safe when nobody revisits it.

**The argument that decides it is §5.1.3 of the draft.** Even with browser tokens protected
perfectly, an attacker with XSS on your origin can run a *silent* authorization-code flow in a
hidden iframe and mint entirely fresh tokens of their own. The draft is blunt that there are no
practical countermeasures for a frontend in that position — short token lifetimes and refresh
rotation do not help, because the attacker is not stealing your token, they are getting their
own. Only a confidential-client BFF defeats it: the attacker obtains an authorization code they
cannot exchange without the server-side secret.

**If your fork needs a different tier**, the escape hatch is a token-mediating backend (the
Curity "token handler" pattern) — the server still owns the tokens and the confidential client,
but hands the browser short-lived, narrowly-scoped credentials. Taking it means owning that
decision explicitly:

- Keep the confidential client and the server-side token store. Do not move a refresh token into
  the browser under any circumstances.
- Audience-restrict and scope-narrow whatever the browser does receive, so an XSS compromise
  yields the smallest possible authority.
- Document the deviation in your fork's own docs. Upstream's guides, defaults, and E2E fixtures
  all assume the BFF, and a silent divergence is how a deployment ends up with neither model
  implemented completely.

The mechanics of the supported path — mounting the tunnel, the CSRF gate, session stores, and
per-request SDK instances — are in the
[BFF Pattern](../integrations/bff-pattern.md) and
[TypeScript SDK](../integrations/typescript-sdk.md) guides, with a start-to-finish walkthrough in
the [Integration Cookbook](../integrations/integration-cookbook.md).

---

## Origins, Issuer, and the Ingress Contract

Rebranding a fork is configuration-only, but **re-hosting** one is not: the moment you change a
hostname, a port, or a path prefix, you are editing one member of a coupled set. The API, the auth
app, and every BFF have to agree on which origin is the OIDC issuer, and the OAuth client records
have to agree on where the browser is allowed to be sent back. Change one and leave the rest and the
deployment still builds, still boots, and still passes health checks — it just fails at login.

### Change one origin, change all of them

| If you move…              | Also update                                                                                                                                                                                                                    |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **The API's public URL**  | `API_PUBLIC_URL` (production compose feeds it to both `OpenIddict__Issuer` and the web app's `OIDC_ISSUER`), and `API_PATH_BASE` if the prefix changed.                                                                         |
| **The auth app's origin** | `AUTH_PUBLIC_URL`, the API's `AuthUrl` (`appsettings*.json` — this is the issuer fallback when `OpenIddict:Issuer` is unset), and `AUTH_BASE_PATH` if the prefix changed. `AUTH_BASE_PATH` is a **build** argument, not runtime. |
| **A frontend's origin**   | That app's `OIDC_REDIRECT_URI` and `OIDC_POST_LOGOUT_REDIRECT_URI`, **and** the URIs registered on its OAuth client — `redirectUris` / `postLogoutRedirectUris` in `api/seed.json` for seeded clients, or the application's settings in the dashboard. |
| **The parent domain**     | `COOKIE_DOMAIN` (`Authentication__CookieDomain`). It scopes the API's identity cookies, and a leading-dot value widens them to **every** subdomain of that parent, present and future. Set it as narrowly as your topology allows. Values per topology: [Reverse Proxy → Required Configuration](../operations/reverse-proxy.md#2-required-configuration-per-service). |

Prefer deriving these from one variable over setting each by hand — that is why the production
compose reads `API_PUBLIC_URL` in two places instead of taking two independent inputs. Whether the
issuer is the API's origin or the auth app's is an environment-by-environment decision, and this
repository answers it differently in dev, E2E, and production; the table and the reasoning are in
[The Issuer and Origin Contract](../integrations/bff-pattern.md#the-issuer-and-origin-contract).

### Giving another app a path prefix

Only `wallow-auth` currently has a base-path knob (`AUTH_BASE_PATH`). If you put `wallow-web` or
`minimal-app` behind a path prefix too, the prefix has to reach that app's **branding assets**, not
just its router. Those two apps import `appIconUrl` and `forkResolvedBranding` from
`@bc-solutions-coder/styles` directly, and the package resolves them at the **site** root: it ships
a prebuilt bundle, so its own `import.meta.env.BASE_URL` is whatever it was when the package was
built, never yours. Behind a path-based ingress the site root is a *different app*, so the icons
silently resolve into it.

Mirror `apps/wallow-auth/src/shared/lib/branding.ts`: one per-app module that hands the prefix to
the package's base-path-aware functions (`toAppIconUrl`, `resolveForkBranding`, and
`mergeClientBranding` for fetched client branding) exactly once, with every screen importing
branding from that module rather than from the package. The string arithmetic itself —
`normalizeBasePath`, `toViteBase`, `stripBasePath`, `withBasePath` — is already shared in
`@bc-solutions-coder/env/base-path`; only the binding is per-app. Note that the prefix is a **build**
argument, not a runtime one, for the reason given in `apps/wallow-auth/src/shared/lib/base-path.ts`.

### Two contracts a fork must not break silently

Both of these hold regardless of how you rebrand or re-host, and both fail in ways that do not point
back at the change that caused them.

1. **Your ingress must send `X-Forwarded-Proto: https`, and the frontends must trust it.** The
   reference stack ships a Caddy ingress
   ([Deployment → Routing Topologies](../operations/deployment.md#2-routing-topologies)), and
   replacing it is supported — but the replacement inherits this requirement. Without the header the
   API builds `http://` redirect URIs and discovery documents, `Secure` cookie logic misreads the
   connection, and server-rendered queries compute a different base URL than the browser does, so
   hydration re-fetches instead of reusing. The Node apps believe the header only from a peer inside
   `WALLOW_TRUSTED_PROXIES` (the production compose defaults it to `private`), so an ingress on an
   address outside that list re-creates the same symptoms with the header present. Details on the proxy side are in
   [Reverse Proxy → Forwarded Headers](../operations/reverse-proxy.md#4-forwarded-headers); the
   frontend consequences are in
   [What the BFF requires from your ingress](../integrations/bff-pattern.md#what-the-bff-requires-from-your-ingress).
2. **The OIDC callback must stay a top-level GET redirect.** The login-transaction cookie holding
   the PKCE verifier, `state`, and `nonce` is written `SameSite=Lax`, which survives a top-level
   navigation and nothing else. Switching to `response_mode=form_post`, or running the flow in an
   iframe, means the cookie is never sent and every callback 400s. See
   [The Callback Must Stay a Top-Level GET Redirect](../integrations/bff-pattern.md#the-callback-must-stay-a-top-level-get-redirect).

---

## Data Protection (GDPR)

If you operate a fork that processes personal data of EU residents, you are the **data controller**. Key responsibilities:

- Update the privacy policy (`/privacy` page) to reflect your organization
- Configure data retention policies appropriate for your jurisdiction
- Ensure the `NOTICE` file attribution does not imply upstream Wallow is the data processor
- Review tenant data isolation — each module uses separate PostgreSQL schemas with query filters on `TenantId`

---

## Configuring Modules

Wallow ships with multiple modules. Most are enabled by default and can be toggled via feature flags -- no source code changes required. Identity is always registered (not behind a feature flag).

### Enabling and disabling modules

Modules are controlled by the `FeatureManagement` section in `appsettings.json`. Each key maps to `Modules.{ModuleName}` with a boolean value:

```json
{
  "FeatureManagement": {
    "Modules.Branding": true,
    "Modules.Storage": true,
    "Modules.Notifications": true,
    "Modules.Announcements": true,
    "Modules.Inquiries": true,
    "Modules.ApiKeys": false
  }
}
```

Only the six optional modules appear. Identity is a core module: it is always registered, so it has no flag and adding one would toggle nothing.

To disable a module, set its value to `false`:

```json
{
  "FeatureManagement": {
    "Modules.Announcements": false
  }
}
```

This is wired in `WallowModules.cs`: `IsModuleFlagEnabled` reads `FeatureManagement:Modules.{Name}` straight off `IConfiguration` once at startup, and filters `WallowModuleRegistry.All` down to the enabled set. Identity is always registered as a required platform dependency. When a module is disabled, its DI services, HTTP controllers, and Wolverine handlers are all excluded from the application — the endpoints return 404 and are absent from the OpenAPI document.

The value must be a **scalar boolean**. An absent key reads as disabled, but a key present as anything other than `true`/`false` — a `Microsoft.FeatureManagement` filter object with `EnabledFor` or `RequirementType`, say — makes the host **refuse to start**. A module flag is read once at startup, so there is no request in flight for a filter to evaluate against, and a module that silently disabled itself would take its endpoints with it. Gate request-scoped behaviour *inside* the module instead of switching the module off.

`IFeatureManager` itself is still registered (`services.AddFeatureManagement()`) and is still the supported extension point for **your fork's own** feature flags — inject it in your handlers and controllers as usual. It is only Wallow's module enable/disable gating that no longer goes through it.

Its **database schema is still migrated**, though: `Wallow.MigrationService` deliberately ignores feature flags and migrates every module the platform ships. A fork that disables a module can therefore re-enable it later without a manual migration step.

### Module-specific configuration

Each module reads its own configuration section from `appsettings.json`. See the [Configuration Guide](configuration.md) for the sections a fork is most likely to change (`Smtp`, `Storage`, `OpenTelemetry`, etc.); `appsettings.json` itself remains the complete list.

### Environment-specific overrides

Use `appsettings.{Environment}.json` or environment variables to configure modules per deployment target:

```bash
# Disable announcements in development
FeatureManagement__Modules.Announcements=false

# Configure SMTP for production
Smtp__Host=smtp.example.com
Smtp__Port=587
Smtp__UseSsl=true
```

---

## Adding a New Module

### 1. Create the module directory structure

```
api/src/Modules/YourModule/
  YourProduct.YourModule.Domain/
  YourProduct.YourModule.Application/
  YourProduct.YourModule.Infrastructure/
  YourProduct.YourModule.Api/
```

### 2. Create the four projects

```bash
cd api/src/Modules/YourModule

dotnet new classlib -n YourProduct.YourModule.Domain
dotnet new classlib -n YourProduct.YourModule.Application
dotnet new classlib -n YourProduct.YourModule.Infrastructure
dotnet new classlib -n YourProduct.YourModule.Api
```

### 3. Wire up project references (Clean Architecture)

```bash
# Application depends on Domain
dotnet add YourProduct.YourModule.Application reference YourProduct.YourModule.Domain

# Infrastructure depends on Application (and transitively Domain)
dotnet add YourProduct.YourModule.Infrastructure reference YourProduct.YourModule.Application

# Api depends on Application
dotnet add YourProduct.YourModule.Api reference YourProduct.YourModule.Application

# Infrastructure also needs Shared.Kernel for base classes
dotnet add YourProduct.YourModule.Infrastructure reference ../../Shared/YourProduct.Shared.Kernel
```

Domain has **no** project references.

**Api must not reference its own module's Infrastructure.** `CleanArchitectureTests.ApiLayer_ShouldNotDependOn_InfrastructureLayer` asserts exactly that, per module. The module's DI registration therefore belongs in Infrastructure — an `AddYourModuleInfrastructure(...)` extension that the composition root (`Wallow.Api`) calls — not in the module's Api project.

The test reads IL rather than the project file, so an unused `ProjectReference` slips past it (`Wallow.Identity.Api.csproj` carries one and still passes). Do not treat that as permission: add the reference and the first type you use across the boundary fails the suite.

### 4. Add shared events

Create integration event records in:

```
api/src/Shared/YourProduct.Shared.Contracts/YourModule/Events/
```

Example:

```csharp
namespace YourProduct.Shared.Contracts.YourModule.Events;

public sealed record SomethingHappenedEvent : IntegrationEvent
{
    public required Guid SomethingId { get; init; }
    public required string Name { get; init; }
}
```

The `IntegrationEvent` base record provides `EventId` and `OccurredAt` automatically.

### 5. Register the module

Implement `IWallowModule` once, in the module's Infrastructure layer
(`Modules/YourModuleModule.cs`):

```csharp
public sealed class YourModuleModule : IWallowModule
{
    internal const string Schema = "yourmodule";

    public string Name => "YourModule";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(CreateSomethingHandler).Assembly,  // .Application
        typeof(YourModuleModule).Assembly,        // .Infrastructure
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(YourModuleDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) =>
        services.AddYourModuleModule(configuration);
}
```

Then add one entry to `WallowModuleRegistry.All` in
`api/src/Wallow.Modules.Registry/WallowModuleRegistry.cs`:

```csharp
new YourModuleModule(),
```

That is the whole registration — there is no per-module `if` block and no startup hook in
`WallowModules.cs`. Both hosts read that one list: `Wallow.Api` filters it by the module's
`FeatureManagement:Modules.YourModule` flag, and `Wallow.MigrationService` takes it unfiltered. The
one remaining host-side edit is a row in `_moduleApiAssemblies` in `WallowModules.cs` naming one of
your module's controller types, so a disabled module's routes can be removed; the host refuses to
start if that table and the registry disagree.

### 6. Add to the solution file

```bash
dotnet sln api/YourProduct.slnx add api/src/Modules/YourModule/YourProduct.YourModule.Domain
dotnet sln api/YourProduct.slnx add api/src/Modules/YourModule/YourProduct.YourModule.Application
dotnet sln api/YourProduct.slnx add api/src/Modules/YourModule/YourProduct.YourModule.Infrastructure
dotnet sln api/YourProduct.slnx add api/src/Modules/YourModule/YourProduct.YourModule.Api
```

### 7. Handler discovery (automatic within the module)

Wolverine discovers handlers in exactly the assemblies your module declared in `HandlerAssemblies` in step 5 — no assembly scan, no name prefix. Because that list names both the `.Application` and the `.Infrastructure` assembly, any handler you add to either project is picked up with no further registration. Messaging uses the in-memory transport, so no manual routing configuration is needed either. Just create handlers following Wolverine conventions:

```csharp
public static class CreateSomethingHandler
{
    public static async Task<Result<SomethingDto>> HandleAsync(
        CreateSomethingCommand command,
        ISomethingRepository repo,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

No manual assembly registration is required.

For more detail, see the [Module Creation Guide](../architecture/module-creation.md).

---

## Configuring Multi-Tenancy for New Modules

Every module that stores tenant-specific data must integrate with the multi-tenancy infrastructure from `Shared.Kernel`.

### 1. Mark domain entities as tenant-scoped

Implement `ITenantScoped` on any entity that belongs to a tenant:

```csharp
using YourProduct.Shared.Kernel.MultiTenancy;

public class Order : AggregateRoot, ITenantScoped
{
    public string OrderNumber { get; private set; }
    public TenantId TenantId { get; set; }
}
```

### 2. Derive from `TenantAwareDbContext<T>`

Do **not** hand-write `HasQueryFilter` per entity. `TenantAwareDbContext<T>` (namespace `Wallow.Shared.Infrastructure.Core.Persistence`) carries the tenant plumbing, and one call to `ApplyTenantQueryFilters(modelBuilder)` applies the filter to every entity that implements the tenant-scoped interface — including the ones you add later:

```csharp
public sealed class OrderDbContext : TenantAwareDbContext<OrderDbContext>
{
    public DbSet<Order> Orders => Set<Order>();

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orders");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
```

`HasQueryFilter` appears in exactly two files across `api/src`, both inside the shared base class. A per-entity filter in your module is a filter someone will forget on the next entity.

### 3. Register the context

Register a pooled factory, then the tenant-aware scoped wrapper and the read context. The interceptor that stamps `TenantId` on new entities goes on the factory's options:

```csharp
services.AddPooledDbContextFactory<OrderDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "orders"));
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});

services.AddTenantAwareScopedContext<OrderDbContext>();
services.AddReadDbContext<OrderDbContext>(configuration);
```

`AddTenantAwareScopedContext<T>()` is what resolves the current tenant per scope and hands it to the pooled instance — pooling and a constructor-injected `ITenantContext` are incompatible, which is the reason the base class takes the tenant from the scope rather than from its constructor.

### 4. Create a design-time factory

`dotnet ef` constructs the context outside the DI container, so give it an
`IDesignTimeDbContextFactory<T>` in your Infrastructure project's `Persistence` folder. Because the context takes no `ITenantContext` in its constructor, the factory needs nothing but a connection string:

```csharp
public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<OrderDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        return new OrderDbContext(optionsBuilder.Options);
    }
}
```

Every module ships one of these — `AnnouncementsDbContextFactory` is the shortest to copy.

### 5. Raw SQL

The global filter is an EF Core construct, so anything that bypasses EF bypasses the filter. If you drop to raw SQL — `FromSql` on the read context, or any hand-written query — you must filter by tenant yourself:

```sql
WHERE tenant_id = @TenantId
```

Pass the ambient `ITenantContext.TenantId.Value` as the parameter. This is the one place in a module where forgetting a `WHERE` clause is a cross-tenant data leak rather than a bug.

---

## Adding API Endpoints

Controllers live in the `Api` layer of your module and depend only on `Application`.

### 1. Create a controller

In `YourProduct.YourModule.Api/Controllers/`:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMessageBus _bus;

    public OrdersController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [HasPermission(PermissionType.OrdersCreate)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        Result<OrderDto> result = await _bus.InvokeAsync<Result<OrderDto>>(
            new CreateOrderCommand(request.CustomerId, request.Items));
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.OrdersRead)]
    public async Task<IActionResult> GetById(Guid id)
    {
        Result<OrderDto> result = await _bus.InvokeAsync<Result<OrderDto>>(
            new GetOrderByIdQuery(id));
        return result.ToActionResult();
    }
}
```

### 2. Add permissions

If your module needs new permissions, add string constants to `PermissionType` in `api/src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` and update the role-to-permission mapping in the Identity module's `RolePermissionMapping.cs`.

### 3. Request/Response contracts

Define request and response types in the Api layer:

```csharp
public record CreateOrderRequest(Guid CustomerId, List<OrderItemRequest> Items);
public record OrderItemRequest(Guid ProductId, int Quantity);
```

DTOs live in the Application layer. Requests and responses live in the Api layer.

---

## Adding Domain Events and Consumers

### Define the event

Add integration events to `Shared.Contracts` so any module can consume them:

```
api/src/Shared/YourProduct.Shared.Contracts/YourModule/Events/OrderPlacedEvent.cs
```

```csharp
namespace YourProduct.Shared.Contracts.YourModule.Events;

public sealed record OrderPlacedEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required Guid CustomerId { get; init; }
    public required decimal Total { get; init; }
}
```

Events use primitive types only -- no strongly-typed domain IDs. This keeps serialization simple across module boundaries. Name events in past tense. They are facts, not commands.

### Publish the event

From any handler, publish after the operation succeeds:

```csharp
await bus.PublishAsync(new OrderPlacedEvent
{
    OrderId = order.Id,
    CustomerId = order.CustomerId,
    Total = order.Total
});
```

### Create a consumer in another module

In the consuming module's Application or Infrastructure layer, Wolverine discovers handlers by convention:

```csharp
// In Notifications.Application/EventHandlers/
public static class OrderPlacedEventHandler
{
    public static async Task HandleAsync(
        OrderPlacedEvent @event,
        INotificationService notifications,
        CancellationToken ct)
    {
        await notifications.CreateAsync(
            @event.CustomerId,
            $"Order {@event.OrderId} placed for {@event.Total:C}",
            ct);
    }
}
```

Wolverine discovers handlers by convention inside the assemblies the module declared in
`HandlerAssemblies` — which is always both its `.Application` and its `.Infrastructure` assembly — so
a handler in either layer needs no manual registration.

---

## Adding Migrations

Each module manages its own migrations through its Infrastructure project.

### Create a migration

```bash
dotnet ef migrations add InitialCreate \
    --project api/src/Modules/YourModule/YourProduct.YourModule.Infrastructure \
    --startup-project api/src/YourProduct.Api \
    --context YourModuleDbContext
```

### Apply the migration

`Wallow.MigrationService` applies every module's migrations, and Aspire runs it before the API starts — so `pnpm backend` is the normal way to get a new migration onto your local database. Register your context with the migration service when you add the module.

To apply one context by hand instead:

```bash
dotnet ef database update \
    --project api/src/Modules/YourModule/YourProduct.YourModule.Infrastructure \
    --startup-project api/src/YourProduct.Api \
    --context YourModuleDbContext
```

**Nothing migrates on API startup.** Modules have no startup hook at all; the only inline path is `WallowModules.RunTestMigrationsAsync`, which is gated to the `Testing` environment so integration tests can migrate their own Testcontainers database. Running `Wallow.Api` on its own against an un-migrated database fails at first query rather than fixing itself.

---

## Adding Plugins and Extensions

Wallow includes a plugin system for product-specific extensions that load dynamically without modifying core code. Plugins are the recommended way to add fork-specific functionality because they don't create merge conflicts when syncing upstream.

### Plugin structure

A plugin is a .NET class library that implements `IWallowPlugin` and ships with a `plugin.json` manifest:

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
  "minWallowVersion": "4.0.0",
  "entryAssembly": "YourPlugin.dll",
  "dependencies": [],
  "requiredPermissions": ["storage:read", "messaging:send"],
  "exportedServices": []
}
```

All ten keys are required — `PluginManifest` is a positional record, so a missing one fails deserialization. `minWallowVersion` is **declarative only**: nothing in the loader compares it against the backend's `<Version>` (currently `4.0.0` in `api/Directory.Build.props`). Record the version you actually built against; do not expect it to stop a mismatched plugin from loading.

**Plugin entry point:**

```csharp
public class YourPlugin : IWallowPlugin
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
| Generic capability useful across products | Module in core Wallow |
| Product-specific feature that only your fork needs | Plugin |
| Feature you want to develop in your fork and later contribute upstream | Start as a plugin, then convert to a module when contributing |

---

## Running Tests for New Modules

### 1. Create the test project

Each module uses a single test project with subdirectories for each layer:

```
api/tests/Modules/YourModule/YourProduct.YourModule.Tests/
  Domain/
  Application/
  Infrastructure/
```

```bash
mkdir -p api/tests/Modules/YourModule
cd api/tests/Modules/YourModule
dotnet new xunit -n YourProduct.YourModule.Tests
```

Add references to the module layers and the shared test infrastructure:

```bash
dotnet add reference ../../../api/src/Modules/YourModule/YourProduct.YourModule.Domain
dotnet add reference ../../../api/src/Modules/YourModule/YourProduct.YourModule.Application
dotnet add reference ../../../api/src/Modules/YourModule/YourProduct.YourModule.Infrastructure
dotnet add reference ../../YourProduct.Tests.Common/YourProduct.Tests.Common.csproj
```

Add the test project to the solution:

```bash
dotnet sln api/YourProduct.slnx add api/tests/Modules/YourModule/YourProduct.YourModule.Tests
```

### 2. Unit tests

Test handlers in isolation by mocking repositories and services:

```csharp
[Fact]
public async Task Should_create_order()
{
    IOrderRepository repo = Substitute.For<IOrderRepository>();
    CreateOrderCommand command = new(customerId, items);

    Result<OrderDto> result = await CreateOrderHandler.HandleAsync(command, repo, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    await repo.Received(1).SaveChangesAsync();
}
```

### 3. Integration tests

Use the shared `WebApplicationFactory` with Testcontainers from `Tests.Common`. Prefer `ICollectionFixture` over `IClassFixture` for container sharing:

```csharp
[Collection("Api")]
public class OrdersControllerTests
{
    private readonly HttpClient _client;

    public OrdersControllerTests(WallowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_returns_201()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

Integration tests require Docker. Testcontainers spins up ephemeral Postgres and Valkey containers.

### 4. Run tests

```bash
# All tests
./scripts/run-tests.sh

# Only your module
./scripts/run-tests.sh api/tests/Modules/YourModule/YourProduct.YourModule.Tests
```

---

## Syncing Upstream Changes

### Initial setup (one-time)

```bash
git remote add upstream https://github.com/your-org/Wallow.git
git fetch upstream
```

### Pulling updates (tagged-release sync)

For stability, sync from tagged releases rather than `upstream/main`:

```bash
git fetch upstream --tags
git checkout main
git merge v2.1.0    # merge a specific release tag
```

Alternatively, track the latest main:

```bash
git fetch upstream
git checkout main
git merge upstream/main
```

### Resolving conflicts

Conflicts typically occur in files where you renamed `Wallow` to `YourProduct`. The recommended workflow:

1. Accept the upstream version of the conflicted file
2. Re-apply the `Wallow -> YourProduct` replacement on that file
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
- **Track upstream-intended commits** -- prefix commits meant for contribution with `[wallow]` in the commit message for easy identification.

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

4. **Open a PR against the upstream repository** following Wallow's commit conventions (`feat:`, `fix:`, etc.).
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

## Checklist (Approach A)

- [ ] Fork created and cloned
- [ ] `packages/styles/branding.json` customized with your product identity
- [ ] `api/src/Wallow.Api/appsettings.json` configured (SMTP, OpenTelemetry service name, connection strings)
- [ ] `api/seed.json` configured with your bootstrap tenant and admin
- [ ] Merge driver activated (`git config merge.ours.driver true`)
- [ ] `dotnet build api/Wallow.slnx` succeeds
- [ ] `./scripts/run-tests.sh` passes
- [ ] Upstream remote added for future syncing
- [ ] Module toggles configured in `FeatureManagement` section

## Checklist (Approach B -- Full Rename)

- [ ] All `Wallow.*` references renamed to `YourProduct.*`
- [ ] `api/Wallow.slnx` renamed and project paths updated
- [ ] Docker Compose configuration updated
- [ ] CI/CD workflows updated
- [ ] `<ContainerRepository>` properties updated in the three publishable projects
- [ ] React app Dockerfiles checked (`apps/wallow-web/`, `apps/wallow-auth/`)
- [ ] `dotnet build api/YourProduct.slnx` succeeds
- [ ] `./scripts/run-tests.sh` passes
