# Security Sweep: Secrets & Information Leakage

**Date:** 2026-03-03
**Auditor:** secrets-scout (automated)
**Scope:** Secrets exposure, information leakage, error handling, logging, and configuration security

---

## Executive Summary

The codebase follows generally good practices for secrets management in production/staging configs (using `OVERRIDE_VIA_ENV_VAR` placeholders). However, several findings exist around hardcoded development credentials committed to source, a Valkey password embedded in the base `appsettings.json`, a hardcoded Keycloak client secret in realm exports, and design-time factory connection strings. The exception handling middleware is well-implemented with proper environment gating, and health check endpoints appropriately limit detail in production.

---

## Findings

### SEC-S01: Valkey/Redis Password Hardcoded in Base appsettings.json

**Severity:** MEDIUM
**File:** `src/Foundry.Api/appsettings.json:15`
**Code:**
```json
"Redis": "localhost:6379,password=FoundryValkey123!,abortConnect=false"
```

**Explanation:** The base `appsettings.json` (which ships with the application and is checked into source control) contains the actual Valkey password `FoundryValkey123!`. While Production and Staging configs use `OVERRIDE_VIA_ENV_VAR`, the base config file serves as a fallback. If production fails to override the `ConnectionStrings:Redis` key, this password is used. This same password also appears in `appsettings.Development.json` and `docker/.env`.

**Recommendation:** Replace the password in the base config with a placeholder like `SET_VIA_ConnectionStrings__Redis_OR_USER_SECRETS` (matching the pattern used for `DefaultConnection`). Keep the actual dev password only in `appsettings.Development.json` or user secrets.

---

### SEC-S02: Keycloak Client Secret Hardcoded in Realm Export

**Severity:** MEDIUM
**File:** `docker/keycloak/realm-export.json:229`
**Code:**
```json
"secret": "foundry-api-secret"
```

**Explanation:** The Keycloak realm export file contains a hardcoded client secret `foundry-api-secret` for the `foundry-api` client. This file is checked into git and imported by Keycloak on startup. While this is only the development Keycloak instance, the secret is also referenced across documentation (`docs/CONFIGURATION_GUIDE.md`, `docs/DEVELOPER_GUIDE.md`, `docs/TROUBLESHOOTING_GUIDE.md`) and test fixtures (`tests/Foundry.Tests.Common/Fixtures/KeycloakFixture.cs`). Anyone with repo access knows this secret.

**Also found in:**
- `docs/CONFIGURATION_GUIDE.md:80,90,381`
- `docs/DEVELOPER_GUIDE.md:72`
- `docs/TROUBLESHOOTING_GUIDE.md:276,312,322`
- `tests/Foundry.Tests.Common/Fixtures/KeycloakFixture.cs:25`
- `tests/Foundry.Tests.Common/foundry-realm.json:17`

**Recommendation:** This is acceptable for development/testing, but ensure production Keycloak instances use a strong, unique client secret injected via environment variables. Add a note in the realm export file clarifying it is for development only.

---

### SEC-S03: Hardcoded Credentials in Design-Time DbContext Factories

**Severity:** LOW
**Files:**
- `src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/StorageDbContextFactory.cs:16`
- `src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/ConfigurationDbContextFactory.cs:19`
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/BillingDbContextFactory.cs:17`

**Code:**
```csharp
optionsBuilder.UseNpgsql("Host=localhost;Database=foundry;Username=foundry;Password=foundry");
// or
optionsBuilder.UseNpgsql("Host=localhost;Database=foundry;Username=postgres;Password=postgres");
```

**Explanation:** Design-time factories used for EF Core migrations contain hardcoded PostgreSQL credentials. These are only used by `dotnet ef` commands and never at runtime, but they are visible in source control. The Billing factory uses `postgres/postgres` while Storage and Configuration use `foundry/foundry`, creating inconsistency.

**Recommendation:** Low risk since these are design-time only. Consider reading from environment variables with a fallback, or document clearly that these are for local dev migration generation only.

---

### SEC-S04: Development Credentials in appsettings.Development.json

**Severity:** LOW
**File:** `src/Foundry.Api/appsettings.Development.json:3-4`
**Code:**
```json
"DefaultConnection": "Host=localhost;Port=5432;Database=foundry;Username=foundry;Password=foundry;SSL Mode=Disable",
"Redis": "localhost:6379,password=FoundryValkey123!,abortConnect=false"
```

**Explanation:** The development config contains plaintext PostgreSQL password `foundry` and Valkey password `FoundryValkey123!`. SSL Mode is set to `Disable` for development, which is expected but means no transport encryption.

**Recommendation:** This is standard practice for local development. Consider using .NET User Secrets for all local credentials instead of checking them into source control. The passwords should match `docker/.env` and be clearly documented as development-only.

---

### SEC-S05: Test Credentials in appsettings.Testing.json

**Severity:** LOW
**File:** `src/Foundry.Api/appsettings.Testing.json:3-5,13,23`
**Code:**
```json
"DefaultConnection": "Host=localhost;Database=test_db;Username=test;Password=test",
"RabbitMq": "amqp://guest:guest@localhost:5672",
"secret": "test-client-secret"
```

**Explanation:** Test configuration contains hardcoded test credentials. The RabbitMQ `guest:guest` credentials and `test-client-secret` are clearly test values for Testcontainers/local CI environments.

**Recommendation:** Acceptable for test configurations. These are only used by integration tests with ephemeral containers. No action required.

---

### SEC-S06: Elsa Workflow Default Signing Key in Development

**Severity:** LOW
**File:** `src/Shared/Foundry.Shared.Infrastructure.Workflows/Workflows/ElsaExtensions.cs:69`
**Code:**
```csharp
return "foundry-default-elsa-signing-key-replace-in-production";
```

**Explanation:** The Elsa workflow engine uses a hardcoded default signing key in development when `Elsa:Identity:SigningKey` is not configured. Non-development environments correctly throw an exception if the key is missing (line 72-73), which is good. However, the default key is visible in source code.

**Recommendation:** This is properly gated by environment checks. The implementation correctly throws for non-dev environments. The development fallback is acceptable.

---

### SEC-S07: Keycloak Placeholder Secrets in Base appsettings.json

**Severity:** INFO
**File:** `src/Foundry.Api/appsettings.json:70,80,88`
**Code:**
```json
"secret": "REPLACE_IN_PRODUCTION"
"AdminClientSecret": "REPLACE_IN_PRODUCTION"
```

**Explanation:** The base config uses `REPLACE_IN_PRODUCTION` as placeholder values for Keycloak credentials. While the Production/Staging configs override these with `OVERRIDE_VIA_ENV_VAR`, the sentinel value `REPLACE_IN_PRODUCTION` is somewhat misleading -- it could accidentally be used as an actual secret if someone copies config files carelessly.

**Recommendation:** Consider using a more explicitly invalid value like `SET_VIA_ENV_VAR` (matching the pattern in production configs) or adding startup validation that rejects known placeholder values.

---

### SEC-S08: docker/.env Contains Weak Credentials (Development Only)

**Severity:** INFO
**File:** `docker/.env` (not tracked in git per `.gitignore`)
**Credentials found:**
```
POSTGRES_PASSWORD=foundry
RABBITMQ_PASSWORD=guest
KEYCLOAK_ADMIN_PASSWORD=admin
VALKEY_PASSWORD=FoundryValkey123!
GF_ADMIN_PASSWORD=admin
```

**Explanation:** The local Docker `.env` file contains weak development passwords. This file is correctly excluded from git tracking (confirmed via `git ls-files` and `.gitignore` entries on lines 19, 45-47). The `.env.example` files properly use `CHANGE_ME` placeholders.

**Recommendation:** No action needed. The `.env` is not tracked, and `.env.example` uses proper placeholders. This is working as designed.

---

## Positive Findings (Good Practices)

### Exception Handling (GlobalExceptionHandler.cs)

**File:** `src/Foundry.Api/Middleware/GlobalExceptionHandler.cs:103-112`

The exception handler properly gates sensitive information by environment:
- **Development**: Exposes `exception.Message` and full `exception.ToString()` (stack trace) -- appropriate for debugging.
- **Non-Development**: Returns generic message `"An unexpected error occurred. Please try again later."` -- no stack traces, no internal details.
- Domain exceptions (`EntityNotFoundException`, `BusinessRuleException`, `ValidationException`) expose controlled error messages in all environments.
- Uses RFC 7807 Problem Details format consistently.

### Health Check Response Writer

**File:** `src/Foundry.Api/Program.cs:428-452`

Health checks properly differentiate between environments:
- **Production**: Returns only `{ "status": "Healthy" }` -- no individual check details.
- **Non-Production**: Returns detailed check information including names, durations, and error messages.

### OpenAPI/Swagger Endpoints

**File:** `src/Foundry.Api/Program.cs:296-307`

OpenAPI and Scalar API reference endpoints are correctly gated behind `IsDevelopment()` check. They are not exposed in production or staging.

### AsyncAPI Endpoints

**File:** `src/Foundry.Api/Extensions/AsyncApiEndpointExtensions.cs:10-13`

AsyncAPI documentation endpoints are correctly gated behind `IsDevelopment()`.

### Elsa Workflow API

**File:** `src/Foundry.Api/Program.cs:382-388`

The Elsa Workflow management API is correctly gated behind `IsDevelopment()`. Only the workflow runtime (not the admin API) runs in production.

### Production/Staging Config Files

**Files:** `src/Foundry.Api/appsettings.Production.json`, `src/Foundry.Api/appsettings.Staging.json`

Both files consistently use `OVERRIDE_VIA_ENV_VAR` placeholders for all sensitive values (database credentials, RabbitMQ credentials, SMTP credentials, Keycloak secrets, Elsa signing key). No actual secrets are present.

### .gitignore Coverage

**File:** `.gitignore:19,45-47`

Properly excludes:
- `.env` (root level)
- `docker/.env` (Docker environment)
- `.env.local` and `.env.*.local` (local overrides)

### Security Headers

**File:** `src/Foundry.Api/Middleware/SecurityHeadersMiddleware.cs`

Proper security headers are applied:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=()`
- `Content-Security-Policy: default-src 'self'`
- `Strict-Transport-Security` in production

### Hangfire Dashboard Authorization

**File:** `src/Foundry.Api/Middleware/HangfireDashboardAuthFilter.cs`

Dashboard access is:
- **Development**: Open (no auth) -- appropriate for local dev.
- **Non-Development**: Requires authenticated user with `Admin` role.

### Data Protection for SSO Secrets

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/IdentityDbContext.cs:35`

SSO configuration secrets are encrypted at rest using .NET Data Protection with a dedicated purpose `"Foundry.Identity.SsoSecrets"`.

### No Sensitive Data in Logs

No instances found of passwords, tokens, secrets, or credit card numbers being logged via `ILogger` or `Log.*` methods across the entire source tree.

---

## Risk Summary

| ID | Finding | Severity | Status |
|----|---------|----------|--------|
| SEC-S01 | Valkey password in base appsettings.json | MEDIUM | Needs fix |
| SEC-S02 | Keycloak client secret in realm export | MEDIUM | Acceptable for dev, document clearly |
| SEC-S03 | Hardcoded creds in design-time factories | LOW | Acceptable, consider improvement |
| SEC-S04 | Dev credentials in appsettings.Development.json | LOW | Standard practice |
| SEC-S05 | Test credentials in appsettings.Testing.json | LOW | Expected behavior |
| SEC-S06 | Elsa default signing key | LOW | Properly gated |
| SEC-S07 | Placeholder secret values | INFO | Consider standardizing |
| SEC-S08 | Weak docker/.env passwords | INFO | Not tracked in git |
