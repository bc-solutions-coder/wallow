# Wolverine module registration — explicit registry, correct 6.x settings

**status: active**

Make Wolverine work correctly against Wallow's existing four-assembly-per-module Clean
Architecture. **No project structure changes.** All 28 module assemblies stay, the layer
boundaries stay compiler-enforced, and the architecture tests keep working unmodified.

> **Revision note (2026-08-04).** This plan was reviewed by four agents (architecture, .NET
> concurrency/messaging, C# implementation feasibility, citation fact-check). The messaging
> reviewer took live measurements by temporarily patching `Program.cs` and restoring it. Several
> claims in the first draft were **wrong** and are corrected below; the corrections are marked
> `[R]` where a reader of the first draft would otherwise be misled. Two planned defect beads were
> withdrawn as non-bugs.

## Problem

### 1. `ExtensionDiscovery.ManualOnly` guards against a code path Wolverine deleted

`api/src/Wallow.Api/Program.cs:180-181`:

```
// Use ManualOnly to prevent Wolverine from scanning native DLLs (QuestPDF/Skia)
// which can cause crashes on macOS (exit codes 139/134)
```

That describes pre-6.0 `AssemblyFinder`, which probed the bin directory at startup. Wolverine
6.0 (GH-2902) replaced it with a compile-time source generator: `JasperFx.SourceGenerator` emits
a `JasperFx.Generated.DiscoveredExtensions` manifest per assembly, and Wolverine reads those
manifests by walking the application's **reference graph**. From `guide/extensions.md`:

> As of Wolverine 6.0 there is no runtime bin-directory assembly scan for extensions — discovery
> is driven by the compile-time manifest described above.

Wallow is on WolverineFx 6.21.0 (`api/Directory.Packages.props:58-62`).

**[R] But the payoff is small.** `ManualOnly` governs `IWolverineExtension` **auto-discovery**,
not handler discovery. Phase 4's `opts.Include(extension)` is explicit registration and works
fine under `ManualOnly`. Dropping the flag unlocks exactly one thing — letting
`WolverineFx.RuntimeCompilation` self-register — which this plan then declines to take. Weighed
against a macOS SIGSEGV/SIGABRT that is refuted only by documentation, **Phase 2 is demoted to a
comment correction** (see Phase 2).

### 2. [R] Handler discovery is coincidence-dependent, not feature-flag-aware

`Program.cs:229-234`:

```csharp
foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("Wallow.", StringComparison.Ordinal) == true))
{
    opts.Discovery.IncludeAssembly(assembly);
}
```

The first draft claimed this loop is "accidentally feature-flag-aware via assembly load order."
**That is wrong.** The only module disabled by default is ApiKeys (`appsettings.json:88`), and
`Wallow.ApiKeys.Infrastructure` loads unconditionally regardless, because `Program.cs` imports it
into the top-level `<Main>$` body itself:

- `Program.cs:26` — `using Wallow.ApiKeys.Infrastructure.Authorization;`
- `Program.cs:561` — `app.UseMiddleware<ApiKeyAuthenticationMiddleware>();`
- same pattern for `Wallow.Notifications.Infrastructure.Jobs` at `Program.cs:34`

The JIT resolves those type tokens when it compiles `<Main>$`, before the `UseWolverine` lambda
body runs. So the assembly is already in `GetAssemblies()` with the module disabled.

The loop is **coincidence-aware**: it causes no damage today only because ApiKeys and Branding
have zero Wolverine handlers. The real exposure is the inverse of what the first draft described:

> **Latent bug.** Adding a single Wolverine handler to ApiKeys or Branding today would fail at
> codegen under `ServiceLocationPolicy.NotAllowed` (`Program.cs:214`), because the module's
> services are never registered when its flag is off.

That is what the registry prevents. It does **not** preserve an existing invariant — it
introduces one.

**Open measurement (blocks Phase 3 scoping).** Whether the `.Application` assemblies — where most
handlers live — stay unloaded when a flag is off was not settled by source reading. Log
`AppDomain.CurrentDomain.GetAssemblies()` inside the lambda with a module disabled before scoping
Phase 3. See `Wallow-vmns.1`-adjacent bead below.

### 3. [R] Multiple handlers share one retry loop — four message types, not one

The first draft said `InquirySubmittedEvent` is the only multi-handler message type, and that the
handlers "share one transaction" so an SSE failure "rolls back the in-app notification." **Both
halves are wrong.**

There are **four** multi-handler message types (verified by parsing all 96 handler methods under
`api/src/Modules` and grouping by first parameter type):

| Message type | Handlers | Note |
|---|---|---|
| `EmailVerifiedEvent` | 2 | **crosses modules** — Inquiries + Notifications |
| `InquirySubmittedEvent` | 3 | all Notifications |
| `InquiryCommentAddedEvent` | 3 | all Notifications |
| `InquiryStatusChangedEvent` | 2 | all Notifications |

There is **no shared transaction to roll back**. Measured baseline: `chains=88, transactional=0`.
`AutoApplyTransactions` is not called, no `[Transactional]` attribute exists in `api/src/Modules`,
and the three `InquirySubmitted*` handlers are `public static` classes that take only `IMessageBus`
(+ `ISseDispatcher`/`ITenantContext`) and touch no `DbContext`.

**The real defect is duplicated side effects on retry.** Under the default
`MultipleHandlerBehavior.ClassicCombineIntoOneLogicalHandler` **[R — there is no member named
`Combined`]**, all handlers for a message form one logical handler with one retry loop. A failure
in the SSE handler re-runs the already-committed email send and in-app notification write,
producing duplicate emails and duplicate notification rows.

`EmailVerifiedEvent` is the strongest case and the lead example: its two handlers live in
**different modules**, and `EmailVerifiedInquiryLinkHandler` does a real
`await repository.SaveChangesAsync(ct)`. Under the current mode a Notifications failure retries
the Inquiries link-up — a module-boundary violation expressed as a retry policy.

### 4. [R] The application assembly is unpinned — preventive only, near-zero current exposure

`opts.ApplicationAssembly` is never set. The Wolverine docs warn that the resolved application
assembly is cached in a **process-wide static**, so in a test process standing up multiple hosts,
whichever runs first pins it for every later host.

Corrections to the first draft:

- The operative static is **`JasperFxOptions.RememberedApplicationAssembly`**, set inside
  `AddJasperFx` via a `DetermineCallingAssembly()` stack walk. `WolverineOptions.RememberedApplicationAssembly`
  measured **null** in this repo.
- The baseline **already resolves correctly** to `Wallow.Api`, because every Wolverine host in the
  suite boots the same `Program.cs` through `WebApplicationFactory<Program>` — the mitigation the
  docs themselves recommend.
- `AuditInterceptorTests.cs` bootstraps **no host** (plain EF Core + Testcontainers) and
  `AuthorizationCodeFlowHarness.cs` consumes an injected factory rather than creating one. The real
  multi-host evidence is two separate `ICollectionFixture<WallowApiFactory>` collections in
  `Wallow.Api.Tests` plus `PerformanceTuningTests.cs:25,69` creating further hosts via
  `WithWebHostBuilder`, and three factories in `Wallow.Identity.IntegrationTests`.

Setting it on the main host is sufficient. **This is hardening, not a fix** — no open bead
attributes a current failure to it. One observable side effect: the setter also `Fill`s the
discovery collection, so `Wallow.Api` appears twice in the assembly list. Harmless.

### 5. [R] `AutoApplyTransactions()` breaks the Testing environment — BLOCKER

The first draft called this the "companion" of `opts.UseEntityFrameworkCoreTransactions()`
(`Program.cs:249`). **That framing is wrong and the change does not build green.**

`UseEntityFrameworkCoreTransactions()` registers the frame *provider* (the capability).
`AutoApplyTransactions` is an `IChainPolicy` that *applies* it to every chain whose
`ServiceDependencies` transitively reach a DbContext. Measured: `transactional` goes **0 → 65**.

With the policy on, `Wallow.Api.Tests` integration tests fail:

```
System.InvalidOperationException : This Wolverine application is not using Database backed message persistence
   at Wolverine.EntityFrameworkCore.Internals.EfCoreEnvelopeTransaction..ctor(...)
```

`Program.cs:241-246` deliberately skips `PersistMessagesWithPostgresql` in the Testing
environment. Today nothing is transactional so that constructor is never reached; with 65
transactional chains it is. **A prerequisite is required**: either enable Postgres message
persistence in Testing, or gate the policy on environment.

Two further hazards the first draft omitted:

- **Commit-boundary shift.** `EFCorePersistenceFrameProvider` moves `SaveChangesAsync` to a
  middleware postprocessor. `SendNotificationHandler` currently saves explicitly then pushes to
  realtime; any handler refactored to drop its explicit save would push **before** commit, creating
  a read-your-writes race for SSE consumers.
- **Multi-DbContext chains become fatal.** `EFCorePersistenceFrameProvider.DetermineDbContextType`
  throws when a chain's transitive dependencies reach more than one DbContext. None exists today,
  but with 7 module DbContexts the first cross-module handler injecting two repositories turns into
  a codegen failure on first message.

### 6. [R] `MessageIdentity.IdAndDestination` is inert here — withdrawn

The first draft asserted it is "required once a message has multiple destinations." **Wrong.**
`WolverineRuntime.AcquireOutgoingEnvelope` assigns a fresh `Envelope.IdGenerator()` per outgoing
envelope, so a fan-out to N local queues already produces N distinct Ids. `MessageIdentity` is a
**dedupe-key** setting consumed only by the persistence schema (it widens the `IncomingEnvelopeTable`
and `DeadLettersTable` primary key from `(id)` to `(id, received_at)`), and it exists for messages
arriving from external brokers or multiple listening endpoints. Wallow registers **no external
transport**.

**Withdrawn from Phase 1.** Revisit if an external broker is ever added.

### 7. [R] A second identical `AppDomain` loop exists, unlisted in the first draft

`api/src/Wallow.Api/Extensions/AsyncApiEndpointExtensions.cs:15-17` runs the same
`AppDomain.CurrentDomain.GetAssemblies().Where(name.StartsWith("Wallow."))` pattern to feed
`EventFlowDiscovery` for the AsyncAPI document — same load-order dependency, same flag blindness,
producing an AsyncAPI doc that varies with JIT order. It is the registry's second consumer.

## Non-goals — and one decision to record

**Collapsing the four-assembly-per-module structure was evaluated and rejected.** JasperFx's
modular-monolith tutorial discourages separate projects per layer, and all five
`JasperFx/CritterStackSamples` monoliths are single-project. Two measurements:

- Ceremony is real and unevenly paid: Branding is 4 projects for 727 LOC / 14 files; Identity is
  4 projects for 15,465 LOC / 263 files. Seven modules total 30,157 LOC.
- Encapsulation is currently unavailable: **[R] exactly 1 `internal` declaration** across all seven
  modules (`StorageInfrastructureExtensions.cs:139`, `internal sealed class ClamAvHealthCheck`).
  The first draft said 4; a rigorous recount excluding doc comments, `[LoggerMessage]` strings and
  the literal `"@mfa.internal"` gives 1. **The argument is stronger than the first draft claimed.**

Rejected because Wallow is a **fork-first base platform** (root `CLAUDE.md`). Module boundaries are
shipped to downstream forks, where "the compiler rejects it" and "a test in a suite they may have
deleted rejects it" are not equivalent guarantees. JasperFx's advice targets teams whose module
discipline is internal.

Secondary: Wolverine codegen emits into the entry assembly and must construct handler dependencies
directly under `ServiceLocationPolicy.NotAllowed`. Wallow's effectively-all-public modules always
satisfy that; a collapsed assembly using `internal` would need `InternalsVisibleTo`.
`WolverineCodegenPolicyTests.InfrastructureImplementations_OfApplicationInterfaces_ShouldBePublic`
is the guard that would need replacing.

Also out of scope:

- The `.slnx` entry and `Wallow.Api.csproj` ProjectReferences stay manual.
- `ServiceLocationPolicy.NotAllowed` and the six `AlwaysUseServiceLocationFor` entries
  (`Program.cs:214-220`) stay exactly as written.
- `CustomizeHandlerDiscovery` excluding `IAuthorizationHandler` (`Program.cs:227`) stays.

## Design

### `IWallowModule`

Interface in `Wallow.Shared.Infrastructure` — verified to already carry
`<FrameworkReference Include="Microsoft.AspNetCore.App" />`
(`Wallow.Shared.Infrastructure.csproj:11`), so `WebApplication`, `IServiceCollection` and
`IConfiguration` are all in scope with **no new package**. All seven module `.Infrastructure`
projects already reference it. No architecture test is violated: `CleanArchitectureTests` forbids
`Microsoft.AspNetCore` only in Domain and Application, never Infrastructure, and does not inspect
`Wallow.Shared.*` at all.

```csharp
public interface IWallowModule
{
    string Name { get; }                       // "Storage" -> FeatureManagement:Modules.Storage
    bool IsCore { get; }                       // Identity: always on, migrates first
    IEnumerable<Assembly> HandlerAssemblies { get; }

    // [R] IHostEnvironment is required: AddIdentityModule takes it (see below).
    IServiceCollection AddServices(
        IServiceCollection services, IConfiguration configuration, IHostEnvironment environment);
}
```

**[R] `AddServices` must take `IHostEnvironment`.** Six modules match `AddXModule(services, configuration)`,
but `IdentityModuleExtensions.cs:12-19` is:

```csharp
public static IServiceCollection AddIdentityModule(
    this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
```

and `WallowModules.cs:46` passes `builder.Environment`. Identity is the one module the registry
cannot make optional, so it is the one the interface must fit. Six wrappers carry an unused
parameter; that is cheaper than special-casing Identity out of the registry.

**[R] `InitializeAsync` is omitted deliberately.** All seven `InitializeXModuleAsync` methods are
no-ops returning `Task.FromResult(app)`. Keeping an interface member alive for seven methods that
do nothing is premature abstraction, and `MigrationRemovalTests.cs:9-18` pins them by file path.
Phase 3 deletes them.

**[R] Marker types are new work, not reuse.** `typeof(StorageApplicationMarker)` does not exist —
the repo has exactly one marker (`INotificationsApplicationMarker.cs`) and it is unreferenced dead
code. Either author 8 markers (7 Application + Identity.Infrastructure) or use an already-public
handler type per assembly, e.g. `typeof(SubmitInquiryHandler).Assembly`. Prefer the latter; no new
files.

### Verified handler census (drives `HandlerAssemblies`)

| Module | `.Application` | `.Infrastructure` |
|---|---|---|
| Notifications | 45 | 0 |
| Announcements | 12 | 0 |
| Inquiries | 11 | 0 |
| Storage | 10 | 0 |
| Identity | 9 | 3 |
| ApiKeys | **0** | **0** |
| Branding | **0** | **0** |

Identity returns two assemblies. ApiKeys and Branding declare their Application assembly anyway —
harmless, and it is what makes Problem 2's latent bug impossible.

### Wolverine wiring

```csharp
opts.ApplicationAssembly = typeof(WallowModules).Assembly;   // verified: internal, same assembly

foreach (Assembly assembly in enabledModules.SelectMany(m => m.HandlerAssemblies))
{
    opts.Discovery.IncludeAssembly(assembly);
}
```

**[R] Where `enabledModules` comes from — the plan must specify this.** Today `IFeatureManager` is
resolved **twice from different providers**: `AddWallowModules` (`WallowModules.cs:36-39`) builds a
temporary `ServiceProvider` from the unfinished `builder.Services` and resolves synchronously;
`InitializeWallowModulesAsync` (`WallowModules.cs:93`) resolves again from the final `app.Services`.
"Resolve once" therefore requires:

1. Compute the enabled set at the `AddWallowModules` call site (`Program.cs:175`) — the only point
   before `UseWolverine` at `:182` needs it.
2. Hold it in a `<Main>$`-scoped local for the Wolverine lambda to capture.
3. Change `AddWallowModules`'s return (currently `IServiceCollection`) and
   `InitializeWallowModulesAsync`'s signature (currently `WebApplication` only) to carry the
   precomputed list rather than re-resolving.

Without step 3 the phase regresses to today's two independent resolutions and only removes
`if`/`else` syntax.

**Why not `[assembly: WolverineModule]`.** It discovers unconditionally. A disabled module's
handlers would be found, codegen would build chains, and `ServiceLocationPolicy.NotAllowed` would
throw on DI that was never registered. Per Problem 2 this is the *live* risk, not hypothetical.

## Work breakdown

### Phase 1 — settings fixes

**Prerequisite (blocker):** resolve the Testing-environment persistence contradiction before
`AutoApplyTransactions` can land. Either enable Postgres message persistence in Testing or gate the
policy on environment.

Then, in `Program.cs`:

```csharp
opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
opts.ApplicationAssembly = typeof(WallowModules).Assembly;
opts.Policies.AutoApplyTransactions();   // only after the prerequisite
```

`MessageIdentity` is withdrawn (Problem 6).

**[R] `Separated` breaks an existing test and must fix it in the same change.**
`HandlerCodegenTests.EveryDiscoveredHandler_Compiles` fails for all four multi-handler message types
with `NoHandlerForEndpointException`, because `Separated` moves handlers into per-endpoint
sub-chains (`ByEndpoint`, each on its own `local://` queue) and the parent chain resolves to
nothing. Fix: iterate `HandlerGraph.AllChains()` — which does yield the sub-chains — and resolve
with the endpoint-aware overload. This is the repo's highest-value Wolverine guard; do not skip it.

**[R] Ordering:** if Phase 1 is split across commits, `Separated` must land **before**
`AutoApplyTransactions`. Under the current mode, `AutoApplyTransactions` was measured to flip
`EmailVerifiedEvent`'s chain to transactional with the DbContext resolved through
`IInquiryRepository` — creating exactly the cross-module shared transaction Problem 3 wrongly
claimed already exists.

**Two more behavioural changes to document:** handlers become **concurrent** (separate local
queues, no ordering guarantee between email / in-app write / SSE push), and durable inbox/outbox
rows **multiply** — one publish of `InquirySubmittedEvent` becomes 3 outbox + 3 inbox rows across
4 affected message types. Not measurable in Testing (durability disabled there); needs a non-Testing
run to quantify.

### Phase 2 — [R] demoted to comment correction

Do **not** drop `ExtensionDiscovery.ManualOnly`. Correct the two misleading comments:

- `Program.cs:180-181` — record that the bin-directory scan no longer exists in 6.x, and that the
  flag is retained for control rather than for the macOS crash.
- `Program.cs:184-189` — the comment claims referencing `WolverineFx.RuntimeCompilation` "does not
  auto-register it here"; that is true *only because* of `ManualOnly`. Say so.

### Phase 3 — the module registry

1. `IWallowModule` in `Wallow.Shared.Infrastructure` (with `IHostEnvironment`, without `InitializeAsync`).
2. Seven implementations in each module's `.Infrastructure`.
3. Enabled set computed once at `Program.cs:175`, threaded per the design section.
4. `WallowModules.cs`: `AddWallowModules` and `RunTestMigrationsAsync` iterate the registry; the
   14-line per-module `using` block goes; `InitializeXModuleAsync` and `InitializeWallowModulesAsync`'s
   per-module dispatch are deleted.
5. `Program.cs` discovery loop as above.
6. **[R] Fold in `AsyncApiEndpointExtensions.cs:15-17`** — second consumer of `HandlerAssemblies`.
7. **[R] `ModuleRegistrationTests.cs` — four separate breakages, verified by reading the file.**
   Three are source-text assertions that read `WallowModules.cs` via `File.ReadAllText` and require
   literal per-module call strings, so Phase 3 breaks them by construction:

   | Lines | Method | Asserts |
   |---|---|---|
   | `:23-31` | `WallowModules_ShouldRegister_AllModules` | text contains `Add{Module}Module(configuration` |
   | `:54-62` | `WallowModules_ShouldInitialize_AllModulesWithDbContext` | text contains `Initialize{Module}ModuleAsync()` |
   | `:169-177` | `AllDiscoveredModules_ShouldBeRegistered_InWallowModules` | same, over reflection-discovered names |

   `.claude/rules/TESTING.md` bans this pattern outright ("no `readFileSync` over `src/`") —
   **delete, do not port.** `MigrationRemovalTests.cs:37,49` (`File.ReadAllText` over the seven
   `*ModuleExtensions.cs` paths at `:9-18`) is the same violation.

   The fourth is **not** a source-text test and the review missed it:
   `Module_ShouldProvide_InitializeModuleExtensionMethod` (`:102-127`) reflects for
   `Initialize{Module}ModuleAsync` and asserts `GetParameters().Should().HaveCount(1)`. Phase 3
   deletes those seven methods, so this `[Theory]` fails on `extensionType.GetMethod(...)` returning
   null. Delete it with them.

   **Safe:** `Module_ShouldProvide_AddModuleExtensionMethod` (`:70-95`) checks only parameters
   `[0]` and `[1]` by position and never asserts a count, so appending `IHostEnvironment` to the six
   `AddXModule` signatures leaves it green.
8. **[R] Delete the unused `Wallow.Identity.Api.csproj:18` reference** to
   `Wallow.Identity.Infrastructure` — grep confirms zero usages in `Wallow.Identity.Api`. Two-character
   diff; `CleanArchitectureTests.ApiLayer_ShouldNotDependOn_InfrastructureLayer` stays green.

### Phase 4 — extend the registry to migrations

**[R] Respecified.** `MigrationService/Program.cs:22-74` registers **nine** DbContexts, of which
`AuditDbContext` and `AuthAuditDbContext` come from `Wallow.Shared.Infrastructure.Core.Auditing`
and belong to **no module**. A singular `DbContextType`/`SchemaName` cannot express that.

- `IWallowModule` gains `IReadOnlyList<Type> DbContextTypes` and `string SchemaName`.
- The two auditing contexts stay **host-owned**, outside the registry, and `CoreMigrationRunners`
  keeps them explicitly.
- Drive `AddDbContext` + `FeatureMigrationRunners` from the registry.

## Verification

- `./scripts/run-tests.sh` green — note Phase 1 is **not** green without its prerequisite.
- `HandlerCodegenTests` migrated to `AllChains()` (Phase 1, required).
- A test proving `Separated`: fail one handler and assert its siblings still commit — covering all
  four message types, with `EmailVerifiedEvent` (cross-module) as the primary case.
- **[R] `Wallow-uqp7` appears already satisfied** by the existing
  `api/tests/Wallow.Api.Tests/Integration/HandlerCodegenTests.cs`, which boots the real Wolverine
  config via `WallowApiFactory` and compiles every discovered chain. Verify and close rather than
  sequencing work behind it.
- Non-Testing run to quantify outbox/inbox row multiplication.

## Defects to file separately

**[R] Withdrawn — verified non-bugs:**

- ~~Arch tests never run against ApiKeys/Inquiries~~. False. `Wallow.Architecture.Tests.csproj:24`
  references `Wallow.Api`, which transitively pulls all seven modules; all seven
  `Wallow.*.Domain.dll` are present in the test output directory (independently confirmed), so
  `TestConstants.AllModules` yields seven. The explicit five-module block at `:29-52` is redundant,
  not load-bearing — deleting it is a cleanup suggestion, not a defect.
- ~~`ModuleToggleTests.DisabledModule_ShouldNotRegister_Services` proves the opposite of its name~~.
  Overstated. `:35` carries the comment "Identity is a required platform dependency — always
  registered even when the feature flag is false," matching `WallowModules.cs:45-46`. It is a
  **naming** defect: rename it.

**Confirmed, still to file:**

1. `Wallow-vmns.1` cites `api/tests/Wallow.Architecture.Tests/MigrationServiceTests.cs:189`; the
   file is at `api/tests/Wallow.MigrationService.Tests/MigrationServiceTests.cs:189`.
2. `MigrationRemovalTests.cs:9-18` is described in `vmns.1` as a hardcoded module list; it is seven
   *file paths* to `*ModuleExtensions.cs` — a different fix.
3. **[R]** Two further hardcoded four-name module lists at `ModuleRegistrationTests.cs:66-69` **and
   `:98-101`** — the `[InlineData]` rows on `Module_ShouldProvide_AddModuleExtensionMethod` and
   `Module_ShouldProvide_InitializeModuleExtensionMethod`, both listing only Notifications /
   Announcements / Identity / Storage. **Named by neither `vmns.1` nor `vmns.7`**, which between
   them cite only `:37-41`. So the epic's "at least FOUR hardcoded module lists" is actually
   **six**: `MigrationServiceTests.cs:189`, `MigrationRemovalTests.cs:9-18`,
   `ModuleRegistrationTests.cs:37-41`, `:66-69`, `:98-101`, and
   `ModuleToggleTests.cs:24-28,51-55`.

## Relationship to existing beads

- **`Wallow-vmns.1`** — superseded in approach. The registry replaces both its proposed hand-rolled
  assembly attribute and the `AppDomain` loop, and carries the DbContext types, schema name and
  migration ordering its `(name, DbContext type, feature-flag key)` shape omits. Its scope collision
  with `vmns.7` over the hardcoded test lists remains unresolved and is not decided here.
- **`Wallow-uqp7`** — likely already satisfied; verify and close (see Verification).
- **`Wallow-qi90.2`** (DLQ observability) — `Separated` changes DLQ granularity to per-handler
  across four message types; sequence this plan first.
