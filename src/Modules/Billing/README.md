# Billing Module

## Overview

The Billing module manages invoices, payments, and subscriptions. It provides a domain model with state machine-based transitions, multi-tenant isolation, and event-driven integration with other modules via Wolverine in-memory messaging.

The module follows Clean Architecture with CQRS patterns, using Wolverine for command/query handling and domain event dispatching. All financial records are tenant-scoped.

## Key Features

- **Invoice Management**: Full lifecycle from draft through payment with line item support
- **Payment Processing**: Transaction recording with multiple payment methods and status tracking
- **Subscription Billing**: Recurring billing plans with period management and status transitions
- **Multi-tenancy**: Automatic tenant isolation via EF Core query filters
- **Event-Driven**: Domain events bridged to integration events for cross-module communication
- **Currency Safety**: Money value object enforcing ISO 4217 codes and same-currency arithmetic

## Architecture

```
src/Modules/Billing/
+-- Wallow.Billing.Domain         # Entities, Value Objects, Domain Events
+-- Wallow.Billing.Application    # Commands, Queries, Handlers, DTOs
+-- Wallow.Billing.Infrastructure # EF Core, Repositories
+-- Wallow.Billing.Api            # Controllers, Request/Response Contracts
```

**Database Schema**: `billing` (PostgreSQL)

## Domain Entities

### Invoice (Aggregate Root)

The primary billing document, containing line items and tracking payment status.

**State Machine**:
```
Draft --> Issued --> Paid
               +--> Overdue --> Paid
               +--> Cancelled

(Cancelled is terminal; Paid invoices cannot be cancelled)
```

### InvoiceLineItem (Entity)

Individual billable items within an invoice. Line items can only be modified while the invoice is in `Draft` status.

### Payment (Aggregate Root)

Records payment transactions against invoices.

**State Machine**:
```
Pending --> Completed --> Refunded
       +--> Failed
```

### Subscription (Aggregate Root)

Recurring billing plan with period tracking.

**State Machine**:
```
Active <--> PastDue
Active/PastDue --> Cancelled
Active/PastDue --> Expired
```

## Value Objects

### Money

Immutable value object representing monetary amounts with currency. Amount must be non-negative, currency must be a 3-letter ISO 4217 code, and arithmetic operations require matching currencies.

## Commands

| Command | Description |
|---------|-------------|
| `CreateInvoiceCommand` | Create a new draft invoice |
| `AddLineItemCommand` | Add line item to draft invoice |
| `IssueInvoiceCommand` | Issue invoice for payment |
| `CancelInvoiceCommand` | Cancel invoice |
| `ProcessPaymentCommand` | Process payment for invoice |
| `CreateSubscriptionCommand` | Create new subscription |
| `CancelSubscriptionCommand` | Cancel subscription |

## Queries

| Query | Returns |
|-------|---------|
| `GetInvoiceByIdQuery` | `Result<InvoiceDto>` |
| `GetAllInvoicesQuery` | `Result<IReadOnlyList<InvoiceDto>>` |
| `GetInvoicesByUserIdQuery` | `Result<IReadOnlyList<InvoiceDto>>` |
| `GetPaymentByIdQuery` | `Result<PaymentDto>` |
| `GetPaymentsByInvoiceIdQuery` | `Result<IReadOnlyList<PaymentDto>>` |
| `GetSubscriptionByIdQuery` | `Result<SubscriptionDto>` |
| `GetSubscriptionsByUserIdQuery` | `Result<IReadOnlyList<SubscriptionDto>>` |

## Domain Events

| Event | Raised When |
|-------|-------------|
| `InvoiceCreatedDomainEvent` | Invoice created |
| `InvoicePaidDomainEvent` | Invoice marked as paid |
| `InvoiceOverdueDomainEvent` | Invoice marked as overdue |
| `PaymentFailedDomainEvent` | Payment failed |
| `SubscriptionCreatedDomainEvent` | Subscription created |
| `SubscriptionCancelledDomainEvent` | Subscription cancelled |

## Integration Events

Events published via Wolverine for cross-module communication. Defined in `Wallow.Shared.Contracts`.

### Published Events

| Event | When |
|-------|------|
| `InvoiceCreatedEvent` | Invoice created |
| `InvoicePaidEvent` | Invoice paid |
| `InvoiceOverdueEvent` | Invoice overdue |
| `PaymentReceivedEvent` | Payment received |

## API Endpoints

All endpoints require authentication.

### Invoices (`/api/invoices`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/invoices` | Get all invoices |
| `GET` | `/api/invoices/{id}` | Get invoice by ID |
| `GET` | `/api/invoices/user/{userId}` | Get invoices by user |
| `POST` | `/api/invoices` | Create invoice |
| `POST` | `/api/invoices/{id}/line-items` | Add line item |
| `POST` | `/api/invoices/{id}/issue` | Issue invoice |
| `DELETE` | `/api/invoices/{id}` | Cancel invoice |

### Payments (`/api/payments`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/payments/{id}` | Get payment by ID |
| `GET` | `/api/payments/invoice/{invoiceId}` | Get payments by invoice |
| `POST` | `/api/payments/invoice/{invoiceId}` | Process payment |

### Subscriptions (`/api/subscriptions`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/subscriptions/{id}` | Get subscription by ID |
| `GET` | `/api/subscriptions/user/{userId}` | Get subscriptions by user |
| `POST` | `/api/subscriptions` | Create subscription |
| `DELETE` | `/api/subscriptions/{id}` | Cancel subscription |

## Configuration

The module uses the shared `DefaultConnection` connection string. No additional configuration is required. The module auto-migrates its schema on startup.

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, value objects, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | Integration event definitions |

## Key Rules

1. **State transitions via aggregate methods**: Never set `Status` directly. Use `Invoice.Issue()`, `Invoice.MarkAsPaid()`, etc.
2. **Line item modifications**: Only allowed in `Draft` status. Once issued, the invoice is immutable.
3. **Currency consistency**: Money arithmetic throws `BusinessRuleException` on currency mismatch.
4. **Multi-tenancy**: All entities implement `ITenantScoped`. Query filters automatically scope by tenant.
5. **Integration events use plain GUIDs**: Strongly-typed IDs are internal to the module; integration events use `Guid` for serialization compatibility.

## Testing

```bash
./scripts/run-tests.sh billing
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext
```
