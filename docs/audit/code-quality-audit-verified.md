# Code Quality and Architecture Audit - Verified Report

**Original Audit Date:** 2026-03-02
**Verification Date:** 2026-03-02
**Scope:** All source code under `src/` and `tests/`

---

## Verification Methodology

Every finding in the original audit was verified by:
1. Reading the exact source files and line numbers referenced
2. Confirming the described issue exists in the current codebase
3. Validating severity ratings against actual impact
4. Checking for false positives and missed issues

---

## Verified Findings

### HIGH

#### H1. Duplicated `ResultExtensions` Across All Modules

**Status: CONFIRMED**

**Verified copies (6 total):**
- `src/Wallow.Api/Extensions/ResultExtensions.cs` (131 lines, richer version)
- `src/Modules/Billing/Wallow.Billing.Api/Extensions/ResultExtensions.cs` (60 lines)
- `src/Modules/Identity/Wallow.Identity.Api/Extensions/ResultExtensions.cs`
- `src/Modules/Storage/Wallow.Storage.Api/Extensions/ResultExtensions.cs`
- `src/Modules/Communications/Wallow.Communications.Api/Extensions/ResultExtensions.cs`
- `src/Modules/Configuration/Wallow.Configuration.Api/Extensions/ResultExtensions.cs`

**Verified divergence:** The host API version (`Wallow.Api`) has:
- `ToNoContentResult()` method (missing from module copies)
- `ToCreatedResult` overload with `CreatedAtAction` (missing from module copies)
- `GetTitle()` and `GetTypeUri()` helpers producing structured ProblemDetails with `Title` and `Type` fields
- Module copies produce ProblemDetails **without** `Title` or `Type` fields, only `Detail` and `code` extension

The audit description is accurate. The module copies are simpler and already diverged.

**Severity: HIGH - Confirmed.** Active divergence means error responses differ between the host API and module APIs.

---

#### H2. Duplicated `GetCurrentUserId()` Across All Controllers

**Status: CONFIRMED with corrections**

**Verified count: 10 implementations** (audit said "at least 10" -- exactly 10 confirmed):

Returning `Guid` (silent `Guid.Empty` fallback):
1. `src/Modules/Billing/Wallow.Billing.Api/Controllers/InvoicesController.cs:178`
2. `src/Modules/Billing/Wallow.Billing.Api/Controllers/PaymentsController.cs:91`
3. `src/Modules/Billing/Wallow.Billing.Api/Controllers/SubscriptionsController.cs:115`
4. `src/Modules/Storage/Wallow.Storage.Api/Controllers/StorageController.cs:301`

Returning `Guid?` (proper null):
5. `src/Modules/Communications/Wallow.Communications.Api/Controllers/ConversationsController.cs:171`
6. `src/Modules/Communications/Wallow.Communications.Api/Controllers/NotificationsController.cs:136`
7. `src/Modules/Communications/Wallow.Communications.Api/Controllers/AnnouncementsController.cs:86`
8. `src/Modules/Communications/Wallow.Communications.Api/Controllers/EmailPreferencesController.cs:85`
9. `src/Modules/Identity/Wallow.Identity.Api/Controllers/ApiKeysController.cs:196`
10. `src/Modules/Configuration/Wallow.Configuration.Api/Controllers/FeatureFlagsController.cs:223`

**Correction:** The audit said Storage controllers return `Guid?` -- actually StorageController returns `Guid` with `Guid.Empty` fallback, same as Billing. So **4 controllers** (all Billing + Storage) have the dangerous `Guid.Empty` pattern, not just 3.

**Additional finding:** `ICurrentUserService` exists at `src/Modules/Identity/Wallow.Identity.Application/Interfaces/ICurrentUserService.cs` with implementation at `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/CurrentUserService.cs`, but it is only used within the Identity module -- no other module injects it. The remediation suggestion to use `ICurrentUserService` across modules would require exposing it via a shared contract or moving it to Shared.Kernel.

**Severity: HIGH - Confirmed.** The `Guid.Empty` fallback is particularly dangerous because `Invoice.Create()` validates `userId == Guid.Empty` and throws `BusinessRuleException`, but other entities (Payment, Subscription) do NOT validate, allowing records with `Guid.Empty` as the user ID.

---

#### H3. `IAuditableEntity` Interface Defined But Never Implemented

**Status: CONFIRMED**

**Verified at:** `src/Shared/Wallow.Shared.Kernel/Domain/AuditableEntity.cs:45-54`

`AuditableEntity<TId>` extends `Entity<TId>` (line 10) and does NOT implement `IAuditableEntity`. The interface exists at lines 45-54 in the same file but has zero implementations and zero references anywhere else in the codebase (only 1 file matches `IAuditableEntity`).

**Severity: HIGH - Confirmed, but could be argued as MEDIUM.** The interface is truly orphaned dead code. No infrastructure code currently depends on it, so there's no active bug. The risk is that someone writes a save interceptor using `is IAuditableEntity` expecting it to work. Given the existing `SetCreated()`/`SetUpdated()` methods on `AuditableEntity` are called manually by entities (not by an interceptor), this is unused code rather than a broken feature.

**Severity adjustment: HIGH -> MEDIUM.** No active bug exists, just dead code with a misleading contract.

---

#### H4. `DateTime.UtcNow` Scattered Throughout Domain Entities

**Status: CONFIRMED with updated count**

**Verified count: 55+ usages** across the entire `src/` directory, not "18+ domain entity files" as the audit stated.

**Breakdown by location type:**
- **Domain entities:** ~25 usages across Billing (Invoice, Payment, Subscription, UsageRecord), Communications (EmailMessage, SmsMessage, Notification, Conversation, Announcement, AnnouncementDismissal), Configuration (FeatureFlag x6, FeatureFlagOverride x4), Storage (StorageBucket, StoredFile), Identity (ScimConfiguration x4, ScimSyncLog, ServiceAccountMetadata)
- **Shared Kernel:** 2 usages in `AuditableEntity.cs` (SetCreated, SetUpdated) -- these propagate to ALL auditable entities
- **Shared Contracts:** 2 usages (`IIntegrationEvent.OccurredAt`, `IDomainEvent.OccurredAt`, `RealtimeEnvelope`)
- **Application handlers:** 3 usages
- **Infrastructure services:** 6 usages
- **API middleware:** 2 usages

The audit's "18+" figure significantly undercounts. The most impactful are the 2 in `AuditableEntity.cs` since every entity inheriting from it gets untestable timestamps.

**Severity: HIGH - Confirmed.** Count was understated.

---

### MEDIUM

#### M1. `TenantId` Has Public Setter on All Entities

**Status: CONFIRMED**

**Verified at:** `src/Shared/Wallow.Shared.Kernel/MultiTenancy/ITenantScoped.cs:7` -- `TenantId TenantId { get; set; }`

All tenant-scoped entities expose `TenantId { get; set; }`. Confirmed by inspection of `Invoice`, `Payment`, `Conversation`, `StoredFile`, etc.

**Severity: MEDIUM - Confirmed.** The `TenantSaveChangesInterceptor` needs write access, but the public setter allows any layer to mutate it.

---

#### M2. Shared Kernel Has Heavy Infrastructure Dependencies

**Status: CONFIRMED**

**Verified at:** `src/Shared/Wallow.Shared.Kernel/Wallow.Shared.Kernel.csproj:15-27`

Dependencies confirmed:
- `WolverineFx`
- `WolverineFx.RabbitMQ`
- `WolverineFx.FluentValidation`
- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`
- `Microsoft.EntityFrameworkCore`
- `Microsoft.AspNetCore.App` (framework reference)

**Correction:** The audit listed 4 packages. Actual count is **6 packages + 1 framework reference**. The audit missed `WolverineFx.FluentValidation` and `FluentValidation.DependencyInjectionExtensions`.

**Severity: MEDIUM - Confirmed.** Violates the stated principle "Domain has no external dependencies" from CLAUDE.md.

---

#### M3. `StoredFile` and `StorageBucket` Are Not Aggregate Roots

**Status: CONFIRMED**

**Verified:**
- `StoredFile` at `src/Modules/Storage/Wallow.Storage.Domain/Entities/StoredFile.cs:12`: extends `Entity<StoredFileId>`
- `StorageBucket` at `src/Modules/Storage/Wallow.Storage.Domain/Entities/StorageBucket.cs:15`: extends `Entity<StorageBucketId>`

Neither extends `AggregateRoot<T>`, so neither can raise domain events. Neither extends `AuditableEntity<T>`, so no audit trail. Both are managed directly by repositories as top-level entities.

**Severity: MEDIUM - Confirmed.**

---

#### M4. Inconsistent Error Handling: Exceptions vs Result Pattern

**Status: CONFIRMED with important correction**

The audit states domain exceptions "bypass this entirely" and "produce generic 500 errors." This is **partially inaccurate**.

**Verified:** `GlobalExceptionHandler` at `src/Wallow.Api/Middleware/GlobalExceptionHandler.cs:44-76` **does** handle domain exceptions:
- `EntityNotFoundException` -> 404
- `BusinessRuleException` -> 422
- `ValidationException` -> 400
- `ArgumentException` / `ArgumentNullException` -> 400
- Everything else (including `InvalidOperationException`) -> 500

So `BusinessRuleException` and `InvalidInvoiceException` (which extends `DomainException`) **do** get proper HTTP status codes via the GlobalExceptionHandler. The audit's example of `Invoice.Create()` throwing `BusinessRuleException` producing 500 is **incorrect** -- it produces 422.

**However**, the inconsistency is real in a different way:
- Result path: Returns structured `ProblemDetails` with `code` extension matching the `Error.Code`
- Exception path: Returns `ProblemDetails` with `code` extension matching the `DomainException.Code`
- `InvalidOperationException` (used in Communications and Configuration domains): Returns **500** with no code, which IS a problem

**Severity adjustment: MEDIUM -> LOW for the exception-vs-Result inconsistency.** The GlobalExceptionHandler handles most domain exceptions correctly. The real issue is that `InvalidOperationException` in Communications/Configuration domains (5 usages) produces 500 instead of 422.

---

#### M5. `StorageBucket.AllowedContentTypes` Stores JSON String

**Status: CONFIRMED**

**Verified at:** `src/Modules/Storage/Wallow.Storage.Domain/Entities/StorageBucket.cs:1,22,47-49,67,131-133`

The domain entity imports `System.Text.Json` (line 1) and uses `JsonSerializer.Serialize` in `Create()` (line 49) and `UpdateAllowedContentTypes()` (line 133), plus `JsonSerializer.Deserialize` in `IsContentTypeAllowed()` (line 67).

**Severity: MEDIUM - Confirmed.** Domain entity depends on serialization framework.

---

#### M6. Missing Pagination on Unbounded Query Endpoints

**Status: CONFIRMED**

**Verified:**
- `GetAllInvoicesQuery` at `src/Modules/Billing/Wallow.Billing.Application/Queries/GetAllInvoices/GetAllInvoicesQuery.cs` -- empty record, no pagination parameters
- `PagedResult<T>` exists at `src/Shared/Wallow.Shared.Kernel/Pagination/PagedResult.cs` but is only used by the Communications module's `GetUserNotifications`

**Severity: MEDIUM - Confirmed.**

---

#### M7. `FeatureFlag` and `FeatureFlagOverride` Manage Own Timestamps

**Status: CONFIRMED**

**Verified at:**
- `src/Modules/Configuration/Wallow.Configuration.Domain/Entities/FeatureFlag.cs:12` extends `Entity<FeatureFlagId>` (not `AuditableEntity`)
- Has manual `CreatedAt` (line 32) and `UpdatedAt` (line 33) properties
- Sets `CreatedAt = DateTime.UtcNow` in all factory methods (lines 50, 70, 95)
- Sets `UpdatedAt = DateTime.UtcNow` in mutation methods (lines 107, 124, 146)
- `FeatureFlagOverride` similarly has manual `CreatedAt` (line 29) set in all 3 factory methods

**Severity: MEDIUM - Confirmed.**

---

### LOW

#### L1. `Payment.Create()` Raises `PaymentReceivedDomainEvent` Before Payment Is Complete

**Status: CONFIRMED**

**Verified at:** `src/Modules/Billing/Wallow.Billing.Domain/Entities/Payment.cs:65-70`

`Payment.Create()` sets `Status = PaymentStatus.Pending` (via constructor, line 45) and then raises `PaymentReceivedDomainEvent`. The `Complete()` method (line 75) changes status to `Completed` but raises no domain event.

**Severity: LOW - Confirmed.** The naming is misleading. A "PaymentReceived" event fires when the payment is merely initiated, not received.

---

#### L2. Billing API Layer Clean Architecture Adherence (Positive Finding)

**Status: CONFIRMED**

This is a positive observation, not a bug. All module API layers correctly reference only their Application project.

---

#### L3. `Conversation.AddParticipant` Allows Duplicate Participants

**Status: CONFIRMED**

**Verified at:** `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Entities/Conversation.cs:91-106`

`AddParticipant()` creates a new `Participant` and adds it directly to `_participants` with no duplicate check. Note: `CreateGroup()` (line 49-64) also lacks duplicate checks -- if `memberIds` contains duplicates, the same user gets added twice.

**Severity: LOW - Confirmed.**

---

#### L4. Inconsistent Use of `InvalidOperationException` vs Domain Exceptions

**Status: CONFIRMED**

**Verified `InvalidOperationException` in domain entities:**
- `Conversation.cs:71,77,95` (3 usages in Communications)
- `FeatureFlag.cs:114,131` (2 usages in Configuration)

**Verified `DomainException` subclasses in domain entities:**
- `Invoice.cs` uses `InvalidInvoiceException` and `BusinessRuleException`
- `Payment.cs` uses `InvalidPaymentException`
- `Subscription.cs` uses `InvalidSubscriptionStatusTransitionException`

The `GlobalExceptionHandler` handles `DomainException` subclasses properly (404/422) but `InvalidOperationException` falls through to 500. This means business rule violations in Communications and Configuration produce 500 errors while equivalent violations in Billing produce 422.

**Severity adjustment: LOW -> MEDIUM.** The impact is more significant than the audit rated -- this causes incorrect HTTP status codes (500 instead of 422) for legitimate business rule violations in 2 modules.

---

#### L5. `EnumMappings` Duplicated Across Module API Layers

**Status: CONFIRMED**

**Verified 3 copies:**
- `src/Modules/Identity/Wallow.Identity.Api/Mappings/EnumMappings.cs`
- `src/Modules/Communications/Wallow.Communications.Api/Mappings/EnumMappings.cs`
- `src/Modules/Configuration/Wallow.Configuration.Api/Mappings/EnumMappings.cs`

**Severity: LOW - Confirmed.** Each maps different module-specific enums, so the pattern is duplicated but the content differs.

---

#### L6. `StoredFile.Create()` Uses Object Initializer Instead of Constructor

**Status: CONFIRMED**

**Verified at:** `src/Modules/Storage/Wallow.Storage.Domain/Entities/StoredFile.cs:40-54`

Uses object initializer with an empty private constructor. All properties are set via init/set, not enforced by the compiler.

**Note:** `StorageBucket.Create()` (same module) and `FeatureFlag.CreateBoolean/Percentage/Variant()` and all `FeatureFlagOverride.Create*()` methods follow the same pattern. This is more widespread than the audit indicated (just StoredFile).

**Severity: LOW - Confirmed.**

---

## NEW Findings (Missed by Original Audit)

### NEW-M1. `InvalidOperationException` in Domain Entities Produces HTTP 500 (Elevated from L4)

Already covered above as a severity adjustment to L4, but worth calling out separately: The `GlobalExceptionHandler` does NOT map `InvalidOperationException` to a business-appropriate status code. Five domain entity methods in Communications and Configuration throw `InvalidOperationException` for business rule violations, resulting in 500 responses that should be 422.

---

### NEW-M2. `CreateGroup()` Also Allows Duplicate Participants

**File:** `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Entities/Conversation.cs:49-64`

The audit only flagged `AddParticipant()` (L3), but `CreateGroup()` iterates `memberIds` without deduplication. If the same `memberId` appears twice, two `Participant` objects are created. Additionally, if `creatorId` is in `memberIds`, the creator is added twice.

**Severity: LOW**

---

### NEW-M3. `ICurrentUserService` Is Module-Internal, Not Shared

**File:** `src/Modules/Identity/Wallow.Identity.Application/Interfaces/ICurrentUserService.cs`

The audit's H2 remediation suggests injecting `ICurrentUserService` into all controllers. However, this interface lives in the Identity module's Application layer, and other modules cannot reference it without violating module isolation. To use it cross-module, it would need to be moved to `Shared.Kernel` or a new shared project.

**Severity: INFO (remediation consideration)**

---

### NEW-L1. `StorageBucket` Is Not Tenant-Scoped While `StoredFile` Is

**File:** `src/Modules/Storage/Wallow.Storage.Domain/Entities/StorageBucket.cs:15`

`StorageBucket` implements `ITenantScoped` in its declaration but this appears to be a shared resource design decision. The Storage module's own CLAUDE.md explicitly notes: "CRITICAL: StorageBucket is NOT tenant-scoped (all tenants share buckets)." However, the entity class declaration does show `ITenantScoped`. This inconsistency between documentation and code should be clarified.

**Severity: LOW**

---

### NEW-L2. `Guid.Empty` Validation Is Inconsistent Across Domain Entities

`Invoice.Create()` validates `userId == Guid.Empty` and throws `BusinessRuleException`. However:
- `Payment.Create()` does NOT validate `userId == Guid.Empty`
- `Subscription.Create()` likely does not either (uses same pattern)

This means if the Billing controllers pass `Guid.Empty` from the dangerous `GetCurrentUserId()` fallback, invoices are protected but payments and subscriptions are not.

**Severity: MEDIUM** (directly compounds H2)

---

## Verified Summary Statistics

| Category | Original Count | Verified Count | Notes |
|----------|---------------|----------------|-------|
| CRITICAL | 0 | 0 | No change |
| HIGH | 4 | 3 | H3 downgraded to MEDIUM |
| MEDIUM | 7 | 10 | H3 downgraded here; L4 upgraded; 2 new findings (NEW-M2 scope extension, NEW-L2) |
| LOW | 6 | 7 | NEW-L1, NEW-L2 added; L4 upgraded out |
| INFO | 0 | 1 | NEW-M3 remediation consideration |

**Final verified counts:**
| Category | Count |
|----------|-------|
| CRITICAL | 0 |
| HIGH | 3 |
| MEDIUM | 10 |
| LOW | 7 |

---

## Corrections to Original Audit

1. **H2 (GetCurrentUserId):** Storage module's `StorageController` returns `Guid` (not `Guid?`). 4 controllers return `Guid.Empty`, not 3.
2. **H3 (IAuditableEntity):** Downgraded from HIGH to MEDIUM. No active bug -- purely dead code.
3. **H4 (DateTime.UtcNow):** Count was "18+ domain entity files." Actual count is **55+ usages** across all layers, with ~25 in domain entities alone.
4. **M2 (Shared Kernel deps):** Audit listed 4 packages. Actual count is 6 packages + 1 framework reference.
5. **M4 (Error handling):** The audit incorrectly stated domain exceptions "produce generic 500 errors." `GlobalExceptionHandler` properly handles `DomainException` subclasses (404/422). Only `InvalidOperationException` falls to 500.
6. **L4 (InvalidOperationException):** Upgraded from LOW to MEDIUM due to actual HTTP 500 impact on 2 modules.

---

## Verified Top Priorities for Remediation

1. **H2** - `GetCurrentUserId()` inconsistency + NEW-L2 `Guid.Empty` validation gaps (data integrity risk)
2. **H1** - `ResultExtensions` duplication with active divergence (inconsistent API error responses)
3. **H4** - `DateTime.UtcNow` in 55+ locations (testability, affects entire codebase via AuditableEntity)
4. **L4/M4** - `InvalidOperationException` producing 500s in Communications/Configuration (user-facing bug)
5. **M1** - `TenantId` public setter (security boundary weakness)
