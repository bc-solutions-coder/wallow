# Verified Security Findings: Secrets & Information Leakage

**Date:** 2026-03-03
**Verifier:** verifier-secrets
**Original Report:** sweep-secrets-leakage.md

---

## Verified Findings

### SEC-S01: Valkey/Redis Password Hardcoded in Base appsettings.json

- **Original Severity:** MEDIUM
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/appsettings.json:15` contains:
  ```json
  "Redis": "localhost:6379,password=WallowValkey123!,abortConnect=false"
  ```
  Production config (`appsettings.Production.json`) does NOT override the `ConnectionStrings:Redis` key at all -- it only overrides `ConnectionStrings:DefaultConnection`. This means in production, the app would fall back to the base config and use the hardcoded Valkey password `WallowValkey123!` unless overridden via environment variables. Staging config also omits a Redis override. This is a real risk if the environment variable is not set.
- **Adjusted Severity:** MEDIUM (accurate)
- **Notes:** The inconsistency between `DefaultConnection` (which uses a proper placeholder in base config) and `Redis` (which has a real password) makes this more concerning. The `DefaultConnection` pattern should be replicated for Redis.

---

### SEC-S02: Keycloak Client Secret Hardcoded in Realm Export

- **Original Severity:** MEDIUM
- **Verdict:** CONFIRMED but severity is appropriate
- **Evidence:** `docker/keycloak/realm-export.json:229` contains:
  ```json
  "secret": "wallow-api-secret"
  ```
  This is the development Keycloak realm import file. The secret `wallow-api-secret` is present. However, the development `appsettings.Development.json` uses `SET_VIA_USER_SECRETS` for Keycloak credentials (lines 32, 42), so the secret isn't duplicated into the app config itself -- it requires user secrets setup. Production and staging configs both override with `OVERRIDE_VIA_ENV_VAR`.
- **Adjusted Severity:** MEDIUM (accurate). This is dev-only infrastructure, but the secret is committed to source control and visible to anyone with repo access.
- **Notes:** The scout correctly identified all the locations. The mitigation is that production uses entirely separate Keycloak instances with different secrets.

---

### SEC-S03: Hardcoded Credentials in Design-Time DbContext Factories

- **Original Severity:** LOW
- **Verdict:** CONFIRMED
- **Evidence:**
  - `src/Modules/Storage/Wallow.Storage.Infrastructure/Persistence/StorageDbContextFactory.cs:16`: `Username=wallow;Password=wallow`
  - `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Persistence/ConfigurationDbContextFactory.cs:19`: `Username=wallow;Password=wallow`
  - `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContextFactory.cs:17`: `Username=postgres;Password=postgres`
  All three factories implement `IDesignTimeDbContextFactory` and are only invoked by `dotnet ef` CLI. They are never called at runtime. The inconsistency (Billing uses postgres/postgres while others use wallow/wallow) is confirmed.
- **Adjusted Severity:** LOW (accurate)
- **Notes:** These are truly design-time only. The `IDesignTimeDbContextFactory` interface is exclusively used by EF Core tooling. No runtime risk.

---

### SEC-S04: Development Credentials in appsettings.Development.json

- **Original Severity:** LOW
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/appsettings.Development.json:3-4`:
  ```json
  "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=wallow;SSL Mode=Disable",
  "Redis": "localhost:6379,password=WallowValkey123!,abortConnect=false"
  ```
  Standard development config with plaintext credentials. Only loaded when `ASPNETCORE_ENVIRONMENT=Development`.
- **Adjusted Severity:** LOW (accurate)
- **Notes:** Standard .NET practice. The file is only loaded in development. User secrets would be a marginal improvement but not strictly necessary for local dev.

---

### SEC-S05: Test Credentials in appsettings.Testing.json

- **Original Severity:** LOW
- **Verdict:** CONFIRMED -- borderline FALSE POSITIVE for severity
- **Evidence:** `src/Wallow.Api/appsettings.Testing.json:3-5,13`:
  ```json
  "DefaultConnection": "Host=localhost;Database=test_db;Username=test;Password=test",
  "RabbitMq": "amqp://guest:guest@localhost:5672",
  "secret": "test-client-secret"
  ```
  These are clearly test-only credentials used with Testcontainers (ephemeral containers). The `guest:guest` is the default RabbitMQ credential only accessible on localhost.
- **Adjusted Severity:** INFO (downgraded from LOW). Test configs with dummy credentials for ephemeral containers are expected and present no real risk.
- **Notes:** No action required. This is the correct and expected pattern for integration test configuration.

---

### SEC-S06: Elsa Workflow Default Signing Key in Development

- **Original Severity:** LOW
- **Verdict:** CONFIRMED -- properly mitigated
- **Evidence:** `src/Shared/Wallow.Shared.Infrastructure.Workflows/Workflows/ElsaExtensions.cs:67-73`:
  ```csharp
  if (environment.IsDevelopment())
  {
      return "wallow-default-elsa-signing-key-replace-in-production";
  }

  throw new InvalidOperationException(
      "Elsa:Identity:SigningKey must be configured in non-Development environments.");
  ```
  The code correctly: (1) checks for a configured key first, (2) falls back to a dev default only in Development, (3) throws an exception in all other environments. Production config also declares the key as `OVERRIDE_VIA_ENV_VAR` (`appsettings.Production.json:52`).
- **Adjusted Severity:** INFO (downgraded from LOW). The implementation has proper fail-safe behavior. The app will not start in production without a real key.
- **Notes:** This is a textbook correct implementation of environment-gated defaults.

---

### SEC-S07: Keycloak Placeholder Secrets in Base appsettings.json

- **Original Severity:** INFO
- **Verdict:** CONFIRMED
- **Evidence:** `src/Wallow.Api/appsettings.json:70,80,88`:
  ```json
  "secret": "REPLACE_IN_PRODUCTION"
  "secret": "REPLACE_IN_PRODUCTION"
  "AdminClientSecret": "REPLACE_IN_PRODUCTION"
  ```
  There is NO startup validation that rejects these placeholder values. If production config fails to override them, the app would attempt to authenticate to Keycloak using `REPLACE_IN_PRODUCTION` as the client secret, which would simply fail auth. This is a fail-safe (Keycloak would reject the invalid secret), but the error would be confusing and not immediately obvious.
- **Adjusted Severity:** INFO (accurate)
- **Notes:** The inconsistent placeholder naming between `REPLACE_IN_PRODUCTION` (base config) and `OVERRIDE_VIA_ENV_VAR` (prod/staging config) and `SET_VIA_USER_SECRETS` (dev config) is a minor code quality issue. Consider standardizing.

---

### SEC-S08: docker/.env Contains Weak Credentials (Development Only)

- **Original Severity:** INFO
- **Verdict:** CONFIRMED -- properly excluded from git
- **Evidence:** `.gitignore:45` contains `docker/.env`. Verified via the gitignore file. The `.env.example` files use `CHANGE_ME` placeholders (as stated by scout). The actual `docker/.env` is a local file only.
- **Adjusted Severity:** INFO (accurate)
- **Notes:** No action needed. Working as designed.

---

## Verification of Positive Findings

### Exception Handling (GlobalExceptionHandler.cs)
- **Verdict:** CONFIRMED GOOD
- **Evidence:** `src/Wallow.Api/Middleware/GlobalExceptionHandler.cs:91-112`. Verified the environment gating logic:
  - `DomainException` (lines 91-95): Exposes `exception.Message` in ALL environments. This is intentional -- domain exception messages are user-facing business rule descriptions (e.g., "Invoice cannot be cancelled after payment").
  - `ValidationException` (lines 96-101): Exposes validation error messages in ALL environments. Also intentional.
  - Generic exceptions (lines 103-112): Only exposes `exception.Message` and `exception.ToString()` in Development. Non-dev returns generic message.
- **Notes:** The pattern is correct. Domain/validation exceptions contain controlled, user-safe messages by design.

### Health Check Response Writer
- **Verdict:** PARTIALLY CONFIRMED -- see NEW FINDING below
- **Evidence:** `src/Wallow.Api/Program.cs:428-452`. The check uses `env.IsProduction()` which means only production suppresses details. Staging and other non-production environments expose health check internals.

### OpenAPI/Swagger, AsyncAPI, Elsa Workflow API
- **Verdict:** CONFIRMED GOOD
- **Evidence:** All gated behind `IsDevelopment()` at lines 296, 10, and 383 respectively.

### Security Headers
- **Verdict:** CONFIRMED GOOD
- **Evidence:** `src/Wallow.Api/Middleware/SecurityHeadersMiddleware.cs:22-31`. All standard headers present. HSTS applied in production only (correct -- avoids HSTS issues in local dev).

### Hangfire Dashboard Authorization
- **Verdict:** CONFIRMED GOOD
- **Evidence:** `src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs:16`. Development is open, non-dev requires auth + Admin role.

---

## NEW FINDINGS

### SEC-S09: Health Check Details Exposed in Staging Environment

- **Severity:** LOW
- **File:** `src/Wallow.Api/Program.cs:428`
- **Evidence:** The health check response writer uses `env.IsProduction()` to gate detail suppression:
  ```csharp
  if (env.IsProduction())
  {
      // Minimal response
  }
  // else: full details including check names, durations, error messages
  ```
  This means staging environments (and any custom environment name) expose health check internals including component names, response times, and error messages. While less critical than production, staging environments are often network-accessible and this information could aid reconnaissance.
- **Recommendation:** Consider using `!env.IsDevelopment()` instead of `env.IsProduction()` to suppress details in all non-development environments, matching the pattern used by GlobalExceptionHandler.

---

### SEC-S10: No Startup Validation for Placeholder/Sentinel Config Values

- **Severity:** LOW
- **File:** `src/Wallow.Api/appsettings.json:70,80,88` and `src/Wallow.Api/Program.cs`
- **Evidence:** No code in the startup path validates that configuration values are not still set to placeholder sentinels like `REPLACE_IN_PRODUCTION`, `OVERRIDE_VIA_ENV_VAR`, or `SET_VIA_*`. Searched for any such validation with `grep` for these patterns in `.cs` files -- zero results. If environment variable overrides are misconfigured in deployment, the app would start with invalid placeholder credentials and fail at runtime with confusing authentication errors rather than failing fast at startup.
- **Recommendation:** Add a startup health check or configuration validator that rejects known placeholder values (`REPLACE_IN_PRODUCTION`, `OVERRIDE_VIA_ENV_VAR`, `SET_VIA_*`) in non-development environments. This provides fail-fast behavior.

---

### SEC-S11: Redis Connection String Not Overridden in Production/Staging Config

- **Severity:** MEDIUM
- **File:** `src/Wallow.Api/appsettings.Production.json`, `src/Wallow.Api/appsettings.Staging.json`
- **Evidence:** Neither production nor staging config files override `ConnectionStrings:Redis`. The base `appsettings.json:15` contains the actual Valkey password `WallowValkey123!`. While this CAN be overridden via environment variable (`ConnectionStrings__Redis`), the production config explicitly overrides `DefaultConnection` but omits `Redis`, creating an inconsistency that increases the chance of deploying with the hardcoded dev password.
  - `appsettings.Production.json` ConnectionStrings section (line 9-11): only `DefaultConnection` is overridden
  - `appsettings.Staging.json` ConnectionStrings section (line 9-11): only `DefaultConnection` is overridden
- **Recommendation:** Add `"Redis": "OVERRIDE_VIA_ENV_VAR"` to both production and staging config files for consistency and to ensure the dev password cannot accidentally leak into production.

---

## Risk Summary

| ID | Finding | Original Severity | Adjusted Severity | Verdict |
|----|---------|-------------------|-------------------|---------|
| SEC-S01 | Valkey password in base appsettings.json | MEDIUM | MEDIUM | CONFIRMED |
| SEC-S02 | Keycloak client secret in realm export | MEDIUM | MEDIUM | CONFIRMED |
| SEC-S03 | Hardcoded creds in design-time factories | LOW | LOW | CONFIRMED |
| SEC-S04 | Dev credentials in appsettings.Development.json | LOW | LOW | CONFIRMED |
| SEC-S05 | Test credentials in appsettings.Testing.json | LOW | INFO | CONFIRMED (downgraded) |
| SEC-S06 | Elsa default signing key | LOW | INFO | CONFIRMED (downgraded, properly mitigated) |
| SEC-S07 | Placeholder secret values | INFO | INFO | CONFIRMED |
| SEC-S08 | Weak docker/.env passwords | INFO | INFO | CONFIRMED |
| SEC-S09 | Health check details in staging (NEW) | -- | LOW | NEW FINDING |
| SEC-S10 | No startup config validation (NEW) | -- | LOW | NEW FINDING |
| SEC-S11 | Redis not overridden in prod/staging config (NEW) | -- | MEDIUM | NEW FINDING |

## Verification Summary

- **8 original findings verified:** All confirmed as real (no false positives)
- **2 severity adjustments:** SEC-S05 and SEC-S06 downgraded due to proper mitigations
- **3 new findings discovered:** SEC-S09 (health check staging exposure), SEC-S10 (no config validation), SEC-S11 (missing Redis override in prod config)
- **Scout quality:** Good. The original report was thorough and accurate. Positive findings were all verified correct. The main gaps were the staging health check exposure and the missing Redis override in prod/staging configs.
