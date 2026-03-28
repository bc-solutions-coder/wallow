# Audit Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix all 167 findings from the codebase audit across three branches with cross-cutting passes first, test-after-every-commit discipline, and dead code verification before deletion.

**Architecture:** Three PRs (quick-wins, medium-effort, cleanup) each building on the previous. Cross-cutting fixes are atomic passes touching all affected modules at once. Module-specific fixes follow in severity order.

**Tech Stack:** .NET 10, EF Core, Wolverine, FluentValidation, Playwright (E2E), xUnit

**Test command:** `./scripts/run-tests.sh` (always use this, never bare `dotnet test`)

---

## Branch 1: `fix/audit-quick-wins`

Create branch from current `feature/update-structure`.

```bash
git checkout -b fix/audit-quick-wins
```

---

### Task 1: Fix "Tenant context is required" error messages

**Files (all use `replace_all` — same string in each file):**
- `src/Modules/ApiKeys/Wallow.ApiKeys.Api/Controllers/ApiKeysController.cs` (lines 66, 175, 209)
- `src/Modules/Notifications/Wallow.Notifications.Api/Controllers/NotificationsController.cs` (lines 45, 74, 96, 122)
- `src/Modules/Notifications/Wallow.Notifications.Api/Controllers/UserNotificationSettingsController.cs` (lines 36, 57, 84)
- `src/Modules/Billing/Wallow.Billing.Api/Controllers/SubscriptionsController.cs` (lines 98, 133)
- `src/Modules/Billing/Wallow.Billing.Api/Controllers/InvoicesController.cs` (lines 100, 140, 168, 191)
- `src/Modules/Billing/Wallow.Billing.Api/Controllers/PaymentsController.cs` (line 99)
- `src/Modules/Messaging/Wallow.Messaging.Api/Controllers/ConversationsController.cs` (lines 45, 80, 105, 141, 168, 186)
- `src/Modules/Storage/Wallow.Storage.Api/Controllers/StorageController.cs` (line 153)
- `src/Modules/Announcements/Wallow.Announcements.Api/Controllers/AnnouncementsController.cs` (lines 37, 67)

**Step 1: Replace all occurrences**

In every file above, replace:
```
"Tenant context is required"
```
with:
```
"Authentication is required"
```

This is a safe mechanical replacement — all 25 occurrences check `userId is null`, not tenant ID.

**Step 2: Run tests**
```bash
./scripts/run-tests.sh
```

**Step 3: Commit**
```bash
git add src/Modules/ApiKeys/Wallow.ApiKeys.Api/Controllers/ApiKeysController.cs \
  src/Modules/Notifications/Wallow.Notifications.Api/Controllers/NotificationsController.cs \
  src/Modules/Notifications/Wallow.Notifications.Api/Controllers/UserNotificationSettingsController.cs \
  src/Modules/Billing/Wallow.Billing.Api/Controllers/SubscriptionsController.cs \
  src/Modules/Billing/Wallow.Billing.Api/Controllers/InvoicesController.cs \
  src/Modules/Billing/Wallow.Billing.Api/Controllers/PaymentsController.cs \
  src/Modules/Messaging/Wallow.Messaging.Api/Controllers/ConversationsController.cs \
  src/Modules/Storage/Wallow.Storage.Api/Controllers/StorageController.cs \
  src/Modules/Announcements/Wallow.Announcements.Api/Controllers/AnnouncementsController.cs
git commit -m "fix: correct misleading 'Tenant context' error to 'Authentication is required'"
```

---

### Task 2: Replace `Console.WriteLine` with structured logging

**Files:**
- Modify: `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs:167`
- Modify: `src/Wallow.Web/Program.cs:129,134`

**Step 1: Fix NotificationsModuleExtensions**

Line 167 currently:
```csharp
Console.WriteLine($"Warning: Unrecognized email provider '{provider}'. Defaulting to Smtp.");
```

This is inside a static extension method, so we can't use `[LoggerMessage]` directly. Replace with `ILogger` from the `IServiceCollection` pattern. If a logger isn't accessible in this static context, use `ILoggerFactory` from the service provider or pass a logger parameter.

Check how the method is structured — if it's a registration method called during startup, the simplest fix is to accept `ILogger` or use `LogWarning` on a logger obtained from the service provider.

**Step 2: Fix Wallow.Web/Program.cs**

Lines 129, 134 currently:
```csharp
Console.WriteLine($"OIDC Auth Failed: {context.Exception}");
Console.WriteLine($"OIDC Remote Failure: {context.Failure}");
```

Replace with `ILogger` obtained from the `context.HttpContext.RequestServices`:
```csharp
ILogger logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Wallow.Web.OidcAuth");
logger.LogError(context.Exception, "OIDC authentication failed");
```

And similarly for RemoteFailure.

**Step 3: Run tests**
```bash
./scripts/run-tests.sh
```

**Step 4: Commit**
```bash
git add src/Modules/Notifications/Wallow.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs \
  src/Wallow.Web/Program.cs
git commit -m "fix: replace Console.WriteLine with structured logging"
```

---

### Task 3: Fix `await Task.CompletedTask` anti-pattern

**Files:**
- Modify: `src/Wallow.Api/Services/RedisPresenceService.cs:37`
- Modify: `src/Wallow.Api/Program.cs:302-306`

**Step 1: Fix RedisPresenceService**

Current (line 36-37):
```csharp
batch.Execute();
await Task.CompletedTask;
```

The method is `async` but doesn't need to be. Either:
- Remove `async` keyword and return `Task.CompletedTask`, or
- If `batch.Execute()` has an async variant, use `await batch.ExecuteAsync()`

Check the StackExchange.Redis API — `CommandBatch` likely has `ExecuteAsync()`.

**Step 2: Fix Program.cs**

Current (lines 302-306):
```csharp
options.ConnectionFactory = async _ =>
{
    await Task.CompletedTask;
    return mux;
};
```

Replace with:
```csharp
options.ConnectionFactory = _ => Task.FromResult<IConnectionMultiplexer>(mux);
```

**Step 3: Run tests**
```bash
./scripts/run-tests.sh
```

**Step 4: Commit**
```bash
git add src/Wallow.Api/Services/RedisPresenceService.cs src/Wallow.Api/Program.cs
git commit -m "fix: remove await Task.CompletedTask anti-pattern"
```

---

### Task 4: Fix Branding silent mutation discard

**Files:**
- Modify: `src/Modules/Branding/Wallow.Branding.Infrastructure/Repositories/ClientBrandingRepository.cs:10-14`

**Step 1: Add `.AsTracking()` to query**

Current:
```csharp
public Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
{
    return context.ClientBrandings
        .FirstOrDefaultAsync(b => b.ClientId == clientId, ct);
}
```

Fix:
```csharp
public Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
{
    return context.ClientBrandings
        .AsTracking()
        .FirstOrDefaultAsync(b => b.ClientId == clientId, ct);
}
```

**Step 2: Run tests**
```bash
./scripts/run-tests.sh branding
```
Then full suite:
```bash
./scripts/run-tests.sh
```

**Step 3: Commit**
```bash
git add src/Modules/Branding/Wallow.Branding.Infrastructure/Repositories/ClientBrandingRepository.cs
git commit -m "fix(branding): add AsTracking to prevent silent mutation discard"
```

---

### Task 5: Fix Inquiries duplicate event publishing

**Files:**
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/AddInquiryComment/AddInquiryCommentHandler.cs:38`
- Reference: `src/Modules/Inquiries/Wallow.Inquiries.Application/EventHandlers/InquiryCommentAddedDomainEventHandler.cs:29`

**Step 1: Remove duplicate publish from command handler**

The command handler at line 38 publishes `InquiryCommentAddedEvent` directly. The domain event handler at line 29 ALSO publishes the same integration event when it handles the domain event. Remove the direct publish from the command handler — let the domain event handler be the single publisher.

Find and remove the `bus.PublishAsync(new InquiryCommentAddedEvent {...})` block from `AddInquiryCommentHandler`. Keep the domain event raising (which triggers the domain event handler).

**Step 2: Run tests**
```bash
./scripts/run-tests.sh inquiries
```
Then full suite:
```bash
./scripts/run-tests.sh
```

**Step 3: Commit**
```bash
git add src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/AddInquiryComment/AddInquiryCommentHandler.cs
git commit -m "fix(inquiries): remove duplicate integration event publishing from AddInquiryComment"
```

---

### Task 6: Fix Inquiries `UpdatedAt` mapping

**Files:**
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Application/DTOs/InquiryDto.cs`
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Application/Mappings/InquiryMappings.cs`
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs:260-261`

**Step 1: Add `UpdatedAt` to InquiryDto**

Add `DateTimeOffset? UpdatedAt` property to the `InquiryDto` record.

**Step 2: Map `UpdatedAt` in InquiryMappings.ToDto()**

In `ToDto()`, map `inquiry.UpdatedAt` to the new DTO property.

**Step 3: Fix `ToInquiryResponse` mapping**

Lines 260-261 currently map both `CreatedAt` and `UpdatedAt` to `dto.CreatedAt.UtcDateTime`. Fix `UpdatedAt` to use `dto.UpdatedAt?.UtcDateTime ?? dto.CreatedAt.UtcDateTime`.

**Step 4: Run tests**
```bash
./scripts/run-tests.sh inquiries
```

**Step 5: Commit**
```bash
git add src/Modules/Inquiries/Wallow.Inquiries.Application/DTOs/InquiryDto.cs \
  src/Modules/Inquiries/Wallow.Inquiries.Application/Mappings/InquiryMappings.cs \
  src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs
git commit -m "fix(inquiries): map UpdatedAt correctly instead of duplicating CreatedAt"
```

---

### Task 7: Fix validation/schema mismatches

**Files:**
- Modify: `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/CreateCustomFieldDefinition/CreateCustomFieldDefinitionValidator.cs:13-17`
- Modify: `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/CreateAnnouncement/CreateAnnouncementValidator.cs:26,34`
- Modify: `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/UpdateAnnouncement/UpdateAnnouncementValidator.cs:29,37`

**Step 1: Fix Billing FieldKey validator**

Current:
```csharp
RuleFor(x => x.FieldKey)
    .NotEmpty().WithMessage("Field key is required")
    .MaximumLength(100).WithMessage("Field key must not exceed 100 characters")
    .Matches("^[a-zA-Z0-9_]+$")
    .WithMessage("Field key must contain only alphanumeric characters and underscores");
```

Fix:
```csharp
RuleFor(x => x.FieldKey)
    .NotEmpty().WithMessage("Field key is required")
    .MaximumLength(50).WithMessage("Field key must not exceed 50 characters")
    .Matches("^[a-z][a-z0-9_]*$")
    .WithMessage("Field key must start with a lowercase letter and contain only lowercase alphanumeric characters and underscores");
```

**Step 2: Fix Announcements validators (both Create and Update)**

In both validators, change `ActionUrl` and `ImageUrl` max length:
```
.MaximumLength(2000).WithMessage("Action URL must not exceed 2000 characters")
```
to:
```
.MaximumLength(500).WithMessage("Action URL must not exceed 500 characters")
```

Same for `ImageUrl` in both files.

**Step 3: Run tests**
```bash
./scripts/run-tests.sh billing && ./scripts/run-tests.sh announcements
```

**Step 4: Commit**
```bash
git add src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/CreateCustomFieldDefinition/CreateCustomFieldDefinitionValidator.cs \
  src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/CreateAnnouncement/CreateAnnouncementValidator.cs \
  src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/UpdateAnnouncement/UpdateAnnouncementValidator.cs
git commit -m "fix: align validator constraints with database schema limits"
```

---

### Task 8: Add `[HasPermission]` to Inquiries `UpdateStatus`

**Files:**
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs:141`

**Step 1: Add permission attribute**

Current (no permission attribute):
```csharp
[HttpPatch("{id:guid}/status")]
[ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
```

Add `[HasPermission(PermissionType.InquiriesWrite)]` before the method:
```csharp
[HasPermission(PermissionType.InquiriesWrite)]
[HttpPatch("{id:guid}/status")]
[ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
```

Verify `PermissionType.InquiriesWrite` exists in `PermissionType.cs`. If not, check what the correct permission constant is for inquiries write operations by looking at other endpoints in the same controller.

**Step 2: Run tests**
```bash
./scripts/run-tests.sh inquiries
```

**Step 3: Commit**
```bash
git add src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs
git commit -m "fix(inquiries): add missing HasPermission to UpdateStatus endpoint"
```

---

### Task 9: Fix raw claim access in Inquiries

**Files:**
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs:125-126,200-201`

**Step 1: Replace raw claim access**

Two locations. Current pattern:
```csharp
bool hasReadPermission = User.Claims
    .Any(c => c.Type == "permission" && c.Value == PermissionType.InquiriesRead);
```

Replace with (using `ClaimsPrincipalExtensions` from `Wallow.Shared.Kernel.Extensions`):
```csharp
bool hasReadPermission = User.GetPermissions().Contains(PermissionType.InquiriesRead);
```

Ensure `using Wallow.Shared.Kernel.Extensions;` is present.

**Step 2: Run tests**
```bash
./scripts/run-tests.sh inquiries
```

**Step 3: Commit**
```bash
git add src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs
git commit -m "fix(inquiries): use ClaimsPrincipalExtensions instead of raw claim access"
```

---

### Task 10: Fix Identity date format

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Application/Settings/IdentitySettingKeys.cs:21`

**Step 1: Fix date format string**

Current:
```csharp
DefaultValue: "YYYY-MM-DD",
```

Fix:
```csharp
DefaultValue: "yyyy-MM-dd",
```

**Step 2: Run tests**
```bash
./scripts/run-tests.sh identity
```

**Step 3: Commit**
```bash
git add src/Modules/Identity/Wallow.Identity.Application/Settings/IdentitySettingKeys.cs
git commit -m "fix(identity): use .NET date format yyyy-MM-dd instead of JavaScript YYYY-MM-DD"
```

---

### Task 11: Add missing DB index on `HashedKey`

**Files:**
- Modify: `src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Persistence/Configurations/ApiKeyConfiguration.cs:61-62`

**Step 1: Add index**

After the existing indexes (lines 61-62):
```csharp
builder.HasIndex(e => e.TenantId);
builder.HasIndex(e => e.ServiceAccountId);
```

Add:
```csharp
builder.HasIndex(e => e.HashedKey);
```

**Step 2: Generate migration**

```bash
dotnet ef migrations add AddApiKeyHashedKeyIndex \
    --project src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure \
    --startup-project src/Wallow.Api \
    --context ApiKeysDbContext
```

**Step 3: Review the generated migration to ensure it only adds the index.**

**Step 4: Run tests**
```bash
./scripts/run-tests.sh apikeys
```

**Step 5: Commit**
```bash
git add src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/
git commit -m "feat(apikeys): add database index on HashedKey for cache-miss lookup performance"
```

---

## Branch 2: `fix/audit-medium`

Create branch from `fix/audit-quick-wins` after merging or from latest commit.

```bash
git checkout -b fix/audit-medium
```

---

### Task 12: Replace `DateTime.UtcNow`/`DateTimeOffset.UtcNow` with `TimeProvider`

**Files (grouped by module):**

**ApiKeys** — `TimeProvider` already injected at line 20:
- `src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Services/RedisApiKeyService.cs` (7 locations: lines 75, 82, 164, 191, 313, 340, 344)

Replace all `DateTimeOffset.UtcNow` with `timeProvider.GetUtcNow()`.

**Billing Infrastructure** — need to inject `TimeProvider`:
- `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/ValkeyMeteringService.cs:175`
- `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/MeteringQueryService.cs:49`

Add `TimeProvider timeProvider` to constructor. Replace `DateTime.UtcNow` with `timeProvider.GetUtcNow().UtcDateTime`.

**Billing Domain** — domain entities need `TimeProvider` parameter on factory methods:
- `src/Modules/Billing/Wallow.Billing.Domain/Metering/Entities/MeterDefinition.cs:62,132`
- `src/Modules/Billing/Wallow.Billing.Domain/Metering/Entities/QuotaDefinition.cs:64,156`

Add `TimeProvider timeProvider` parameter to `Create()` and `Update()` methods. Replace `DateTimeOffset.UtcNow` with `timeProvider.GetUtcNow()`. Update all callers.

**Billing Event Handlers** — static Wolverine handlers, need `TimeProvider` as parameter:
- `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/PaymentCreatedDomainEventHandler.cs:33`
- `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/InvoiceCreatedDomainEventHandler.cs:38`

Wolverine injects parameters automatically. Add `TimeProvider timeProvider` parameter. Replace `DateTime.UtcNow` with `timeProvider.GetUtcNow().UtcDateTime`.

**Inquiries Event Handlers** — same static Wolverine pattern:
- `src/Modules/Inquiries/Wallow.Inquiries.Application/EventHandlers/InquiryStatusChangedDomainEventHandler.cs:25`
- `src/Modules/Inquiries/Wallow.Inquiries.Application/EventHandlers/InquirySubmittedDomainEventHandler.cs:34`

Add `TimeProvider timeProvider` parameter. Replace `DateTime.UtcNow` with `timeProvider.GetUtcNow().UtcDateTime`.

**Branding Domain** — entity factory method needs `TimeProvider`:
- `src/Modules/Branding/Wallow.Branding.Domain/Entities/ClientBranding.cs:35,79,85`

Add `TimeProvider` parameter to `Create()` and `Update()` methods. Update all callers.

**Run tests after ALL replacements:**
```bash
./scripts/run-tests.sh
```

**Commit:**
```bash
git add -u
git commit -m "fix: replace DateTime.UtcNow with TimeProvider across all modules"
```

---

### Task 13: Fix ApiKeys cross-tenant lookup

**Files:**
- Modify: `src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Services/RedisApiKeyService.cs:151`
- Modify: `src/Modules/ApiKeys/Wallow.ApiKeys.Application/Interfaces/IApiKeyRepository.cs` (add new method)
- Modify: `src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Repositories/ApiKeyRepository.cs` (implement new method)

**Step 1: Add `GetByHashAsync(string hash, CancellationToken)` overload to repository**

Add a new method that searches across all tenants (no tenantId filter):
```csharp
Task<ApiKey?> GetByHashAsync(string hashedKey, CancellationToken ct = default);
```

Implementation queries without the `TenantId` filter.

**Step 2: Update `RedisApiKeyService.cs:151`**

Replace:
```csharp
ApiKey? domainKey = await apiKeyRepository.GetByHashAsync(keyHash, Guid.Empty, ct);
```
With:
```csharp
ApiKey? domainKey = await apiKeyRepository.GetByHashAsync(keyHash, ct);
```

**Step 3: Run tests**
```bash
./scripts/run-tests.sh apikeys
```

**Commit:**
```bash
git add src/Modules/ApiKeys/
git commit -m "fix(apikeys): fix cross-tenant API key lookup to search all tenants"
```

---

### Task 14: Fix Storage missing domain events on deletion

**Files:**
- Modify: `src/Modules/Storage/Wallow.Storage.Application/Commands/DeleteFile/DeleteFileHandler.cs:25-28`
- Modify: `src/Modules/Storage/Wallow.Storage.Application/Commands/DeleteBucket/DeleteBucketHandler.cs:40-42`

**Step 1: Fix DeleteFileHandler**

Before `fileRepository.Remove(file)`, call `file.MarkAsDeleted()` to raise the `FileDeletedEvent` domain event:
```csharp
file.MarkAsDeleted();
await storageProvider.DeleteAsync(file.StorageKey, cancellationToken);
fileRepository.Remove(file);
await fileRepository.SaveChangesAsync(cancellationToken);
```

**Step 2: Fix DeleteBucketHandler**

Before removing files and bucket, call domain methods:
```csharp
bucket.Delete(); // raises BucketDeletedEvent
foreach (var file in files)
{
    file.MarkAsDeleted(); // raises FileDeletedEvent per file
    await storageProvider.DeleteAsync(file.StorageKey, cancellationToken);
    fileRepository.Remove(file);
}
bucketRepository.Remove(bucket);
```

**Step 3: Run tests**
```bash
./scripts/run-tests.sh storage
```

**Commit:**
```bash
git add src/Modules/Storage/Wallow.Storage.Application/Commands/
git commit -m "fix(storage): raise domain events before deletion in delete handlers"
```

---

### Task 15: Fix Branding upsert ordering

**Files:**
- Modify: `src/Modules/Branding/Wallow.Branding.Api/Controllers/ClientBrandingController.cs:127-133`

**Step 1: Reorder to upload-then-save**

Current order: save DB → upload storage. Reverse to: upload storage → save DB.

Move the storage upload block BEFORE `repository.SaveChangesAsync()`:
```csharp
// Upload logo first (if upload fails, DB is unchanged)
if (logo is not null && logoStorageKey is not null)
{
    await using Stream stream = logo.OpenReadStream();
    await storageProvider.UploadAsync(stream, logoStorageKey, logo.ContentType, ct);
}

// Save DB after successful upload
await repository.SaveChangesAsync(ct);
```

**Step 2: Run tests**
```bash
./scripts/run-tests.sh branding
```

**Commit:**
```bash
git add src/Modules/Branding/Wallow.Branding.Api/Controllers/ClientBrandingController.cs
git commit -m "fix(branding): upload logo before saving DB to prevent orphaned references"
```

---

### Task 16: Wire ApiKeys `Revoke()` through domain

**Files:**
- Modify: `src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Repositories/ApiKeyRepository.cs:35-41`

**Step 1: Replace `ExecuteUpdateAsync` with domain method**

Current:
```csharp
public async Task RevokeAsync(ApiKeyId id, Guid tenantId, CancellationToken ct)
{
    TenantId tid = new(tenantId);
    await context.ApiKeys
        .Where(x => x.Id == id && x.TenantId == tid)
        .ExecuteUpdateAsync(s => s.SetProperty(k => k.IsRevoked, true), ct);
}
```

Replace with loading the aggregate and invoking the domain method:
```csharp
public async Task RevokeAsync(ApiKeyId id, Guid tenantId, CancellationToken ct)
{
    TenantId tid = new(tenantId);
    ApiKey? key = await context.ApiKeys
        .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tid, ct);

    if (key is null) return;

    key.Revoke(tenantId, timeProvider);
    await context.SaveChangesAsync(ct);
}
```

Check the `ApiKey.Revoke()` signature — it takes `(Guid revokedBy, TimeProvider timeProvider)`. Ensure `TimeProvider` is injected into the repository. Also check if `SaveChangesAsync` is needed or if the caller handles it.

**Step 2: Run tests**
```bash
./scripts/run-tests.sh apikeys
```

**Commit:**
```bash
git add src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Repositories/ApiKeyRepository.cs
git commit -m "fix(apikeys): route revocation through domain method for audit trail"
```

---

### Task 17: Fix sync-over-async in PushProviderFactory

**Files:**
- Modify: `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/PushProviderFactory.cs:14-18`

**Step 1: Make method async**

Current:
```csharp
public IPushProvider GetProvider(PushPlatform platform)
{
    var config = configurationRepository.GetByPlatformAsync(platform).GetAwaiter().GetResult();
```

Change to:
```csharp
public async Task<IPushProvider> GetProviderAsync(PushPlatform platform)
{
    var config = await configurationRepository.GetByPlatformAsync(platform);
```

**Step 2: Update interface if one exists**

Check for `IPushProviderFactory` interface and update the method signature.

**Step 3: Update all callers**

Search for calls to `GetProvider(` and update to `await GetProviderAsync(`.

**Step 4: Run tests**
```bash
./scripts/run-tests.sh notifications
```

**Commit:**
```bash
git add src/Modules/Notifications/
git commit -m "fix(notifications): make PushProviderFactory async to prevent potential deadlock"
```

---

### Task 18: Fix Messaging inactive participant access

**Files:**
- Modify: `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Services/EfMessagingQueryService.cs:54-57`

**Step 1: Add `IsActive` check**

Current query (lines 54-57):
```csharp
.Where(m => _dbContext.Participants
    .Any(p => p.ConversationId == m.ConversationId
           && p.UserId == userId))
```

Fix:
```csharp
.Where(m => _dbContext.Participants
    .Any(p => p.ConversationId == m.ConversationId
           && p.UserId == userId
           && p.IsActive))
```

**Step 2: Run tests**
```bash
./scripts/run-tests.sh messaging
```

**Commit:**
```bash
git add src/Modules/Messaging/Wallow.Messaging.Infrastructure/Services/EfMessagingQueryService.cs
git commit -m "fix(messaging): restrict message access to active participants only"
```

---

### Task 19: Resolve permission duplication

**Files:**
- Modify: `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs:46,66`

**Step 1: Determine canonical name**

Check which constant is actually used across the codebase:
- Grep for `NotificationsRead` (plural)
- Grep for `NotificationRead` (singular)

Keep whichever has more references. Remove the other.

**Step 2: Update all references to use the surviving constant**

**Step 3: Run tests**
```bash
./scripts/run-tests.sh
```

**Commit:**
```bash
git add -u
git commit -m "fix: consolidate duplicate notification read permission constant"
```

---

### Task 20: Wire `InvoiceCreatedDomainEventHandler` email

**Files:**
- Modify: `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/InvoiceCreatedDomainEventHandler.cs:34`

**Step 1: Add `IUserQueryService` parameter and resolve email**

Current (static Wolverine handler):
```csharp
UserEmail = string.Empty,
```

Add `IUserQueryService userQueryService` parameter to `HandleAsync` (Wolverine auto-injects). Resolve email like sibling handlers:
```csharp
string userEmail = await userQueryService.GetUserEmailAsync(domainEvent.UserId, cancellationToken);
```

Then use it:
```csharp
UserEmail = userEmail,
```

Check how `PaymentCreatedDomainEventHandler` does it — mirror that pattern exactly.

**Step 2: Run tests**
```bash
./scripts/run-tests.sh billing
```

**Commit:**
```bash
git add src/Modules/Billing/Wallow.Billing.Application/EventHandlers/InvoiceCreatedDomainEventHandler.cs
git commit -m "fix(billing): resolve user email in InvoiceCreatedDomainEventHandler"
```

---

### Task 21: Align Inquiries query handlers to module convention

**Files:**
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiries/GetInquiriesHandler.cs`
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiryById/GetInquiryByIdHandler.cs`
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetSubmittedInquiries/GetSubmittedInquiriesHandler.cs`
- Modify: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiryComments/GetInquiryCommentsHandler.cs`

**IMPORTANT NOTE:** The research revealed these are `sealed class` with primary constructors injecting repositories — this is the correct pattern for Wolverine handlers that need DI. The event handlers are `static class` (no DI needed). These are BOTH valid Wolverine patterns.

**Before changing, verify** what the actual module convention is by checking other modules (Identity, Billing, etc.) query handlers. If they use `sealed class` with primary constructors too, then this task is a no-op — the audit finding may be incorrect.

If the convention truly is `static class` with `HandleAsync`, the handlers would need the repository passed as a Wolverine-injected parameter instead of constructor injection.

**Run tests after any changes:**
```bash
./scripts/run-tests.sh inquiries
```

**Commit (if changes made):**
```bash
git add src/Modules/Inquiries/
git commit -m "refactor(inquiries): align query handlers to module conventions"
```

---

## Branch 3: `refactor/audit-cleanup`

Create branch from `fix/audit-medium`.

```bash
git checkout -b refactor/audit-cleanup
```

---

### Task 22: Delete verified dead files

**Step 1: Investigate "needs investigation" files**

For each file below, check git log and determine: delete or create bead.

All came from initial framework commits (`v0.1.0` or initial module implementations). Decision framework:
- If it's part of a planned feature channel (SMS, rate limiting, plugins) → **create bead**
- If it's a superseded implementation or scaffolding artifact → **delete**

**Recommended decisions based on git history:**

| File | Decision | Reasoning |
|------|----------|-----------|
| `NoOpMfaExemptionChecker.cs` | Bead | Extension point for forks |
| `AuthorizeMfaPartialAttribute.cs` | Bead | Part of MFA overhaul, may be needed |
| `EnumMappings.cs` | Delete | v0.1.0 artifact, never used |
| `SmsPreference.cs` + `SmsPreferenceId.cs` | Bead | Part of SMS channel feature |
| `InvoiceRepositoryExtensions.cs` | Delete | v0.1.0 artifact, never used |
| `InvoiceCreatedTrigger.cs` | Delete | Stub placeholder, never wired |
| `IRateLimitService.cs` + `ValkeyRateLimitService.cs` | Bead | Registered in DI, planned feature |
| `ExternalServiceException.cs` | Bead | Shared exception type for adoption |
| `PluginPermission.cs` | Bead | Plugin system scaffolding |

**Step 2: Delete high-confidence files**

Delete these files (confirmed zero references outside tests/docs):
- `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SignalRNotificationService.cs`
- `tests/Modules/Notifications/Wallow.Notifications.Tests/Infrastructure/Services/SignalRNotificationServiceTests.cs`
- All 5 `DesignTimeTenantContext.cs` files:
  - `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/DesignTimeTenantContext.cs`
  - `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/DesignTimeTenantContext.cs`
  - `src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Persistence/DesignTimeTenantContext.cs`
  - `src/Modules/Branding/Wallow.Branding.Infrastructure/Persistence/DesignTimeTenantContext.cs`
  - `src/Modules/Storage/Wallow.Storage.Infrastructure/Persistence/DesignTimeTenantContext.cs`
- 4 marker interfaces:
  - `src/Modules/Announcements/Wallow.Announcements.Application/IAnnouncementsApplicationMarker.cs`
  - `src/Modules/Announcements/Wallow.Announcements.Domain/IAnnouncementsDomainMarker.cs`
  - `src/Modules/Messaging/Wallow.Messaging.Application/IMessagingApplicationMarker.cs`
  - `src/Modules/Messaging/Wallow.Messaging.Domain/IMessagingDomainMarker.cs`
- "Confirmed delete" from investigation:
  - `src/Modules/Identity/Wallow.Identity.Api/Mappings/EnumMappings.cs`
  - `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/InvoiceRepositoryExtensions.cs`
  - `src/Modules/Billing/Wallow.Billing.Infrastructure/Workflows/InvoiceCreatedTrigger.cs`

Also delete any test files that only test deleted code (e.g., `DesignTimeTenantContextTests.cs`, `StorageDbContextFactoryTests.cs` if it only tests the deleted class).

**NOTE:** Do NOT delete `PerformanceOptions.cs` — it's used in `Program.cs`.

**Step 3: Remove DI registrations for deleted services**

Search for any `services.AddScoped<SignalRNotificationService>` or similar registrations and remove them.

**Step 4: Run tests**
```bash
./scripts/run-tests.sh
```

**Step 5: Commit**
```bash
git add -u
git commit -m "refactor: remove verified dead code (SignalR, DesignTime, markers, stale files)"
```

---

### Task 23: Delete unused methods/properties/enums/events

**Step 1: Delete unused methods and properties**

Work through the list from the audit report. For each deletion:
1. Remove the method/property
2. Remove any interface declarations for that method
3. Check for test methods that only test the deleted code — remove those too

**Identity:**
- `SaveSamlConfigRequest` in `Application/DTOs/SsoConfigurationDto.cs:39-53`
- `UpdateSamlConfig()` in `Domain/Entities/SsoConfiguration.cs:191-241`
- `UpdateDetails()` in `Domain/Entities/ServiceAccountMetadata.cs:148-167`

**Billing:**
- `EntityTypeDto` in `Application/CustomFields/DTOs/CustomFieldDefinitionDto.cs:22`
- `GetByUserIdAsync` in `Application/Interfaces/IPaymentRepository.cs:10` + implementation
- `Remove(Invoice)` in `Application/Interfaces/IInvoiceRepository.cs:16` + implementation
- `RemoveLineItem` in `Domain/Entities/Invoice.cs:113`
- `MarkAsOverdue` in `Domain/Entities/Invoice.cs:161`
- `MeterDefinition.Update` in `Domain/Metering/Entities/MeterDefinition.cs:105`
- `CustomFieldDefinition.Activate` in `Domain/CustomFields/Entities/CustomFieldDefinition.cs:180`

**Notifications:**
- `Toggle()` in `Domain/Channels/Email/Entities/EmailPreference.cs`
- `ResetForRetry()` in `Domain/Channels/Push/Entities/PushMessage.cs`
- `ResetForRetry()` in `Domain/Channels/Sms/Entities/SmsMessage.cs`
- `TenantId` property in `Application/Channels/Push/Queries/GetUserDevices/GetUserDevicesQuery.cs`

**Branding:**
- `ClearLogo()` in `Domain/Entities/ClientBranding.cs`
- `ClientBrandingUpdatedEvent` in `Shared/Wallow.Shared.Contracts/Branding/Events/ClientBrandingUpdatedEvent.cs`

**ApiKeys:**
- `GetByIdAsync` in `Application/Interfaces/IApiKeyRepository.cs:12` + implementation

**Shared:**
- `RegionSettings` in `Kernel/MultiTenancy/RegionConfiguration.cs`
- `EnsureId<TId>()` in `Kernel/Identity/StronglyTypedIdConverter.cs`
- `Result.Create(bool, Error)` in `Kernel/Results/Result.cs`

**Step 2: Delete unused enums**

- `Timeline.cs` in `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/`
- `BudgetRange.cs` in `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/`
- `ProjectType.cs` in `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/`
- `Webhook = 4` entry from `ChannelType.cs` in Notifications (just the enum member, not the whole file)

**Step 3: Delete unused telemetry**

Remove unused instruments from:
- `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Telemetry/NotificationsModuleTelemetry.cs`
- `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Telemetry/EmailModuleTelemetry.cs`
- `src/Modules/Storage/Wallow.Storage.Application/Telemetry/StorageModuleTelemetry.cs`

Only remove instruments with zero references (listed in audit). Keep any that are actually used.

**Step 4: Delete unused domain events**

Remove event classes that are raised but have zero handlers:
- 8 Notification domain events (see audit section 2.6)
- `ConversationCreatedIntegrationEvent` in Messaging

Also remove the `AddDomainEvent(new ...)` calls that raise these events from the entity methods — or leave the entity methods intact and just remove the event class files. Decide based on whether the entity methods have other side effects.

**Step 5: Run tests**
```bash
./scripts/run-tests.sh
```

**Step 6: Commit**
```bash
git add -u
git commit -m "refactor: remove unused methods, enums, telemetry, and domain events"
```

---

### Task 24: Create beads for forward-looking and scaffolded code

**Step 1: Create beads for forward-looking domain methods**

Use `bd create` for each. Add `[UsedImplicitly]` annotation to each method to suppress dead-code warnings.

```bash
# Billing domain methods
bd create --title="Implement Subscription.Renew workflow" --description="Domain method exists but has no callers. Wire up renewal flow." --type=feature --priority=4
bd create --title="Implement Subscription.MarkPastDue workflow" --description="Domain method exists but has no callers. Wire up past-due detection." --type=feature --priority=4
bd create --title="Implement Subscription.Expire workflow" --description="Domain method exists but has no callers. Wire up expiration handling." --type=feature --priority=4
bd create --title="Implement Payment.Refund workflow" --description="Domain method exists but has no callers. Wire up refund processing." --type=feature --priority=4

# Storage domain methods
bd create --title="Wire up StoredFile mutation methods" --description="UpdateMetadata, SetPublic, MarkAsDeleted exist but have no API callers." --type=feature --priority=4
bd create --title="Wire up StorageBucket management methods" --description="UpdateDescription, UpdateAccess, UpdateMaxFileSize, UpdateAllowedContentTypes, UpdateRetention, UpdateVersioning, Delete exist but have no API callers." --type=feature --priority=4

# Messaging
bd create --title="Implement Participant.Leave workflow" --description="Domain method exists but no API endpoint triggers it." --type=feature --priority=4

# Announcements
bd create --title="Wire up Announcement lifecycle methods" --description="Announcement.Expire, ChangelogEntry.Update/Unpublish, ChangelogItem.Update exist but have no callers." --type=feature --priority=4
```

**Step 2: Create beads for scaffolded code kept from investigation**

```bash
bd create --title="Implement NoOpMfaExemptionChecker extension point" --description="MFA exemption checker exists as fork extension point but is never registered." --type=feature --priority=4
bd create --title="Implement AuthorizeMfaPartialAttribute" --description="Attribute exists from MFA overhaul but has zero usage." --type=feature --priority=4
bd create --title="Implement SMS preferences and channel" --description="SmsPreference entity exists with full domain model but no repository, handler, or API." --type=feature --priority=4
bd create --title="Wire up Inquiries rate limiting" --description="IRateLimitService + ValkeyRateLimitService registered in DI but never injected." --type=feature --priority=4
bd create --title="Adopt ExternalServiceException in service integrations" --description="Shared exception type exists but has zero catch/throw sites." --type=feature --priority=4
bd create --title="Wire up PluginPermission constants" --description="Plugin permission constants defined but unused. Part of plugin system scaffolding." --type=feature --priority=4
```

**Step 3: Add `[UsedImplicitly]` annotations**

For each forward-looking domain method, add `[UsedImplicitly]` from `JetBrains.Annotations` (or the project's local shim at `Wallow.Shared.Contracts/Annotations/UsedImplicitlyAttribute.cs`).

**Step 4: Run tests**
```bash
./scripts/run-tests.sh
```

**Step 5: Commit**
```bash
git add -u
git commit -m "refactor: annotate forward-looking code and create tracking beads"
```

---

### Task 25: Extract shared patterns

**Step 1: Extract `SettingUpdateRequest`**

Create `src/Shared/Wallow.Shared.Contracts/Settings/SettingUpdateRequest.cs`:
```csharp
namespace Wallow.Shared.Contracts.Settings;

public sealed record SettingUpdateRequest(string Key, string Value);
```

Remove the duplicate definitions from:
- `src/Modules/Billing/Wallow.Billing.Api/Controllers/BillingSettingsController.cs:185`
- `src/Modules/Identity/Wallow.Identity.Api/Controllers/IdentitySettingsController.cs:185`
- `src/Modules/Storage/Wallow.Storage.Api/Controllers/StorageSettingsController.cs:185`

Update `using` statements in each controller to reference the shared type.

**Step 2: Extract `CreateAuthenticatedClient()`**

Create a `BearerTokenDelegatingHandler` in `src/Wallow.Web/Infrastructure/`:
```csharp
public class BearerTokenDelegatingHandler(ITokenProvider tokenProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(tokenProvider.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenProvider.AccessToken);
        }
        return base.SendAsync(request, ct);
    }
}
```

Register it in DI for the `"WallowApi"` named client. Remove `CreateAuthenticatedClient()` from:
- `src/Wallow.Web/Services/AppRegistrationService.cs:115-124`
- `src/Wallow.Web/Services/OrganizationApiService.cs:68-77`
- `src/Wallow.Web/Services/MfaApiClient.cs:73-82`
- `src/Wallow.Web/Services/InquiryService.cs:20-29`

Replace with `httpClientFactory.CreateClient("WallowApi")` (token is now set by the handler).

**Step 3: Extract `HashToken()` in Identity**

Create `src/Modules/Identity/Wallow.Identity.Domain/ValueObjects/TokenHash.cs` (Domain layer so ScimConfiguration can use it):
```csharp
public static class TokenHash
{
    public static string Compute(string token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(token);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
```

Replace the 4 duplicate `HashToken()` methods:
- `Domain/Entities/ScimConfiguration.cs`
- `Infrastructure/Authorization/ScimBearerAuthenticationHandler.cs`
- `Infrastructure/Authorization/ScimAuthenticationMiddleware.cs`
- `Infrastructure/Services/ScimService.cs`

**Step 4: Extract Announcements `MapToDto`**

Create `src/Modules/Announcements/Wallow.Announcements.Application/Mappings/AnnouncementMappings.cs` and `ChangelogMappings.cs`. Extract the duplicated mapping logic from the 7 files identified.

**Step 5: Run tests**
```bash
./scripts/run-tests.sh
```

**Commit:**
```bash
git add -u
git commit -m "refactor: extract shared patterns (SettingUpdateRequest, auth client, HashToken, mappings)"
```

---

### Task 26: Performance fixes

**Step 1: Push filter to repository in GetInquiriesHandler**

Current (loads all, filters in memory):
```csharp
IReadOnlyList<Inquiry> inquiries = await inquiryRepository.GetAllAsync(cancellationToken);
IEnumerable<Inquiry> filtered = query.Status is not null
    ? inquiries.Where(i => i.Status == query.Status.Value)
    : inquiries;
```

Add `GetByStatusAsync(InquiryStatus? status, CancellationToken)` to `IInquiryRepository`. Implement with EF Core `Where` clause. Update handler to call it.

**Step 2: Fix EfMessagingQueryService.GetConversationsAsync**

Current loads all messages per conversation via `.Include(c => c.Messages)`. Replace with a subquery that only fetches the last message per conversation:

Remove `.Include(c => c.Messages)`. After loading conversations, load last messages separately:
```csharp
var lastMessages = await _dbContext.Messages
    .Where(m => conversationIds.Contains(m.ConversationId))
    .GroupBy(m => m.ConversationId)
    .Select(g => g.OrderByDescending(m => m.SentAt).First())
    .ToDictionaryAsync(m => m.ConversationId, cancellationToken);
```

**Step 3: Replace reflection in PermissionType.All**

Current:
```csharp
public static IReadOnlyList<string> All { get; } = typeof(PermissionType)
    .GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.IsLiteral && f.FieldType == typeof(string))
    .Select(f => (string)f.GetRawConstantValue()!)
    .ToArray();
```

Replace with explicit array listing all constants. This is fragile to maintain but avoids runtime reflection. Alternative: keep reflection but cache the result (it's already a static property so it's computed once).

**Decision:** Keep the reflection — it runs once at startup and auto-discovers new permissions. Add a comment explaining why.

**Step 4: Run tests**
```bash
./scripts/run-tests.sh
```

**Commit:**
```bash
git add -u
git commit -m "perf: push inquiry filtering to DB and optimize message loading"
```

---

### Task 27: Complete stubs

**Step 1: Fix PaymentCreatedDomainEventHandler**

File: `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/PaymentCreatedDomainEventHandler.cs`

- `PaymentMethod = string.Empty` → Check if `PaymentCreatedDomainEvent` carries payment method info. If so, map it. If not, add it to the domain event from `Payment.Create()`.
- `PaidAt = DateTime.UtcNow` → Use `domainEvent.PaidAt` if available, or `timeProvider.GetUtcNow()` (already fixed in Task 12).

**Step 2: Document intentional stubs**

For these stubs that require cross-module integration not yet built:
- `UploadBrandingLogoAsync` — needs Storage module integration events
- `ResolveTargetUsersAsync` — needs Identity module user query integration
- `UsageReportService.BillableAmount` — needs pricing service integration

Add clear `// TODO(bead-xxx):` comments referencing the beads created in Task 24, so future developers know these are tracked.

Create beads if not already covered:
```bash
bd create --title="Integrate Identity branding logo upload with Storage module" --description="UploadBrandingLogoAsync returns placeholder path. Wire to Storage module via integration events." --type=feature --priority=3
bd create --title="Implement announcement user targeting resolution" --description="ResolveTargetUsersAsync returns empty list. Implement query logic to resolve users by plan/tenant/role." --type=feature --priority=3
bd create --title="Integrate billing usage reports with pricing service" --description="BillableAmount always 0m, currency always USD. Integrate with pricing service for real calculations." --type=feature --priority=3
```

**Step 3: Run tests**
```bash
./scripts/run-tests.sh
```

**Commit:**
```bash
git add -u
git commit -m "fix(billing): wire payment method in PaymentCreatedDomainEventHandler and document stubs"
```

---

### Task 28: Remaining readability/style fixes

**Step 1: Narrow exception catches**

- `src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuditInterceptor.cs` — narrow `catch (Exception)` to specific types
- `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs:345-348` — narrow bare `catch` to `CryptographicException`

**Step 2: Standardize ID construction in Messaging**

- `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/SendMessage/SendMessageHandler.cs:17`
- Replace `new ConversationId(...)` with `ConversationId.Create(...)` (or vice versa — check which is canonical)

**Step 3: Minor style fixes**

- Collapse nested ifs in `src/Modules/Branding/Wallow.Branding.Api/Controllers/ClientBrandingController.cs:78-85`
- Replace `new List<T>()` with `[]` in `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginManifestLoader.cs` (2 locations)
- Extract duplicate `ApiHealthCheck` from `src/Wallow.Auth/ApiHealthCheck.cs` and `src/Wallow.Web/ApiHealthCheck.cs` to shared project

**Step 4: Run tests**
```bash
./scripts/run-tests.sh
```

**Commit:**
```bash
git add -u
git commit -m "refactor: narrow exception catches, standardize ID construction, minor style fixes"
```

---

## Post-Implementation

After all three branches are complete and tested:

1. Create PRs in order: quick-wins → medium → cleanup
2. Each PR targets `main` (or quick-wins targets main, medium targets quick-wins branch, etc.)
3. Run `bd dolt push` to sync beads
4. Run `git push` on each branch
