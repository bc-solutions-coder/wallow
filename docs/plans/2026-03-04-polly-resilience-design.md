# Polly Resilience Integration Design

**Date:** 2026-03-04
**Status:** Draft
**Epic:** Polly/Resilience integration for Foundry

## Context

Foundry makes outbound HTTP calls to Keycloak, Twilio, SMTP servers, and ClamAV. Some of these already use `AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience` v10.0.0 (a Polly v8 wrapper). Others use manual retry loops or have no resilience at all.

This design standardizes resilience across all external calls using Polly v8 via the `Microsoft.Extensions.Http.Resilience` package already in the codebase.

## Current State

| External Service | Client Type | Current Resilience | Gap |
|-----------------|-------------|-------------------|-----|
| Keycloak Admin API | Named: `KeycloakAdminClient` | `AddStandardResilienceHandler()` (defaults) | No custom tuning for Keycloak-specific behavior |
| Keycloak Token | Named: `KeycloakTokenClient` | `AddStandardResilienceHandler()` (defaults) | No custom tuning |
| Twilio SMS | Typed: `TwilioSmsProvider` | `AddStandardResilienceHandler()` | No custom tuning |
| Health Check HTTP | Named: `HealthChecks` | 5s timeout only | No retry, no circuit breaker |
| SMTP Email | Direct socket (`SmtpClient`) | Manual retry loop with exponential backoff | Should use Polly `ResiliencePipeline` directly |
| ClamAV | Unknown | None found | Needs investigation and resilience |
| PostgreSQL | EF Core / Npgsql | `EnableRetryOnFailure(5, 30s)` | Adequate — leave as-is |
| RabbitMQ | Wolverine | Built-in retry with cooldown + DLQ | Adequate — leave as-is |
| S3 (AWS SDK) | AWS SDK | AWS SDK built-in retry | Adequate — leave as-is |

## Design Decisions

### 1. Keep `Microsoft.Extensions.Http.Resilience` as the foundation

The package is already installed (v10.0.0) and wraps Polly v8. No need to add raw `Polly` or `Polly.Core` packages. All HTTP client resilience goes through `AddStandardResilienceHandler()` with custom configuration.

### 2. Do not replace database or messaging retry

Npgsql's `EnableRetryOnFailure` and Wolverine's retry/DLQ are purpose-built for their transports. Wrapping them in Polly adds complexity without benefit.

### 3. Replace SMTP manual retry with Polly `ResiliencePipeline`

The `SmtpEmailProvider` has a hand-rolled retry loop. Replace it with an injected `ResiliencePipeline` for consistency, testability, and observability.

### 4. Create a shared resilience configuration extension

Centralize resilience policy definitions in a shared extension method so all modules configure resilience consistently.

## Implementation Plan

### Task 1: Create shared resilience configuration

**File:** `src/Shared/Foundry.Shared.Infrastructure.Core/Resilience/ResilienceExtensions.cs`

Create an extension method that provides named resilience configurations:

```csharp
public static class ResilienceExtensions
{
    public static IHttpClientBuilder AddFoundryResilienceHandler(
        this IHttpClientBuilder builder,
        string profileName = "default")
    {
        return builder.AddStandardResilienceHandler(options =>
        {
            switch (profileName)
            {
                case "identity-provider":
                    // Keycloak-tuned: longer timeouts, more retries
                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
                    options.Retry.BackoffType = DelayBackoffType.Exponential;
                    options.Retry.UseJitter = true;
                    options.CircuitBreaker.FailureRatio = 0.5;
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                    options.CircuitBreaker.MinimumThroughput = 10;
                    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                    break;

                case "external-api":
                    // Third-party APIs (Twilio, etc.): conservative retries
                    options.Retry.MaxRetryAttempts = 2;
                    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
                    options.Retry.BackoffType = DelayBackoffType.Exponential;
                    options.Retry.UseJitter = true;
                    options.CircuitBreaker.FailureRatio = 0.3;
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                    options.CircuitBreaker.MinimumThroughput = 5;
                    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(60);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                    break;

                case "health-check":
                    // Fast fail for health probes
                    options.Retry.MaxRetryAttempts = 1;
                    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
                    options.Retry.BackoffType = DelayBackoffType.Constant;
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                    break;

                default:
                    // Sensible defaults
                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.Delay = TimeSpan.FromMilliseconds(100);
                    options.Retry.BackoffType = DelayBackoffType.Exponential;
                    options.Retry.UseJitter = true;
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                    break;
            }
        });
    }
}
```

### Task 2: Customize Keycloak HTTP client resilience

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`

Replace `.AddStandardResilienceHandler()` with `.AddFoundryResilienceHandler("identity-provider")` on both `KeycloakAdminClient` and `KeycloakTokenClient`.

### Task 3: Customize Twilio HTTP client resilience

**File:** `src/Modules/Communications/Foundry.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs`

Replace `.AddStandardResilienceHandler()` with `.AddFoundryResilienceHandler("external-api")` on the `TwilioSmsProvider` client.

### Task 4: Add resilience to Health Check HTTP client

**File:** `src/Foundry.Api/Extensions/ServiceCollectionExtensions.cs`

Add `.AddFoundryResilienceHandler("health-check")` to the `HealthChecks` named client registration. Remove the manual 5s timeout (Polly handles it).

### Task 5: Replace SMTP manual retry with Polly ResiliencePipeline

**File:** `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SmtpEmailProvider.cs`

1. Register a named `ResiliencePipeline` in DI:

```csharp
services.AddResiliencePipeline("smtp", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});
```

2. Inject `ResiliencePipelineProvider<string>` into `SmtpEmailProvider`.
3. Replace the manual while-loop retry with `pipeline.ExecuteAsync(...)`.

### Task 6: Investigate and add ClamAV resilience

Locate the ClamAV client integration. If it uses `HttpClient`, add `.AddFoundryResilienceHandler("external-api")`. If it uses a direct socket/library, add a `ResiliencePipeline` wrapper similar to SMTP.

### Task 7: Add resilience logging and metrics

Ensure all Polly events flow through the existing OpenTelemetry pipeline:

1. Verify `Microsoft.Extensions.Http.Resilience` automatically emits metrics (it does by default in v10).
2. Add structured logging in the shared extension for circuit breaker state changes (open/close).
3. Optionally add a Grafana dashboard panel for resilience metrics (retry count, circuit breaker state).

### Task 8: Add integration tests for resilience behavior

1. Test that circuit breaker opens after sustained Keycloak failures.
2. Test that SMTP retry exhaustion surfaces the correct error.
3. Test that health check client fails fast when Keycloak is down.
4. Use WireMock (already in test infrastructure) to simulate failure scenarios.

## Files Changed

| File | Change |
|------|--------|
| `src/Shared/Foundry.Shared.Infrastructure.Core/Resilience/ResilienceExtensions.cs` | **New** — shared resilience profiles |
| `src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs` | Replace `AddStandardResilienceHandler()` with `AddFoundryResilienceHandler("identity-provider")` |
| `src/Modules/Communications/Foundry.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs` | Replace on Twilio client; register SMTP pipeline |
| `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SmtpEmailProvider.cs` | Replace manual retry loop with Polly pipeline |
| `src/Foundry.Api/Extensions/ServiceCollectionExtensions.cs` | Add resilience to health check client |
| ClamAV integration file (TBD) | Add resilience |
| Test files | Add resilience integration tests |

## Out of Scope

- **Database retry** — Npgsql's built-in retry is sufficient.
- **RabbitMQ/Wolverine retry** — Wolverine owns its own retry/DLQ.
- **S3/AWS SDK** — AWS SDK has built-in retry with exponential backoff.
- **Rate limiting** — Not needed yet; the standard handler includes it with sensible defaults.
- **Hedging** — Overkill for current use cases (single-instance services).
- **Caching policies** — Token caching is already handled by Keycloak services.

## Risks

1. **Circuit breaker state sharing** — In a multi-instance deployment, circuit breakers are per-process. This is acceptable for now; distributed circuit breaking would require Redis-backed state.
2. **Timeout conflicts** — Ensure `HttpClient.Timeout` is removed where Polly manages timeouts, to avoid double-timeout behavior.
3. **Test flakiness** — Resilience tests involving timing (circuit breakers, retries) need deterministic time control. Use `FakeTimeProvider` in tests.
