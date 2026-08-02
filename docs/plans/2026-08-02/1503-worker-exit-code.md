**status: active**

# One-Shot Worker Exit Code Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make `Wallow.SeederService` and `Wallow.MigrationService` exit non-zero when their
`BackgroundService` fails, so a failed seed or migration stops the Compose dependency chain
instead of silently reporting success.

**Architecture:** A tiny `WorkerRunOutcome` singleton lives in `Wallow.ServiceDefaults` (already
referenced by both hosts). Each worker marks it on its failure path; each `Program.cs` resolves
it **before** `await host.RunAsync()` and returns its exit code. No framework behaviour is
overridden — we simply stop discarding the information the host already has.

**Tech Stack:** .NET 10, `Microsoft.Extensions.Hosting`, xUnit + NSubstitute + AwesomeAssertions,
Docker Compose.

**Bead:** Wallow-2y1t

---

## Background — measured, not assumed

A minimal Worker reproducing `SeederWorker`'s exact `try`/`catch`-rethrow/`finally` shape was
built and run on SDK 10.0.302. Real process exit codes:

| Variant                                                | Process exit |
| ------------------------------------------------------ | ------------ |
| default (no `HostOptions` configuration)               | **0**        |
| `BackgroundServiceExceptionBehavior.StopHost` explicit | **0**        |
| outcome sink resolved **before** `RunAsync`, step throws | **1**        |
| outcome sink resolved **before** `RunAsync`, all steps succeed | **0**  |
| outcome sink resolved **after** `RunAsync`             | **ObjectDisposedException** |

### Two facts that override the bead's own wording

1. **`StopHost` is already the default** (has been since .NET 6). The default-mode run emitted the
   framework's message verbatim: *"The HostOptions.BackgroundServiceExceptionBehavior is configured
   to StopHost."* The bead's claim that the host "never overrides to StopHost" is wrong.
2. **Therefore the bead's first suggested fix is a no-op.** Setting `StopHost` explicitly still
   exits 0. Do not implement it. `StopHost` governs *whether the host stops*, never the exit code.

**Mechanism:** `Host.TryExecuteBackgroundServiceAsync` catches the faulted `ExecuteAsync` task,
logs it, calls `StopApplication()`, and never rethrows. `RunAsync()` therefore completes
*successfully*, and a top-level `await host.RunAsync();` returning no value exits with
`Environment.ExitCode` — still 0.

### The trap this plan exists to avoid

`HostingAbstractionsHostExtensions.RunAsync` disposes the host in a `finally`. Resolving the sink
**after** `await host.RunAsync()` throws `ObjectDisposedException`. Every `Program.cs` change below
resolves it **before**. This is not stylistic — the obvious ordering crashes.

### Current state is healthy

Verified against a throwaway migrated database: all six seed steps ran, seeding 3 OIDC clients,
32 OpenIddict scopes, 27 API scopes, 1 user, 1 org, 1 membership, 3 roles; a second run was
idempotent. `./scripts/run-tests.sh seeder` passes 14/14. **This is a latent failure-reporting
defect, not an active malfunction.** Nothing here fixes a broken seed; it stops the next broken
seed from being invisible.

### Expected consequence

Previously-silent failures will start failing `scripts/e2e.sh` loudly. That is the point. If a
step is throwing intermittently and nobody has noticed, this is what surfaces it.

---

## Design decisions

**Chosen: an injected `WorkerRunOutcome` singleton.**

Rejected alternatives, with reasons:

| Alternative | Why not |
| ----------- | ------- |
| `BackgroundServiceExceptionBehavior.StopHost` | Measured no-op; already the default. |
| `Environment.ExitCode = 1` inside the worker | Global mutable process state. Parallel-unsafe to assert under xUnit (tests share a process), and invisible at the `Program.cs` seam where exit codes belong. |
| Rethrow out of `Main` | Works, but exits 134 on Unix (SIGABRT) with a raw stack dump. Ugly in container logs, and hostile to `docker inspect`. |
| Poll `worker.ExecuteTask.IsFaulted` from `Program.cs` | Couples `Program.cs` to the hosted-service instance and races host shutdown. |

**Why `Wallow.ServiceDefaults`:** both hosts already `ProjectReference` it, so no new edges are
introduced into the dependency graph. Namespace is `Wallow.ServiceDefaults`.

**Scope note:** the bead is titled for `SeederService`, but `MigrationService` has the same defect
and is arguably worse — `MigrationWorker.ExecuteAsync` has no `try`/`catch`/`finally` at all, so a
failed migration is not even logged Critical by our own code. `docker-compose.test.yml` gates both
`wallow-seeder` and `wallow-migrations` on `service_completed_successfully`. Fixing only the seeder
leaves half the hole open. Phase 3 is separable into its own bead if you want to keep the bead
boundary strict.

---

## Task 1: The shared outcome sink

**Files:**

- Create: `api/src/Wallow.ServiceDefaults/WorkerRunOutcome.cs`

**Step 1: Write the type**

```csharp
namespace Wallow.ServiceDefaults;

/// <summary>
/// Records whether a one-shot worker's run failed, so <c>Program.cs</c> can turn that into a
/// non-zero process exit code.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> that throws does NOT fail the
/// process. The host catches the faulted <c>ExecuteAsync</c> task, logs it, stops the application,
/// and never rethrows — so <c>RunAsync()</c> completes successfully and the process exits 0. That
/// is true regardless of <c>BackgroundServiceExceptionBehavior</c>, which governs whether the host
/// stops, not the exit code. Without this sink a failed seed or migration reports success, and a
/// Compose <c>depends_on: condition: service_completed_successfully</c> edge lets dependents start
/// against a half-built database.
/// </para>
/// <para>
/// Resolve this from <c>host.Services</c> BEFORE awaiting <c>host.RunAsync()</c>. <c>RunAsync</c>
/// disposes the host in a <c>finally</c>, so resolving afterwards throws
/// <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
public sealed class WorkerRunOutcome
{
    /// <summary>
    /// Gets a value indicating whether the worker's run failed.
    /// </summary>
    public bool Failed { get; private set; }

    /// <summary>
    /// Gets the process exit code this run should produce.
    /// </summary>
    public int ExitCode => Failed ? 1 : 0;

    /// <summary>
    /// Marks the run as failed. Called from the worker's exception path.
    /// </summary>
    public void MarkFailed() => Failed = true;
}
```

No lock or `Interlocked` is needed: the single write happens on the worker's `ExecuteAsync` path,
and the read in `Program.cs` happens after `await host.RunAsync()`, which establishes the
happens-before edge.

**Step 2: Build**

Run: `dotnet build api/Wallow.slnx`
Expected: succeeds. (Warnings are errors here, and StyleCop requires the doc comments above — do
not strip them.)

**Step 3: Commit**

```bash
dotnet format api/Wallow.slnx
git add api/src/Wallow.ServiceDefaults/WorkerRunOutcome.cs
git commit -m "feat(hosting): add WorkerRunOutcome for one-shot worker exit codes"
```

---

## Task 2: Seeder — failing test first

**Files:**

- Test: `api/tests/Wallow.SeederService.Tests/SeederWorkerExitCodeTests.cs` (create)

**Step 1: Write the failing test**

The cheapest way to make a seed step throw is to hand the worker a scope whose `ServiceProvider`
resolves nothing — `GetRequiredService<RoleManager<WallowRole>>()` in step 1 then throws. That
exercises the real `ExecuteAsync` path without a database.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.ServiceDefaults;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Wallow-2y1t: a seed step that threw was logged Critical and then swallowed by the host, which
/// exits the process 0. docker-compose.test.yml gates wallow-api and wallow-web on the seeder's
/// <c>service_completed_successfully</c>, so a failed seed silently started the stack against a
/// database with zero OIDC clients. These tests pin the failure onto <see cref="WorkerRunOutcome"/>,
/// which Program.cs turns into a non-zero exit.
/// </summary>
public class SeederWorkerExitCodeTests
{
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly RecordingLogger<SeederWorker> _logger = new();
    private readonly WorkerRunOutcome _outcome = new();

    [Fact]
    public async Task ExecuteAsync_WhenASeedStepThrows_MarksTheRunFailed()
    {
        using SeederWorker worker = CreateWorkerWithEmptyScope();

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _outcome.Failed.Should().BeTrue(
            "a thrown seed step must reach Program.cs as a non-zero exit code, or Compose's "
            + "service_completed_successfully gate lets dependents start against a half-seeded database");
        _outcome.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenASeedStepThrows_LogsCritical()
    {
        using SeederWorker worker = CreateWorkerWithEmptyScope();

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task ExecuteAsync_WhenASeedStepThrows_StillStopsTheApplication()
    {
        using SeederWorker worker = CreateWorkerWithEmptyScope();

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public void WorkerRunOutcome_BeforeAnyFailure_ExitsZero()
    {
        // The happy path must not regress: a successful seed still has to satisfy Compose's
        // service_completed_successfully edge.
        _outcome.Failed.Should().BeFalse();
        _outcome.ExitCode.Should().Be(0);
    }

    /// <summary>
    /// Drives the real <c>ExecuteAsync</c>. <c>StartAsync</c> returns as soon as the worker yields,
    /// so the faulted task must be awaited through <c>ExecuteTask</c>; awaiting only
    /// <c>StartAsync</c> would miss a failure that happens after the first await.
    /// </summary>
    private static Func<Task> RunToCompletionAsync(SeederWorker worker) => async () =>
    {
        await worker.StartAsync(CancellationToken.None);

        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
    };

    private SeederWorker CreateWorkerWithEmptyScope()
    {
        // An empty provider: the first step's GetRequiredService<RoleManager<WallowRole>>() throws,
        // which is exactly how the Wallow-smvc DI gap surfaced in the wild.
        ServiceCollection services = new();
        ServiceProvider emptyProvider = services.BuildServiceProvider();

        IServiceScope scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(emptyProvider);

        IServiceScopeFactory inner = Substitute.For<IServiceScopeFactory>();
        inner.CreateScope().Returns(scope);
        _scopeFactory.CreateScope().Returns(scope);

        return new SeederWorker(
            _scopeFactory,
            Options.Create(new SeedOptions()),
            _lifetime,
            _outcome,
            _logger);
    }
}
```

> **Note on `CreateAsyncScope`:** `SeederWorker` calls `scopeFactory.CreateAsyncScope()`, an
> extension method that calls `CreateScope()` and casts the result to `IAsyncDisposable`. The
> substituted `IServiceScope` must therefore also implement `IAsyncDisposable`. If the test fails
> with an `InvalidCastException` rather than the expected `InvalidOperationException`, substitute
> the two interfaces together:
> `IServiceScope scope = Substitute.For<IServiceScope, IAsyncDisposable>();`
> Verify this when you run Step 2 and adjust before proceeding.

**Step 2: Run the test to verify it fails**

Run: `./scripts/run-tests.sh seeder`
Expected: FAIL to **compile** — `SeederWorker` has no `WorkerRunOutcome` constructor parameter, and
`Wallow.SeederService.Tests` has no reference to `Wallow.ServiceDefaults`. That compile failure is
the red phase.

**Step 3: Add the missing test project reference**

Modify `api/tests/Wallow.SeederService.Tests/Wallow.SeederService.Tests.csproj`, adding to the
existing `ProjectReference` group:

```xml
<ProjectReference Include="..\..\src\Wallow.ServiceDefaults\Wallow.ServiceDefaults.csproj" />
```

Re-run `./scripts/run-tests.sh seeder`. Expected: still fails to compile, now only on the
`SeederWorker` constructor arity. Good — that is the exact gap Task 3 closes.

---

## Task 3: Seeder — make it pass

**Files:**

- Modify: `api/src/Wallow.SeederService/SeederWorker.cs:11-46`
- Modify: `api/src/Wallow.SeederService/Program.cs:29-32`

**Step 1: Take the sink on the worker's constructor**

```csharp
public sealed partial class SeederWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> seedOptions,
    IHostApplicationLifetime lifetime,
    WorkerRunOutcome outcome,
    ILogger<SeederWorker> logger) : BackgroundService
```

Add `using Wallow.ServiceDefaults;` to the file's using block (alphabetical order — StyleCop
enforces it, and `Wallow.ServiceDefaults` sorts before `Wallow.Identity.*`? No: it sorts *after*
`Wallow.Identity.Infrastructure.Services`. Let `dotnet format` settle it).

**Step 2: Mark the outcome on the failure path**

Change only the `catch` block of `ExecuteAsync`:

```csharp
        catch (Exception ex)
        {
            // The host swallows this after logging it: RunAsync() still completes successfully and
            // the process would exit 0. Program.cs reads this flag to exit non-zero instead.
            outcome.MarkFailed();
            LogSeederFailed(ex);
            throw;
        }
```

Leave the `try`, the step order, and `finally { lifetime.StopApplication(); }` untouched.

**Step 3: Register the sink and return the exit code**

In `api/src/Wallow.SeederService/Program.cs`, add `using Wallow.ServiceDefaults;` at the top, then:

```csharp
builder.Services.AddSingleton<WorkerRunOutcome>();
builder.Services.AddHostedService<SeederWorker>();

IHost host = builder.Build();

// Resolve BEFORE RunAsync: RunAsync disposes the host in a finally, so resolving afterwards throws
// ObjectDisposedException.
WorkerRunOutcome outcome = host.Services.GetRequiredService<WorkerRunOutcome>();

await host.RunAsync();

return outcome.ExitCode;
```

`Program.cs` is excluded from coverage (`[*]*Program` in `coverage.runsettings`), so this needs no
test of its own.

**Step 4: Run the tests to verify they pass**

Run: `./scripts/run-tests.sh seeder`
Expected: PASS — 18 tests (14 existing + 4 new).

**Step 5: Commit**

```bash
dotnet format api/Wallow.slnx
git add api/src/Wallow.SeederService api/tests/Wallow.SeederService.Tests
git commit -m "fix(seeder): exit non-zero when a seed step fails"
```

---

## Task 4: Share `RecordingLogger`

`RecordingLogger<T>` is `internal` in `Wallow.SeederService.Tests`. Task 5's migration test needs
the same helper, and copying a 34-line helper into a second test project is the wrong answer —
`Wallow.Tests.Common` exists for exactly this.

**Files:**

- Move: `api/tests/Wallow.SeederService.Tests/RecordingLogger.cs` → `api/tests/Wallow.Tests.Common/RecordingLogger.cs`
- Modify: `api/tests/Wallow.SeederService.Tests/Wallow.SeederService.Tests.csproj`

**Step 1: Move and widen**

```bash
git mv api/tests/Wallow.SeederService.Tests/RecordingLogger.cs api/tests/Wallow.Tests.Common/RecordingLogger.cs
```

Change the namespace to `Wallow.Tests.Common` and both types from `internal sealed` to
`public sealed` (`RecordingLogger<T>` and `LogEntry`). Keep the existing XML doc comment about
`Microsoft.Gen.Logging` clearing thread-local state — it is the reason the helper exists.

**Step 2: Reference it from the seeder tests**

Add to `Wallow.SeederService.Tests.csproj`:

```xml
<ProjectReference Include="..\Wallow.Tests.Common\Wallow.Tests.Common.csproj" />
```

Add `using Wallow.Tests.Common;` to `SeederWorkerBootstrapAdminTests.cs` and
`SeederWorkerExitCodeTests.cs`.

**Step 3: Verify nothing broke**

Run: `./scripts/run-tests.sh seeder`
Expected: PASS, 18 tests. If `Wallow.Tests.Common` lacks
`Microsoft.Extensions.Logging.Abstractions`, add the `PackageReference` — it is in
`Directory.Packages.props` already.

**Step 4: Commit**

```bash
dotnet format api/Wallow.slnx
git add api/tests
git commit -m "test: move RecordingLogger into Wallow.Tests.Common for reuse"
```

---

## Task 5: Migration service — test project + failing test

`Wallow.MigrationService` has no test project. Create one.

**Files:**

- Create: `api/tests/Wallow.MigrationService.Tests/Wallow.MigrationService.Tests.csproj`
- Create: `api/tests/Wallow.MigrationService.Tests/MigrationWorkerExitCodeTests.cs`
- Modify: `api/Wallow.slnx:70` (the `/tests/` folder block)
- Modify: `scripts/run-tests.sh:30` (shorthand table)

**Step 1: Create the csproj** (modelled on the seeder's, minus the Identity references)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Wallow.MigrationService.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="AwesomeAssertions" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="AwesomeAssertions" />
    <Using Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Wallow.MigrationService\Wallow.MigrationService.csproj" />
    <ProjectReference Include="..\..\src\Wallow.ServiceDefaults\Wallow.ServiceDefaults.csproj" />
    <ProjectReference Include="..\Wallow.Tests.Common\Wallow.Tests.Common.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Register it in the solution**

In `api/Wallow.slnx`, inside `<Folder Name="/tests/">`, add between the `Architecture.Tests` and
`SeederService.Tests` lines (the block is alphabetical):

```xml
    <Project Path="tests/Wallow.MigrationService.Tests/Wallow.MigrationService.Tests.csproj" />
```

**Step 3: Add the runner shorthand**

In `scripts/run-tests.sh`, beside the existing `seeder)` line:

```bash
        migrations)      echo "$REPO_ROOT/api/tests/Wallow.MigrationService.Tests" ;;
```

**Step 4: Write the failing test**

`IMigrationRunner` is a plain interface, so a throwing runner is a one-line substitute — no
database needed.

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.ServiceDefaults;
using Wallow.Tests.Common;

namespace Wallow.MigrationService.Tests;

/// <summary>
/// Wallow-2y1t: MigrationWorker.ExecuteAsync had no try/catch at all, so a failed migration was
/// neither logged Critical by our own code nor reflected in the process exit code — the host
/// swallowed it and the container exited 0. docker-compose.test.yml gates wallow-seeder on
/// wallow-migrations' service_completed_successfully, so an unmigrated database cascaded silently.
/// </summary>
public class MigrationWorkerExitCodeTests
{
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly RecordingLogger<MigrationWorker> _logger = new();
    private readonly WorkerRunOutcome _outcome = new();

    [Fact]
    public async Task ExecuteAsync_WhenACoreMigrationThrows_MarksTheRunFailed()
    {
        using MigrationWorker worker = CreateWorker(ThrowingRunner());

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _outcome.Failed.Should().BeTrue();
        _outcome.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenACoreMigrationThrows_LogsCritical()
    {
        using MigrationWorker worker = CreateWorker(ThrowingRunner());

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task ExecuteAsync_WhenACoreMigrationThrows_StopsTheApplication()
    {
        using MigrationWorker worker = CreateWorker(ThrowingRunner());

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        // Before this change the failure path never called StopApplication at all; only the host's
        // own StopHost default stopped the process.
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task ExecuteAsync_WhenEveryMigrationSucceeds_ExitsZero()
    {
        using MigrationWorker worker = CreateWorker(SucceedingRunner());

        await RunToCompletionAsync(worker)();

        _outcome.Failed.Should().BeFalse();
        _outcome.ExitCode.Should().Be(0);
        _lifetime.Received(1).StopApplication();
    }

    private static Func<Task> RunToCompletionAsync(MigrationWorker worker) => async () =>
    {
        await worker.StartAsync(CancellationToken.None);

        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
    };

    private static IMigrationRunner ThrowingRunner()
    {
        IMigrationRunner runner = Substitute.For<IMigrationRunner>();
        runner.ContextName.Returns("FailingDbContext");
        runner.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("migration failed")));
        return runner;
    }

    private static IMigrationRunner SucceedingRunner()
    {
        IMigrationRunner runner = Substitute.For<IMigrationRunner>();
        runner.ContextName.Returns("HealthyDbContext");
        runner.MigrateAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return runner;
    }

    private MigrationWorker CreateWorker(IMigrationRunner coreRunner) =>
        new(
            new CoreMigrationRunners([coreRunner]),
            new FeatureMigrationRunners([]),
            _lifetime,
            _outcome,
            _logger);
}
```

**Step 5: Run to verify it fails**

Run: `./scripts/run-tests.sh migrations`
Expected: FAIL to compile — `MigrationWorker` has no `WorkerRunOutcome` parameter.

---

## Task 6: Migration service — make it pass

**Files:**

- Modify: `api/src/Wallow.MigrationService/MigrationWorker.cs:3-26`
- Modify: `api/src/Wallow.MigrationService/Program.cs` (last 3 lines)

**Step 1: Add the sink and wrap the body**

```csharp
using Wallow.ServiceDefaults;

namespace Wallow.MigrationService;

public sealed partial class MigrationWorker(
    CoreMigrationRunners coreRunners,
    FeatureMigrationRunners featureRunners,
    IHostApplicationLifetime lifetime,
    WorkerRunOutcome outcome,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogMigrationStarted();

        try
        {
            // Core contexts must be migrated first (Identity, Audit, AuthAudit) - sequentially
            foreach (IMigrationRunner runner in coreRunners.Runners)
            {
                LogMigratingContext(runner.ContextName);
                await runner.MigrateAsync(stoppingToken);
            }

            // Feature module contexts can be migrated in parallel
            LogMigratingFeatureModules();
            await Task.WhenAll(featureRunners.Runners.Select(runner => runner.MigrateAsync(stoppingToken)));

            LogMigrationCompleted();
        }
        catch (Exception ex)
        {
            // The host swallows this after logging it, exiting the process 0. Program.cs reads this
            // flag to exit non-zero instead, so Compose's service_completed_successfully gate holds.
            outcome.MarkFailed();
            LogMigrationFailed(ex);
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
```

Note `lifetime.StopApplication()` moves out of the success path into `finally` — previously a
failed migration never called it.

**Step 2: Add the Critical log message**

Beside the other `[LoggerMessage]` declarations:

```csharp
    [LoggerMessage(Level = LogLevel.Critical, Message = "Database migration failed")]
    private partial void LogMigrationFailed(Exception ex);
```

**Step 3: Register and return the exit code**

In `api/src/Wallow.MigrationService/Program.cs`, add `using Wallow.ServiceDefaults;` and change the
tail:

```csharp
builder.Services.AddSingleton<WorkerRunOutcome>();
builder.Services.AddHostedService<MigrationWorker>();

IHost host = builder.Build();

// Resolve BEFORE RunAsync: RunAsync disposes the host in a finally.
WorkerRunOutcome outcome = host.Services.GetRequiredService<WorkerRunOutcome>();

await host.RunAsync();

return outcome.ExitCode;
```

**Step 4: Run to verify it passes**

Run: `./scripts/run-tests.sh migrations`
Expected: PASS — 4 tests.

**Step 5: Commit**

```bash
dotnet format api/Wallow.slnx
git add api/src/Wallow.MigrationService api/tests/Wallow.MigrationService.Tests api/Wallow.slnx scripts/run-tests.sh
git commit -m "fix(migrations): exit non-zero when a migration fails"
```

---

## Task 7: Verification

**Step 1: Full backend suite**

Run: `./scripts/run-tests.sh`
Expected: PASS. Watch `arch` in particular — `Wallow.Architecture.Tests` enforces module
boundaries, and `WorkerRunOutcome` is new public surface in `ServiceDefaults`.

**Step 2: Prove the happy path still exits 0 against a real database**

```bash
docker exec wallow-postgres psql -U wallow -d postgres -c 'DROP DATABASE IF EXISTS wallow_exitcheck;' -c 'CREATE DATABASE wallow_exitcheck;'
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=wallow_exitcheck;Username=wallow;Password=$(docker exec wallow-postgres printenv POSTGRES_PASSWORD)"
dotnet run --project api/src/Wallow.MigrationService; echo "MIGRATION EXIT = $?"
dotnet run --project api/src/Wallow.SeederService;   echo "SEEDER EXIT = $?"
```

Expected: both `0`. Then confirm the data landed:

```bash
docker exec wallow-postgres psql -U wallow -d wallow_exitcheck -c 'select (select count(*) from identity."OpenIddictApplications") as oidc_clients, (select count(*) from identity.users) as users, (select count(*) from identity.organizations) as orgs;'
```

Expected: `3 | 1 | 1`.

**Step 3: Prove the failure path exits 1 (the actual regression)**

Point the seeder at a database that has no schema. Every step fails at the first query.

```bash
docker exec wallow-postgres psql -U wallow -d postgres -c 'DROP DATABASE IF EXISTS wallow_unmigrated;' -c 'CREATE DATABASE wallow_unmigrated;'
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=wallow_unmigrated;Username=wallow;Password=$(docker exec wallow-postgres printenv POSTGRES_PASSWORD)" \
  dotnet run --project api/src/Wallow.SeederService; echo "SEEDER EXIT = $?"
```

Expected **before** this change: `0` (the bug). Expected **after**: `1`.

**Step 4: Clean up the scratch databases**

```bash
docker exec wallow-postgres psql -U wallow -d postgres -c 'DROP DATABASE IF EXISTS wallow_exitcheck;' -c 'DROP DATABASE IF EXISTS wallow_unmigrated;'
```

**Step 5: End-to-end**

Run: `./scripts/e2e.sh`
Expected: PASS unchanged. This is the regression risk that matters — if a seed step was failing
silently, e2e will now fail loudly here, and that failure is real, not caused by this change.

**Step 6: Close out**

```bash
bd close Wallow-2y1t
git pull --rebase && bd dolt push && git push
```

Per the root `CLAUDE.md`, `bd dolt push` is not optional and `git push` does not carry beads.
Confirm with `git ls-remote origin refs/dolt/data` that the hash changed.

---

## Risks

| Risk | Mitigation |
| ---- | ---------- |
| A seed step is already failing intermittently and nobody noticed | Task 7 Step 5 surfaces it. Treat any new e2e failure as a pre-existing bug this change revealed, not as a regression from it. |
| `Substitute.For<IServiceScope>()` fails the `CreateAsyncScope` cast | Substitute `IServiceScope, IAsyncDisposable` together (noted inline in Task 2). |
| `ObjectDisposedException` from resolving the sink after `RunAsync` | Every `Program.cs` snippet resolves before. Do not "tidy" this into one line. |
| Aspire AppHost treats a non-zero seeder exit as a failed resource | Correct behaviour — that is the point. Only occurs when seeding actually failed. |
