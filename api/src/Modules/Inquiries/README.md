# Inquiries Module

## Overview

The Inquiries module handles contact form submissions and inquiry management. Visitors (or authenticated users) submit inquiries with project details, and administrators review, respond to, and track them through a defined workflow. The module supports comments (internal and external), rate limiting via Valkey, and automatic submitter linking when a user later verifies their email.

The module follows Clean Architecture with CQRS patterns, using Wolverine for command/query handling and domain event dispatching. All records are tenant-scoped.

## Key Features

- **Inquiry Submission**: Capture name, email, phone, company, project type, budget, timeline, and message
- **Status Workflow**: Inquiries progress through New, Reviewed, Contacted, and Closed states
- **Comments**: Internal (staff-only) and external comments on inquiries
- **Submitter Linking**: Automatically links anonymous inquiries to user accounts when their email is verified (via `EmailVerifiedEvent`)
- **Rate Limiting**: Valkey-backed rate limiting (5 requests per 15 minutes per key)
- **Multi-tenancy**: Automatic tenant isolation via EF Core query filters
- **Event-Driven**: Domain events bridged to integration events for cross-module communication

## Architecture

```
src/Modules/Inquiries/
+-- Wallow.Inquiries.Domain         # Entities, Enums, Domain Events, Strongly-Typed IDs
+-- Wallow.Inquiries.Application    # Commands, Queries, Handlers, DTOs, Event Handlers
+-- Wallow.Inquiries.Infrastructure # EF Core, Repositories, Valkey Rate Limiting
+-- Wallow.Inquiries.Api            # Controllers, Request/Response Contracts
```

**Database Schema**: `inquiries` (PostgreSQL)

## Domain Entities

### Inquiry (Aggregate Root)

The primary entity representing a contact inquiry with project details.

**State Machine**:
```
New --> Reviewed --> Contacted --> Closed
```

All transitions are sequential; no skipping or backward transitions.

### InquiryComment (Aggregate Root)

Staff or external comments attached to an inquiry. Comments have an `IsInternal` flag to control visibility -- internal comments are only visible to users with the `InquiriesRead` permission.

## Enums

| Enum | Values |
|------|--------|
| `InquiryStatus` | New, Reviewed, Contacted, Closed |
| `ProjectType` | WebApplication, MobileApplication, ApiIntegration, Consulting, Other |
| `BudgetRange` | Under5K, From5KTo15K, From15KTo50K, Over50K, NotSure |
| `Timeline` | Asap, OneToThreeMonths, ThreeToSixMonths, SixPlusMonths, Flexible |

## Commands

| Command | Description |
|---------|-------------|
| `SubmitInquiryCommand` | Submit a new inquiry |
| `UpdateInquiryStatusCommand` | Transition inquiry to a new status |
| `AddInquiryCommentCommand` | Add a comment to an inquiry |

## Queries

| Query | Returns |
|-------|---------|
| `GetInquiriesQuery` | All inquiries, optionally filtered by status |
| `GetInquiryByIdQuery` | Single inquiry by ID |
| `GetSubmittedInquiriesQuery` | Inquiries submitted by the current user |
| `GetInquiryCommentsQuery` | Comments for an inquiry (with internal visibility control) |

## Domain Events

| Event | Raised When |
|-------|-------------|
| `InquirySubmittedDomainEvent` | Inquiry created |
| `InquiryStatusChangedDomainEvent` | Status transitions |
| `InquiryCommentAddedDomainEvent` | Comment added |

## Integration Events

Published via Wolverine in-memory messaging for cross-module communication. Defined in `Wallow.Shared.Contracts.Inquiries.Events`.

| Event | When |
|-------|------|
| `InquirySubmittedEvent` | Inquiry submitted (includes admin notification config) |
| `InquiryStatusChangedEvent` | Status changed |
| `InquiryCommentAddedEvent` | Comment added (includes submitter details for notifications) |

### Consumed Events

| Event | Handler | Purpose |
|-------|---------|---------|
| `EmailVerifiedEvent` | `EmailVerifiedInquiryLinkHandler` | Links unlinked inquiries to a user when their email is verified |

## API Endpoints

All endpoints require authentication. Base path: `/api/v1/inquiries`

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| `POST` | `/` | `InquiriesWrite` | Submit an inquiry |
| `GET` | `/` | `InquiriesRead` | List all inquiries (optional `?status=` filter) |
| `GET` | `/submitted` | Authenticated | Get current user's submitted inquiries |
| `GET` | `/{id}` | `InquiriesRead` or submitter | Get inquiry by ID |
| `PATCH` | `/{id}/status` | Authenticated | Update inquiry status |
| `POST` | `/{id}/comments` | `InquiriesWrite` | Add a comment |
| `GET` | `/{id}/comments` | `InquiriesRead` or submitter | Get comments (internal comments hidden from submitters) |

## Configuration

The module uses the shared `DefaultConnection` connection string and auto-migrates its schema in Development/Testing environments.

Optional configuration in `appsettings.json`:

| Key | Purpose | Default |
|-----|---------|---------|
| `Inquiries:AdminEmail` | Admin notification email | `admin@wallow.local` |
| `Inquiries:AdminUserIds` | List of admin user GUIDs for notifications | `[]` |

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, strongly-typed IDs, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | Integration event definitions |
| `Wallow.Shared.Infrastructure.Core` | Tenant-aware DbContext, interceptors |

## Testing

```bash
./scripts/run-tests.sh inquiries
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/Inquiries/Wallow.Inquiries.Infrastructure \
    --startup-project src/Wallow.Api \
    --context InquiriesDbContext
```
