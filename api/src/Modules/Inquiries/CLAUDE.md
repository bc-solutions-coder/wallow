# Inquiries Module — Agent Guide

## What This Module Does

Contact form inquiries: submission, status tracking (New → Reviewed → Contacted → Closed), and
comments (internal/external). Rate-limited via Valkey. Automatically links anonymous inquiries to
users when they verify their email.

## Conventions and Patterns

- **Handlers**: the command handlers and event handlers are **static classes** with a static
  `HandleAsync` — a local exception, not the repo convention. Query handlers follow the usual
  shape: `public sealed class` with primary-constructor DI. Wolverine auto-discovers both.
- **Status transitions** are enforced in `Inquiry.TransitionTo()`; only sequential transitions
  are valid: New → Reviewed → Contacted → Closed.
- **Submitter identification**: `ExtractSubmitterId()` in the controller returns `null` for
  service accounts (client IDs starting with `sa-`), otherwise the user ID.
- **Comment visibility**: the `IsInternal` flag controls submitter visibility; the `GetComments`
  endpoint filters based on the `InquiriesRead` permission.
- **Rate limiting**: `IRateLimitService` backed by Valkey
  (`Infrastructure/Services/ValkeyRateLimitService.cs`), 5 requests per 15 minutes per key.

## Cross-Module Communication

- **Publishes** (via Wolverine, defined in `Shared.Contracts/Inquiries/Events/`):
  - `InquirySubmittedEvent` — includes `AdminEmail` and `AdminUserIds` from configuration
  - `InquiryStatusChangedEvent` — old/new status and submitter email
  - `InquiryCommentAddedEvent` — submitter details for notification routing
- **Consumes** `EmailVerifiedEvent` (from Identity) — `EmailVerifiedInquiryLinkHandler` links
  unlinked inquiries to the verified user.

## Configuration

- `Inquiries:AdminEmail` — admin email for submission notifications (default: `admin@wallow.local`)
- `Inquiries:AdminUserIds` — list of admin user GUIDs for in-app notifications

## Permissions

`InquiriesRead` (view all inquiries and internal comments), `InquiriesWrite` (submit inquiries
and add comments) — defined in `Wallow.Shared.Kernel.Identity.Authorization.PermissionType`.

## Database

Schema: `inquiries`; context: `InquiriesDbContext`.

## Running Tests

`./scripts/run-tests.sh inquiries`
