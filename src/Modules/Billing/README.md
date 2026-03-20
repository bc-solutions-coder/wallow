# Billing Module

## Overview

The Billing module manages the complete financial lifecycle for the Wallow platform, including invoices, payments, and subscriptions. It provides a rich domain model with state machine-based transitions, multi-tenant isolation, and event-driven integration with other modules via RabbitMQ.

This module demonstrates Clean Architecture with CQRS patterns, using Wolverine for command/query handling and domain event dispatching. All financial records are tenant-scoped and support GDPR compliance through data export and erasure capabilities.

## Key Features

- **Invoice Management**: Full lifecycle from draft through payment with line item support
- **Payment Processing**: Transaction recording with multiple payment methods and status tracking
- **Subscription Billing**: Recurring billing plans with period management and status transitions
- **Multi-tenancy**: Automatic tenant isolation via EF Core query filters
- **Event-Driven**: Domain events bridged to integration events for cross-module communication
- **GDPR Compliance**: Built-in data export and erasure for user data requests
- **Currency Safety**: Money value object enforcing ISO 4217 codes and same-currency arithmetic

## Architecture

The module follows Clean Architecture with four layers:

```
src/Modules/Billing/
+-- Wallow.Billing.Domain         # Entities, Value Objects, Domain Events
+-- Wallow.Billing.Application    # Commands, Queries, Handlers, DTOs
+-- Wallow.Billing.Infrastructure # EF Core, Repositories, Compliance
+-- Wallow.Billing.Api            # Controllers, Request/Response Contracts
```

### Data Flow

```
+-------------------+     +-------------------+     +-------------------+
|  API Controllers  |---->|  Wolverine        |---->|  Command/Query    |
|                   |     |  Message Bus      |     |  Handlers         |
+-------------------+     +-------------------+     +-------------------+
                                                            |
                                                            v
+-------------------+     +-------------------+     +-------------------+
|  Integration      |<----|  Domain Event     |<----|  Aggregate        |
|  Events (RabbitMQ)|     |  Handlers         |     |  Roots            |
+-------------------+     +-------------------+     +-------------------+
                                                            |
                                                            v
                                                    +-------------------+
                                                    |  EF Core          |
                                                    |  (billing schema) |
                                                    +-------------------+
```

**Database Schema**: `billing` (PostgreSQL)

## Domain Entities

### Invoice (Aggregate Root)

The primary billing document sent to users, containing line items and tracking payment status.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `InvoiceId` | Strongly-typed identifier |
| `TenantId` | `TenantId` | Multi-tenant scope |
| `UserId` | `Guid` | Owner of the invoice |
| `InvoiceNumber` | `string` | Unique invoice identifier |
| `Status` | `InvoiceStatus` | Current lifecycle state |
| `TotalAmount` | `Money` | Calculated sum of line items |
| `DueDate` | `DateTime?` | Payment due date |
| `PaidAt` | `DateTime?` | When payment was received |
| `LineItems` | `IReadOnlyCollection<InvoiceLineItem>` | Invoice line items |

**State Machine**:
```
Draft --> Issued --> Paid
               +--> Overdue --> Paid
               +--> Cancelled

(Cancelled is terminal; Paid invoices cannot be cancelled)
```

### InvoiceLineItem (Entity)

Individual billable items within an invoice.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `InvoiceLineItemId` | Strongly-typed identifier |
| `InvoiceId` | `InvoiceId` | Parent invoice reference |
| `Description` | `string` | Item description |
| `UnitPrice` | `Money` | Price per unit |
| `Quantity` | `int` | Number of units |
| `LineTotal` | `Money` | Calculated total (UnitPrice * Quantity) |

### Payment (Aggregate Root)

Records payment transactions against invoices.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `PaymentId` | Strongly-typed identifier |
| `TenantId` | `TenantId` | Multi-tenant scope |
| `InvoiceId` | `InvoiceId` | Associated invoice |
| `UserId` | `Guid` | User who made payment |
| `Amount` | `Money` | Payment amount |
| `Method` | `PaymentMethod` | Payment method used |
| `Status` | `PaymentStatus` | Current status |
| `TransactionReference` | `string?` | External transaction ID |
| `FailureReason` | `string?` | Reason if payment failed |
| `CompletedAt` | `DateTime?` | When payment completed |

**State Machine**:
```
Pending --> Completed --> Refunded
       +--> Failed
```

### Subscription (Aggregate Root)

Recurring billing plan with period tracking.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `SubscriptionId` | Strongly-typed identifier |
| `TenantId` | `TenantId` | Multi-tenant scope |
| `UserId` | `Guid` | Subscriber |
| `PlanName` | `string` | Subscription plan name |
| `Price` | `Money` | Recurring price |
| `Status` | `SubscriptionStatus` | Current status |
| `StartDate` | `DateTime` | When subscription began |
| `EndDate` | `DateTime?` | When subscription ended |
| `CurrentPeriodStart` | `DateTime` | Current billing period start |
| `CurrentPeriodEnd` | `DateTime` | Current billing period end |
| `CancelledAt` | `DateTime?` | When cancelled |

**State Machine**:
```
Active <--> PastDue
Active/PastDue --> Cancelled
Active/PastDue --> Expired
```

## Value Objects

### Money

Immutable value object representing monetary amounts with currency.

```csharp
// Create money
var price = Money.Create(99.99m, "USD");
var zero = Money.Zero("EUR");

// Arithmetic (throws on currency mismatch)
var total = price + Money.Create(10.00m, "USD");

// Properties
decimal amount = price.Amount;   // 99.99
string currency = price.Currency; // "USD"
```

**Rules**:
- Amount must be non-negative
- Currency must be a 3-letter ISO 4217 code
- Arithmetic operations require matching currencies

## Enums

### InvoiceStatus
| Value | Description |
|-------|-------------|
| `Draft` | Invoice being prepared, can add/remove line items |
| `Issued` | Invoice sent to user, awaiting payment |
| `Paid` | Invoice has been paid |
| `Overdue` | Invoice past due date |
| `Cancelled` | Invoice cancelled |

### PaymentStatus
| Value | Description |
|-------|-------------|
| `Pending` | Payment initiated, awaiting completion |
| `Completed` | Payment successful |
| `Failed` | Payment failed |
| `Refunded` | Payment was refunded |

### PaymentMethod
| Value | Description |
|-------|-------------|
| `CreditCard` | Credit card payment |
| `BankTransfer` | Bank transfer |
| `PayPal` | PayPal payment |

### SubscriptionStatus
| Value | Description |
|-------|-------------|
| `Active` | Subscription is active |
| `PastDue` | Payment overdue |
| `Cancelled` | User cancelled |
| `Expired` | Subscription expired |

## Commands (CQRS)

Commands modify state and are handled by Wolverine handlers.

### Invoice Commands

| Command | Description |
|---------|-------------|
| `CreateInvoiceCommand(UserId, InvoiceNumber, Currency, DueDate?)` | Create a new draft invoice |
| `AddLineItemCommand(InvoiceId, Description, UnitPrice, Quantity, UpdatedByUserId)` | Add line item to draft invoice |
| `IssueInvoiceCommand(InvoiceId, IssuedByUserId)` | Issue invoice for payment |
| `CancelInvoiceCommand(InvoiceId, CancelledByUserId)` | Cancel invoice |

### Payment Commands

| Command | Description |
|---------|-------------|
| `ProcessPaymentCommand(InvoiceId, UserId, Amount, Currency, PaymentMethod)` | Process payment for invoice |

### Subscription Commands

| Command | Description |
|---------|-------------|
| `CreateSubscriptionCommand(UserId, PlanName, Price, Currency, StartDate, PeriodEnd)` | Create new subscription |
| `CancelSubscriptionCommand(SubscriptionId, CancelledByUserId)` | Cancel subscription |

## Queries (CQRS)

Queries read data without side effects.

### Invoice Queries

| Query | Returns |
|-------|---------|
| `GetInvoiceByIdQuery(InvoiceId)` | `Result<InvoiceDto>` |
| `GetAllInvoicesQuery()` | `Result<IReadOnlyList<InvoiceDto>>` |
| `GetInvoicesByUserIdQuery(UserId)` | `Result<IReadOnlyList<InvoiceDto>>` |

### Payment Queries

| Query | Returns |
|-------|---------|
| `GetPaymentByIdQuery(PaymentId)` | `Result<PaymentDto>` |
| `GetPaymentsByInvoiceIdQuery(InvoiceId)` | `Result<IReadOnlyList<PaymentDto>>` |

### Subscription Queries

| Query | Returns |
|-------|---------|
| `GetSubscriptionByIdQuery(SubscriptionId)` | `Result<SubscriptionDto>` |
| `GetSubscriptionsByUserIdQuery(UserId)` | `Result<IReadOnlyList<SubscriptionDto>>` |

## Domain Events

Internal events raised by aggregate roots during state transitions.

| Event | Raised When |
|-------|-------------|
| `InvoiceCreatedDomainEvent` | Invoice created |
| `InvoicePaidDomainEvent` | Invoice marked as paid |
| `InvoiceOverdueDomainEvent` | Invoice marked as overdue |
| `PaymentReceivedDomainEvent` | Payment created |
| `PaymentFailedDomainEvent` | Payment failed |
| `SubscriptionCreatedDomainEvent` | Subscription created |
| `SubscriptionCancelledDomainEvent` | Subscription cancelled |

## Integration Events

Events published to RabbitMQ for cross-module communication. Defined in `Wallow.Shared.Contracts`.

### Published Events

| Event | When | Typical Consumers |
|-------|------|-------------------|
| `InvoiceCreatedEvent` | Invoice created | Email (send invoice), Notifications |
| `InvoicePaidEvent` | Invoice paid | Email (receipt), Notifications |
| `InvoiceOverdueEvent` | Invoice overdue | Email (reminder), Notifications |
| `PaymentReceivedEvent` | Payment received | Notifications |

### Consumed Events

| Event | From | Action |
|-------|------|--------|
| `OrganizationCreatedEvent` | Identity | Initialize billing for new tenant |

## API Endpoints

All endpoints require authentication and return JSON responses.

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

The module requires a PostgreSQL connection string configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=wallow;Username=postgres;Password=..."
  }
}
```

**No additional configuration required.** The module auto-migrates its schema on startup.

### Module Registration

In `Program.cs`:

```csharp
// Add services
builder.Services.AddBillingModule(builder.Configuration);

// Configure middleware (runs migrations)
await app.UseBillingModuleAsync();
```

## Dependencies

### Internal Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, value objects, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | Integration event definitions |

### External Packages

| Package | Purpose |
|---------|---------|
| Microsoft.EntityFrameworkCore | ORM for persistence |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL provider |
| WolverineFx | CQRS mediator and message bus |
| FluentValidation | Command validation |

## GDPR Compliance

The module implements `IDataExporter` and `IDataEraser` for GDPR compliance:

### Data Export (`BillingDataExporter`)

Exports all user billing data including:
- Invoices with line items
- Payment records
- Subscriptions

### Data Erasure (`BillingDataEraser`)

- **Invoices**: Anonymized (user link removed) but retained for legal/tax requirements
- **Payments**: Deleted (no legal requirement to retain)
- **Subscriptions**: Deleted

## Usage Examples

### Creating an Invoice

```csharp
// 1. Create draft invoice
var createCmd = new CreateInvoiceCommand(
    userId: currentUserId,
    invoiceNumber: "INV-2024-001",
    currency: "USD",
    dueDate: DateTime.UtcNow.AddDays(30));

var result = await bus.InvokeAsync<Result<InvoiceDto>>(createCmd);
var invoiceId = result.Value.Id;

// 2. Add line items (only while in Draft status)
await bus.InvokeAsync<Result<InvoiceDto>>(new AddLineItemCommand(
    invoiceId: invoiceId,
    description: "Professional Services",
    unitPrice: 150.00m,
    quantity: 8,
    updatedByUserId: currentUserId));

// 3. Issue invoice (transitions from Draft to Issued)
await bus.InvokeAsync<Result<InvoiceDto>>(new IssueInvoiceCommand(
    invoiceId: invoiceId,
    issuedByUserId: currentUserId));
```

### Processing a Payment

```csharp
var paymentCmd = new ProcessPaymentCommand(
    invoiceId: invoiceId,
    userId: currentUserId,
    amount: 1200.00m,
    currency: "USD",
    paymentMethod: PaymentMethod.CreditCard);

var result = await bus.InvokeAsync<Result<PaymentDto>>(paymentCmd);
```

### Creating a Subscription

```csharp
var subscriptionCmd = new CreateSubscriptionCommand(
    userId: currentUserId,
    planName: "Professional",
    price: 49.99m,
    currency: "USD",
    startDate: DateTime.UtcNow,
    periodEnd: DateTime.UtcNow.AddMonths(1));

var result = await bus.InvokeAsync<Result<SubscriptionDto>>(subscriptionCmd);
```

## Key Rules and Constraints

1. **State transitions via aggregate methods**: Never set `Status` directly. Use `Invoice.Issue()`, `Invoice.MarkAsPaid()`, etc.

2. **Line item modifications**: Only allowed in `Draft` status. Once issued, invoice is immutable.

3. **Currency consistency**: Money arithmetic throws `BusinessRuleException` on currency mismatch.

4. **Multi-tenancy**: All entities implement `ITenantScoped`. Query filters automatically scope by tenant.

5. **Integration events use plain GUIDs**: Strongly-typed IDs are internal to the module; integration events use `Guid` for serialization compatibility.

6. **No cross-module references**: Publish integration events through `Shared.Contracts` for inter-module communication.

7. **Database isolation**: Uses `billing` PostgreSQL schema. Do not share tables with other modules.

## Testing

```bash
# Run all Billing module tests
dotnet test tests/Modules/Billing/Modules.Billing.Tests

# Run specific test class
dotnet test tests/Modules/Billing/Modules.Billing.Tests --filter "FullyQualifiedName~InvoiceTests"
```

## EF Core Migrations

```bash
# Add a new migration
dotnet ef migrations add MigrationName \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext

# Apply migrations (also runs automatically on startup)
dotnet ef database update \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext
```
