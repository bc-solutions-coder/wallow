# Inquiries Module — Agent Guide

## What This Module Does

Handles contact form inquiries: submission, status tracking (New -> Reviewed -> Contacted -> Closed), and comments (internal/external). Rate-limited via Valkey. Automatically links anonymous inquiries to users when they verify their email.

## Key File Locations

| Area | Path |
|------|------|
| Aggregate roots | `Wallow.Inquiries.Domain/Entities/Inquiry.cs`, `InquiryComment.cs` |
| Strongly-typed IDs | `Wallow.Inquiries.Domain/Identity/InquiryId.cs`, `InquiryCommentId.cs` |
| Enums | `Wallow.Inquiries.Domain/Enums/` (InquiryStatus, ProjectType, BudgetRange, Timeline) |
| Domain events | `Wallow.Inquiries.Domain/Events/` |
| Commands | `Wallow.Inquiries.Application/Commands/` (SubmitInquiry, UpdateInquiryStatus, AddInquiryComment) |
| Queries | `Wallow.Inquiries.Application/Queries/` (GetInquiries, GetInquiryById, GetInquiryComments, GetSubmittedInquiries) |
| Event handlers | `Wallow.Inquiries.Application/EventHandlers/` |
| Repositories | `Wallow.Inquiries.Infrastructure/Persistence/Repositories/` |
| EF configurations | `Wallow.Inquiries.Infrastructure/Persistence/Configurations/` |
| Rate limiting | `Wallow.Inquiries.Infrastructure/Services/ValkeyRateLimitService.cs` |
| DI registration | `Wallow.Inquiries.Infrastructure/Extensions/InquiriesModuleExtensions.cs` |
| Controller | `Wallow.Inquiries.Api/Controllers/InquiriesController.cs` |
| Integration events | `src/Shared/Wallow.Shared.Contracts/Inquiries/Events/` |
| Tests | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/` |

## Conventions and Patterns

- **Handlers are static classes** with a `HandleAsync` static method (Wolverine convention). No interfaces or DI registration needed.
- **Status transitions** are enforced in `Inquiry.TransitionTo()` with a state machine pattern. Only sequential transitions are valid: New -> Reviewed -> Contacted -> Closed.
- **Domain events** are raised inside aggregate factory methods and state-change methods, then bridged to integration events in Application-layer event handlers.
- **The controller is a partial class** to support `[LoggerMessage]` source generator attributes at the bottom of the file.
- **Submitter identification**: `ExtractSubmitterId()` in the controller returns `null` for service accounts (client IDs starting with `sa-`), otherwise returns the user ID.
- **Comment visibility**: `IsInternal` flag controls whether comments are visible to submitters. The `GetComments` endpoint filters based on `InquiriesRead` permission.
- **Rate limiting**: `IRateLimitService` backed by Valkey, 5 requests per 15 minutes per key.

## Cross-Module Communication

### Published Integration Events (via Wolverine)

- `InquirySubmittedEvent` — includes `AdminEmail` and `AdminUserIds` from configuration
- `InquiryStatusChangedEvent` — includes old/new status and submitter email
- `InquiryCommentAddedEvent` — includes submitter details for notification routing

### Consumed Integration Events

- `EmailVerifiedEvent` (from Identity module via `Shared.Contracts.Identity.Events`) — handled by `EmailVerifiedInquiryLinkHandler` to link unlinked inquiries to the verified user

## Configuration

- `Inquiries:AdminEmail` — admin email for submission notifications (default: `admin@wallow.local`)
- `Inquiries:AdminUserIds` — list of admin user GUIDs for in-app notifications

## Permissions

- `InquiriesRead` — view all inquiries and internal comments
- `InquiriesWrite` — submit inquiries and add comments

Defined in `Wallow.Shared.Kernel.Identity.Authorization.PermissionType`.

## Database

- Schema: `inquiries`
- DbContext: `InquiriesDbContext` (extends `TenantAwareDbContext`)
- Default tracking: `NoTracking`
- Auto-migrates in Development/Testing environments

## Running Tests

```bash
./scripts/run-tests.sh inquiries
```
