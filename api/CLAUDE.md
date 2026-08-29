# api/CLAUDE.md — .NET Backend

The Wallow backend: a **.NET 10 modular monolith** with multi-tenancy, Clean Architecture,
DDD, CQRS, and Wolverine in-memory messaging. The solution is `api/Wallow.slnx`; central
build config, analyzers, and the `.editorconfig`/`stylecop.json` rulesets live here in
`api/`. Root-repo/monorepo context is in `/CLAUDE.md`.

## Commands

```bash
# Run the full backend via the Aspire host (Api + Auth + Web + MigrationService + SeederService,
# plus Postgres, Valkey, Garage, Mailpit and ClamAV containers)
dotnet run --project api/src/Wallow.AppHost        # == `pnpm backend` from repo root

# Run just the API
dotnet run --project api/src/Wallow.Api            # http://localhost:5001

# Seed roles, scopes, admin, and OIDC clients from api/seed.json
dotnet run --project api/src/Wallow.SeederService

# Build & format (always path the solution as api/Wallow.slnx)
dotnet build api/Wallow.slnx
dotnet format api/Wallow.slnx                      # run before every commit

# Tests — always via the script (structured per-assembly results; applies coverage.runsettings)
./scripts/run-tests.sh                             # fast suites; Category=Integration EXCLUDED
./scripts/run-tests.sh integration                 # ONLY Category=Integration, solution-wide (Docker)
./scripts/run-tests.sh all                         # both, in one run (Docker)
./scripts/run-tests.sh identity                    # one module
./scripts/run-tests.sh storage integration         # one target's integration tests (2nd arg = tier)
# Shorthands: identity, storage, notifications, announcements, inquiries, branding, apikeys,
#             api, arch (= architecture), seeder, migrations, shared, kernel, integration, all
# `integration`/`all` select by CATEGORY across api/Wallow.slnx (integration tests live in seven
# assemblies, including Wallow.Api.Tests's HandlerCodegenTests — the only guard that every
# discovered Wolverine handler compiles). Every other invocation appends
# --filter "Category!=E2E&Category!=Integration" and says so in its output.
# Anything else is taken as a project path and must exist — a typo'd shorthand exits 2 with the
# shorthand list. (E2E is per-app Playwright — see .claude/rules/E2E.md)

# EF Core migrations (per module DbContext)
dotnet ef migrations add MigrationName \
    --project api/src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context {Module}DbContext
```

Never run bare `dotnet test` — the script adds `--settings api/tests/coverage.runsettings`,
without which generated code inflates coverage. Test doctrine: `.claude/rules/TESTING.md`.

## Solution Layout (`api/src/`)

**Host / app projects**

| Project | Role |
|---------|------|
| `Wallow.Api` | Main REST API host; assembles all modules, Wolverine, OpenIddict resource server. Port 5001. |
| `Wallow.AppHost` | **.NET Aspire** host that orchestrates everything (`pnpm backend`). |
| `Wallow.MigrationService` | Aspire worker; applies EF migrations for all module DbContexts on startup. |
| `Wallow.SeederService` | Seeds from `api/seed.json`. |
| `Wallow.ServiceDefaults` | Aspire shared defaults: OpenTelemetry, health checks, service discovery, resilience. |

The TypeScript apps use ports 3000 (`apps/wallow-web`) and 3002 (`apps/wallow-auth`); keep new
ports clear of those and of Grafana on 3001.

**Shared (`api/src/Shared/`)**

- `Wallow.Shared.Kernel` — DDD primitives (`Entity<TId>`, `AggregateRoot<TId>`, strongly-typed
  IDs, `ValueObject`, `IDomainEvent`, `Result<T>`), multi-tenancy (`ITenantContext`,
  `TenantSaveChangesInterceptor`), **`ClaimsPrincipalExtensions`** (JWT claim helpers), and
  `PermissionType` + `ScopePermissionMapper` (the permission registry all modules share).
- `Wallow.Shared.Contracts` — **the only assembly modules reference across boundaries**:
  integration events, cross-module service interfaces (`ISseDispatcher`, `IApiKeyService`,
  `IStorageProvider`, `IUserService`, `IUserQueryService`, `IEmailService`, `IRealtimeDispatcher`,
  and others), and a few shared command records — `Storage/Commands/UploadFileCommand.cs` is the
  one to know: Storage's handler lives in the module but the record does not.
- `Wallow.Shared.Infrastructure` / `.Core` / `.BackgroundJobs` (Hangfire) / `.Plugins`, and
  `Wallow.Shared.Api` — cross-cutting plumbing (settings, module registration, middleware,
  caching, messaging, auditing).

## Modules (`api/src/Modules/`)

Seven: **Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding**. Each
is a 4-project Clean Architecture stack `Wallow.{Module}.{Domain,Application,Infrastructure,Api}`:

- Dependency flow: **Domain (no external deps) → Application (Domain only) → Infrastructure → Api.**
- Each module owns a **separate Postgres schema** (enforced by `Wallow.Architecture.Tests`).
- Modules talk only via **Wolverine** integration events through `Shared.Contracts` — never
  direct project references. Handlers auto-discovered across all `Wallow.*` assemblies.
- Every module has its own nested `CLAUDE.md` — read the one for the module you are inside.
- **Migrations run inline only in the `Testing` environment**
  (`WallowModules.RunTestMigrationsAsync`); everywhere else, Development included,
  `Wallow.MigrationService` applies them. This holds for every module.
- Module tests: `./scripts/run-tests.sh <shorthand>` runs the module's fast suites; add
  `integration` as a second argument for its Testcontainers-backed suites (Docker).

## Backend Patterns (preserve these)

- **Handlers:** most modules use CQRS via Wolverine — a handler is normally a
  **`public sealed class`** taking dependencies through a **primary constructor**, with
  `Handle`/`HandleAsync`; Wolverine also discovers **static** handlers (mostly event handlers).
  Either shape is auto-discovered with **no DI registration**. Exceptions: **Branding and
  ApiKeys** deliberately use direct service/repository-from-controller (no CQRS/Wolverine).
- **Anything a handler can inject is `public`.** Wolverine's generated handlers construct their
  dependencies inline, and `ServiceLocationPolicy.NotAllowed` turns a non-public concrete type into
  a codegen failure on the *first message*, not at startup. So every Infrastructure implementation
  of an Application or `Shared.Contracts` interface is public — enforced by
  `WolverineCodegenPolicyTests`, with `HandlerCodegenTests` compiling every handler in the
  integration run. An interface the codegen genuinely cannot construct (opaque lambda registration)
  goes on the `AlwaysUseServiceLocationFor` list in `Program.cs`, which those tests also pin.
- **A handler chain may reach exactly ONE module `DbContext`.** `opts.Policies.AutoApplyTransactions()`
  gives every chain whose transitive service dependencies reach a `DbContext` a
  `<Module>DbContext.SaveChangesAsync` postprocessor. If a chain reaches **two**,
  `EFCorePersistenceFrameProvider.DetermineDbContextType` throws — on the *first message* of that
  type, not at startup. A handler injecting its own module's repository **and** a second module's
  repository is a runtime codegen failure, not a compile error. Cross-module work goes through a
  `Shared.Contracts` integration event, never a second repository. The shared service interfaces
  are safe to inject because none of them reaches a `DbContext` (`ISseDispatcher` is Redis;
  `IEmailService`, `IStorageProvider` and `IFileScanner` are external providers).
- **A handler that saves and then dispatches must keep that order.** The transaction middleware
  *adds* a `SaveChangesAsync` postprocessor; it does not remove an explicit save. Do not delete an
  explicit save on the theory that the postprocessor covers it — `SendNotificationHandler` saves
  and *then* pushes to realtime; reversing that hands SSE consumers a read-your-writes race. The
  middleware only covers `SaveChangesAsync`: `ExecuteDeleteAsync`/`ExecuteUpdateAsync` bulk
  statements sit outside the handler's unit of work.
- **DbContexts** extend `TenantAwareDbContext` (automatic tenant query filters +
  `TenantSaveChangesInterceptor`), default `NoTracking` — mutations attach explicitly.
- **State changes go through aggregate methods** (`Publish()`, `Archive()`, `Revoke()`,
  `TransitionTo()`) — never set `Status` directly. Domain events raised in aggregates are
  bridged to integration events in Application event handlers.
- **EF Core is the only data-access technology.** Writes go through the module's
  `TenantAwareDbContext`; reads go through `IReadDbContext<T>` (`NoTracking`, routed to
  `ReadReplicaConnection` when configured). No Dapper — if raw SQL is genuinely needed, it is
  EF Core's `FromSql`/`ExecuteSql`, and tenant query filters do **not** apply to it.
- **Enum properties persist as strings, never ints** — every entity configuration pairs
  `.HasConversion<string>()` with an explicit `.HasMaxLength(50)` (20 for short status enums), so
  adding or reordering an enum member never silently reinterprets stored rows.
- **Controllers are `partial`** to host `[LoggerMessage]` source-gen and source-generated regex.

## C# Conventions

Analyzers run on every non-test project (`Directory.Build.targets` gates them on
`IsTestProject`), and `AnalysisMode=All` + `TreatWarningsAsErrors=true` turns each rule below into
a build error — enforced, not advisory.

- **Always write the explicit type, never `var`** — `.editorconfig` sets all three
  `csharp_style_var_*` options to `false:warning` with `EnforceCodeStyleInBuild`.
- **Read JWT claims through `ClaimsPrincipalExtensions`** (`Wallow.Shared.Kernel.Extensions`),
  never raw `FindFirst`/`FindFirstValue`/`FindAll` on a `ClaimsPrincipal`:
  - Single-value: `GetUserId()`, `GetClientId()`, `GetTenantId()`, `GetTenantName()`, `GetEmail()`,
    `GetDisplayName()`, `GetFirstName()`, `GetLastName()`, `GetAuthMethod()`, `GetTenantRegion()`.
    Predicates: `IsOperator()`, `IsGlobalAdmin()`.
  - Multi-value: `GetRoles()`, `GetPermissions()`, `GetScopes()` — each returns `IReadOnlyList<string>`.
  - A claim with no helper gets a new helper on `ClaimsPrincipalExtensions`, not a raw `FindFirst`.
- **Log through the `[LoggerMessage]` source generator**, never `logger.LogInformation(...)` or any
  other `ILogger` extension method (CA1848/CA1873). Mark the class `partial`, inject `ILogger<T>`
  via the primary constructor, add `using Microsoft.Extensions.Logging;`, and put `private partial
  void` declarations at the bottom of the class:

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Something happened for {EntityId} by user {UserId}")]
private partial void LogSomethingHappened(Guid entityId, string? userId);
```

## Central Build Config (`api/`)

| File | Governs |
|------|---------|
| `Directory.Build.props` | `net10.0`, nullable + implicit usings, **`TreatWarningsAsErrors=true`**, `AnalysisMode=All`, central package management, `<Version>` (release-please). |
| `Directory.Build.targets` | Injects analyzers into non-test projects: NetAnalyzers, StyleCop, Meziantou, Roslynator. |
| `Directory.Packages.props` | **Central Package Management** — single source for all NuGet versions. |
| `global.json` | Pins the .NET SDK (`rollForward: latestMinor`). |
| `stylecop.json`, `.editorconfig` | Style rulesets driving `EnforceCodeStyleInBuild`. |
| `seed.json` | Seeder input. (Fork branding is NOT here — no backend code reads it; it lives at `packages/styles/branding.json`.) |

Run `dotnet format api/Wallow.slnx` before every commit and stage the formatting changes it
makes — never commit unformatted code. No `--` inside XML comments in
`.csproj`/`.props`/`.targets` (`.claude/rules/CONVENTIONS.md`).

## Tests (`api/tests/`)

- **Module tests:** `Modules/{Module}/Wallow.{Module}.Tests` (unit + Testcontainers Postgres).
  Identity adds `Wallow.Identity.IntegrationTests` — no shorthand; reach it by path plus the tier
  (`./scripts/run-tests.sh api/tests/Modules/Identity/Wallow.Identity.IntegrationTests integration`).
- **Host:** `Wallow.Api.Tests`.
- **Architecture:** `Wallow.Architecture.Tests` (`arch`) enforces module boundaries.
- **E2E:** three per-app `@playwright/test` suites — `apps/wallow-auth/e2e/`,
  `apps/wallow-web/e2e/`, `apps/wallow-web/e2e-cross-app/`. See `.claude/rules/E2E.md`;
  `./scripts/e2e.sh` runs all three.
- **Shared/Kernel:** `Wallow.Shared.Infrastructure.Tests` (`shared`),
  `Wallow.Shared.Kernel.Tests` (`kernel`); helpers in `Wallow.Tests.Common` (no tests, no shorthand).
- **Services:** `Wallow.SeederService.Tests` (`seeder`), `Wallow.MigrationService.Tests`
  (`migrations`), `Wallow.AppHost.Tests` (no shorthand).
- **Benchmarks:** `Benchmarks` — BenchmarkDotNet, not part of the test run.
- **Coverage:** `coverage.runsettings` (cobertura, `Include=[Wallow.*]*`, excludes migrations,
  Program/Startup, generated files). CI enforces a 90% line threshold.
