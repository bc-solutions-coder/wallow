**status: completed**

# Wolverine DLQ Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Wolverine dead-letter-queue failures visible — a depth gauge and health signal on the existing OpenTelemetry pipeline, plus a guaranteed Error-level log when an envelope is dead-lettered.

**Architecture:** Three thin additions to existing seams, no new pipeline. (1) `Wallow.ServiceDefaults` registers Wolverine's runtime meter (`Wolverine:{ServiceName}`) with the OTel SDK, which un-drops the built-in `wolverine-dead-letter-queue` counter. (2) A new `WolverineDeadLetterQueueHealthCheck` in `Wallow.Api` queries Wolverine's message storage via `IMessageStore.Admin.FetchCountsAsync()`, reports Degraded when the DLQ is non-empty, and records depth on a `Wallow.Messaging` gauge; the existing `HealthCheckMetricsPublisher` then also exports it as `wallow.healthcheck.status{check_name="wolverine-dlq"}` for free. (3) The Error log already exists inside Wolverine (`Envelope {envelope} was moved to the error queue`, event id 108, exception attached) — we pin it rather than duplicate it: an integration test asserts the `MovedToErrorQueue` runtime event fires on retry exhaustion, and an architecture test pins the Serilog `"Wolverine"` override at an Error-passing level so config can never silence it.

**Tech Stack:** .NET 10, Wolverine 6.21 (`WolverineFx` + Postgres durability), OpenTelemetry SDK, xUnit + FluentAssertions + NSubstitute, Testcontainers via `WallowApiFactory`.

**Spec:** Bead `Wallow-qi90.2` (`bd show Wallow-qi90.2`) — "add DLQ observability for Wolverine handler failures". Wants (a) DLQ depth surfaced as a metric/health signal and (b) an Error log at dead-letter time naming the message type and exception, hung off the existing OTel pipeline (`Wallow.ServiceDefaults` + `CustomInstrumentExportTests`), not a new one. Origin: the SendEmailHandler incident, where failures landed silently in the DLQ behind an HTTP 200.

## Global Constraints

- **No `var`** — explicit types everywhere (`api/CLAUDE.md`).
- **Never use `--` inside an XML doc comment** — MSB4025-class problem; also reads wrong in C# XML docs. Spell out flag names in prose.
- **Backend tests only via `./scripts/run-tests.sh`** — never bare `dotnet test`. Shorthands used here: `arch` (Wallow.Architecture.Tests), `api` (Wallow.Api.Tests fast tests), `api integration` (Wallow.Api.Tests integration tier, needs Docker).
- **`TreatWarningsAsErrors` is on** — an unused `using` (IDE0005) is a build break. Run `dotnet format api/Wallow.slnx` before every commit.
- **Conventional commits**, lowercase imperative, first line < 72 chars, module scope where relevant.
- **`Xunit` and `FluentAssertions` are global usings** in every test project — do not add `using Xunit;` / `using FluentAssertions;` to test files.
- **Do not touch `api/Directory.Packages.props`** — it carries unrelated uncommitted dependency bumps that stay out of these commits.
- **Blanket `AddMeter("*")` is banned** by `AddServiceDefaults_ShouldNotExport_MetersOutsideTheConfiguredPrefix` — Wolverine's meter must be registered by a scoped pattern.
- The bead: claim with `bd update Wallow-qi90.2 --status in_progress` before Task 1; close and sync at the end (Task 6).

## Verified facts the plan builds on

Every claim below was checked against Wolverine 6.21.0 (the version in `api/Directory.Packages.props`) or this repo's source — do not re-derive them, but do not "fix" code that contradicts them either:

- Wolverine's runtime meter is named `"Wolverine:" + options.ServiceName`; `ServiceName` defaults to the application assembly name. `Program.cs` pins `opts.ApplicationAssembly = typeof(WallowModules).Assembly`, so the meter is **`Wolverine:Wallow.Api`**. The built-in `wolverine-dead-letter-queue` counter (tags include `message.type`) lives on it.
- `ConfigureOpenTelemetry` (`api/src/Wallow.ServiceDefaults/Extensions.cs:96`) currently calls `AddMeter(namespacePrefix, moduleNamespaces)` only — Wolverine's meter is recorded in-process and thrown away. The `*` wildcard in `AddMeter` is a suffix match (the existing tests rely on this for `Wallow.*`).
- `IMessageStore` (namespace `Wolverine.Persistence.Durability`) is in DI courtesy of `UseWolverine`. `IMessageStore.Admin.FetchCountsAsync()` returns `Wolverine.Logging.PersistedCounts` with settable `int` properties `Incoming`, `Scheduled`, `Outgoing`, `Handled`, `DeadLetter` and a parameterless ctor — `new PersistedCounts { DeadLetter = 3 }` compiles.
- In the Testing environment durable inbox/outbox are skipped but Postgres message persistence is still configured; `MessageContext.MoveToDeadLetterQueueAsync` falls through to `Storage.Inbox.MoveToDeadLetterStorageAsync` for local queues, so dead letters DO land in the `wolverine.wolverine_dead_letters` table under `WallowApiFactory`.
- The same runtime method (`WolverineRuntime.Tracking.cs` → `MovedToErrorQueue`) increments the dead-letter counter, raises the tracked `MovedToErrorQueue` event, and writes the Error log `"Envelope {envelope} was moved to the error queue"` (event id 108) with the exception attached — one tracked event pins all three.
- Health endpoint mapping (`Program.cs:664-685`): `/health` runs every check (`Predicate = _ => true`), `/health/ready` only `"ready"`-tagged, `/health/startup` only `"startup"`-tagged. In the Testing/Development environments `WriteHealthCheckResponse` emits per-check JSON: `{ status, duration, checks: [{ name, status, duration, description, error }] }` (camelCase).
- `SendEmailValidator` rejects a `To` that is not an email address, and validation failure exhausts retries into the DLQ — publishing `InquiryStatusChangedEvent` with a non-email `SubmitterEmail` dead-letters exactly one handler with zero test doubles (the established pattern in `MultipleHandlerSeparationTests`).
- `NSubstitute` 5.3.0 is referenced by `Wallow.Api.Tests`.
- Serilog override `"Wolverine": "Warning"` exists in `appsettings.json` and `appsettings.Production.json`; the other three appsettings files carry no Wolverine override. `Warning` passes Error-level events.

---

### Task 1: Export Wolverine's runtime meter from ServiceDefaults

**Files:**
- Modify: `api/tests/Wallow.Architecture.Tests/CustomInstrumentExportTests.cs` (add one theory after `AddServiceDefaults_ShouldNotExport_MetersOutsideTheConfiguredPrefix`, ~line 120)
- Modify: `api/src/Wallow.ServiceDefaults/Extensions.cs:96` (the `AddMeter` call) and add one constant

**Interfaces:**
- Consumes: the existing `IsMeterCollected(string probeMeterName, string? namespacePrefix = null, string? alsoRegister = null)` helper in the same test file.
- Produces: `ConfigureOpenTelemetry` registers the meter pattern `"Wolverine:*"`. Task 5's docs must state this pattern verbatim.

- [ ] **Step 1: Write the failing test**

In `CustomInstrumentExportTests.cs`, immediately after `AddServiceDefaults_ShouldNotExport_MetersOutsideTheConfiguredPrefix` (before the `// ---- traces` divider), add:

```csharp
    [Theory]
    [InlineData("Wolverine:Wallow.Api")]
    [InlineData("Wolverine:Contoso.Api")]
    public void AddServiceDefaults_ShouldExport_TheWolverineRuntimeMeter(string meterName)
    {
        bool collected = IsMeterCollected(meterName);

        collected.Should().BeTrue(
            "Wolverine records its built-in instruments (wolverine-dead-letter-queue, " +
            "wolverine-inbox-count, …) on a meter named \"Wolverine:\" + ServiceName, which " +
            "defaults to the application assembly name — so the registration must be the " +
            "wildcard pattern \"Wolverine:*\", or \"{0}\" is recorded in-process and thrown " +
            "away and a dead-letter pile-up stays invisible (Wallow-qi90.2)",
            meterName);
    }
```

The `Contoso.Api` case proves the registration survives a fork renaming the API assembly (Wolverine derives `ServiceName` from the assembly, not from `Logging:NamespacePrefix` — that is why this theory takes no `namespacePrefix` argument).

- [ ] **Step 2: Run the test to verify it fails**

Run: `./scripts/run-tests.sh arch`
Expected: both `AddServiceDefaults_ShouldExport_TheWolverineRuntimeMeter` cases FAIL (`collected` is false); everything else green.

- [ ] **Step 3: Implement the registration**

In `api/src/Wallow.ServiceDefaults/Extensions.cs`, add a constant after `DefaultNamespacePrefix` (line 35):

```csharp
    /// <summary>
    /// Wolverine names its runtime meter "Wolverine:" + ServiceName, and ServiceName defaults to
    /// the application assembly name, so this is a wildcard rather than a literal: it must keep
    /// matching when a fork renames the API assembly. That meter carries the built-in messaging
    /// instruments, including the dead-letter counter this repo alerts on (Wallow-qi90.2).
    /// </summary>
    private const string WolverineMeterPattern = "Wolverine:*";
```

Then change the metrics registration (line 96) from:

```csharp
                    .AddMeter(namespacePrefix, moduleNamespaces);
```

to:

```csharp
                    .AddMeter(namespacePrefix, moduleNamespaces, WolverineMeterPattern);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `./scripts/run-tests.sh arch`
Expected: PASS, including the pre-existing `AddServiceDefaults_ShouldNotExport_MetersOutsideTheConfiguredPrefix` (the probe meter `Zzz.Unrelated.ThirdParty` does not match `Wolverine:*`).

- [ ] **Step 5: Format and commit**

```bash
dotnet format api/Wallow.slnx
git add api/src/Wallow.ServiceDefaults/Extensions.cs api/tests/Wallow.Architecture.Tests/CustomInstrumentExportTests.cs
git commit -m "feat(observability): export the wolverine runtime meter from service defaults"
```

---

### Task 2: `WolverineDeadLetterQueueHealthCheck` with a depth gauge

**Files:**
- Create: `api/src/Wallow.Api/HealthChecks/WolverineDeadLetterQueueHealthCheck.cs`
- Test: `api/tests/Wallow.Api.Tests/HealthChecks/WolverineDeadLetterQueueHealthCheckTests.cs`

**Interfaces:**
- Consumes: `Wolverine.Persistence.Durability.IMessageStore` (from DI, registered by `UseWolverine`), `Wallow.Shared.Kernel.Diagnostics.CreateMeter(string)`.
- Produces: `internal sealed class WolverineDeadLetterQueueHealthCheck(IMessageStore messageStore) : IHealthCheck` — Healthy when `PersistedCounts.DeadLetter == 0`, Degraded (never Unhealthy) when > 0, Unhealthy only when storage is unreachable. Records `wallow.messaging.dead_letter_queue.depth` (Gauge&lt;long&gt;, meter `Wallow.Messaging`) on every evaluation. Task 3 registers it under the check name `"wolverine-dlq"`; Task 5 documents the gauge.

- [ ] **Step 1: Write the failing tests**

Create `api/tests/Wallow.Api.Tests/HealthChecks/WolverineDeadLetterQueueHealthCheckTests.cs`:

```csharp
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Wallow.Api.HealthChecks;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;

namespace Wallow.Api.Tests.HealthChecks;

public class WolverineDeadLetterQueueHealthCheckTests
{
    private static IMessageStore StoreWithDeadLetterCount(int deadLetterCount)
    {
        // NSubstitute auto-substitutes the Admin property, so only the count needs configuring.
        IMessageStore store = Substitute.For<IMessageStore>();
        store.Admin.FetchCountsAsync().Returns(new PersistedCounts { DeadLetter = deadLetterCount });
        return store;
    }

    [Fact]
    public async Task CheckHealthAsync_WithAnEmptyDeadLetterQueue_ReturnsHealthy()
    {
        WolverineDeadLetterQueueHealthCheck sut = new(StoreWithDeadLetterCount(0));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithDeadLetteredEnvelopes_ReturnsDegraded_NamingTheCount()
    {
        WolverineDeadLetterQueueHealthCheck sut = new(StoreWithDeadLetterCount(3));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(
            HealthStatus.Degraded,
            "a poison message is degraded service, not a dead process — Unhealthy would let a " +
            "single bad envelope fail orchestrator probes and restart-loop the container");
        result.Description.Should().Contain("3");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageIsUnreachable_ReturnsUnhealthy_WithTheException()
    {
        IMessageStore store = Substitute.For<IMessageStore>();
        InvalidOperationException failure = new("storage down");
        store.Admin.FetchCountsAsync().ThrowsAsync(failure);
        WolverineDeadLetterQueueHealthCheck sut = new(store);

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task CheckHealthAsync_RecordsTheDepthGauge()
    {
        long? recordedDepth = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "wallow.messaging.dead_letter_queue.depth")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (_, measurement, _, _) => recordedDepth = measurement);
        listener.Start();

        WolverineDeadLetterQueueHealthCheck sut = new(StoreWithDeadLetterCount(7));
        await sut.CheckHealthAsync(new HealthCheckContext());

        recordedDepth.Should().Be(
            7,
            "the health check doubles as the depth sampler: the periodic health publisher drives " +
            "it, so every evaluation must land the current queue depth on the messaging meter");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./scripts/run-tests.sh api`
Expected: FAIL to compile — `WolverineDeadLetterQueueHealthCheck` does not exist. That is the expected red state for a new class.

- [ ] **Step 3: Write the implementation**

Create `api/src/Wallow.Api/HealthChecks/WolverineDeadLetterQueueHealthCheck.cs`:

```csharp
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wallow.Shared.Kernel;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;

namespace Wallow.Api.HealthChecks;

/// <summary>
/// Surfaces the Wolverine dead-letter queue as a health signal and a depth gauge (Wallow-qi90.2).
/// A dead-lettered envelope is work the API accepted, answered 200 for, and then silently dropped
/// after retry exhaustion; before this check the only evidence was a row in
/// <c>wolverine.wolverine_dead_letters</c> that nothing read. A non-empty queue reports
/// <see cref="HealthStatus.Degraded" /> rather than Unhealthy, and the registration deliberately
/// omits the "ready" tag: a poison message must show up on <c>/health</c> without failing
/// readiness probes and restart-looping the container. The depth lands on the
/// <c>Wallow.Messaging</c> meter every evaluation, so the periodic health publisher doubles as
/// the metric sampler.
/// </summary>
internal sealed class WolverineDeadLetterQueueHealthCheck(IMessageStore messageStore) : IHealthCheck
{
    private static readonly Meter _messagingMeter = Diagnostics.CreateMeter("Messaging");

    private static readonly Gauge<long> _deadLetterQueueDepth = _messagingMeter.CreateGauge<long>(
        "wallow.messaging.dead_letter_queue.depth",
        description: "Number of envelopes currently in the Wolverine dead-letter queue");

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PersistedCounts counts = await messageStore.Admin.FetchCountsAsync();
            _deadLetterQueueDepth.Record(counts.DeadLetter);

            return counts.DeadLetter == 0
                ? HealthCheckResult.Healthy("Dead-letter queue is empty.")
                : HealthCheckResult.Degraded(
                    $"{counts.DeadLetter} envelope(s) in the Wolverine dead-letter queue.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Could not query Wolverine message storage.", ex);
        }
    }
}
```

Notes for the implementer: the static-meter-field shape mirrors `HealthCheckMetricsPublisher.cs` in the same folder (the accepted pattern here); `FetchCountsAsync` takes no cancellation token — that is Wolverine's signature, not an oversight.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `./scripts/run-tests.sh api`
Expected: all four new tests PASS; no other failures.

- [ ] **Step 5: Format and commit**

```bash
dotnet format api/Wallow.slnx
git add api/src/Wallow.Api/HealthChecks/WolverineDeadLetterQueueHealthCheck.cs api/tests/Wallow.Api.Tests/HealthChecks/WolverineDeadLetterQueueHealthCheckTests.cs
git commit -m "feat(api): add a wolverine dead-letter queue health check with a depth gauge"
```

---

### Task 3: Register the check and pin the whole DLQ path with an integration test

**Files:**
- Modify: `api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs:61-77` (the `AddHealthChecks` chain)
- Test: `api/tests/Wallow.Api.Tests/Integration/DeadLetterObservabilityTests.cs` (create)

**Interfaces:**
- Consumes: `WolverineDeadLetterQueueHealthCheck` from Task 2; `WallowApiFactory` and `ApiIntegrationTestCollection` from `Wallow.Tests.Common` / the Integration folder; `InquiryStatusChangedEvent` from `Wallow.Shared.Contracts.Inquiries.Events` (properties: `InquiryId`, `OldStatus`, `NewStatus`, `ChangedAt`, `SubmitterEmail`).
- Produces: the check registered as name **`"wolverine-dlq"`** with tags `["messaging"]` — Task 5's docs and the `wallow.healthcheck.status{check_name="wolverine-dlq"}` series both use that exact name.

- [ ] **Step 1: Write the failing integration test**

Create `api/tests/Wallow.Api.Tests/Integration/DeadLetterObservabilityTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Tests.Common.Factories;
using Wolverine.Persistence.Durability;
using Wolverine.Tracking;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Pins the dead-letter observability chain end to end (Wallow-qi90.2): a handler that exhausts
/// its retries must terminate in the tracked <c>MovedToErrorQueue</c> event, leave a persisted
/// row Wolverine's storage counts can see, and degrade the <c>wolverine-dlq</c> entry on
/// <c>/health</c> — while never touching <c>/health/ready</c>, because a poison message must not
/// fail readiness and restart-loop the container.
/// <para>
/// The poison is data, not a test double: <c>SendEmailValidator</c> rejects a recipient that is
/// not an email address, so an <see cref="InquiryStatusChangedEvent" /> carrying one makes
/// exactly the email handler fail through the standard retry policy into the DLQ (the same
/// lever <c>MultipleHandlerSeparationTests</c> uses).
/// </para>
/// <para>
/// The tracked <c>MovedToErrorQueue</c> record is the pin on the Error log the bead asks for:
/// the same <c>WolverineRuntime</c> method raises the tracking event, increments the
/// <c>wolverine-dead-letter-queue</c> counter, and writes "Envelope … was moved to the error
/// queue" at Error with the exception attached. <c>WolverineDeadLetterLoggingTests</c> guards
/// the Serilog side, so together they pin log emission without capturing sinks.
/// </para>
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class DeadLetterObservabilityTests(WallowApiFactory factory)
{
    /// <summary>Not an email address, so the email handler dead-letters on validation.</summary>
    private const string PoisonedRecipient = "dlq-observability-probe-not-an-email";

    private static readonly TimeSpan _trackingTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task RetryExhaustion_LandsInTheDlq_AndDegradesTheHealthEndpoint()
    {
        ITrackedSession session = await factory.Services.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(_trackingTimeout)
            .PublishMessageAndWaitAsync(new InquiryStatusChangedEvent
            {
                InquiryId = Guid.NewGuid(),
                OldStatus = "New",
                NewStatus = "Reviewed",
                ChangedAt = DateTime.UtcNow,
                SubmitterEmail = PoisonedRecipient
            }, null);

        session.MovedToErrorQueue.RecordsInOrder()
            .Where(record => record.Message is InquiryStatusChangedEvent)
            .Should().NotBeEmpty(
                "retry exhaustion must terminate in the MovedToErrorQueue runtime event — the " +
                "one that increments the dead-letter counter and writes the Error log");

        IMessageStore messageStore = factory.Services.GetRequiredService<IMessageStore>();
        int depth = await WaitForDeadLetterDepthAsync(messageStore);

        depth.Should().BeGreaterThan(
            0,
            "the dead-lettered envelope must be persisted where FetchCountsAsync can count it, " +
            "or the health check has nothing to observe");

        using HttpClient client = factory.CreateClient();

        // GetAsync, not GetStringAsync: /health answers 503 whenever ANY check is unhealthy
        // (HealthCheckTests accepts both), and the detailed body is what this test is after.
        using HttpResponseMessage health = await client.GetAsync("/health");
        string healthBody = await health.Content.ReadAsStringAsync();
        using JsonDocument healthDocument = JsonDocument.Parse(healthBody);
        JsonElement dlqEntry = healthDocument.RootElement.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "wolverine-dlq");

        dlqEntry.GetProperty("status").GetString().Should().Be(
            "Degraded",
            "a non-empty dead-letter queue is degraded service: visible on /health, but never " +
            "a dead process");

        using HttpResponseMessage ready = await client.GetAsync("/health/ready");
        string readyBody = await ready.Content.ReadAsStringAsync();
        using JsonDocument readyDocument = JsonDocument.Parse(readyBody);

        readyDocument.RootElement.GetProperty("checks").EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .Should().NotContain(
                "wolverine-dlq",
                "the check must not carry the \"ready\" tag — a poison message failing " +
                "readiness would restart-loop the container without fixing anything");
    }

    /// <summary>
    /// The storage write happens on the dead-letter path itself, but poll briefly anyway so a
    /// slow container round-trip cannot flake this: the claim under test is "persisted", not
    /// "persisted within one scheduler tick".
    /// </summary>
    private static async Task<int> WaitForDeadLetterDepthAsync(IMessageStore messageStore)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        int depth = (await messageStore.Admin.FetchCountsAsync()).DeadLetter;

        while (depth == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            depth = (await messageStore.Admin.FetchCountsAsync()).DeadLetter;
        }

        return depth;
    }
}
```

Deliberate choices the implementer should not "improve": the test asserts the `wolverine-dlq` **entry**, never the report's overall status — sibling checks (hangfire, s3) are allowed to be whatever they are in the harness. `DeadLetter` is asserted `> 0`, not `== 1` — the collection shares one database with `MultipleHandlerSeparationTests`, which also dead-letters envelopes.

- [ ] **Step 2: Run the test to verify it fails on the missing registration**

Run: `./scripts/run-tests.sh api integration` (Docker required)
Expected: `RetryExhaustion_LandsInTheDlq_AndDegradesTheHealthEndpoint` FAILS at the `.Single(...)` over `/health` checks — no entry named `wolverine-dlq` exists yet. The tracking and depth assertions before it should already pass (they exercise Wolverine behavior that predates this bead).

- [ ] **Step 3: Register the health check**

In `api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs`, extend the `AddHealthChecks` chain — after the `.AddRedis(...)` call (ends line 73) and before `.AddCheck("startup", ...)` (line 74) — with:

```csharp
            // Not tagged "ready" on purpose: a poison message in the DLQ must degrade /health
            // without failing readiness probes and restart-looping the container (Wallow-qi90.2).
            .AddCheck<WolverineDeadLetterQueueHealthCheck>("wolverine-dlq", tags: ["messaging"])
```

No extra `using` is needed if the file already imports `Wallow.Api.HealthChecks` for `S3HealthCheck` and `HealthCheckMetricsPublisher`; add it only if the compiler asks.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `./scripts/run-tests.sh api integration`
Expected: PASS, including the pre-existing `HealthCheckTests` and `MultipleHandlerSeparationTests` (same collection, same database).

- [ ] **Step 5: Format and commit**

```bash
dotnet format api/Wallow.slnx
git add api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs api/tests/Wallow.Api.Tests/Integration/DeadLetterObservabilityTests.cs
git commit -m "feat(api): surface the wolverine dlq on /health as wolverine-dlq"
```

---

### Task 4: Pin the Serilog "Wolverine" override at an Error-passing level

**Files:**
- Test/Create: `api/tests/Wallow.Architecture.Tests/WolverineDeadLetterLoggingTests.cs`

**Interfaces:**
- Consumes: the committed `api/src/Wallow.Api/appsettings*.json` files (all five: base, Development, Production, Staging, Testing).
- Produces: nothing for later tasks — this is a pure guard. It is the config half of the "Error log" deliverable; the runtime half is Task 3's `MovedToErrorQueue` assertion.

This test intentionally has no red step: it pins existing correct configuration so it cannot silently regress (the same posture as the docs-content tests in `CustomInstrumentExportTests`). Verify it goes red by breaking the config by hand in Step 2, then restore.

- [ ] **Step 1: Write the test**

Create `api/tests/Wallow.Architecture.Tests/WolverineDeadLetterLoggingTests.cs`:

```csharp
using System.Text.Json;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Wallow does not write its own dead-letter log line, because Wolverine already does:
/// <c>WolverineRuntime</c> logs "Envelope {envelope} was moved to the error queue" at Error
/// (event id 108) with the exception attached, and the envelope rendering names the message
/// type — exactly what bead Wallow-qi90.2 asks for. The only thing this repo owns on that path
/// is the Serilog level override for the "Wolverine" source: raise it past Error in any
/// committed appsettings file and the sole terminal log of a dropped message vanishes, which is
/// how the SendEmailHandler incident stayed invisible. These tests pin every appsettings file
/// that carries the override to a level Error-level events pass through.
/// </summary>
public class WolverineDeadLetterLoggingTests
{
    /// <summary>Serilog levels that let an Error-level event through. Absent: Fatal.</summary>
    private static readonly string[] _errorPassingLevels =
        ["Verbose", "Debug", "Information", "Warning", "Error"];

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.Production.json")]
    [InlineData("appsettings.Staging.json")]
    [InlineData("appsettings.Testing.json")]
    public void SerilogConfig_MustNotSilence_WolverinesDeadLetterErrorLog(string fileName)
    {
        string path = Path.Combine(FindRepoRoot(), "api", "src", "Wallow.Api", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        string? effectiveLevel = ResolveEffectiveWolverineLevel(document.RootElement);

        if (effectiveLevel is null)
        {
            // No Serilog section, or one that names no level reaching the Wolverine source —
            // this file inherits from appsettings.json, which the base-file case covers.
            return;
        }

        _errorPassingLevels.Should().Contain(
            effectiveLevel,
            "{0} sets the minimum level governing the \"Wolverine\" log source, and anything " +
            "above Error swallows the only log line a dead-lettered envelope produces",
            fileName);
    }

    /// <summary>
    /// The level governing the "Wolverine" source in one file: its explicit override when
    /// present, otherwise the file's own Default, otherwise null (nothing declared here).
    /// </summary>
    private static string? ResolveEffectiveWolverineLevel(JsonElement root)
    {
        if (!root.TryGetProperty("Serilog", out JsonElement serilog)
            || !serilog.TryGetProperty("MinimumLevel", out JsonElement minimumLevel))
        {
            return null;
        }

        if (minimumLevel.TryGetProperty("Override", out JsonElement overrides)
            && overrides.TryGetProperty("Wolverine", out JsonElement wolverineOverride))
        {
            return wolverineOverride.GetString();
        }

        return minimumLevel.TryGetProperty("Default", out JsonElement defaultLevel)
            ? defaultLevel.GetString()
            : null;
    }

    private static string FindRepoRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "pnpm-workspace.yaml")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root (no pnpm-workspace.yaml found walking up from "
            + Directory.GetCurrentDirectory());
    }
}
```

(`FindRepoRoot` is duplicated from `CustomInstrumentExportTests` on purpose — it is private there, and a two-file helper extraction is not worth a shared type for a path walk.)

- [ ] **Step 2: Prove the test can fail, then run it green**

Temporarily change `"Wolverine": "Warning"` to `"Wolverine": "Fatal"` in `api/src/Wallow.Api/appsettings.json`, run `./scripts/run-tests.sh arch`, and confirm exactly the `appsettings.json` case FAILS. Revert the change (`git checkout -- api/src/Wallow.Api/appsettings.json`).

Run: `./scripts/run-tests.sh arch`
Expected: all five cases PASS.

- [ ] **Step 3: Commit**

```bash
dotnet format api/Wallow.slnx
git add api/tests/Wallow.Architecture.Tests/WolverineDeadLetterLoggingTests.cs
git commit -m "test(arch): pin the serilog wolverine override at an error-passing level"
```

---

### Task 5: Document the DLQ observability surface (docs pinned by tests)

**Files:**
- Modify: `api/tests/Wallow.Architecture.Tests/CustomInstrumentExportTests.cs` (documentation section, after `ObservabilityDocs_ShouldList_TheHealthCheckGauge_InTheCustomInstrumentTable`, ~line 228)
- Modify: `docs/operations/observability.md` (five edits, exact locations below)

**Interfaces:**
- Consumes: names fixed by earlier tasks, verbatim: pattern `Wolverine:*` (Task 1), gauge `wallow.messaging.dead_letter_queue.depth` and class `WolverineDeadLetterQueueHealthCheck` (Task 2), check name `wolverine-dlq` (Task 3), test class `WolverineDeadLetterLoggingTests` (Task 4).
- Produces: nothing further.

- [ ] **Step 1: Write the failing docs tests**

In `CustomInstrumentExportTests.cs`, after `ObservabilityDocs_ShouldList_TheHealthCheckGauge_InTheCustomInstrumentTable`, add:

```csharp
    [Fact]
    public void ObservabilityDocs_ShouldList_TheDeadLetterDepthGauge_InTheCustomInstrumentTable()
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().Contain(
            "wallow.messaging.dead_letter_queue.depth",
            "the custom instrument table is the census a reader audits against ServiceDefaults " +
            "registration, and the DLQ depth gauge recorded by WolverineDeadLetterQueueHealthCheck " +
            "is part of it (Wallow-qi90.2)");
    }

    [Fact]
    public void ObservabilityDocs_ShouldDocument_TheWolverineRuntimeMeterExport()
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().Contain(
            "Wolverine:*",
            "ConfigureOpenTelemetry registers Wolverine's runtime meter by this wildcard, and " +
            "the docs must say so or a reader auditing exported meters would conclude Wolverine " +
            "metrics are still dropped");

        source.Should().Contain(
            "wolverine-dead-letter-queue",
            "the built-in dead-letter counter is the alerting signal Wallow-qi90.2 exists to " +
            "surface — documenting the meter without naming it buries the lede");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./scripts/run-tests.sh arch`
Expected: both new docs facts FAIL (`observability.md` contains none of those strings yet).

- [ ] **Step 3: Edit `docs/operations/observability.md`**

Five edits:

**(a) Auto-instrumentation prose (lines 184-186).** Replace:

```markdown
Entity Framework Core and Wolverine instrumentation are **not** registered. Database calls appear
inside the ASP.NET Core server span rather than as their own spans, and Wolverine message handling
produces no dedicated spans (see [Message flow](#tracing-message-flow)).
```

with:

```markdown
Entity Framework Core and Wolverine **tracing** instrumentation are **not** registered. Database
calls appear inside the ASP.NET Core server span rather than as their own spans, and Wolverine
message handling produces no dedicated spans (see [Message flow](#tracing-message-flow)). Wolverine
**metrics** are the exception: its runtime meter is exported (see
[Wolverine runtime metrics](#wolverine-runtime-metrics)).
```

**(b) New section before `### Custom Instruments in the Codebase` (line 276).** Insert:

```markdown
### Wolverine Runtime Metrics

Wolverine records its built-in messaging instruments on a meter named `Wolverine:{ServiceName}`,
where `ServiceName` defaults to the application assembly name — `Wolverine:Wallow.Api` here.
`ConfigureOpenTelemetry` registers it with the wildcard pattern `Wolverine:*`, so a fork that
renames the API assembly keeps the export without touching ServiceDefaults. The instruments worth
alerting on:

- `wolverine-dead-letter-queue` — counter incremented each time an envelope is moved to the
  dead-letter queue, tagged with `message.type` and `message.destination`. Any non-zero rate means
  handlers are exhausting their retries and work is being dropped.
- `wolverine-inbox-count` / `wolverine-outbox-count` / `wolverine-scheduled-count` — gauges over
  the durable inbox, outbox, and scheduled-message tables.

### Dead-Letter Queue Observability

A dead-lettered envelope is work the API accepted (usually behind an HTTP 200) and then silently
dropped after retry exhaustion. Three signals cover it, all riding the standard pipeline:

1. **Depth gauge and health check** — `WolverineDeadLetterQueueHealthCheck`
   (`api/src/Wallow.Api/HealthChecks/WolverineDeadLetterQueueHealthCheck.cs`) counts the
   `wolverine.wolverine_dead_letters` table via Wolverine's `IMessageStore` and reports
   **Degraded** on `/health` (check name `wolverine-dlq`) while the queue is non-empty, recording
   the count on `wallow.messaging.dead_letter_queue.depth` each evaluation. The check is
   deliberately not tagged `ready`: a poison message must degrade `/health`, not fail
   `/health/ready` and restart-loop the container. Through `HealthCheckMetricsPublisher` it also
   surfaces as `wallow.healthcheck.status{check_name="wolverine-dlq"}`.
2. **Rate counter** — the `wolverine-dead-letter-queue` counter above fires at the moment of each
   dead-letter, tagged with the message type.
3. **Error log** — Wolverine logs `Envelope {envelope} was moved to the error queue` at `Error`
   with the exception attached; the envelope rendering names the message type. The Serilog
   override for the `Wolverine` source is `Warning`, so this always reaches the sinks —
   `WolverineDeadLetterLoggingTests` pins every committed appsettings file at an Error-passing
   level.

To inspect or replay stuck envelopes, query `wolverine.wolverine_dead_letters` directly or use
`IMessageStore.DeadLetters` (query, replay, discard).
```

**(c) Custom instrument table (lines 289-297).** Add one row after the `wallow.messaging.domain_events_published_total` row:

```markdown
| `wallow.messaging.dead_letter_queue.depth` | Gauge | `Wallow.Messaging` | `WolverineDeadLetterQueueHealthCheck` |
```

**(d) The `Exporting Custom Instruments` code snippet (~lines 310-320).** In the fenced C# block, change:

```csharp
    .AddMeter(namespacePrefix, moduleNamespaces);
```

to:

```csharp
    .AddMeter(namespacePrefix, moduleNamespaces, "Wolverine:*");
```

(match however the snippet renders that line — it is illustrative prose, so keep it consistent with the real `Extensions.cs` after Task 1; if the snippet shows the tracing half too, leave `AddSource` untouched).

**(e) Troubleshooting cross-link (line ~540).** The line "Check the Wolverine envelope tables for stuck or errored messages" — extend it to point at the new signals, e.g.:

```markdown
2. Check the Wolverine envelope tables for stuck or errored messages — the `wolverine-dlq` entry
   on `/health` and the `wallow.messaging.dead_letter_queue.depth` gauge surface the dead-letter
   count without a database session
```

- [ ] **Step 4: Run the tests to verify they pass, and build the docs site**

Run: `./scripts/run-tests.sh arch`
Expected: PASS, including the pre-existing stale-claim theories (`ObservabilityDocs_ShouldNotClaim_ThatCustomInstrumentsAreNeverExported` — none of the new prose reintroduces "no meters or activity sources", "never exported", or "until the meters are registered").

Then verify the docs build and anchors resolve: `./scripts/docs-serve.sh`, spot-check `/operations/observability.html` renders the new sections, Ctrl+C.

- [ ] **Step 5: Commit**

```bash
git add docs/operations/observability.md api/tests/Wallow.Architecture.Tests/CustomInstrumentExportTests.cs
git commit -m "docs(operations): document wolverine dlq observability"
```

---

### Task 6: Full gates, bead close-out, push

**Files:** none new — verification and bookkeeping only.

- [ ] **Step 1: Run the full backend gates**

```bash
dotnet format api/Wallow.slnx --verify-no-changes
./scripts/run-tests.sh
./scripts/run-tests.sh api integration
```

Expected: format clean, fast suites green, integration tier green. (No frontend files changed, so `pnpm check` is not required; run it anyway if anything under `packages/` or `apps/` was touched by mistake.)

- [ ] **Step 2: Confirm the working tree is exactly the plan's commits**

```bash
git status
```

Expected: only `api/Directory.Packages.props` remains modified (pre-existing, unrelated — leave it), plus this plan file if not yet committed. Commit the plan:

```bash
git add docs/plans/2026-08-28/1709-dlq-observability.md
git commit -m "docs(plans): add dlq observability plan (Wallow-qi90.2)"
```

- [ ] **Step 3: Close the bead and sync**

```bash
bd note Wallow-qi90.2 "Implemented per docs/plans/2026-08-28/1709-dlq-observability.md: ServiceDefaults exports Wolverine:* (built-in wolverine-dead-letter-queue counter now leaves the process); WolverineDeadLetterQueueHealthCheck reports wolverine-dlq Degraded on /health (never ready-tagged) and records wallow.messaging.dead_letter_queue.depth; Error log is Wolverine's own MovedToErrorQueue line, pinned by the tracked event in DeadLetterObservabilityTests plus the Serilog-override guard WolverineDeadLetterLoggingTests; docs/operations/observability.md updated and pinned by CustomInstrumentExportTests."
bd close Wallow-qi90.2
```

Then mark this plan `**status: completed**` (edit the first line), amend or commit that change, and finish the session per `CLAUDE.md`:

```bash
git pull --rebase && bd dolt push && git push
git status
git ls-remote origin refs/dolt/data
```

Expected: "up to date with origin", and the dolt ref hash changed from before the push.
