# Audit Fixes Design

**Date:** 2026-03-28
**Source:** `.claude/audit/REPORT.md` (167 findings, 62 high, 56 medium, 49 low)

## Approach

Fix all 27 recommended action items from the audit report across three branches, organized by tier. Cross-cutting patterns are fixed as dedicated atomic passes before module-specific work. Full test suite runs after every commit.

## Branching Strategy

Three branches, three PRs:

1. `fix/audit-quick-wins` — Items 1-10 (critical bugs, security, validation)
2. `fix/audit-medium` — Items 11-20 (design decisions, cross-tenant, async, conventions)
3. `refactor/audit-cleanup` — Items 21-27 (dead code, shared patterns, performance, stubs)

## Branch 1: `fix/audit-quick-wins`

### Commit 1 — Cross-cutting: Fix "Tenant context" error messages
- Find-replace across 6+ modules (~15 endpoints)
- Change "Tenant context is required" to "Authentication is required" where the check is on user ID

### Commit 2 — Cross-cutting: Replace `Console.WriteLine` with structured logging
- `NotificationsModuleExtensions.cs` line 167
- `Wallow.Web/Program.cs` lines 127-136
- Use `[LoggerMessage]` source generator pattern per project rules

### Commit 3 — Cross-cutting: Fix `await Task.CompletedTask` anti-pattern
- `RedisPresenceService.cs` lines 36-37
- `Program.cs` lines 302-306

### Commit 4 — Bug: Branding silent mutation discard
- Add `.AsTracking()` to `ClientBrandingRepository.GetByClientIdAsync`

### Commit 5 — Bug: Inquiries duplicate event publishing
- Remove direct `Publish` call from `AddInquiryCommentHandler` line 38
- Let domain event handler be the single publisher

### Commit 6 — Bug: Inquiries `UpdatedAt` mapping
- Add `UpdatedAt` to `InquiryDto`
- Map in `InquiryMappings.ToDto()`
- Use in `ToInquiryResponse`

### Commit 7 — Validation: Fix schema mismatches
- Billing `FieldKey` validator: 100 to 50 chars, regex lowercase only
- Announcements `ActionUrl`/`ImageUrl` validators: 2000 to 500 chars (Create and Update)

### Commit 8 — Security: Add `[HasPermission]` to Inquiries `UpdateStatus`
- Add permission attribute to prevent unauthorized status changes

### Commit 9 — Security: Fix raw claim access in Inquiries
- Replace `User.Claims.Any(...)` with `User.GetPermissions().Contains(...)`

### Commit 10 — Bug: Fix Identity date format
- Change `"YYYY-MM-DD"` to `"yyyy-MM-dd"` in `IdentitySettingKeys.cs`

### Commit 11 — Bug: Add missing DB index on `HashedKey`
- Add index in `ApiKeyConfiguration`
- Generate migration

## Branch 2: `fix/audit-medium`

### Commit 1 — Cross-cutting: Replace `DateTime.UtcNow`/`DateTimeOffset.UtcNow` with `TimeProvider`
- ApiKeys: 7 locations in `RedisApiKeyService.cs`
- Billing: `MeterDefinition` (2), `QuotaDefinition` (2), `ValkeyMeteringService` (1), `MeteringQueryService` (1), `MeteringMiddleware` (2), `PaymentCreatedDomainEventHandler` (1), `InvoiceCreatedDomainEventHandler` (1)
- Inquiries: `InquiryStatusChangedDomainEventHandler` (1), `InquirySubmittedDomainEventHandler` (1)
- Branding: `ClientBranding.cs` (1)
- Inject `TimeProvider` where not already available

### Commit 2 — Bug: ApiKeys cross-tenant lookup
- Redesign `GetByHashAsync` to search across all tenants
- Remove or replace the `Guid.Empty` sentinel pattern

### Commit 3 — Bug: Storage missing domain events on deletion
- `DeleteFileHandler`: call `file.MarkAsDeleted()` before `Remove()`
- `DeleteBucketHandler`: call `bucket.Delete()` before `Remove()`

### Commit 4 — Bug: Branding upsert ordering
- Reorder to upload logo first then save DB, or add compensating rollback on storage failure

### Commit 5 — Bug: ApiKeys `Revoke()` bypasses domain
- Route revocation through `ApiKey.Revoke()` domain method
- Ensures audit trail via `SetUpdated`

### Commit 6 — Fix sync-over-async in `PushProviderFactory`
- Make `GetProvider` async or restructure the repository call
- Update callers

### Commit 7 — Fix Messaging inactive participant access
- Add `p.IsActive` check in `GetMessagesAsync`

### Commit 8 — Resolve permission duplication
- Consolidate `NotificationsRead` vs `NotificationRead` to one constant
- Update all references

### Commit 9 — Wire `InvoiceCreatedDomainEventHandler` email
- Resolve user email via `IUserQueryService` like sibling handlers

### Commit 10 — Align Inquiries query handlers to module convention
- Convert 4 handlers from `sealed class` with `Handle` to `static class` with `HandleAsync`

## Branch 3: `refactor/audit-cleanup`

### Commit 1 — Dead code: Delete verified dead files
Before deleting, verify each via git history (commit message, author, timing). Files that appear intentionally scaffolded get beads instead.

**High confidence (delete):**
- `SignalRNotificationService.cs` (superseded by SSE)
- `DesignTimeTenantContext.cs` (5 modules, scaffolding artifact)
- Marker interfaces (4 files, never referenced)
- `PerformanceOptions.cs` (never bound to IOptions)

**Investigate first (delete or create bead):**
- `NoOpMfaExemptionChecker.cs`
- `AuthorizeMfaPartialAttribute.cs`
- `EnumMappings.cs`
- `SmsPreference.cs` + `SmsPreferenceId.cs`
- `InvoiceRepositoryExtensions.cs`
- `InvoiceCreatedTrigger.cs`
- `IRateLimitService.cs` + `ValkeyRateLimitService.cs`
- `ExternalServiceException.cs`
- `PluginPermission.cs`

Remove DI registrations for deleted services.

### Commit 2 — Dead code: Delete unused methods/properties
- Identity: `SaveSamlConfigRequest`, `UpdateSamlConfig()`, `ServiceAccountMetadata.UpdateDetails()`
- Billing: `EntityTypeDto`, `IPaymentRepository.GetByIdAsync`, `IInvoiceRepository.Remove`, `RemoveLineItem`, `MarkAsOverdue`, `MeterDefinition.Update`, `CustomFieldDefinition.Activate`
- Notifications: `EmailPreference.Toggle()`, `PushMessage.ResetForRetry()`, `SmsMessage.ResetForRetry()`, `GetUserDevicesQuery.TenantId`
- Branding: `ClearLogo()`, `ClientBrandingUpdatedEvent`
- ApiKeys: `IApiKeyRepository.GetByIdAsync`
- Shared: `RegionSettings`, `EnsureId<TId>()`, `Result.Create(bool, Error)`
- Unused enums: `Timeline`, `BudgetRange`, `ProjectType`, `ChannelType.Webhook`
- Unused telemetry instruments (Notifications, Storage)
- Unused domain events with zero handlers

### Commit 3 — Create beads for forward-looking domain methods
Add `[UsedImplicitly]` annotation and create tracking beads for:
- Billing: `Subscription.Renew`, `Subscription.MarkPastDue`, `Subscription.Expire`, `Payment.Refund`
- Storage: `StoredFile.UpdateMetadata/SetPublic/MarkAsDeleted`, `StorageBucket` update methods
- Messaging: `Participant.Leave`
- Announcements: `Announcement.Expire`, `ChangelogEntry.Update/Unpublish`, `ChangelogItem.Update`
- Plus any "investigate first" items from Commit 1 that turn out to be scaffolded

### Commit 4 — Extract shared patterns
- `SettingUpdateRequest` to shared location
- `CreateAuthenticatedClient()` to `DelegatingHandler` or base class in Wallow.Web
- Duplicate `MapToDto` in Announcements to shared mapper extension
- Duplicate user ID null-check to action filter or shared extension
- Duplicate `appUrl` fallback to shared helper in Notifications
- Duplicate `HashToken()` in Identity to single location

### Commit 5 — Performance fixes
- `GetInquiriesHandler`: push filter to repository
- `EfMessagingQueryService.GetConversationsAsync`: avoid loading all messages
- `PermissionType.All`: replace runtime reflection with static array

### Commit 6 — Complete stubs
- `UploadBrandingLogoAsync` in Identity (wire to Storage module)
- `ResolveTargetUsersAsync` in Announcements
- `BillableAmount`/currency in Billing `UsageReportService`
- `PaymentMethod` in `PaymentCreatedDomainEventHandler`

### Commit 7 — Remaining readability/style fixes
- Narrow broad `catch (Exception)` in `AuditInterceptor` and `AccountController`
- Standardize `ConversationId.Create()` vs `new ConversationId()`
- Collapse nested ifs in Branding controller
- Replace `new List<T>()` with `[]` collection expressions
- `ApiHealthCheck` duplicate across Auth/Web to shared project
- Other minor style items

## Key Decisions

- Cross-cutting patterns fixed as dedicated atomic passes before module-specific work
- Dead code verified via git history before deletion; scaffolded code gets beads not deleted
- Forward-looking domain methods get `[UsedImplicitly]` plus tracking beads
- Full test suite after every commit
- Three PRs for reviewability and safe incremental merge
