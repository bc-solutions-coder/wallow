# bcordes.dev Foundry Integration - Design Spec

**Date:** 2026-03-13
**Status:** Approved

The bcordes.dev frontend is migrating to Foundry as its sole API backend. This spec covers all backend changes required: scopes/permissions, inquiry submission overhaul, user inquiry views, SignalR events, and inquiry comments.

## Build Order

| Phase | Items | Priority | Depends On |
|-------|-------|----------|------------|
| 1 | Scopes & permissions, submission overhaul, phone field | Required | None |
| 2 | SubmitterId + user inquiry view | Deferred | Phase 1 |
| 3 | Inquiries read scope | Deferred | Phase 2 |
| 4 | SignalR events via Notifications | Deferred | None |
| 5 | Inquiry comments | Deferred | Phase 2, Phase 4 (optional) |

New DB: all schema changes consolidated into one migration.

---

## 1. Scopes & Permissions

### PermissionType additions

Add to `Foundry.Shared.Kernel/Identity/Authorization/PermissionType.cs`:

```
InquiriesRead = "InquiriesRead"
InquiriesWrite = "InquiriesWrite"
```

### ApiScopes.ValidScopes additions

Add to `Foundry.Identity.Application/Constants/ApiScopes.cs`:

- `"showcases.read"`
- `"inquiries.read"`
- `"inquiries.write"`

### PermissionExpansionMiddleware additions

Add to `MapScopeToPermission` in `Foundry.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs`:

- `"inquiries.read" => PermissionType.InquiriesRead`
- `"inquiries.write" => PermissionType.InquiriesWrite`

`showcases.read` is already mapped. No change needed.

### ApiScopeSeeder additions

Add 3 new scopes to the seeder with categories "Showcases" and "Inquiries". Update `ApiScopeSeederGapTests` expected count from 11 to 14.

### RolePermissionMapping additions

- **admin**: `InquiriesRead`, `InquiriesWrite`
- **manager**: `InquiriesRead`
- **user**: none (users access via `/submitted` which requires no specific permission)

---

## 2. Inquiry Submission Overhaul

### API contract change

**Before** (`SubmitInquiryRequest`): Name, Email, Company, Phone, Subject, Message
**After**: Name, Email, Company, Phone, ProjectType, BudgetRange, Timeline, Message

- Drop `Subject`
- Add `ProjectType` (string, required), `BudgetRange` (string, required), `Timeline` (string, required)
- Keep `Phone` (string?, optional)

### Domain entity changes

Add to `Inquiry.cs`:

- `Phone` property: `string?`, max 20 chars
- `SubmitterId` property: `string?`, max 128 chars (Keycloak `sub` claim)

Update `Inquiry.Create()` factory method to accept `phone` and `submitterId` parameters.

### Controller changes

- Remove `[AllowAnonymous]` from POST endpoint
- Add `[HasPermission(PermissionType.InquiriesWrite)]`
- Map all request fields directly to command (remove hardcoded empty strings for BudgetRange/Timeline)
- Extract `SubmitterId`: if caller's `azp` claim starts with `sa-`, set null; otherwise read `sub` claim

### Validator fix

`BudgetRange` and `Timeline` remain required (NotEmpty). The hardcoded empty strings were a bug. Add `Phone` validation: optional, max 20 chars when present.

### Response contract update

Update `InquiryResponse` to include: `ProjectType`, `BudgetRange`, `Timeline`, `Phone`, `SubmitterId`. Drop `Subject`.

### SubmitInquiryCommand update

Add `Phone` and `SubmitterId` fields to the command record.

---

## 3. User Inquiry View

### New endpoint: `GET /api/v1/inquiries/submitted`

- Route: `api/v1/inquiries/submitted`
- Auth: `[Authorize]` (any authenticated user, no specific permission)
- Query: `WHERE email = {jwt.email} OR submitter_id = {jwt.sub}`
- Optional `?status=` query parameter for filtering
- Returns `IReadOnlyList<InquiryResponse>`, ordered by `CreatedAt` descending

### New query/handler

- `GetSubmittedInquiriesQuery`: properties `Email` (string), `SubmitterId` (string), `Status` (InquiryStatus?)
- `GetSubmittedInquiriesHandler`: queries repository with dual-match condition

### Repository addition

Add `GetBySubmitterAsync(string email, string? submitterId, InquiryStatus? status)` to `IInquiryRepository`.

### Modified endpoint: `GET /api/v1/inquiries`

Change from `[Authorize]` to `[HasPermission(PermissionType.InquiriesRead)]`. Admin/manager sees all inquiries.

### Modified endpoint: `GET /api/v1/inquiries/{id}`

Allow access if caller has `InquiriesRead` permission OR is the submitter (email match or SubmitterId match). Return 403 otherwise.

---

## 4. SignalR Events via Notifications Module

The Inquiries module already publishes `InquirySubmittedEvent` and `InquiryStatusChangedEvent` as integration events via RabbitMQ/Wolverine. The Notifications module subscribes to these events and dispatches SignalR notifications.

### New handlers in Notifications module

**`InquirySubmittedEventHandler`** (in `Foundry.Notifications.Application` or `.Infrastructure`):
- Subscribes to `InquirySubmittedEvent`
- Dispatches: `RealtimeEnvelope.Create("Inquiries", "InquirySubmitted", new { InquiryId, Name, Email })`
- Target: tenant group via `IRealtimeDispatcher.SendToTenantAsync()`
- Client receives on `ReceiveInquiries`

**`InquiryStatusChangedEventHandler`**:
- Subscribes to `InquiryStatusChangedEvent`
- Dispatches: `RealtimeEnvelope.Create("Inquiries", "InquiryStatusUpdated", new { InquiryId, NewStatus })`
- Same tenant group dispatch pattern

No changes to the Inquiries module for SignalR.

---

## 5. Inquiry Comments

### New domain entity: `InquiryComment`

Properties:
- `Id`: `InquiryCommentId` (Guid wrapper, strongly typed)
- `InquiryId`: `InquiryId` (FK to Inquiry)
- `AuthorId`: `string` (Keycloak user ID, max 128 chars)
- `AuthorName`: `string` (max 200 chars)
- `Content`: `string` (max 5000 chars)
- `IsInternal`: `bool` (true = admin-only, false = visible to submitter)
- `CreatedAt`: `DateTimeOffset`

### New identity type

`InquiryCommentId` in `Foundry.Inquiries.Domain/Identity/` - Guid wrapper following existing pattern.

### Endpoints

**`POST /api/v1/inquiries/{id}/comments`**
- Auth: `[HasPermission(PermissionType.InquiriesWrite)]`
- Body: `{ content: string, isInternal: bool }`
- Extracts `AuthorId` from JWT `sub` claim, `AuthorName` from JWT `name` claim
- Validates: content not empty, max 5000 chars

**`GET /api/v1/inquiries/{id}/comments`**
- Auth: `[Authorize]`
- Admin (has `InquiriesRead`): returns all comments
- Submitter (matched by email or SubmitterId): returns only `IsInternal = false` comments
- Others: 403 Forbidden

### Integration event

`InquiryCommentAddedEvent` (in `Foundry.Shared.Contracts/Inquiries/Events/`):
- Properties: `InquiryId`, `CommentId`, `IsInternal`, `AddedAt`
- Published via Wolverine bus from the domain event handler

Notifications module subscribes and dispatches:
- `RealtimeEnvelope.Create("Inquiries", "InquiryCommentAdded", new { InquiryId, CommentId, IsInternal })`
- Sent to tenant group via `ReceiveInquiries`

### Database

New table `inquiries.inquiry_comments`:

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | `uuid` | PK |
| `inquiry_id` | `uuid` | NOT NULL, FK to inquiries.inquiries, indexed |
| `author_id` | `varchar(128)` | NOT NULL |
| `author_name` | `varchar(200)` | NOT NULL |
| `content` | `varchar(5000)` | NOT NULL |
| `is_internal` | `boolean` | NOT NULL, default false |
| `created_at` | `timestamptz` | NOT NULL |

---

## 6. Database Migration

Since the target is a new database, consolidate all schema changes into one migration:

**Inquiry table additions:**
- `phone` column: `varchar(20)`, nullable
- `submitter_id` column: `varchar(128)`, nullable, indexed

**New table:** `inquiries.inquiry_comments` (schema above)

Implementation: remove existing migration, create a fresh `InitialCreate` migration that includes the complete schema.

---

## 7. Files Changed Summary

### Shared.Kernel
- `PermissionType.cs` - add `InquiriesRead`, `InquiriesWrite`

### Identity module
- `ApiScopes.cs` - add 3 scopes to `ValidScopes`
- `PermissionExpansionMiddleware.cs` - add 2 scope mappings
- `RolePermissionMapping.cs` - add permissions to admin/manager roles
- `ApiScopeSeeder.cs` - add 3 scope seed entries
- `ApiScopeSeederGapTests.cs` - update expected count

### Inquiries module
- `Inquiry.cs` - add Phone, SubmitterId properties; update Create() factory
- `InquiryId.cs` - no change
- `InquiryCommentId.cs` - new file
- `InquiryComment.cs` - new entity
- `InquirySubmittedDomainEvent.cs` - no change
- `InquiryCommentAddedDomainEvent.cs` - new domain event
- `SubmitInquiryCommand.cs` - add Phone, SubmitterId fields
- `SubmitInquiryHandler.cs` - pass new fields through
- `SubmitInquiryValidator.cs` - add Phone validation
- `GetSubmittedInquiriesQuery.cs` - new query
- `GetSubmittedInquiriesHandler.cs` - new handler
- `AddInquiryCommentCommand.cs` - new command
- `AddInquiryCommentHandler.cs` - new handler
- `GetInquiryCommentsQuery.cs` - new query
- `GetInquiryCommentsHandler.cs` - new handler
- `InquiryCommentAddedDomainEventHandler.cs` - new handler
- `InquiryDto.cs` - add Phone, SubmitterId
- `InquiryCommentDto.cs` - new DTO
- `InquiryMappings.cs` - update mappings
- `IInquiryRepository.cs` - add GetBySubmitterAsync
- `IInquiryCommentRepository.cs` - new interface
- `InquiryRepository.cs` - implement GetBySubmitterAsync
- `InquiryCommentRepository.cs` - new repository
- `InquiryConfiguration.cs` - add Phone, SubmitterId columns
- `InquiryCommentConfiguration.cs` - new EF config
- `InquiriesDbContext.cs` - add InquiryComments DbSet
- Migration files - fresh InitialCreate
- `SubmitInquiryRequest.cs` - update fields
- `InquiryResponse.cs` - update fields
- `AddInquiryCommentRequest.cs` - new request
- `InquiryCommentResponse.cs` - new response
- `InquiriesController.cs` - update Submit, add submitted/comments endpoints
- `InquiriesInfrastructureExtensions.cs` - register new repository

### Notifications module
- `InquirySubmittedSignalRHandler.cs` - new handler
- `InquiryStatusChangedSignalRHandler.cs` - new handler
- `InquiryCommentAddedSignalRHandler.cs` - new handler

### Shared.Contracts
- `InquiryCommentAddedEvent.cs` - new integration event

### Tests
- Update existing tests for new contract/auth requirements
- Add tests for new queries, commands, handlers
