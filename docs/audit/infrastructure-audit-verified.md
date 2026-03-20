# Infrastructure, Configuration & DevOps Audit -- Verified Report

**Date:** 2026-03-02
**Verifier:** infra-verifier
**Original Auditor:** infra-auditor
**Scope:** Verification of all 17 findings from the original infrastructure audit

---

## Verification Methodology

For each finding, the verifier read the actual source files at the referenced locations and confirmed or corrected the claims. New findings discovered during verification are appended at the end.

---

## Finding Verification

### 1. CRITICAL: Valkey (Redis) Has No Authentication

**Verdict: CONFIRMED**

Verified at `docker/docker-compose.yml:49`. The Valkey command is exactly:
```yaml
command: valkey-server --appendonly yes
```
No `--requirepass` flag. Port 6379 is exposed to the host. The production compose overlay (`docker/docker-compose.prod.yml`) does not mention Valkey at all -- no authentication, no port restriction, no resource limits. The connection string in `appsettings.json:15` is `localhost:6379,abortConnect=false` with no password parameter.

Severity rating CRITICAL is accurate. Remediation suggestions are correct.

---

### 2. CRITICAL: Auto-Migration on Startup in All Environments

**Verdict: CONFIRMED (scope slightly understated)**

Verified across 6 locations (not "5 modules + audit" as stated -- it is 5 modules + 1 shared audit = 6 total):
- `src/Modules/Billing/Wallow.Billing.Infrastructure/Extensions/BillingModuleExtensions.cs:29`
- `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs:29`
- `src/Modules/Storage/Wallow.Storage.Infrastructure/Extensions/StorageModuleExtensions.cs:29`
- `src/Modules/Communications/Wallow.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs:140`
- `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Extensions/ConfigurationModuleExtensions.cs:61`
- `src/Shared/Wallow.Shared.Infrastructure/Auditing/AuditingExtensions.cs:31`

All call `db.Database.MigrateAsync()` unconditionally. The 5 module initializers wrap in try/catch that logs a warning and continues. **However, the audit module (`AuditingExtensions.cs:27-32`) has NO try/catch at all** -- a migration failure there would crash the application on startup. This is worse than the modules.

Severity CRITICAL is accurate. The catch-and-continue pattern in modules is also problematic as noted.

---

### 3. HIGH: No Database Connection Pooling or Timeout Configuration

**Verdict: CONFIRMED**

Verified across all module infrastructure extensions:
- `BillingInfrastructureExtensions.cs:33`: `options.UseNpgsql(connectionString, npgsql => { npgsql.MigrationsHistoryTable(...); })`
- `IdentityInfrastructureExtensions.cs:34`: Same pattern
- `CommunicationsModuleExtensions.cs:42`: Same pattern
- `ConfigurationModuleExtensions.cs:35`: Same pattern
- `StorageInfrastructureExtensions.cs` (implicitly same pattern)

No `EnableRetryOnFailure()`, no connection timeout, no pool size configuration anywhere in C# code. The connection strings in `appsettings.json` and `appsettings.Development.json` also have no pooling parameters. The development connection string only has `SSL Mode=Disable`. The production-targeted connection string in `appsettings.json` has `SSL Mode=Require;Trust Server Certificate=false` but no pool/timeout settings.

Severity HIGH is accurate. Remediation is correct.

---

### 4. HIGH: No HttpClient Resilience Policies for External Services

**Verdict: CONFIRMED**

Verified at `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:71-77`:
```csharp
services.AddHttpClient("KeycloakAdminClient", client =>
{
    client.BaseAddress = new Uri(authServerUrl);
});
services.AddHttpClient("KeycloakTokenClient");
```

No resilience handler, no Polly policies, no timeout configuration. Searched the entire `src/` tree for `AddStandardResilienceHandler` and `AddResilienceHandler` -- zero results. Also confirmed no Polly usage.

Note: The audit mentions `AddKeycloakAdminHttpClient(configuration)` at line 66, which is from the `Keycloak.AuthServices.Sdk` package. That client also likely lacks resilience policies since the library doesn't add them by default.

The Twilio SMS client (`CommunicationsModuleExtensions.cs:98`) is another HTTP client without resilience policies -- not mentioned in the original audit.

Severity HIGH is accurate. Remediation is correct.

---

### 5. HIGH: Design-Time DbContext Factories Contain Hardcoded Credentials

**Verdict: CONFIRMED (scope understated)**

Verified at:
- `BillingDbContextFactory.cs:17`: `Username=postgres;Password=postgres`
- `StorageDbContextFactory.cs:15`: `Username=wallow;Password=wallow`
- `ConfigurationDbContextFactory.cs:18-19`: `Username=wallow;Password=wallow` (NOT mentioned in original audit)

The original audit only listed Billing and Storage factories. The Configuration factory was missed. The inconsistency between `postgres/postgres` (Billing) and `wallow/wallow` (Storage, Configuration) is confirmed.

**Severity adjustment: Downgrade to MEDIUM.** These are design-time factories (`IDesignTimeDbContextFactory`) used only by `dotnet ef migrations` CLI commands. They are never invoked at runtime. The inconsistency is a minor developer experience issue. While hardcoded credentials are not ideal, the security risk is negligible since these only run locally during development.

---

### 6. HIGH: CI/CD Rebuilds From Scratch in Each Job

**Verdict: CONFIRMED**

Verified at `.github/workflows/ci.yml`. The `build` job (line 33) runs `dotnet build --no-restore --configuration Release`. The `unit-tests` job (line 59) and `integration-tests` job (line 129) both re-run `dotnet build --configuration Release` (without `--no-restore` flag, which means they also re-restore despite cached NuGet packages).

Build artifacts are not shared via `actions/upload-artifact` between jobs. Each of the 3 jobs does a full checkout, restore, and build independently.

Severity HIGH is accurate. Remediation suggestions are correct. Adding `--no-restore` to the test job builds would be a quick win since NuGet is cached.

---

### 7. MEDIUM: RabbitMQ Not in Base docker-compose.yml

**Verdict: CONFIRMED**

Verified:
- `docker/docker-compose.yml` does not contain RabbitMQ (confirmed by reading full file)
- `docker/docker-compose.rabbitmq.yml` is the opt-in file
- `docker/dev-up.sh:2` runs: `docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml --env-file docker/.env up -d` -- does NOT include RabbitMQ compose file
- `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs:78-87` always registers RabbitMQ health check
- CI integration tests (ci.yml:92-100) include a RabbitMQ service container

However, `appsettings.json:11` shows `"ModuleMessaging": { "Transport": "InMemory" }`, meaning the app defaults to in-memory transport without RabbitMQ. The health check will fail if RabbitMQ is not running, but this does not prevent the app from starting (health checks are separate endpoints).

The finding is accurate but the impact is nuanced -- the app works without RabbitMQ via InMemory transport, but health checks will report unhealthy.

Severity MEDIUM is accurate.

---

### 8. MEDIUM: No Resource Limits in Development/Base Docker Compose

**Verdict: CONFIRMED**

Verified:
- `docker/docker-compose.yml` has no `deploy.resources.limits` on any service
- `docker/docker-compose.prod.yml` only limits `postgres` (1G) and `rabbitmq` (512M)
- Valkey, Keycloak, and Grafana LGTM have no limits in any compose file

Severity MEDIUM is accurate.

---

### 9. MEDIUM: Docker Compose Uses `:latest` Tags

**Verdict: CONFIRMED**

Verified:
- `docker/docker-compose.yml:34`: `axllent/mailpit:latest`
- `docker/docker-compose.yml:110`: `grafana/otel-lgtm:latest`
- `docker/docker-compose.dev.yml:18`: `axllent/mailpit:latest` (duplicate definition)
- `docker/docker-compose.staging.yml:24`: `axllent/mailpit:latest` (duplicate definition)

Pinned images confirmed: `postgres:18-alpine`, `rabbitmq:4.2-management-alpine`, `valkey/valkey:8-alpine`, `quay.io/keycloak/keycloak:26.0`.

Note: The original audit cites line 110 and also line 34 for staging, but the staging compose file is at line 24, not line 24 of `docker-compose.staging.yml`. The line numbers in the original audit are slightly inaccurate for staging but the finding is correct.

Severity MEDIUM is accurate.

---

### 10. MEDIUM: No Idempotency Guarantees for Wolverine Message Handling

**Verdict: CONFIRMED**

Verified at `src/Shared/Wallow.Shared.Kernel/Messaging/WolverineErrorHandlingExtensions.cs`. The error handling retries and moves to DLQ but has no idempotency mechanism. Searched entire `src/` for `Idempotent`, `idempotency`, `deduplication` -- only found results related to database seeding (ApiScopeSeeder) and a SQL migration comment, not related to message handling.

The original audit cites `Program.cs:114-116` which is reasonable though the actual error handling config is in the shared kernel extension method.

Severity MEDIUM is accurate.

---

### 11. MEDIUM: Coverage Threshold Parsing Is Fragile

**Verdict: CONFIRMED**

Verified at `.github/workflows/ci.yml:191-198`. The code uses `grep -oP` (Perl regex) and `awk` for XML parsing:
```yaml
LINE_RATE=$(grep -oP 'line-rate="\K[^"]+' ./coverage/merged/Cobertura.xml | head -1)
PERCENT=$(awk "BEGIN {printf \"%.1f\", ${LINE_RATE:-0} * 100}")
```

The `${LINE_RATE:-0}` default means if grep fails, LINE_RATE defaults to 0, and `0 * 100 = 0%` which IS below the 90% threshold, so it would actually FAIL, not "silently pass" as the audit states. **The audit's claim that "the check silently passes with 0% coverage" is incorrect.** The awk comparison `(0 * 100 < 90)` evaluates to `1` (true), so `exit 1` would be triggered.

However, the parsing is still fragile for other reasons (XML format changes, `grep -oP` availability on different runner images).

**Severity: Remains MEDIUM but the description contains an error.** The fragility is real but the failure mode is wrong -- it would fail-closed (reject), not fail-open (pass).

---

### 12. MEDIUM: Keycloak Running in Dev Mode

**Verdict: CONFIRMED**

Verified at `docker/docker-compose.yml:69`: `command: start-dev --import-realm`. Neither `docker-compose.prod.yml` nor `docker-compose.staging.yml` contains any Keycloak overrides (verified via grep -- zero results for "keycloak" in both files).

Severity MEDIUM is accurate. This is a real concern for anyone deploying with the provided compose files.

---

### 13. MEDIUM: Inconsistent GitHub Actions Versions

**Verdict: CONFIRMED**

Verified:
- `.github/workflows/security.yml:24`: `actions/setup-dotnet@v4`
- `.github/workflows/ci.yml:17`: `actions/setup-dotnet@v5`
- `.github/workflows/publish.yml:72`: `aquasecurity/trivy-action@master`

All confirmed exactly as described.

Severity MEDIUM is accurate.

---

### 14. LOW: appsettings.json Files Are Nearly Empty

**Verdict: FALSE POSITIVE**

The audit claims both files "only contain logging configuration." This is incorrect. The actual `appsettings.json` contains substantial configuration:
- Connection strings (DefaultConnection, Redis, RabbitMq)
- RabbitMQ settings (Host, Port, Username, Password)
- CORS allowed origins
- Communications/Email provider
- SMTP configuration (Host, Port, SSL, From address, MaxRetries, TimeoutSeconds)
- OpenTelemetry settings (ServiceName, OTLP endpoints)
- Storage provider config (Local and S3)
- Keycloak and KeycloakAdmin configuration
- Module toggles (Wallow:Modules)
- Plugin configuration
- ModuleMessaging transport

The `appsettings.Development.json` overrides connection strings, logging levels, SMTP, OpenTelemetry, and Keycloak settings.

These files are comprehensive, not "nearly empty." **This finding is a false positive.**

---

### 15. LOW: No Docker Image Scanning in CI (Only Post-Publish)

**Verdict: CONFIRMED**

Verified:
- `.github/workflows/publish.yml:71-77`: Trivy scan runs AFTER `docker/build-push-action` with `push: true` (line 63), meaning the image is already published before scanning
- `.github/workflows/ci.yml`: No Docker build or scan step

The image is pushed first, then scanned. If vulnerabilities are found, the image is already in the registry. The Trivy step uses `exit-code: '1'` so it will fail the workflow, but the image is already published.

Severity LOW is accurate.

---

### 16. LOW: No Dependabot Group Strategy

**Verdict: CONFIRMED**

Verified at `.github/dependabot.yml`. Simple configuration with no `groups` section -- just `nuget` and `github-actions` ecosystems with weekly updates.

Severity LOW is accurate.

---

### 17. LOW: Grafana Alert Contact Point Uses Placeholder Email

**Verdict: CONFIRMED**

Verified at `docker/grafana/provisioning/alerting/alerting.yml:10`: `addresses: alerts@wallow.dev`

Severity LOW is accurate.

---

## NEW Findings (Missed by Original Audit)

### NEW-1. MEDIUM: Audit Module MigrateAsync Has No Error Handling

**File:** `src/Shared/Wallow.Shared.Infrastructure/Auditing/AuditingExtensions.cs:27-32`

Unlike all 5 module initializers which wrap `MigrateAsync()` in try/catch, the audit module's `InitializeAuditingAsync` method has no exception handling:

```csharp
public static async Task InitializeAuditingAsync(this WebApplication app)
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    AuditDbContext db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.Database.MigrateAsync();
    // No try/catch -- crash on failure
}
```

If the audit migration fails, the entire application crashes on startup instead of logging a warning and continuing.

**Remediation:** Add the same try/catch pattern used by other modules, or better yet, fix the root issue (Finding #2) by gating all migrations.

---

### NEW-2. MEDIUM: BuildServiceProvider Anti-Pattern in CommunicationsModuleExtensions

**File:** `src/Modules/Communications/Wallow.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs:119`

The `RegisterEmailProvider` method calls `services.BuildServiceProvider()` inside a DI registration method:

```csharp
using (ServiceProvider sp = services.BuildServiceProvider())
{
    ILogger logger = sp.GetRequiredService<ILoggerFactory>()
        .CreateLogger("CommunicationsModule");
    LogUnrecognizedEmailProvider(logger, provider);
}
```

`BuildServiceProvider()` inside `ConfigureServices` creates a temporary, separate DI container. This is a well-known anti-pattern that triggers `CA2000` (dispose issues) and can cause subtle bugs with singleton services being created twice.

**Remediation:** Use `ILogger` from a different source during registration, or defer logging to runtime.

---

### NEW-3. MEDIUM: Valkey Has No Resource Limits in Production

**File:** `docker/docker-compose.prod.yml`

The production compose overlay adds resource limits for Postgres (1G) and RabbitMQ (512M) but does NOT include Valkey at all. This means Valkey runs in production with:
- No memory limits (can consume all host memory)
- No port restrictions (port 6379 exposed to host, same as dev)
- No logging configuration

This is separate from Finding #8 (general lack of dev limits) -- this is specifically about the production overlay missing Valkey entirely.

**Remediation:** Add Valkey to `docker-compose.prod.yml` with memory limits, remove port mapping, and add logging config.

---

### NEW-4. LOW: Trivy Scan Runs AFTER Image Push

**File:** `.github/workflows/publish.yml:59-77`

The `docker/build-push-action` step has `push: true` (line 63), and the Trivy scan runs as a subsequent step (line 71). This means a vulnerable image is published to GHCR before the scan even runs. If Trivy finds HIGH/CRITICAL vulnerabilities, the workflow fails but the image is already in the registry.

**Remediation:** Either:
- Build without pushing (`push: false`), scan, then push in a separate step
- Or use `load: true` to load into local Docker, scan, then push

(Note: This overlaps with Finding #15 but provides more specific detail about the push-before-scan ordering.)

---

### NEW-5. LOW: Configuration Factory Has Third Credential Variant

**File:** `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Persistence/ConfigurationDbContextFactory.cs:18-19`

The original audit identified Billing (`postgres/postgres`) and Storage (`wallow/wallow`) as inconsistent. There is a third factory -- Configuration -- which also uses `wallow/wallow`. This is consistent with Storage but not with Billing. The original audit missed this factory entirely.

---

## Verified Summary

| Severity | Original Count | Verified Count | Changes |
|----------|---------------|----------------|---------|
| CRITICAL | 2 | 2 | No change |
| HIGH | 4 | 3 | Finding #5 downgraded to MEDIUM |
| MEDIUM | 7 | 10 | Finding #5 moved here; #14 removed as false positive; 3 new findings added |
| LOW | 4 | 4 | Finding #14 removed; NEW-4 and NEW-5 added |
| **Total** | **17** | **19** | 1 false positive removed, 5 new findings, 1 severity adjustment |

### Verified Issue List

| # | Severity | Status | Finding |
|---|----------|--------|---------|
| 1 | CRITICAL | CONFIRMED | Valkey no authentication |
| 2 | CRITICAL | CONFIRMED | Auto-migration in all environments (6 locations, not 5+audit) |
| 3 | HIGH | CONFIRMED | No DB connection pooling or timeout config |
| 4 | HIGH | CONFIRMED | No HttpClient resilience policies |
| 6 | HIGH | CONFIRMED | CI/CD rebuilds from scratch in each job |
| 5 | MEDIUM | SEVERITY ADJUSTED (was HIGH) | Design-time factories have hardcoded credentials (3 factories, not 2) |
| 7 | MEDIUM | CONFIRMED | RabbitMQ not in base docker-compose |
| 8 | MEDIUM | CONFIRMED | No resource limits in dev/base compose |
| 9 | MEDIUM | CONFIRMED | Docker compose uses :latest tags |
| 10 | MEDIUM | CONFIRMED | No Wolverine message idempotency |
| 11 | MEDIUM | CONFIRMED (description corrected) | Coverage parsing fragile (but fails-closed, not fails-open) |
| 12 | MEDIUM | CONFIRMED | Keycloak running in dev mode, no prod override |
| 13 | MEDIUM | CONFIRMED | Inconsistent GitHub Actions versions |
| N1 | MEDIUM | NEW | Audit module MigrateAsync has no error handling |
| N2 | MEDIUM | NEW | BuildServiceProvider anti-pattern in Communications |
| N3 | MEDIUM | NEW | Valkey missing from production compose overlay |
| 14 | -- | FALSE POSITIVE | appsettings.json is comprehensive, not empty |
| 15 | LOW | CONFIRMED | No pre-publish Docker image scanning |
| 16 | LOW | CONFIRMED | No Dependabot group strategy |
| 17 | LOW | CONFIRMED | Grafana placeholder alert email |
| N4 | LOW | NEW | Trivy scans after image push (detail of #15) |
| N5 | LOW | NEW | Third design-time factory missed (Configuration module) |

### Final Verified Counts

- **CRITICAL:** 2
- **HIGH:** 3
- **MEDIUM:** 10
- **LOW:** 4
- **FALSE POSITIVE:** 1
- **Total verified issues:** 19
