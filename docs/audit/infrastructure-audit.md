# Infrastructure, Configuration & DevOps Audit

**Date:** 2026-03-02
**Auditor:** infra-auditor
**Scope:** Docker, CI/CD, configuration, observability, messaging, database, dependencies, resilience, security hardening, health checks

---

## Summary

The Wallow infrastructure is well-architected for a modular monolith. Docker uses proper multi-stage builds, CI/CD has good coverage enforcement, observability is comprehensive with OpenTelemetry + Grafana, and health checks cover all critical dependencies. However, there are several areas needing attention, particularly around Valkey security, migration safety in production, missing HttpClient resilience policies, and database connection configuration.

---

## Findings

### 1. CRITICAL: Valkey (Redis) Has No Authentication

**Files:** `docker/docker-compose.yml:46-61`, `docker/docker-compose.prod.yml`

Valkey is started without any password protection (`--requirepass` not set). The production compose file does not add authentication either. Any network-adjacent service can connect to Valkey and read/write cached data, session state, and SignalR backplane messages.

```yaml
# docker/docker-compose.yml:49
command: valkey-server --appendonly yes
# No --requirepass flag
```

The production compose overlay (`docker-compose.prod.yml`) only adds memory limits and logging -- it does not restrict ports or add authentication.

**Remediation:**
- Add `--requirepass ${VALKEY_PASSWORD}` to the Valkey command in `docker-compose.yml`
- Add `VALKEY_PASSWORD` to `.env.example` with `CHANGE_ME` placeholder
- Update `ConnectionStrings:Redis` to include the password: `localhost:6379,password=...`
- In production, remove the port mapping entirely (access via Docker network only)

---

### 2. CRITICAL: Auto-Migration on Startup in All Environments

**Files:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Extensions/BillingModuleExtensions.cs:29`, and equivalent in all 5 modules + audit

All modules call `db.Database.MigrateAsync()` on startup unconditionally (no environment check). This means production deployments automatically apply EF Core migrations, which is dangerous:

- No human review of migration SQL before execution
- No rollback plan if migration fails mid-way
- Concurrent container starts could race on migration execution
- A bad migration can corrupt production data with no recovery path

```csharp
// BillingModuleExtensions.cs:29
await db.Database.MigrateAsync();
```

The catch block only logs a warning and continues, meaning the app starts with a potentially inconsistent schema:

```csharp
catch (Exception ex)
{
    LogStartupFailed(logger, ex);
    // App continues running with potentially broken schema
}
```

**Remediation:**
- Gate auto-migration behind `IsDevelopment()` or a config flag (`Wallow:AutoMigrate: true`)
- In production, use a dedicated migration runner (CI/CD step or init container) with `dotnet ef database update`
- Add a migration concurrency lock (EF Core advisory lock or Wolverine's built-in migration lock)
- Fail fast if migration fails in production instead of continuing with broken schema

---

### 3. HIGH: No Database Connection Pooling or Timeout Configuration

**Files:** All module `*InfrastructureExtensions.cs` files (e.g., `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:34`)

All `UseNpgsql()` calls use bare connection strings with no explicit pool size, connection timeout, or command timeout configuration:

```csharp
options.UseNpgsql(connectionString, npgsqlOptions =>
    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity"));
```

Under load, the default Npgsql pool size (100) may be inadequate or excessive per-module, and there is no command timeout to prevent long-running queries from holding connections.

**Remediation:**
- Add connection string parameters: `Pooling=true;Minimum Pool Size=5;Maximum Pool Size=20;Timeout=30;Command Timeout=30`
- Or configure via NpgsqlDataSourceBuilder with explicit timeouts
- Consider per-module pool sizes based on expected load
- Add `EnableRetryOnFailure()` for transient PostgreSQL failures:
  ```csharp
  npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
  ```

---

### 4. HIGH: No HttpClient Resilience Policies for External Services

**Files:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:71-77`

HttpClients for Keycloak admin, token service, and other external APIs are registered without any resilience policies (retry, circuit breaker, timeout):

```csharp
services.AddHttpClient("KeycloakAdminClient", client =>
{
    client.BaseAddress = new Uri(authServerUrl);
});

services.AddHttpClient("KeycloakTokenClient");
```

If Keycloak is slow or down, every request through these clients will hang until the default HTTP timeout (100 seconds).

**Remediation:**
- Add `Microsoft.Extensions.Http.Resilience` (already a transitive dependency via Elsa)
- Configure standard resilience pipeline:
  ```csharp
  services.AddHttpClient("KeycloakAdminClient")
      .AddStandardResilienceHandler();
  ```
- Or configure explicit retry + circuit breaker + timeout policies via Polly

---

### 5. HIGH: Design-Time DbContext Factories Contain Hardcoded Credentials

**Files:**
- `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContextFactory.cs:17`
- `src/Modules/Storage/Wallow.Storage.Infrastructure/Persistence/StorageDbContextFactory.cs:15`

Design-time factories contain hardcoded connection strings with different credentials:

```csharp
// BillingDbContextFactory.cs
optionsBuilder.UseNpgsql("Host=localhost;Database=wallow;Username=postgres;Password=postgres");

// StorageDbContextFactory.cs
optionsBuilder.UseNpgsql("Host=localhost;Database=wallow;Username=wallow;Password=wallow");
```

While these are only used at design-time for `dotnet ef migrations`, the inconsistent credentials (postgres vs wallow) indicate configuration drift and the hardcoded strings could confuse developers.

**Remediation:**
- Standardize all design-time factories to use the same connection string matching docker `.env.example`
- Consider reading from environment variables or a shared constant

---

### 6. HIGH: CI/CD Rebuilds From Scratch in Each Job

**Files:** `.github/workflows/ci.yml`

The `unit-tests` and `integration-tests` jobs both re-run `dotnet build` despite depending on the `build` job. NuGet packages are cached, but build artifacts are not shared between jobs:

```yaml
unit-tests:
    needs: build
    # ...
    - name: Build
      run: dotnet build --configuration Release  # Rebuilds everything
```

This doubles CI time unnecessarily. The `build` job validates the build succeeds, but its artifacts are not reused.

**Remediation:**
- Use `actions/upload-artifact` in the `build` job to share build output
- Or consolidate build + test into fewer jobs
- At minimum, add `--no-restore` to subsequent builds since restore is cached

---

### 7. MEDIUM: RabbitMQ Not in Base docker-compose.yml

**Files:** `docker/docker-compose.yml`, `docker/docker-compose.rabbitmq.yml`

RabbitMQ is in a separate opt-in compose file, but the health check configuration in `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs:78-87` always registers a RabbitMQ health check. The CI integration tests also assume RabbitMQ is available. The `dev-up.sh` script does not include the RabbitMQ compose file:

```bash
# dev-up.sh
docker compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml --env-file docker/.env up -d
# Missing: -f docker/docker-compose.rabbitmq.yml
```

This creates confusion about whether RabbitMQ is required or optional.

**Remediation:**
- Either add RabbitMQ to the base compose file (since it's always health-checked and tested)
- Or make the health check conditional on `ModuleMessaging:Transport` configuration
- Update `dev-up.sh` to include the RabbitMQ compose file

---

### 8. MEDIUM: No Resource Limits in Development/Base Docker Compose

**Files:** `docker/docker-compose.yml`

The base compose file has no memory/CPU limits on any service. Only the production overlay adds limits for Postgres (1G) and RabbitMQ (512M). Valkey, Keycloak, and Grafana have no limits in any environment.

```yaml
# docker-compose.prod.yml only limits postgres and rabbitmq
# Keycloak, Valkey, Grafana have no limits anywhere
```

Keycloak in particular can consume significant memory (1-2GB+).

**Remediation:**
- Add resource limits to the production overlay for all services
- Consider limits in development to prevent runaway containers

---

### 9. MEDIUM: Docker Compose Uses `:latest` Tags

**Files:** `docker/docker-compose.yml:34,110`, `docker/docker-compose.dev.yml:18`, `docker/docker-compose.staging.yml:24`

Several images use `:latest` which can lead to unpredictable behavior:

```yaml
mailpit:
    image: axllent/mailpit:latest

grafana-lgtm:
    image: grafana/otel-lgtm:latest
```

The Postgres, RabbitMQ, and Valkey images properly pin major versions (`postgres:18-alpine`, `rabbitmq:4.2-management-alpine`, `valkey/valkey:8-alpine`).

**Remediation:**
- Pin Mailpit and Grafana OTEL-LGTM to specific versions
- Example: `axllent/mailpit:v1.21`, `grafana/otel-lgtm:0.8.0`

---

### 10. MEDIUM: No Idempotency Guarantees for Wolverine Message Handling

**Files:** `src/Shared/Wallow.Shared.Kernel/Messaging/WolverineErrorHandlingExtensions.cs`, `src/Wallow.Api/Program.cs:114-116`

The error handling configuration retries messages and moves them to DLQ, but there is no mention of idempotency keys or deduplication:

```csharp
opts.Policies.OnAnyException()
    .RetryTimes(1)
    .Then.MoveToErrorQueue();
```

Wolverine's durable outbox guarantees at-least-once delivery, meaning handlers may process the same message multiple times. Without idempotency checks, this could lead to duplicate invoices, payments, or notifications.

**Remediation:**
- Implement idempotency via Wolverine's built-in `[Idempotent]` attribute or message deduplication
- Or add idempotency keys to commands and check for duplicates in handlers
- Wolverine's PostgreSQL persistence does provide some deduplication, but explicit handler-level idempotency is safer

---

### 11. MEDIUM: Coverage Threshold Parsing Is Fragile

**Files:** `.github/workflows/ci.yml:191-198`

The coverage enforcement step uses `grep -oP` (Perl regex) to extract line-rate from XML, then uses `awk` for comparison. This is fragile:

```yaml
LINE_RATE=$(grep -oP 'line-rate="\K[^"]+' ./coverage/merged/Cobertura.xml | head -1)
PERCENT=$(awk "BEGIN {printf \"%.1f\", ${LINE_RATE:-0} * 100}")
```

If the XML format changes or `grep -oP` is not available on the runner, the check silently passes with 0% coverage.

**Remediation:**
- Use a proper XML parser (e.g., `xmllint --xpath`)
- Or use ReportGenerator's built-in threshold enforcement
- Add a guard: fail if `LINE_RATE` is empty

---

### 12. MEDIUM: Keycloak Running in Dev Mode

**Files:** `docker/docker-compose.yml:69`

Keycloak is started with `start-dev` which disables HTTPS, caching, and other production features:

```yaml
command: start-dev --import-realm
```

This is fine for local development but the production compose overlay does not override this command. If someone accidentally uses the base compose in production, Keycloak runs in dev mode.

**Remediation:**
- Add a Keycloak command override in `docker-compose.prod.yml`:
  ```yaml
  keycloak:
      command: start --import-realm --hostname-strict=false
  ```

---

### 13. MEDIUM: Inconsistent GitHub Actions Versions

**Files:** `.github/workflows/security.yml:24`, `.github/workflows/ci.yml:17`

The security workflow uses `actions/setup-dotnet@v4` while CI uses `actions/setup-dotnet@v5`:

```yaml
# security.yml
- uses: actions/setup-dotnet@v4

# ci.yml
- uses: actions/setup-dotnet@v5
```

Also, the Trivy scanner in `publish.yml` uses `@master` instead of a pinned version:

```yaml
uses: aquasecurity/trivy-action@master
```

**Remediation:**
- Standardize all workflows to use the same action versions
- Pin Trivy to a specific version tag (e.g., `@0.28.0`) instead of `@master`

---

### 14. LOW: appsettings.json Files Are Nearly Empty

**Files:** `appsettings.json`, `appsettings.Development.json`

Both root appsettings files only contain logging configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

All actual configuration (connection strings, Keycloak, CORS, OpenTelemetry, etc.) appears to be provided via environment variables or module-level configuration. While this works, it means there is no discoverable documentation of all required configuration keys.

**Remediation:**
- Add commented-out or placeholder configuration in `appsettings.Development.json` showing all required keys
- Or create an `appsettings.json.template` documenting the full configuration schema
- The CORS configuration throws at startup if missing in non-dev environments (good), but other config silently defaults

---

### 15. LOW: No Docker Image Scanning in CI (Only Post-Publish)

**Files:** `.github/workflows/publish.yml:71-77`, `.github/workflows/ci.yml`

Container vulnerability scanning only runs after the image is built and pushed in the publish workflow. The CI workflow does not scan the image, meaning vulnerabilities are only caught after a release tag is created.

**Remediation:**
- Add a build-only Docker step in CI that builds but does not push, then scan
- Or add `dotnet list package --vulnerable` to the CI build job

---

### 16. LOW: No Dependabot Group Strategy

**Files:** `.github/dependabot.yml`

Dependabot is configured but without grouping, creating many individual PRs:

```yaml
- package-ecosystem: "nuget"
  directory: "/"
  schedule:
    interval: "weekly"
  open-pull-requests-limit: 10
```

**Remediation:**
- Add groups to batch related updates:
  ```yaml
  groups:
    dotnet-minor:
      patterns: ["Microsoft.*", "System.*"]
      update-types: ["minor", "patch"]
    testing:
      patterns: ["xunit*", "FluentAssertions", "NSubstitute", "Bogus"]
  ```

---

### 17. LOW: Grafana Alert Contact Point Uses Placeholder Email

**Files:** `docker/grafana/provisioning/alerting/alerting.yml:9`

```yaml
settings:
    addresses: alerts@wallow.dev
```

This is a non-routable domain placeholder. Alerts will not be delivered.

**Remediation:**
- Make the alert contact point configurable via environment variable
- Or document that this must be updated for each deployment

---

## What's Done Well

1. **Multi-stage Dockerfile** - Clean separation of restore/build/publish stages with BuildKit `--parents` for optimal layer caching. Non-root user (`$APP_UID`). No `UseAppHost` for smaller image.

2. **Centralized package management** - `Directory.Packages.props` with `ManagePackageVersionsCentrally` and `CentralPackageTransitivePinningEnabled` prevents version conflicts. Well-organized with clear sections.

3. **Comprehensive health checks** - Separate `/health`, `/health/ready`, and `/health/live` endpoints covering PostgreSQL, RabbitMQ, Hangfire, and Redis. Production response hides implementation details.

4. **Full observability stack** - OpenTelemetry traces, metrics, and logs with Serilog integration. Grafana dashboards for ASP.NET Core, .NET runtime, messaging, billing, and SLO monitoring. SLO alerts configured for P99 latency and error rates.

5. **Security headers middleware** - X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy, Permissions-Policy, and HSTS in production.

6. **Structured logging** - Serilog with correlation IDs (`TraceId`), module enrichment, and OTLP export. Request logging includes host and user agent.

7. **Rate limiting** - Global, auth, and upload rate limiting with proper IP and user partitioning.

8. **Wolverine error handling** - Structured retry policies with exponential backoff and dead letter queues. Durable outbox for at-least-once delivery.

9. **Security scanning** - CodeQL analysis on push/PR/schedule, Trivy container scanning on publish, Dependabot for dependency updates.

10. **Environment-specific compose overlays** - Clean separation of dev/staging/prod with appropriate port exposure and logging configuration.

11. **90% code coverage enforcement** - CI enforces a minimum 90% line coverage threshold with merged unit + integration coverage.

12. **Deterministic builds** - `ContinuousIntegrationBuild` enabled in CI, `TreatWarningsAsErrors` globally enabled.

13. **Comprehensive `.dockerignore`** - Excludes `.git`, `.env`, `bin/`, `obj/`, IDE files, and Docker files from build context.

14. **Per-module database schemas** - Clean isolation via PostgreSQL schemas (`identity`, `billing`, `communications`, `storage`, `configuration`, `audit`).

15. **gitignored secrets** - `docker/.env` is properly in `.gitignore` with a `.env.example` template using `CHANGE_ME` placeholders.

---

## Priority Summary

| Severity | Count | Key Issues |
|----------|-------|------------|
| CRITICAL | 2 | Valkey no auth, auto-migration in production |
| HIGH | 4 | No DB connection tuning, no HttpClient resilience, hardcoded credentials in factories, CI rebuild waste |
| MEDIUM | 7 | RabbitMQ compose confusion, no resource limits, latest tags, no idempotency, fragile coverage parsing, Keycloak dev mode, inconsistent action versions |
| LOW | 4 | Empty appsettings, no pre-publish scanning, no Dependabot groups, placeholder alert email |
