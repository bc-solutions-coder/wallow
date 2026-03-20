# Wallow Codebase Audit — Sweep 2 Design Document

**Date:** 2026-03-02
**Branch:** expansion
**Methodology:** 3-sweep audit (7 investigators → 2 verifiers → final synthesis)
**Findings:** 64 verified issues across 6 phases

---

## Audit Methodology

- **Wave 1:** 7 parallel investigators deep-dived each module (Identity, Storage, Communications, Billing, Configuration), Shared libraries, and API host/architecture
- **Wave 2:** 2 verifiers spot-checked 46 Critical/High/Medium findings against actual source code — 42 confirmed, 3 partially confirmed, 1 refuted
- **Wave 3:** Final synthesis into prioritized phases

**Refuted finding:** Template injection in `SimpleEmailTemplateService.RenderTemplate` was claimed to be recursive — verification confirmed it is a single linear pass over properties. XSS risk exists but not template injection.

---

## Phase 1: Critical Security & Runtime Crashes

Priority: **Immediate** — actively broken or exploitable.

### 1.1 Storage: S3StorageProvider Singleton Captures Scoped ITenantContext
- **File:** `src/Modules/Storage/Wallow.Storage.Infrastructure/Extensions/StorageInfrastructureExtensions.cs:53`
- **Bug:** `S3StorageProvider` registered as Singleton but constructor takes Scoped `ITenantContext`. The tenant context from the first request is captured forever — all subsequent requests resolve buckets using the wrong tenant's region.
- **Fix:** Register `S3StorageProvider` as Scoped, or resolve `ITenantContext` per-call via `IServiceProvider`.

### 1.2 Storage: Presigned Upload Creates No DB Record
- **File:** `src/Modules/Storage/Wallow.Storage.Application/Queries/GetUploadPresignedUrl/GetUploadPresignedUrlHandler.cs`
- **Bug:** Handler generates a presigned S3 URL but never writes a `StoredFile` record. Files uploaded via presigned URLs are orphaned in S3 with no DB tracking.
- **Fix:** Add a confirmation endpoint that registers the file after upload completes, or create a pending `StoredFile` record before generating the URL.

### 1.3 Billing: Invoice Never Marked Paid After Payment
- **File:** `src/Modules/Billing/Wallow.Billing.Application/Commands/ProcessPayment/ProcessPaymentHandler.cs`
- **Bug:** Handler creates a `Payment` entity but never calls `invoice.MarkAsPaid()`. Invoices remain in Issued/Pending state permanently after successful payment.
- **Fix:** After payment creation, check if invoice is fully paid and call `invoice.MarkAsPaid()`.

### 1.4 Billing: FlushUsageJob SaveChanges Outside Tenant Scope
- **File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs`
- **Bug:** `SaveChangesAsync` called after all `using (_tenantContextFactory.CreateScope(tenantId))` blocks are disposed. EF interceptors stamp wrong/null tenant context. If SaveChanges fails after Redis GETSET already zeroed counters, data is permanently lost.
- **Fix:** Move `SaveChangesAsync` inside each tenant scope iteration.

### 1.5 Billing: MeteringMiddleware Fire-and-Forget With Scoped Service
- **File:** `src/Modules/Billing/Wallow.Billing.Api/Middleware/MeteringMiddleware.cs:82-89`
- **Bug:** `Task.Run` captures scoped `IMeteringService`. After request ends, scope disposes, causing `ObjectDisposedException`.
- **Fix:** Use `IServiceScopeFactory` to create a new scope inside `Task.Run`, or use Wolverine `IMessageBus.SendAsync`.

### 1.6 Configuration: FeatureFlagsController Create/Update Type Mismatch
- **Files:** `src/Modules/Configuration/Wallow.Configuration.Api/Controllers/FeatureFlagsController.cs:75,100`
- **Bug:** `CreateFeatureFlagHandler` returns `Result<Guid>` but controller calls `InvokeAsync<Result<FeatureFlagDto>>`. Same for Update (handler returns `Result`, controller expects `Result<FeatureFlagDto>`). Runtime type mismatch — empty/broken response body.
- **Fix:** Align handler return types with controller expectations, or have controllers request the correct type and re-query.

### 1.7 Identity: Cross-Tenant User Lookup
- **File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/UsersController.cs:46`
- **Bug:** `GetUserById(Guid id)` passes the ID directly to `KeycloakAdminService` with no tenant membership check. Keycloak user IDs are realm-scoped, not tenant-scoped. Any tenant admin with `UsersRead` can fetch any user's email, name, and roles.
- **Fix:** After fetching the user, verify they belong to the current tenant's Keycloak organization before returning.

### 1.8 Identity: OIDC Client Secret Stored in Plaintext
- **File:** `src/Modules/Identity/Wallow.Identity.Domain/Entities/SsoConfiguration.cs:48`
- **Bug:** `OidcClientSecret` persisted as plain string to database. No encryption at rest.
- **Fix:** Use EF Core value encryption, a data protection provider, or a secret management service.

### 1.9 Communications: ArchiveNotification IDOR
- **File:** `src/Modules/Communications/Wallow.Communications.Application/Channels/InApp/Commands/ArchiveNotification/ArchiveNotificationHandler.cs:21-25`
- **Bug:** Checks `notification.TenantId != command.TenantId` but not `notification.UserId != command.UserId`. Any user within a tenant can archive another user's notifications.
- **Fix:** Add `notification.UserId != command.UserId` check.

### 1.10 Architecture: Trivy Scans Wrong Image
- **File:** `.github/workflows/publish.yml:74`
- **Bug:** Trivy `image-ref` points to `ghcr.io/.../latest` (remote registry) but the newly built image was only loaded locally. Scans the previous release, not the new one.
- **Fix:** Use the SHA-tagged local image reference for scanning.

### 1.11 Architecture: Release PRs Auto-Merged
- **File:** `.github/workflows/release-please.yml:38-43`
- **Bug:** `gh pr merge --squash --auto` merges release PRs without human review. Changelog errors or unexpected version bumps go straight to main.
- **Fix:** Remove auto-merge. Require manual review and approval.

---

## Phase 2: Multi-Tenancy Hardening

Priority: **High** — systemic tenant isolation gaps.

### 2.1 Storage: Bucket Name Unique Index Not Per-Tenant
- **File:** `src/Modules/Storage/Wallow.Storage.Infrastructure/Persistence/Configurations/StorageBucketConfiguration.cs:68`
- **Bug:** `HasIndex(b => b.Name).IsUnique()` — global uniqueness. Tenants share bucket namespace.
- **Fix:** Migration to change to `HasIndex(b => new { b.TenantId, b.Name }).IsUnique()`.

### 2.2 Storage: DeleteBucketCommand No TenantId
- **File:** `src/Modules/Storage/Wallow.Storage.Application/Commands/DeleteBucket/DeleteBucketHandler.cs`
- **Bug:** Retrieves bucket by name only, relies entirely on EF global filter for tenant isolation.
- **Fix:** Add explicit `TenantId` to `DeleteBucketCommand` and verify in handler.

### 2.3 Billing: InvoiceNumber Unique Index Not Per-Tenant
- **File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/InvoiceConfiguration.cs:97`
- **Bug:** `HasIndex(i => i.InvoiceNumber).IsUnique()` — two tenants can't use `INV-001`.
- **Fix:** Migration to composite `(TenantId, InvoiceNumber)` unique index.

### 2.4 Configuration: Cross-Tenant Override Exposure
- **File:** `src/Modules/Configuration/Wallow.Configuration.Application/FeatureFlags/Queries/GetOverridesForFlag/GetOverridesForFlagHandler.cs`
- **Bug:** Returns ALL tenants' overrides. Leaks TenantId/UserId pairs.
- **Fix:** Filter overrides by caller's tenant in handler or repository.

### 2.5 Configuration: CreateOverride No TenantId Authorization
- **File:** `src/Modules/Configuration/Wallow.Configuration.Application/FeatureFlags/Commands/CreateOverride/CreateOverrideHandler.cs`
- **Bug:** Accepts arbitrary TenantId — admin from Tenant A can create overrides for Tenant B.
- **Fix:** Validate `command.TenantId` matches caller's tenant context.

### 2.6 Identity: Admin Tenant Override Audit Trail
- **File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs:32-39`
- **Issue:** `X-Tenant-Id` override works correctly for realm admins but only logs at Warning level. No structured audit event.
- **Fix:** Publish a structured audit event. Consider restricting to a dedicated super-admin role separate from tenant-level admin.

### 2.7 Communications: Announcement Not ITenantScoped
- **File:** `src/Modules/Communications/Wallow.Communications.Domain/Announcements/Entities/Announcement.cs`
- **Bug:** No `ITenantScoped` implementation. EF global filter doesn't apply. All tenants' announcements load into memory for targeting.
- **Fix:** If announcements are tenant-scoped, add `ITenantScoped`. If system-wide by design, document and add DB-level filtering in repository.

### 2.8 Communications: EmailMessage TenantId Never Set
- **File:** `src/Modules/Communications/Wallow.Communications.Application/Channels/Email/Commands/SendEmail/SendEmailHandler.cs`
- **Bug:** `EmailMessage.Create()` never receives TenantId. All emails stored with default/empty tenant.
- **Fix:** Pass `ITenantContext.TenantId` into the factory method or command.

### 2.9 Communications: GetMessages Info Disclosure
- **File:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/ConversationsController.cs:100-122`
- **Bug:** Non-participants get empty results instead of 403 — leaks conversation existence.
- **Fix:** Pre-check participant membership and return 403/404.

### 2.10 Architecture: SignalR Cross-Tenant Presence Broadcast
- **File:** `src/Wallow.Api/Hubs/RealtimeHub.cs:22-27`
- **Bug:** `SendToAllAsync` broadcasts `UserOnline`/`UserOffline` to ALL clients regardless of tenant.
- **Fix:** Scope presence broadcasts to tenant groups via `SendToGroupAsync`.

---

## Phase 3: Broken Workflows & Functional Bugs

Priority: **High** — silently wrong results.

### 3.1 Billing: Payment Amount Not Validated Against Invoice
- **File:** `src/Modules/Billing/Wallow.Billing.Application/Commands/ProcessPayment/ProcessPaymentHandler.cs`
- **Fix:** Validate payment amount <= invoice outstanding balance. Reject overpayments.

### 3.2 Billing: InvoicesController Always Bills Authenticated User
- **File:** `src/Modules/Billing/Wallow.Billing.Api/Controllers/InvoicesController.cs`
- **Fix:** Add optional `UserId` to `CreateInvoiceRequest` for admin use. Default to current user.

### 3.3-3.4 Billing: Empty UserEmail in Event Handlers
- **Files:** `PaymentCreatedDomainEventHandler.cs`, `InvoiceOverdueDomainEventHandler.cs`
- **Fix:** Fetch user email via `IUserQueryService` before publishing integration events.

### 3.5 Billing: UsageFlushedEvent Published With TenantId = Guid.Empty
- **File:** `UsageFlushedDomainEventHandler.cs`
- **Fix:** Publish per-tenant events with correct TenantId, or remove TenantId from the contract if flush is cross-tenant.

### 3.6 Configuration: GetAllFlagsAsync N+1
- **File:** `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Services/FeatureFlagService.cs:56-68`
- **Fix:** Batch-load all flags with overrides in a single query. Evaluate in-memory.

### 3.7 Configuration: CachedFeatureFlagService Cache Key Collision
- **File:** `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Services/CachedFeatureFlagService.cs`
- **Fix:** Include result type in cache key (e.g., `ff:bool:{key}:{tenant}` vs `ff:variant:{key}:{tenant}`).

### 3.8 Configuration: UpdateCustomFieldDefinition Can't Clear Description
- **Fix:** Use a sentinel value or separate `ClearDescription` flag to distinguish "not provided" from "set to empty."

### 3.9 Configuration: Guid.Empty as createdBy in CustomField Handlers
- **Fix:** Inject `ICurrentUserService` and pass actual user ID.

### 3.10 Configuration: Domain Events Raised at Wrong Layer
- **Fix:** Move event raising into `FeatureFlag` aggregate methods. Change `FeatureFlag` from `Entity` to `AggregateRoot`.

### 3.11 Communications: Pagination Count Incorrect
- **File:** `GetUserNotificationsHandler.cs`
- **Fix:** Push `IsArchived`/`ExpiresAt` filters into the DB query, not in-memory post-filter.

### 3.12 Communications: Three NotificationType Definitions
- **Fix:** Consolidate into a single enum or string-constant class in `Wallow.Communications.Domain`.

### 3.13 Communications: RetryFailedEmailsJob Never Scheduled
- **Fix:** Add Hangfire recurring job registration in `CommunicationsModuleExtensions`.

### 3.14 Communications: SendEmailRequestedEventHandler No Audit Trail
- **Fix:** Create and persist an `EmailMessage` record before sending, matching `SendEmailHandler` pattern.

### 3.15 Identity: UserRoleChangedEvent.OldRole Wrong
- **File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakAdminService.cs:185-192`
- **Fix:** Fetch current roles BEFORE the assignment, then publish with the actual old role.

---

## Phase 4: Architecture & Clean Separation

Priority: **Medium** — extensibility and maintainability.

### 4.1 Shared.Kernel: Remove WolverineFx References
- Move `WolverineErrorHandlingExtensions` to `Shared.Infrastructure`. Remove `WolverineFx`, `WolverineFx.RabbitMQ`, `WolverineFx.FluentValidation` from `Shared.Kernel.csproj`.

### 4.2 Shared.Kernel: Move CurrentUserService to Shared.Infrastructure
- Remove ASP.NET Core `FrameworkReference` from Kernel (or keep it minimal). Move `CurrentUserService` implementation to Shared.Infrastructure.

### 4.3 Shared.Infrastructure: Split Monolith
- Create `Shared.Infrastructure.Core` (EF, tenancy, auditing, persistence base)
- Create `Shared.Infrastructure.Workflows` (Elsa)
- Create `Shared.Infrastructure.BackgroundJobs` (Hangfire)
- Create `Shared.Infrastructure.Plugins` (plugin system)
- Modules reference only what they need.

### 4.4 Shared: Redesign PermissionType
- Replace monolithic enum with string-based permissions or per-module registration. New modules should not touch Shared.Kernel to add permissions.

### 4.5 Shared: AuditInterceptor PII Exclusion
- Add `[AuditIgnore]` attribute. `AuditInterceptor` skips properties with this attribute during serialization.

### 4.6 Identity: Eliminate Duplicate ICurrentUserService
- Remove `Wallow.Identity.Application.Interfaces.ICurrentUserService`. Use `Shared.Kernel.Services.ICurrentUserService` everywhere.

### 4.7 Configuration: FeatureFlag to AggregateRoot
- Change `FeatureFlag : Entity<FeatureFlagId>` to `FeatureFlag : AggregateRoot<FeatureFlagId>`. Move domain event raising from handlers into the aggregate.

### 4.8 Configuration: Add FluentValidation for CustomField Commands
- Create validators for `CreateCustomFieldDefinition`, `UpdateCustomFieldDefinition`, `ReorderCustomFields`, `DeactivateCustomFieldDefinition`.

### 4.9 Storage: Add Domain Events
- Add `FileUploadedEvent`, `FileDeletedEvent`, `BucketCreatedEvent`, `BucketDeletedEvent`. Publish from aggregate methods.

### 4.10 Communications: Permission-Based Auth on Admin Controllers
- Replace `[Authorize(Roles = "Admin")]` with `[HasPermission(PermissionType.AnnouncementManage)]` and `[HasPermission(PermissionType.ChangelogManage)]` on `AdminAnnouncementsController` and `AdminChangelogController`.

---

## Phase 5: CI/CD & Deployment Hardening

Priority: **Medium** — production readiness.

### 5.1 Fix Trivy Scan Image Reference
- Use SHA-tagged local image: `image-ref: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:sha-${{ github.sha }}`

### 5.2 Remove Release PR Auto-Merge
- Remove the auto-merge step from `release-please.yml`. Add branch protection requiring approval.

### 5.3 Restrict Manual Publish Dispatch
- Add `version` input to `workflow_dispatch` or remove manual trigger entirely.

### 5.4 Pin Docker Base Images
- Pin `aspnet:10.0` and `sdk:10.0` to specific digest or patch version.

### 5.5 Pin Keycloak Image
- Pin `keycloak:26.0` to digest in `docker-compose.yml`.

### 5.6 Add Dockerfile HEALTHCHECK
- Add `HEALTHCHECK CMD curl -f http://localhost:8080/healthz || exit 1`.

### 5.7 Remove RabbitMQ Management Port in Production
- Add `ports: !reset []` for RabbitMQ management in `docker-compose.prod.yml`.

### 5.8 Add Production API Service Definition
- Add `wallow-api` service to `docker-compose.prod.yml` with resource limits, health check, and log rotation.

---

## Phase 6: Consistency & Code Quality

Priority: **Low** — technical debt reduction.

### 6.1 Standardize DateTimeOffset
- Replace all `DateTime` usage in domain entities with `DateTimeOffset`. Affected: Identity (ScimConfiguration, ServiceAccountMetadata, ScimSyncLog), Billing (event handlers).

### 6.2 Standardize TimeProvider Injection
- Inject `TimeProvider` into `CustomFieldDefinition`, metering entities (`MeterDefinition`, `QuotaDefinition`), and `EmailPreference`. Remove direct `DateTimeOffset.UtcNow` calls.

### 6.3 Fix var Usage
- Audit and replace `var` with explicit types per project rule. Exception: anonymous types where `var` is required.

### 6.4 Identity: Configurable Keycloak Realm
- Replace hardcoded `"wallow"` string with `KeycloakAuthenticationOptions.Realm` or a configuration value.

### 6.5 Identity: Batch Keycloak Role Lookups
- Cache role lookups or use a batch Keycloak admin endpoint to reduce N+1 calls in `GetUsersAsync`.

### 6.6 Identity: Remove ScimController.GetGroup Stub
- Either implement `GetGroup` for SCIM compliance or remove the endpoint entirely.

### 6.7 Storage: Remove StorageKey From DTOs
- Remove `StorageKey` from `StoredFileDto` and `PresignedUploadResponse`. Internal implementation detail should not be in API responses.

### 6.8 Storage: Add Pagination to File Listing
- Add `PageNumber`/`PageSize` parameters to `GetFilesByBucket` query and endpoint.

### 6.9 Billing: Add Permission Checks to UsageController
- Add `[HasPermission(PermissionType.BillingRead)]` to all `UsageController` endpoints.

### 6.10 Billing: Fix Metering Route Convention
- Move from `/api/v1/metering/usage` to `/api/v1/billing/metering/usage`.

### 6.11 Configuration: Add FeatureFlagOverride Unique Constraint
- Migration to add unique index on `(FlagId, TenantId, UserId)`.

### 6.12 Configuration: Compute ChangedProperties Dynamically
- Track which fields actually changed in `UpdateFeatureFlagHandler` instead of hardcoding.

### 6.13 Configuration: Version Evaluate Endpoint
- Move `/api/feature-flags/evaluate` to `/api/v{version}/configuration/feature-flags/evaluate`.

### 6.14 Communications: SMTP Connection Pooling
- Implement connection reuse via MailKit `SmtpClient` pooling or a connection-per-tenant cache.

### 6.15 Communications: Remove Unused Dependencies
- Remove unused `INotificationRepository _` from `AnnouncementPublishedEventHandler`.

### 6.16 Architecture: Fix CSP for Internal Dashboards
- Add path-based CSP exceptions for `/hangfire` (needs `'unsafe-inline'` and CDN sources) and SignalR (`connect-src`).

### 6.17 Architecture: Fix SystemHeartbeatJob Logging
- Replace `Serilog.Log.Information` with injected `ILogger<SystemHeartbeatJob>`.

### 6.18 Architecture: Fix Module Registration Test
- Replace `File.ReadAllText` string search with reflection-based module discovery test.

### 6.19 Architecture: Update Outdated CLAUDE.md Files
- Update Configuration module's `CLAUDE.md` "Known Issues" section (claims no flag evaluation service and no caching — both exist now).
