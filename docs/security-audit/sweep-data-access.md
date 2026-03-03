# Security Audit: Data Access & Database Security

**Auditor:** db-scout
**Date:** 2026-03-03
**Scope:** All database access patterns across all modules
**Status:** Complete

---

## Executive Summary

The codebase demonstrates strong security fundamentals in database access. All Dapper queries use parameterized queries. EF Core raw SQL usage is limited and properly mitigated. Multi-tenancy is enforced at the ORM level with global query filters. Several low-to-medium severity findings were identified, primarily around connection string hygiene, design-time hardcoded credentials, and audit trail gaps in Dapper queries.

**Critical:** 0
**High:** 1
**Medium:** 4
**Low:** 5
**Informational:** 3

---

## Findings

### HIGH-1: Hardcoded Redis Password in appsettings.json (Committed to Source Control)

**Severity:** HIGH
**File:** `src/Foundry.Api/appsettings.json:15`
**Also in:** `src/Foundry.Api/appsettings.Development.json:5`

```json
"Redis": "localhost:6379,password=FoundryValkey123!,abortConnect=false"
```

**Issue:** The Redis/Valkey password `FoundryValkey123!` is hardcoded in `appsettings.json` (the base config, not environment-specific) and committed to source control. Unlike the PostgreSQL and RabbitMQ connection strings which use placeholder values (`SET_VIA_...` / `OVERRIDE_VIA_ENV_VAR`), the Redis connection string contains the actual password in the base config.

**Impact:** Anyone with repository access obtains the Valkey credential. If this password is reused in staging/production, it exposes the cache layer (which stores feature flag evaluations and session data).

**Recommendation:** Replace with a placeholder pattern consistent with other connection strings. Use environment variables or user secrets for the actual value.

---

### MED-1: Docker .env File with Default Credentials Committed to Repository

**Severity:** MEDIUM
**File:** `docker/.env`

```
POSTGRES_USER=foundry
POSTGRES_PASSWORD=foundry
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=admin
VALKEY_PASSWORD=FoundryValkey123!
GF_ADMIN_PASSWORD=admin
```

**Issue:** The `docker/.env` file contains default credentials for all infrastructure services and is committed to source control. While intended for local development, teams forking this repository may inadvertently use these credentials in higher environments.

**Impact:** Supply chain risk for downstream teams. Credential reuse across environments.

**Recommendation:** Rename to `docker/.env.example` and add `docker/.env` to `.gitignore`. Document that users must copy and customize.

---

### MED-2: Design-Time DbContext Factories Contain Hardcoded Connection Strings

**Severity:** MEDIUM
**Files:**
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/BillingDbContextFactory.cs:17`
- `src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/ConfigurationDbContextFactory.cs:18-19`
- `src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/StorageDbContextFactory.cs:16`

```csharp
// BillingDbContextFactory.cs:17
optionsBuilder.UseNpgsql("Host=localhost;Database=foundry;Username=postgres;Password=postgres");

// ConfigurationDbContextFactory.cs:18-19
optionsBuilder.UseNpgsql(
    "Host=localhost;Database=foundry;Username=foundry;Password=foundry", ...);

// StorageDbContextFactory.cs:16
optionsBuilder.UseNpgsql("Host=localhost;Database=foundry;Username=foundry;Password=foundry");
```

**Issue:** Design-time migration factories contain hardcoded connection strings with credentials. These are only used by `dotnet ef migrations` tooling, but they are committed to source control.

**Impact:** Low direct risk (design-time only), but contributes to credential sprawl in the codebase. Inconsistent credentials (`postgres/postgres` vs `foundry/foundry`) suggest copy-paste.

**Recommendation:** Read from environment variables with a fallback, or use a consistent placeholder.

---

### MED-3: Dapper Queries Bypass EF Core Audit Interceptor

**Severity:** MEDIUM
**Files:**
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/InvoiceQueryService.cs`
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/RevenueReportService.cs`
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/InvoiceReportService.cs`
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/PaymentReportService.cs`
- `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/MessagingQueryService.cs`

**Issue:** All Dapper queries bypass EF Core's `AuditInterceptor` and `TenantSaveChangesInterceptor`. While the Dapper queries are read-only (SELECT statements), there is no architectural guardrail preventing a future developer from adding write operations via Dapper that would skip tenant stamping and audit logging.

**Impact:** No current data mutation risk (all Dapper queries are reads). However, the pattern creates a blind spot -- if anyone adds Dapper-based writes in the future, tenant isolation and audit trail would be silently bypassed.

**Recommendation:** Add a code review checklist item or architecture test ensuring Dapper is never used for write operations. Consider wrapping Dapper access in a base class that enforces read-only semantics.

---

### MED-4: InvoiceReportService and PaymentReportService Create Standalone DB Connections

**Severity:** MEDIUM
**Files:**
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/InvoiceReportService.cs:43`
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/PaymentReportService.cs:43`

```csharp
await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
```

**Issue:** These two services create their own `NpgsqlConnection` directly from a stored connection string, bypassing EF Core's connection management entirely. This means they do not participate in any EF Core interceptors, connection pooling configuration, or ambient transactions. Other Dapper services (e.g., `InvoiceQueryService`, `MessagingQueryService`) correctly obtain the connection from `_context.Database.GetDbConnection()`.

**Impact:** Connection pool fragmentation. No EF Core interceptor coverage. Inconsistent pattern with other Dapper services in the same module.

**Recommendation:** Refactor to use `BillingDbContext.Database.GetDbConnection()` consistent with `InvoiceQueryService` and `RevenueReportService`.

---

### LOW-1: IgnoreQueryFilters Usage Requires Careful Review

**Severity:** LOW
**Files:**
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/QuotaDefinitionRepository.cs:46`
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/MeteringDbSeeder.cs:69`
- `src/Modules/Identity/Foundry.Identity.Infrastructure/Repositories/ServiceAccountRepository.cs:31`
- `src/Shared/Foundry.Shared.Kernel/MultiTenancy/TenantQueryExtensions.cs:13` (the `AllTenants()` extension)

**Issue:** `IgnoreQueryFilters()` disables EF Core global query filters, including the tenant isolation filter. Each current usage has documented justification:
- `QuotaDefinitionRepository`: Reads system-wide defaults (TenantId = Guid.Empty) -- correctly filters by system tenant.
- `MeteringDbSeeder`: Seeds system-wide quota defaults -- correctly filters by system tenant.
- `ServiceAccountRepository`: Middleware lookup by Keycloak client ID before tenant is resolved -- expected pattern.

**Impact:** Currently safe. The `AllTenants()` extension method in `TenantQueryExtensions` makes it trivially easy to bypass tenant isolation anywhere in the codebase without explicit justification.

**Recommendation:** Consider adding an architecture test that flags any new `IgnoreQueryFilters()` / `AllTenants()` usage for mandatory review. Add XML doc comments to `AllTenants()` warning about the security implications.

---

### LOW-2: Dapper Queries in Report Services Do Not Pass CancellationToken

**Severity:** LOW
**Files:**
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/InvoiceQueryService.cs:34,53,71,89`
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/RevenueReportService.cs:44`

```csharp
// InvoiceQueryService accepts CancellationToken but does not pass it to Dapper
public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
{
    // ...
    decimal result = await connection.QuerySingleAsync<decimal>(sql, new { ... });
    // ct is never passed to QuerySingleAsync
}
```

**Issue:** The `CancellationToken` parameter is accepted but not forwarded to Dapper's query methods. Compare with `MessagingQueryService` which correctly uses `CommandDefinition` with `cancellationToken`.

**Impact:** Long-running report queries cannot be cancelled by the client, leading to resource waste. Not a direct security vulnerability but impacts availability.

**Recommendation:** Use `CommandDefinition` with `cancellationToken` parameter, consistent with `MessagingQueryService`.

---

### LOW-3: DomainException Messages Exposed in Non-Development Environments

**Severity:** LOW
**File:** `src/Foundry.Api/Middleware/GlobalExceptionHandler.cs:91-94`

```csharp
if (exception is DomainException domainException)
{
    problemDetails.Extensions["code"] = domainException.Code;
    problemDetails.Detail = exception.Message;
}
```

**Issue:** `DomainException` messages are returned in all environments (the development-only guard at line 103 only applies to the generic `else` branch). While domain exception messages are designed to be user-facing, they could inadvertently leak internal details (entity names, IDs, business rule internals).

**Impact:** Low -- domain exceptions are intentionally descriptive. However, messages like "Invoice INV-2024-001 cannot be cancelled in Paid status" leak entity identifiers.

**Recommendation:** Review all `DomainException` message templates to ensure they don't expose sensitive internal state. Consider a flag to use generic messages in production.

---

### LOW-4: Audit Trail Records Full Entity Values Including Potentially Sensitive Fields

**Severity:** LOW
**File:** `src/Shared/Foundry.Shared.Infrastructure.Core/Auditing/AuditInterceptor.cs:139-152`

```csharp
private static string SerializeValues(PropertyValues propertyValues)
{
    Dictionary<string, object?> dict = new();
    foreach (IProperty property in propertyValues.Properties)
    {
        PropertyInfo? propertyInfo = property.PropertyInfo;
        if (propertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() != null)
            continue;
        dict[property.Name] = propertyValues[property];
    }
    return JsonSerializer.Serialize(dict);
}
```

**Issue:** The audit interceptor serializes all entity property values (old and new) into JSON, with opt-out via `[AuditIgnore]`. This is an opt-out model -- new sensitive fields are logged by default unless a developer remembers to add `[AuditIgnore]`.

**Impact:** Sensitive data (e.g., encrypted SSO secrets before encryption, API keys, PII) could be stored in plaintext in the audit table if developers forget to annotate.

**Recommendation:** Audit SSO entity configurations to verify `[AuditIgnore]` is applied to secret fields. Consider switching to an opt-in model for entities containing sensitive data, or adding a sensitive data scan to CI.

---

### LOW-5: SSL Disabled for PostgreSQL in Development

**Severity:** LOW
**File:** `src/Foundry.Api/appsettings.Development.json:3`

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=foundry;Username=foundry;Password=foundry;SSL Mode=Disable"
```

**Issue:** SSL is explicitly disabled for the development PostgreSQL connection. While acceptable for local development, the base `appsettings.json` does include `SSL Mode=Require;Trust Server Certificate=false` which is good.

**Impact:** No production risk. Development traffic is unencrypted on localhost.

**Recommendation:** No action needed. The production/staging configs correctly use environment variables, and the base config has SSL enabled.

---

### INFO-1: All Dapper Queries Use Parameterized Queries (Positive Finding)

**Severity:** INFORMATIONAL (Positive)
**Files:** All 5 Dapper service files

All Dapper queries across the codebase use parameterized queries via anonymous objects or `NpgsqlParameter`. No string concatenation or interpolation is used in SQL query construction. Example:

```csharp
// InvoiceQueryService.cs - Correct parameterization
decimal result = await connection.QuerySingleAsync<decimal>(
    sql,
    new { TenantId = _tenantContext.TenantId.Value, From = from, To = to });
```

**Assessment:** No SQL injection risk in Dapper queries.

---

### INFO-2: CustomFieldIndexManager Correctly Validates DDL Identifiers (Positive Finding)

**Severity:** INFORMATIONAL (Positive)
**File:** `src/Modules/Configuration/Foundry.Configuration.Infrastructure/Services/CustomFieldIndexManager.cs`

The `CustomFieldIndexManager` uses `ExecuteSqlRawAsync` with string interpolation for DDL statements (which cannot use parameterized identifiers). However, all identifier inputs are validated against a strict regex (`^[a-zA-Z_][a-zA-Z0-9_]{0,62}$`) before use, and the parameterized check query uses `NpgsqlParameter` correctly.

```csharp
[GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]{0,62}$", RegexOptions.NonBacktracking)]
private static partial Regex SafeIdentifierRegex();

private static void ValidateIdentifier(string value, string parameterName)
{
    if (!SafeIdentifierRegex().IsMatch(value))
        throw new ArgumentException(...);
}
```

**Assessment:** Well-implemented defense against SQL injection in dynamic DDL. The regex is restrictive enough to prevent injection while allowing valid PostgreSQL identifiers.

---

### INFO-3: SSO Secrets Properly Encrypted at Rest (Positive Finding)

**Severity:** INFORMATIONAL (Positive)
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/IdentityDbContext.cs:35-36`

```csharp
IDataProtector protector = _dataProtectionProvider.CreateProtector("Foundry.Identity.SsoSecrets");
EncryptedStringConverter encryptedStringConverter = new(protector);
```

SSO configuration secrets (client secrets for OIDC/SAML providers) are encrypted using ASP.NET Core Data Protection before storage. This is applied via an EF Core value converter, ensuring encryption is transparent to the application layer.

**Assessment:** Good practice for secrets at rest.

---

## Summary Table

| ID | Severity | Category | Finding |
|----|----------|----------|---------|
| HIGH-1 | HIGH | Connection Strings | Hardcoded Redis password in base appsettings.json |
| MED-1 | MEDIUM | Connection Strings | Docker .env with default credentials committed |
| MED-2 | MEDIUM | Connection Strings | Design-time factories with hardcoded credentials |
| MED-3 | MEDIUM | Audit Trail | Dapper queries bypass EF Core audit interceptor |
| MED-4 | MEDIUM | Connection Management | Report services create standalone DB connections |
| LOW-1 | LOW | Tenant Isolation | IgnoreQueryFilters needs architectural guardrails |
| LOW-2 | LOW | Availability | Dapper queries don't pass CancellationToken |
| LOW-3 | LOW | Information Disclosure | DomainException messages exposed in all environments |
| LOW-4 | LOW | Audit Trail | Audit logs sensitive fields by default (opt-out model) |
| LOW-5 | LOW | Encryption in Transit | SSL disabled for dev PostgreSQL (expected) |
| INFO-1 | INFO | SQL Injection | All Dapper queries properly parameterized |
| INFO-2 | INFO | SQL Injection | CustomFieldIndexManager validates DDL identifiers |
| INFO-3 | INFO | Encryption at Rest | SSO secrets encrypted via Data Protection |

---

## Areas Reviewed (No Issues Found)

- **EF Core raw SQL:** Only `CustomFieldIndexManager` uses `ExecuteSqlRawAsync` / `SqlQueryRaw` -- properly mitigated with identifier validation and parameterization.
- **Mass assignment:** EF Core entity configurations use explicit property mapping. No `[Bind]` or auto-mapping from user input to entities detected.
- **Transaction isolation:** EF Core's default `ReadCommitted` isolation is used. Wolverine provides transactional outbox for message reliability.
- **Auto-migration safety:** Most modules gate `MigrateAsync()` behind `IsDevelopment()`. Communications and Identity also allow `Testing` environment. No module runs auto-migration in production/staging.
- **Database schema isolation:** Each module uses its own PostgreSQL schema (`billing`, `communications`, `configuration`, `identity`, `storage`, `audit`, `hangfire`, `wolverine`). Good separation.
- **No `EnableSensitiveDataLogging`:** Not enabled anywhere in source code.
