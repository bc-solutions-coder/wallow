# Inquiries Module Design

## Purpose

Capture inbound business inquiries from potential clients via a public-facing form, persist them, and provide a status-based workflow for managing them. Notifications are sent via the Communications module.

## Domain Model

### Entity: Inquiry (Aggregate Root)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Id | Guid | Yes | Generated on creation |
| Name | string | Yes | Submitter's name |
| Email | string | Yes | Submitter's email |
| Company | string | No | Optional company/organization |
| ProjectType | ProjectType | Yes | Enum dropdown |
| BudgetRange | BudgetRange | Yes | Enum dropdown |
| Timeline | Timeline | Yes | Enum dropdown |
| Message | string | Yes | Free-text project description |
| Status | InquiryStatus | Yes | Lifecycle state |
| SubmitterIpAddress | string | Yes | For rate limiting |
| CreatedAt | DateTime | Yes | UTC timestamp |
| UpdatedAt | DateTime | Yes | UTC timestamp |

### Enums

**ProjectType:** WebApplication, MobileApplication, ApiIntegration, Consulting, Other

**BudgetRange:** Under5K, From5KTo15K, From15KTo50K, Over50K, NotSure

**Timeline:** Asap, OneToThreeMonths, ThreeToSixMonths, SixPlusMonths, Flexible

**InquiryStatus:** New, Reviewed, Contacted, Closed

### Status Transitions

```
New → Reviewed → Contacted → Closed
```

Transitions are enforced in the domain entity. Skipping stages is not allowed.

### Domain Events

- **InquirySubmittedEvent** — Published when a new inquiry is created. Triggers admin notification and submitter confirmation emails via Communications module.
- **InquiryStatusChangedEvent** — Published when status transitions occur, for audit/tracking.

## Application Layer

### Commands

**SubmitInquiryCommand** (public, no auth)
- Validates input via FluentValidation
- Checks rate limit (rejects if exceeded)
- Checks honeypot field (silently rejects if filled)
- Creates Inquiry entity with Status = New
- Publishes InquirySubmittedEvent

**UpdateInquiryStatusCommand** (authenticated, admin only)
- Validates the status transition
- Updates the entity
- Publishes InquiryStatusChangedEvent

### Queries

**GetInquiriesQuery** — List inquiries with filtering by status, date range, pagination, sorted by date descending.

**GetInquiryByIdQuery** — Single inquiry detail by ID.

### Validation Rules

| Field | Rules |
|-------|-------|
| Name | Required, max 200 characters |
| Email | Required, valid email format |
| Message | Required, min 10, max 5000 characters |
| ProjectType | Required, valid enum value |
| BudgetRange | Required, valid enum value |
| Timeline | Required, valid enum value |
| Company | Optional, max 200 characters |

## Infrastructure

### Database

- Own PostgreSQL schema: `inquiries`
- EF Core for writes
- Dapper available for complex reads
- Single `Inquiries` table

### Rate Limiting

- Valkey-backed (already in the stack)
- Track submissions per IP address
- Configurable threshold (default: 3 submissions per hour per IP)
- Returns 429 Too Many Requests when exceeded

### Spam Protection

- Honeypot field: hidden form field, silently reject if populated
- Rate limiting per IP as described above

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | /api/inquiries | Public | Submit a new inquiry |
| GET | /api/inquiries | Admin | List inquiries with filters |
| GET | /api/inquiries/{id} | Admin | Get inquiry detail |
| PUT | /api/inquiries/{id}/status | Admin | Update inquiry status |

## Cross-Module Integration

The Inquiries module publishes `InquirySubmittedEvent` to RabbitMQ via Wolverine. The event contract lives in `Shared.Contracts`.

The Communications module subscribes with `InquirySubmittedHandler`, which sends two emails:

1. **Admin notification** — "New inquiry from {Name} at {Company}" with all submitted details
2. **Submitter confirmation** — "Thanks for reaching out, we'll be in touch shortly"

## Project Structure

```
src/Modules/Inquiries/
├── Wallow.Inquiries.Domain/
│   ├── Entities/Inquiry.cs
│   ├── Enums/ProjectType.cs, BudgetRange.cs, Timeline.cs, InquiryStatus.cs
│   └── Events/InquirySubmittedEvent.cs, InquiryStatusChangedEvent.cs
├── Wallow.Inquiries.Application/
│   ├── Commands/SubmitInquiry/, UpdateInquiryStatus/
│   ├── Queries/GetInquiries/, GetInquiryById/
│   └── Validators/
├── Wallow.Inquiries.Infrastructure/
│   ├── Data/InquiriesDbContext.cs, InquiryConfiguration.cs
│   ├── Repositories/
│   └── Services/RateLimitService.cs
└── Wallow.Inquiries.Api/
    ├── Endpoints/InquiryEndpoints.cs
    └── InquiriesModule.cs
```

Communications module addition:

```
src/Modules/Communications/
└── Wallow.Communications.Application/
    └── EventHandlers/InquirySubmittedHandler.cs
```

Shared contracts addition:

```
src/Shared/Wallow.Shared.Contracts/
└── Inquiries/InquirySubmittedEvent.cs, InquiryStatusChangedEvent.cs
```
