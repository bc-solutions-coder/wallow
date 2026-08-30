# api/CLAUDE.md — .NET backend

Solution: `api/Wallow.slnx`. Central build config, analyzers, and the
`.editorconfig`/`stylecop.json` rulesets live here in `api/`.

## Commands

```bash
# Full backend via the Aspire host (Api + Auth + Web + MigrationService + SeederService,
# plus Postgres, Valkey, Garage, Mailpit and ClamAV containers)
dotnet run --project api/src/Wallow.AppHost        # == `pnpm backend` from repo root

dotnet run --project api/src/Wallow.Api            # just the API — http://localhost:5001
dotnet run --project api/src/Wallow.SeederService  # seed roles/scopes/admin/OIDC clients from api/seed.json

dotnet build api/Wallow.slnx                       # always path the solution as api/Wallow.slnx
dotnet format api/Wallow.slnx                      # before every commit; stage what it changes

# Tests: shorthands (module names, api, arch, seeder, migrations, shared, kernel) run a target's
# fast suites; `integration`/`all` select Category=Integration solution-wide (Docker); a second
# argument narrows the tier (`./scripts/run-tests.sh storage integration`). Anything else is a
# project path — a typo'd shorthand exits 2 and prints the shorthand list.
./scripts/run-tests.sh <shorthand|path> [integration]

# EF Core migrations (per module DbContext)
dotnet ef migrations add MigrationName \
    --project api/src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context {Module}DbContext
```

- **Migrations run inline only in the `Testing` environment**
  (`WallowModules.RunTestMigrationsAsync`); everywhere else, Development included,
  `Wallow.MigrationService` applies them.
- TypeScript apps use ports 3000/3002; keep new ports clear of those and of Grafana on 3001.

## Shared.Contracts

`Wallow.Shared.Contracts` is **the only assembly modules reference across boundaries**:
integration events, cross-module service interfaces (`ISseDispatcher`, `IApiKeyService`,
`IStorageProvider`, `IEmailService`, …), and a few shared command records —
`Storage/Commands/UploadFileCommand.cs` is the one to know: Storage's handler lives in the
module but the record does not.

## Backend patterns (preserve these)

- **Handlers:** most modules use CQRS via Wolverine — a handler is normally a
  **`public sealed class`** taking dependencies through a **primary constructor**, with
  `Handle`/`HandleAsync`; Wolverine also discovers **static** handlers (mostly event handlers).
  Either shape is auto-discovered with **no DI registration**. Exceptions: **Branding and
  ApiKeys** deliberately use direct service/repository-from-controller (no command handlers;
  Branding still consumes cross-module integration events through Wolverine).
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
  are safe to inject because none of them reaches a `DbContext`.
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
- **Enum properties persist as strings, never ints** — pair `.HasConversion<string>()` with an
  explicit `.HasMaxLength(50)` (20 for short status enums), so adding or reordering an enum
  member never silently reinterprets stored rows.
- **Controllers are `partial`** to host `[LoggerMessage]` source-gen and source-generated regex.

## C# conventions

`Directory.Build.props` sets `TreatWarningsAsErrors=true` + `AnalysisMode=All`;
`Directory.Build.targets` injects the analyzers into every non-test project — each rule below is
a build error, not advisory. `Directory.Packages.props` is central package management.

- **Always write the explicit type, never `var`** — `.editorconfig` enforces it as a warning
  with `EnforceCodeStyleInBuild`.
- **Read JWT claims through `ClaimsPrincipalExtensions`** (`Wallow.Shared.Kernel.Extensions`),
  never raw `FindFirst`/`FindFirstValue`/`FindAll` on a `ClaimsPrincipal` — `GetUserId()`,
  `GetClientId()`, `GetTenantId()`, `GetEmail()`, `GetRoles()`, `GetPermissions()`,
  `GetScopes()`, `IsOperator()`, `IsGlobalAdmin()`, and friends. A claim with no helper gets a
  new helper there, not a raw `FindFirst`.
- **Log through the `[LoggerMessage]` source generator**, never `logger.LogInformation(...)` or
  any other `ILogger` extension (CA1848/CA1873). Mark the class `partial`, inject `ILogger<T>`
  via the primary constructor, put `private partial void` declarations at the bottom:

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Something happened for {EntityId}")]
private partial void LogSomethingHappened(Guid entityId);
```

`api/seed.json` is the seeder's input. Fork branding is NOT here — no backend code reads it; it
lives at `packages/styles/branding.json`.

## Tests (`api/tests/`)

- `Wallow.Architecture.Tests` (`arch`) enforces module boundaries.
- `Wallow.Tests.Common` — shared helpers; no tests, no shorthand.
- `Benchmarks` — BenchmarkDotNet, not part of the test run.
- Coverage: CI enforces a 90% line threshold.
