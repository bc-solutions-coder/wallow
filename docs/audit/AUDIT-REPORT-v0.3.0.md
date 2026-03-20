# Wallow v0.3.0 Pre-Release Audit Report

**Date:** 2026-03-02
**Branch:** `expansion`
**Methodology:** Two-pass audit (initial scan + independent verification) across 928 source files
**Audited Areas:** Security, Performance, Code Quality & Architecture, Infrastructure & DevOps

---

## Executive Summary

Wallow is a well-architected .NET 10 modular monolith with strong foundations: excellent module isolation, proper multi-tenancy, rich domain models, comprehensive observability, and consistent CQRS patterns. The codebase demonstrates mature engineering practices in many areas.

However, the audit identified **5 critical**, **14 high**, **36 medium**, and **21 low** findings across all domains that should be addressed before or shortly after the v0.3.0 release.

| Severity | Security | Performance | Code Quality | Infrastructure | Total |
|----------|----------|-------------|--------------|----------------|-------|
| CRITICAL | 0 | 3 | 0 | 2 | **5** |
| HIGH | 2 | 6 | 3 | 3 | **14** |
| MEDIUM | 4 | 12 | 10 | 10 | **36** |
| LOW | 5 | 5 | 7 | 4 | **21** |
| **Total** | **11** | **26** | **20** | **19** | **76** |

---

## Critical Findings (Must Fix Before Release)

### C1. N+1 Query in GetConversationsAsync (Performance)
**File:** `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/MessagingQueryService.cs:170-189`
**Impact:** Fetches participants per-conversation in a loop. 50 conversations = 51 queries.
**Fix:** Single batch query joining conversations with participants.

### C2. N+1 HTTP Calls in GetUsersAsync (Performance)
**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakAdminService.cs:156-197`
**Impact:** Sequential `GetUserRolesAsync` HTTP call per user. 20 users = 21 HTTP requests to Keycloak.
**Fix:** Batch role fetching or parallel HTTP calls with `Task.WhenAll`.

### C3. MeteringMiddleware Runs 2-4 Redis + 2 DB Queries Per Request (Performance)
**File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Middleware/MeteringMiddleware.cs`
**Impact:** Every `/api/*` request cascades through quota checking: DB -> DB -> Redis -> threshold check. Adds significant latency to all API calls.
**Fix:** Cache plan/quota data with short TTL; check metering asynchronously.

### C4. Valkey (Redis) Has No Authentication (Infrastructure)
**File:** `docker/docker-compose.yml:49`
**Impact:** No `--requirepass` flag. Port 6379 exposed. Production overlay doesn't mention Valkey at all.
**Fix:** Add `--requirepass` to Valkey command, update connection strings, restrict ports in production.

### C5. Auto-Migration Runs Unconditionally in All Environments (Infrastructure)
**Files:** 6 locations across all modules + audit
**Impact:** `MigrateAsync()` runs on every startup including production. The audit module (`AuditingExtensions.cs:27-32`) has NO try/catch -- migration failure crashes the app.
**Fix:** Gate behind environment check or feature flag; use migration bundles for production.

---

## High Findings (Fix Soon After Release)

### H1. SCIM Controller Exposes Internal Exception Messages (Security)
**File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs` (7 catch blocks)
**Impact:** `ex.Message` returned in all environments, exposing Keycloak errors, DB constraints, etc.
**Fix:** Use environment-aware error messages as in `GlobalExceptionHandler`.

### H2. Message Body Not HTML-Sanitized -- Stored XSS Risk (Security)
**File:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/ConversationsController.cs:130-131`
**Impact:** `request.Body` passed directly to `SendMessageCommand` without sanitization. `AdminAnnouncementsController` uses `IHtmlSanitizationService` but `ConversationsController` does not. Stored XSS via conversation messages.
**Fix:** Sanitize in controller or domain layer before persistence.

### H3. Zero `AsNoTracking()` Usage Across Entire Codebase (Performance)
**Impact:** All EF Core read queries carry change tracking overhead. Dapper queries are exempt.
**Fix:** Add `AsNoTracking()` to all read-only queries, or configure `QueryTrackingBehavior.NoTracking` as default.

### H4. Multiple Unbounded Queries Without Pagination (Performance)
**Files:** Invoices, payments, subscriptions, notifications, announcements repositories
**Impact:** Full table scans per tenant with no `Take()` limit. Large tenants risk memory exhaustion.
**Fix:** Add pagination to all list endpoints; remove or limit the non-paged `GetByUserIdAsync`.

### H5. Three Separate Redis ConnectionMultiplexer Instances (Performance)
**File:** `src/Wallow.Api/Program.cs:192-224`
**Impact:** Three connections to the same Redis instance instead of sharing one.
**Fix:** Register singleton `IConnectionMultiplexer` and share across all consumers.

### H6. UserQueryService Fetches All Members for Count Operations (Performance)
**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/UserQueryService.cs`
**Impact:** Count methods (`GetNewUsersCountAsync`, `GetActiveUsersCountAsync`, `GetTotalUsersCountAsync`) fetch full member lists just to count them.
**Fix:** Use `COUNT(*)` queries or Keycloak API count endpoints.

### H7. `ResultExtensions` Duplicated 6 Times with Active Divergence (Code Quality / DRY)
**Files:** 6 copies across `Wallow.Api` + all 5 module API layers
**Impact:** The host API version has richer ProblemDetails (`Title`, `Type` fields) while module copies produce bare responses. Error responses differ between host and module APIs.
**Fix:** Consolidate to `Shared.Kernel` or a shared API utilities project.

### H8. `GetCurrentUserId()` Duplicated 10 Times with Inconsistent Behavior (Code Quality / DRY)
**Files:** 10 implementations across all module controllers
**Impact:** 4 controllers (Billing + Storage) return `Guid.Empty` on auth failure; 6 return `null` with proper 401. `Invoice.Create()` validates against `Guid.Empty` but `Payment.Create()` and `Subscription.Create()` do not -- data integrity risk.
**Fix:** Move to shared `ICurrentUserService` in `Shared.Kernel`; always return 401 on missing claims.

### H9. `DateTime.UtcNow` in 55+ Locations (Code Quality)
**Files:** ~25 in domain entities, 2 in `AuditableEntity.cs` (affects all auditable entities), plus infrastructure
**Impact:** Time-dependent logic is untestable. The 2 usages in `AuditableEntity.cs` propagate to every entity.
**Fix:** Inject `TimeProvider` (built-in .NET 8+) and use `TimeProvider.System.GetUtcNow()`.

### H10. No Database Connection Pooling or Timeout Configuration (Infrastructure)
**Files:** All module `InfrastructureExtensions` and connection strings
**Impact:** Default Npgsql pool (100 connections), no retry, no timeout. Under load, connection exhaustion with no resilience.
**Fix:** Add `EnableRetryOnFailure()`, configure `MaxPoolSize`, `Command Timeout`, and `Connection Idle Lifetime`.

### H11. No HttpClient Resilience Policies for External Services (Infrastructure)
**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:71-77`
**Impact:** Keycloak and Twilio HTTP clients have no retry, timeout, or circuit breaker policies.
**Fix:** Add `Microsoft.Extensions.Http.Resilience` with `AddStandardResilienceHandler()`.

### H12. CI/CD Rebuilds From Scratch in Each Job (Infrastructure)
**File:** `.github/workflows/ci.yml`
**Impact:** Build, unit-test, and integration-test jobs each do full checkout + restore + build independently.
**Fix:** Share build artifacts between jobs or use a single job with steps.

---

## Clean Architecture Compliance

**Overall: Strong with Minor Violations**

**What's done well:**
- Module isolation is excellent -- zero cross-module references verified across all `.csproj` files
- Correct dependency direction: Domain -> Application -> Infrastructure -> API in all modules
- Modules communicate only via `Shared.Contracts` and RabbitMQ events
- Each module owns its database schema (separate PostgreSQL schemas)

**Violations found:**
- **Shared.Kernel has infrastructure dependencies** (MEDIUM): References `WolverineFx`, `WolverineFx.RabbitMQ`, `FluentValidation`, `Microsoft.EntityFrameworkCore`, and `Microsoft.AspNetCore.App`. This violates "Domain has no external dependencies" from the project's own CLAUDE.md.
- **`StorageBucket` stores JSON in domain** (MEDIUM): `StorageBucket.AllowedContentTypes` uses `System.Text.Json.JsonSerializer` directly in the domain entity, coupling the domain to a serialization framework.
- **`ICurrentUserService` is module-internal** (INFO): Lives in Identity.Application, cannot be shared cross-module without violating isolation. Other modules duplicate `GetCurrentUserId()` instead.

---

## DDD Adherence

**Overall: Good with Room for Improvement**

**What's done well:**
- Rich domain models with state machines (Invoice, Subscription lifecycle management)
- Proper invariant enforcement in entity factory methods
- Strongly-typed IDs throughout (`InvoiceId`, `TenantId`, `ConversationId`, etc.)
- Well-designed value objects
- Domain events raised from aggregate roots

**Violations found:**
- **`IAuditableEntity` interface is orphaned** (MEDIUM): Defined but never implemented by `AuditableEntity<TId>`. Dead code with misleading contract.
- **Storage entities are not aggregate roots** (MEDIUM): `StoredFile` and `StorageBucket` extend `Entity<T>` instead of `AggregateRoot<T>`, so they cannot raise domain events and have no audit trail.
- **`Payment.Create()` raises misleading event** (LOW): `PaymentReceivedDomainEvent` fires at creation (status=Pending), not when payment is actually received. The `Complete()` method raises no event.
- **Inconsistent `Guid.Empty` validation** (MEDIUM): `Invoice.Create()` validates but `Payment.Create()` and `Subscription.Create()` do not.
- **`Conversation.AddParticipant` and `CreateGroup` allow duplicate participants** (LOW): No uniqueness check before adding to the participants collection.

---

## DRY Principle Compliance

**Overall: Moderate -- Several Significant Violations**

| Duplication | Copies | Severity |
|-------------|--------|----------|
| `ResultExtensions` | 6 (with divergence) | HIGH |
| `GetCurrentUserId()` | 10 (with behavioral differences) | HIGH |
| `DateTime.UtcNow` | 55+ direct usages | HIGH |
| `EnumMappings` pattern | 3 (different content per module) | LOW |
| Design-time DB factories | 3 (with credential inconsistency) | MEDIUM |
| Module initializer try/catch pattern | 5 (identical), 1 missing | MEDIUM |

The `ResultExtensions` and `GetCurrentUserId()` duplications are the most impactful -- they have actively diverged, causing inconsistent API behavior and data integrity risks.

---

## Security Summary

| Finding | Severity | Status |
|---------|----------|--------|
| SCIM exception message leak | HIGH | 7 catch blocks exposing `ex.Message` |
| Stored XSS in conversation messages | HIGH | Missing HTML sanitization |
| Missing `[HasPermission]` on 13 controllers | MEDIUM | Includes financial data controllers |
| GetMessages IDOR (within tenant) | MEDIUM | Missing participant authorization |
| SSO test endpoint returns full stack trace | MEDIUM | `ex.ToString()` in `DebugInfo` field |
| `GetCurrentUserId` returns `Guid.Empty` | MEDIUM | 4 controllers with silent failure |
| Multi-tenancy isolation | STRONG | EF query filters + Dapper parameterization confirmed |
| SQL injection prevention | STRONG | All queries properly parameterized |
| File upload validation | STRONG | Magic byte checking + filename sanitization |

---

## Performance Summary

| Finding | Severity | Impact |
|---------|----------|--------|
| N+1 conversations query | CRITICAL | 51 queries for 50 conversations |
| N+1 Keycloak user roles | CRITICAL | 21 HTTP calls for 20 users |
| MeteringMiddleware per-request overhead | CRITICAL | 2-4 Redis + 2 DB queries per API call |
| Zero AsNoTracking usage | HIGH | Change tracking overhead on all reads |
| Unbounded queries | HIGH | No pagination on 5+ endpoints |
| 3 Redis ConnectionMultiplexers | HIGH | 3x connection overhead |
| Cache invalidation broken | MEDIUM | Wrong key pattern in `CachedFeatureFlagService` |
| BuildServiceProvider anti-pattern | MEDIUM | Temporary DI container during registration |
| Dapper missing CancellationToken | MEDIUM | Can't cancel long-running queries |

---

## Infrastructure Summary

| Finding | Severity | Impact |
|---------|----------|--------|
| Valkey no authentication | CRITICAL | Open Redis accessible on host |
| Auto-migration in all environments | CRITICAL | Uncontrolled schema changes in production |
| No DB connection pooling config | HIGH | Default pool, no retry, no timeout |
| No HttpClient resilience | HIGH | No retry/circuit breaker for Keycloak/Twilio |
| CI rebuilds everything per job | HIGH | Wasted compute, slow pipelines |
| No message idempotency | MEDIUM | Duplicate processing risk |
| Keycloak dev mode in all environments | MEDIUM | No production override |
| Valkey missing from prod compose | MEDIUM | No limits, no port restriction |
| Trivy scans after image push | LOW | Vulnerable images published before scan |

---

## Strengths (What's Done Well)

1. **Module isolation** -- Zero cross-module references. Modules communicate only via events and shared contracts.
2. **Multi-tenancy** -- Robust tenant isolation via EF Core query filters, Dapper parameterization, and interceptors.
3. **Rich domain models** -- Invoice, Subscription, and Conversation have proper state machines with invariant enforcement.
4. **Observability** -- Full OpenTelemetry + Grafana stack with SLO alerts, structured Serilog logging, 3 health check endpoints.
5. **Security headers** -- Comprehensive middleware for HSTS, CSP, X-Frame-Options, etc.
6. **Strongly-typed IDs** -- No raw GUIDs in domain layer; proper `InvoiceId`, `TenantId`, etc.
7. **Async patterns** -- Proper async/await throughout; no sync-over-async detected.
8. **Rate limiting** -- Applied to auth and upload endpoints.
9. **Test coverage** -- 90% threshold enforced in CI; 80+ test files in Billing alone.
10. **Docker hardening** -- Multi-stage Dockerfile with non-root user, comprehensive `.dockerignore`.

---

## Prioritized Remediation Roadmap

### Before v0.3.0 Release (Critical)
1. Add authentication to Valkey (`--requirepass`)
2. Gate auto-migration behind environment check
3. Fix N+1 in GetConversationsAsync (batch query)
4. Fix MeteringMiddleware (cache quota data)
5. Sanitize SCIM error responses
6. Add participant check to GetMessagesAsync (IDOR fix)

### Shortly After Release (High)
7. HTML-sanitize conversation message bodies
8. Add `AsNoTracking()` globally
9. Consolidate `ResultExtensions` to shared project
10. Consolidate `GetCurrentUserId()` to shared service
11. Add pagination to all unbounded queries
12. Share single Redis ConnectionMultiplexer
13. Add DB connection pooling and resilience
14. Add HttpClient resilience policies
15. Inject `TimeProvider` instead of `DateTime.UtcNow`

### Next Sprint (Medium)
16. Add `[HasPermission]` to all 13 bare `[Authorize]` controllers
17. Remove `DebugInfo` from `SsoTestResult`
18. Fix cache invalidation key pattern
19. Add message idempotency to Wolverine handlers
20. Override Keycloak to production mode
21. Add Valkey to production compose with limits
22. Fix `InvalidOperationException` -> proper domain exceptions
23. Optimize CI to share build artifacts between jobs

### Backlog (Low)
24. Pin Docker image tags for Mailpit and Grafana
25. Add Dependabot grouping strategy
26. Scan Docker images before publishing
27. Add composite index on Notification (UserId, IsRead)
28. Deduplicate participants in `CreateGroup()`

---

## Detailed Reports

- [Security Audit (Verified)](security-audit-verified.md)
- [Performance Audit (Verified)](performance-audit-verified.md)
- [Code Quality Audit (Verified)](code-quality-audit-verified.md)
- [Infrastructure Audit (Verified)](infrastructure-audit-verified.md)
