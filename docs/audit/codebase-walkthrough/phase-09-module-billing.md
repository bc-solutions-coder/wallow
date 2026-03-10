# Phase 9: Billing Module

**Scope:** Complete Billing module - Domain, Application, Infrastructure, Api layers + all tests
**Status:** Not Started
**Files:** 163 source files, 81 test files

## How to Use This Document
- Work through layers bottom-up: Domain -> Application -> Infrastructure -> Api -> Tests
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

---

## Domain Layer

### Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Entities/Invoice.cs` | Invoice aggregate root with state machine (Draft->Issued->Paid/Overdue/Cancelled) | Create, AddLineItem, RemoveLineItem, Issue, MarkAsPaid, MarkAsOverdue, Cancel; RecalculateTotal on line item changes | AggregateRoot, ITenantScoped, IHasCustomFields, Money, InvoiceLineItem, domain events | |
| 2 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Entities/InvoiceLineItem.cs` | Child entity of Invoice representing a single billable line | Create factory with validation; computes LineTotal = UnitPrice * Quantity | Entity, Money, InvoiceId | |
| 3 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Entities/Payment.cs` | Payment aggregate root tracking transactions against invoices | Create, Complete, Fail, Refund; state machine Pending->Completed/Failed, Completed->Refunded | AggregateRoot, ITenantScoped, IHasCustomFields, Money, PaymentMethod/Status enums, domain events | |
| 4 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Entities/Subscription.cs` | Subscription aggregate root for recurring billing plans | Create, Renew, MarkPastDue, Cancel, Expire; state machine Active->PastDue/Cancelled/Expired | AggregateRoot, ITenantScoped, IHasCustomFields, Money, SubscriptionStatus enum, domain events | |

### Value Objects

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 5 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/ValueObjects/Money.cs` | Immutable money value object with currency enforcement | Create (non-negative, 3-letter ISO), Zero factory, operator+ (currency mismatch throws), GetEqualityComponents | ValueObject base from Shared.Kernel | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 6 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Enums/InvoiceStatus.cs` | Invoice lifecycle states | Draft=0, Issued=1, Paid=2, Overdue=3, Cancelled=4 | None | |
| 7 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Enums/PaymentMethod.cs` | Payment method types | CreditCard=0, BankTransfer=1, PayPal=2 | None | |
| 8 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Enums/PaymentStatus.cs` | Payment lifecycle states | Pending=0, Completed=1, Failed=2, Refunded=3 | None | |
| 9 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Enums/SubscriptionStatus.cs` | Subscription lifecycle states | Active=0, PastDue=1, Cancelled=2, Expired=3 | None | |

### Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 10 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Events/InvoiceCreatedDomainEvent.cs` | Raised when invoice is created | Record with InvoiceId, UserId, TotalAmount, Currency | DomainEvent base | |
| 11 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Events/InvoiceOverdueDomainEvent.cs` | Raised when invoice is marked overdue | Record with InvoiceId, UserId, DueDate | DomainEvent base | |
| 12 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Events/InvoicePaidDomainEvent.cs` | Raised when invoice is paid | Record with InvoiceId, PaymentId, PaidAt | DomainEvent base | |
| 13 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Events/PaymentCreatedDomainEvent.cs` | Raised when payment is created | Record with PaymentId, InvoiceId, Amount, Currency, UserId | DomainEvent base | |
| 14 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Events/PaymentFailedDomainEvent.cs` | Raised when payment fails | Record with PaymentId, InvoiceId, Reason, UserId | DomainEvent base | |
| 15 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Events/SubscriptionCancelledDomainEvent.cs` | Raised when subscription is cancelled | Record with SubscriptionId, UserId, CancelledAt | DomainEvent base | |
| 16 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Events/SubscriptionCreatedDomainEvent.cs` | Raised when subscription is created | Record with SubscriptionId, UserId, PlanName, Amount, Currency | DomainEvent base | |

### Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Exceptions/InvalidInvoiceException.cs` | Domain exception for invalid invoice operations | Wraps message with code "Billing.InvalidInvoice" | DomainException base | |
| 18 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Exceptions/InvalidPaymentException.cs` | Domain exception for invalid payment operations | Wraps message with code "Billing.InvalidPayment" | DomainException base | |
| 19 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Exceptions/InvalidSubscriptionStatusTransitionException.cs` | Domain exception for invalid subscription state transitions | Formats from/to status in message | DomainException base | |

### Identity (Strongly-Typed IDs)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 20 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Identity/InvoiceId.cs` | Strongly-typed ID for Invoice | readonly record struct with Create and New factory methods | IStronglyTypedId | |
| 21 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Identity/InvoiceLineItemId.cs` | Strongly-typed ID for InvoiceLineItem | readonly record struct with Create and New factory methods | IStronglyTypedId | |
| 22 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Identity/PaymentId.cs` | Strongly-typed ID for Payment | readonly record struct with Create and New factory methods | IStronglyTypedId | |
| 23 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Identity/SubscriptionId.cs` | Strongly-typed ID for Subscription | readonly record struct with Create and New factory methods | IStronglyTypedId | |

### Metering - Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 24 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Entities/MeterDefinition.cs` | Defines a trackable/limitable meter (e.g., api.calls, storage.bytes) | Create with validation, Update method; Code, DisplayName, Unit, Aggregation, IsBillable, ValkeyKeyPattern | AuditableEntity, MeterAggregation enum | |
| 25 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Entities/QuotaDefinition.cs` | Defines usage limits per plan or tenant override | CreatePlanQuota, CreateTenantOverride, UpdateLimit; tenant overrides take precedence | AuditableEntity, ITenantScoped, QuotaPeriod/QuotaAction enums | |
| 26 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Entities/UsageRecord.cs` | Billing-grade usage record flushed from Valkey to PostgreSQL | Create with period validation, AddValue for upsert operations | Entity, ITenantScoped | |

### Metering - Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 27 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Enums/MeterAggregation.cs` | Aggregation strategy for meter values | Sum=0 (total calls), Max=1 (peak storage) | None | |
| 28 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Enums/QuotaAction.cs` | Action when quota exceeded | Block=0 (429 response), Warn=1 (warning header), Throttle=2 (rate limit) | None | |
| 29 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Enums/QuotaPeriod.cs` | Time period for quota evaluation | Hourly=0, Daily=1, Monthly=2 | None | |

### Metering - Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 30 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Events/QuotaThresholdReachedEvent.cs` | Raised when usage hits quota threshold (80%, 90%, 100%) | Record with TenantId, MeterCode, MeterDisplayName, CurrentUsage, Limit, PercentUsed | DomainEvent base | |
| 31 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Events/UsageFlushedEvent.cs` | Raised when usage is flushed from Valkey to PostgreSQL | Record with FlushedAt, RecordCount | DomainEvent base | |

### Metering - Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Identity/MeterDefinitionId.cs` | Strongly-typed ID for MeterDefinition | readonly record struct with Create and New | IStronglyTypedId | |
| 33 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Identity/QuotaDefinitionId.cs` | Strongly-typed ID for QuotaDefinition | readonly record struct with Create and New | IStronglyTypedId | |
| 34 | [ ] | `src/Modules/Billing/Foundry.Billing.Domain/Metering/Identity/UsageRecordId.cs` | Strongly-typed ID for UsageRecord | readonly record struct with Create and New | IStronglyTypedId | |

---

## Application Layer

### Commands - AddLineItem

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 35 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/AddLineItem/AddLineItemCommand.cs` | Command record to add a line item to an invoice | Fields: InvoiceId, Description, UnitPrice, Quantity, UpdatedByUserId | None | |
| 36 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/AddLineItem/AddLineItemHandler.cs` | Wolverine handler for adding line items | Loads invoice with line items, creates Money, calls invoice.AddLineItem, saves | IInvoiceRepository, TimeProvider | |
| 37 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/AddLineItem/AddLineItemValidator.cs` | FluentValidation validator for AddLineItemCommand | Validates InvoiceId, Description (max 500), UnitPrice >= 0, Quantity > 0, UpdatedByUserId | FluentValidation | |

### Commands - CancelInvoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 38 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CancelInvoice/CancelInvoiceCommand.cs` | Command record to cancel an invoice | Fields: InvoiceId, CancelledByUserId | None | |
| 39 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CancelInvoice/CancelInvoiceHandler.cs` | Wolverine handler for cancelling invoices | Loads invoice, calls invoice.Cancel, saves | IInvoiceRepository, TimeProvider | |
| 40 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CancelInvoice/CancelInvoiceValidator.cs` | FluentValidation validator for CancelInvoiceCommand | Validates InvoiceId and CancelledByUserId not empty | FluentValidation | |

### Commands - CancelSubscription

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 41 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CancelSubscription/CancelSubscriptionCommand.cs` | Command record to cancel a subscription | Fields: SubscriptionId, CancelledByUserId | None | |
| 42 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CancelSubscription/CancelSubscriptionHandler.cs` | Wolverine handler for cancelling subscriptions | Loads subscription, calls subscription.Cancel, saves | ISubscriptionRepository, TimeProvider | |
| 43 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CancelSubscription/CancelSubscriptionValidator.cs` | FluentValidation validator for CancelSubscriptionCommand | Validates SubscriptionId and CancelledByUserId not empty | FluentValidation | |

### Commands - CreateInvoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 44 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CreateInvoice/CreateInvoiceCommand.cs` | Command record to create an invoice | Fields: UserId, InvoiceNumber, Currency, DueDate, CustomFields | None | |
| 45 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CreateInvoice/CreateInvoiceHandler.cs` | Wolverine handler for creating invoices | Checks duplicate invoice number, calls Invoice.Create, saves; OTel activity tracing | IInvoiceRepository, TimeProvider, BillingModuleTelemetry | |
| 46 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CreateInvoice/CreateInvoiceValidator.cs` | FluentValidation validator for CreateInvoiceCommand | Validates UserId, InvoiceNumber (max 50), Currency (exactly 3 chars) | FluentValidation | |

### Commands - CreateSubscription

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 47 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CreateSubscription/CreateSubscriptionCommand.cs` | Command record to create a subscription | Fields: UserId, PlanName, Price, Currency, StartDate, PeriodEnd, CustomFields | None | |
| 48 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CreateSubscription/CreateSubscriptionHandler.cs` | Wolverine handler for creating subscriptions | Creates Money, calls Subscription.Create, saves | ISubscriptionRepository, TimeProvider | |
| 49 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/CreateSubscription/CreateSubscriptionValidator.cs` | FluentValidation validator for CreateSubscriptionCommand | Validates UserId, PlanName (max 100), Price >= 0, Currency (3 chars), PeriodEnd > StartDate | FluentValidation | |

### Commands - IssueInvoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 50 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/IssueInvoice/IssueInvoiceCommand.cs` | Command record to issue (activate) an invoice | Fields: InvoiceId, IssuedByUserId | None | |
| 51 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/IssueInvoice/IssueInvoiceHandler.cs` | Wolverine handler for issuing invoices | Loads invoice with line items, calls invoice.Issue, saves | IInvoiceRepository, TimeProvider | |
| 52 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/IssueInvoice/IssueInvoiceValidator.cs` | FluentValidation validator for IssueInvoiceCommand | Validates InvoiceId and IssuedByUserId not empty | FluentValidation | |

### Commands - ProcessPayment

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 53 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/ProcessPayment/ProcessPaymentCommand.cs` | Command record to process a payment | Fields: InvoiceId, UserId, Amount, Currency, PaymentMethod, CustomFields | None | |
| 54 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/ProcessPayment/ProcessPaymentHandler.cs` | Wolverine handler for processing payments | Validates overpayment/currency mismatch, creates Payment, auto-marks invoice paid if fully covered | IPaymentRepository, IInvoiceRepository, TimeProvider | |
| 55 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Commands/ProcessPayment/ProcessPaymentValidator.cs` | FluentValidation validator for ProcessPaymentCommand | Validates InvoiceId, UserId, Amount > 0, Currency (3 chars), PaymentMethod not empty | FluentValidation | |

### DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 56 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/DTOs/InvoiceDto.cs` | DTO for Invoice with line items and custom fields | Record with all invoice fields including LineItems list and CustomFields | None | |
| 57 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/DTOs/InvoiceLineItemDto.cs` | DTO for InvoiceLineItem | Record with Id, Description, UnitPrice, Currency, Quantity, LineTotal | None | |
| 58 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/DTOs/PaymentDto.cs` | DTO for Payment with custom fields | Record with all payment fields including CustomFields | None | |
| 59 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/DTOs/SubscriptionDto.cs` | DTO for Subscription with custom fields | Record with all subscription fields including CustomFields | None | |

### EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 60 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/EventHandlers/InvoiceCreatedDomainEventHandler.cs` | Bridges InvoiceCreatedDomainEvent to integration event | Enriches with invoice data, publishes InvoiceCreatedEvent via IMessageBus, records telemetry metrics | IInvoiceRepository, IMessageBus, BillingModuleTelemetry | |
| 61 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/EventHandlers/InvoiceOverdueDomainEventHandler.cs` | Bridges InvoiceOverdueDomainEvent to integration event | Enriches with user email via IUserQueryService, publishes InvoiceOverdueEvent | IInvoiceRepository, IUserQueryService, IMessageBus | |
| 62 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/EventHandlers/InvoicePaidDomainEventHandler.cs` | Bridges InvoicePaidDomainEvent to integration event | Enriches with invoice data, publishes InvoicePaidEvent | IInvoiceRepository, IMessageBus | |
| 63 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/EventHandlers/PaymentCreatedDomainEventHandler.cs` | Bridges PaymentCreatedDomainEvent to integration event | Gets user email, publishes PaymentReceivedEvent via IMessageBus | IMessageBus, ITenantContext, IUserQueryService | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 64 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Extensions/ApplicationExtensions.cs` | DI registration for Application layer | AddBillingApplication registers FluentValidation validators from assembly | FluentValidation, IServiceCollection | |

### Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 65 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Interfaces/IInvoiceRepository.cs` | Repository interface for Invoice aggregate | GetById, GetByIdWithLineItems, GetByUserId, GetAll, ExistsByInvoiceNumber, Add, Update, Remove, SaveChanges | Invoice, InvoiceId | |
| 66 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Interfaces/IPaymentRepository.cs` | Repository interface for Payment aggregate | GetById, GetByInvoiceId, GetByUserId, GetAll, Add, Update, SaveChanges | Payment, PaymentId, InvoiceId | |
| 67 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Interfaces/ISubscriptionRepository.cs` | Repository interface for Subscription aggregate | GetById, GetByUserId, GetAll, GetActiveByUserId, Add, Update, SaveChanges | Subscription, SubscriptionId | |

### Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 68 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Mappings/InvoiceMappings.cs` | Extension methods mapping Invoice/InvoiceLineItem to DTOs | ToDto() on Invoice and InvoiceLineItem; maps Money to separate Amount/Currency fields | InvoiceDto, InvoiceLineItemDto | |
| 69 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Mappings/PaymentMappings.cs` | Extension method mapping Payment to DTO | ToDto() on Payment; maps Money, enums to strings | PaymentDto | |
| 70 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Mappings/SubscriptionMappings.cs` | Extension method mapping Subscription to DTO | ToDto() on Subscription; maps Money, enums to strings | SubscriptionDto | |

### Queries - Invoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 71 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetAllInvoices/GetAllInvoicesQuery.cs` | Query record to get all invoices | Empty record (parameterless) | None | |
| 72 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetAllInvoices/GetAllInvoicesHandler.cs` | Handler returning all invoices as DTOs | Gets all from repository, maps to DTOs | IInvoiceRepository | |
| 73 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetInvoiceById/GetInvoiceByIdQuery.cs` | Query record to get invoice by ID | Record with InvoiceId field | None | |
| 74 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetInvoiceById/GetInvoiceByIdHandler.cs` | Handler returning single invoice with line items | Gets by ID with line items, returns NotFound or DTO | IInvoiceRepository | |
| 75 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetInvoicesByUserId/GetInvoicesByUserIdQuery.cs` | Query record to get invoices by user | Record with UserId field | None | |
| 76 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetInvoicesByUserId/GetInvoicesByUserIdHandler.cs` | Handler returning user's invoices as DTOs | Gets by user ID from repository, maps to DTOs | IInvoiceRepository | |

### Queries - Payment

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 77 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetPaymentById/GetPaymentByIdQuery.cs` | Query record to get payment by ID | Record with PaymentId field | None | |
| 78 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetPaymentById/GetPaymentByIdHandler.cs` | Handler returning single payment as DTO | Gets by ID, returns NotFound or DTO | IPaymentRepository | |
| 79 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetPaymentsByInvoiceId/GetPaymentsByInvoiceIdQuery.cs` | Query record to get payments by invoice | Record with InvoiceId field | None | |
| 80 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetPaymentsByInvoiceId/GetPaymentsByInvoiceIdHandler.cs` | Handler returning invoice's payments as DTOs | Gets by invoice ID from repository, maps to DTOs | IPaymentRepository | |

### Queries - Subscription

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 81 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetSubscriptionById/GetSubscriptionByIdQuery.cs` | Query record to get subscription by ID | Record with SubscriptionId field | None | |
| 82 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetSubscriptionById/GetSubscriptionByIdHandler.cs` | Handler returning single subscription as DTO | Gets by ID, returns NotFound or DTO | ISubscriptionRepository | |
| 83 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetSubscriptionsByUserId/GetSubscriptionsByUserIdQuery.cs` | Query record to get subscriptions by user | Record with UserId field | None | |
| 84 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Queries/GetSubscriptionsByUserId/GetSubscriptionsByUserIdHandler.cs` | Handler returning user's subscriptions as DTOs | Gets by user ID from repository, maps to DTOs | ISubscriptionRepository | |

### Telemetry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 85 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Telemetry/BillingModuleTelemetry.cs` | OTel instrumentation for Billing module | ActivitySource "Billing", Meter "Billing"; InvoicesCreatedTotal counter, InvoiceAmount histogram | Shared.Kernel.Diagnostics | |

### Metering - Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 86 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/IncrementMeter/IncrementMeterCommand.cs` | Command record to increment a meter counter | Record with MeterCode and Value (default 1) | None | |
| 87 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/IncrementMeter/IncrementMeterHandler.cs` | Handler delegating to IMeteringService | Calls meteringService.IncrementAsync; swallows exceptions with warning log | IMeteringService | |
| 88 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/SetQuotaOverride/SetQuotaOverrideCommand.cs` | Command record to set tenant-specific quota override | Record with TenantId, MeterCode, Limit, Period, OnExceeded | QuotaPeriod, QuotaAction enums | |
| 89 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/SetQuotaOverride/SetQuotaOverrideHandler.cs` | Handler for setting quota overrides | Validates meter exists, upserts quota (updates existing or creates new tenant override) | IQuotaDefinitionRepository, IMeterDefinitionRepository | |
| 90 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/SetQuotaOverride/SetQuotaOverrideValidator.cs` | FluentValidation validator for SetQuotaOverrideCommand | Validates TenantId, MeterCode (max 100), Limit >= 0, Period/OnExceeded are valid enums | FluentValidation | |
| 91 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/RemoveQuotaOverride/RemoveQuotaOverrideCommand.cs` | Command record to remove tenant quota override | Record with TenantId, MeterCode | None | |
| 92 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/RemoveQuotaOverride/RemoveQuotaOverrideHandler.cs` | Handler for removing quota overrides | Finds tenant override, removes it; returns NotFound if not exists | IQuotaDefinitionRepository | |
| 93 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Commands/RemoveQuotaOverride/RemoveQuotaOverrideValidator.cs` | FluentValidation validator for RemoveQuotaOverrideCommand | Validates TenantId and MeterCode (max 100) not empty | FluentValidation | |

### Metering - DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 94 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/DTOs/MeterDefinitionDto.cs` | DTO for meter definitions | Record with Id, Code, DisplayName, Unit, Aggregation, IsBillable | None | |
| 95 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/DTOs/QuotaCheckResult.cs` | Result of a quota check operation | IsAllowed, CurrentUsage, Limit, PercentUsed, ActionIfExceeded; static Unlimited factory | QuotaAction enum | |
| 96 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/DTOs/QuotaStatusDto.cs` | Status of a quota for a tenant | Record with MeterCode, CurrentUsage, Limit, PercentUsed, Period, OnExceeded, IsOverride | None | |
| 97 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/DTOs/UsageRecordDto.cs` | DTO for historical usage records | Record with Id, TenantId, MeterCode, PeriodStart, PeriodEnd, Value, FlushedAt | None | |
| 98 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/DTOs/UsageSummaryDto.cs` | Summary of current usage across meters | Record with MeterCode, DisplayName, Unit, CurrentValue, Limit, PercentUsed, Period | None | |

### Metering - EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 99 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/EventHandlers/QuotaThresholdReachedDomainEventHandler.cs` | Bridges QuotaThresholdReachedEvent to integration event | Publishes Shared.Contracts.Metering.Events.QuotaThresholdReachedEvent for Notifications module | IMessageBus | |
| 100 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/EventHandlers/UsageFlushedDomainEventHandler.cs` | Bridges UsageFlushedEvent to integration event | Publishes Shared.Contracts.Metering.Events.UsageFlushedEvent | IMessageBus | |

### Metering - Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 101 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Interfaces/IMeterDefinitionRepository.cs` | Repository interface for MeterDefinition | GetById, GetByCode, GetAll, Add, SaveChanges | MeterDefinition, MeterDefinitionId | |
| 102 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Interfaces/IQuotaDefinitionRepository.cs` | Repository interface for QuotaDefinition | GetById, GetEffectiveQuota (tenant override > plan default), GetTenantOverride, GetAllForTenant, Add, Remove, SaveChanges | QuotaDefinition, QuotaDefinitionId | |
| 103 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Interfaces/IUsageRecordRepository.cs` | Repository interface for UsageRecord | GetById, GetHistory, GetForPeriod, Add, Update, SaveChanges | UsageRecord, UsageRecordId | |

### Metering - Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 104 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetCurrentUsage/GetCurrentUsageQuery.cs` | Query for current usage optionally filtered by meter/period | Record with optional MeterCode and Period | QuotaPeriod enum | |
| 105 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetCurrentUsage/GetCurrentUsageHandler.cs` | Handler returning current usage summaries | Iterates meters, gets current value from Valkey + quota check, builds UsageSummaryDto list | IMeterDefinitionRepository, IMeteringService | |
| 106 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetMeterDefinitions/GetMeterDefinitionsQuery.cs` | Query for all meter definitions | Empty record (parameterless) | None | |
| 107 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetMeterDefinitions/GetMeterDefinitionsHandler.cs` | Handler returning all meter definitions as DTOs | Gets all from repository, maps to MeterDefinitionDto | IMeterDefinitionRepository | |
| 108 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetQuotaStatus/GetQuotaStatusQuery.cs` | Query for quota status of current tenant | Empty record (parameterless) | None | |
| 109 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetQuotaStatus/GetQuotaStatusHandler.cs` | Handler returning quota status for all meters with configured quotas | Iterates meters, checks quotas via IMeteringService, checks for tenant overrides | IQuotaDefinitionRepository, IMeterDefinitionRepository, IMeteringService | |
| 110 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetUsageHistory/GetUsageHistoryQuery.cs` | Query for historical usage records | Record with MeterCode, From, To | None | |
| 111 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Queries/GetUsageHistory/GetUsageHistoryHandler.cs` | Handler returning historical usage records as DTOs | Gets from repository by meter/date range, maps to UsageRecordDto | IUsageRecordRepository | |

### Metering - Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 112 | [ ] | `src/Modules/Billing/Foundry.Billing.Application/Metering/Services/IMeteringService.cs` | Core metering service interface | IncrementAsync, CheckQuotaAsync, GetCurrentUsageAsync (Valkey), GetUsageHistoryAsync (PostgreSQL) | QuotaCheckResult, UsageRecordDto, QuotaPeriod | |

---

## Infrastructure Layer

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 113 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Extensions/BillingInfrastructureExtensions.cs` | DI registration for Infrastructure layer | Registers BillingDbContext (Npgsql with retry/timeout), all repositories, all services (query/report/metering), FlushUsageJob | All repository and service interfaces | |
| 114 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Extensions/BillingModuleExtensions.cs` | Top-level module registration and initialization | AddBillingModule (DI), InitializeBillingModuleAsync (auto-migrate in Dev/Testing, seed metering data) | BillingDbContext, MeteringDbSeeder | |

### Jobs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 115 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Jobs/FlushUsageJob.cs` | Hangfire job flushing Valkey counters to PostgreSQL | Enumerates meter index Set, atomic get-and-reset per key, upserts UsageRecord, publishes UsageFlushedEvent | IConnectionMultiplexer, IUsageRecordRepository, IMessageBus, ITenantContextFactory, TimeProvider | |

### Persistence - DbContext

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 116 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/BillingDbContext.cs` | EF Core DbContext for billing schema | DbSets for Invoice, Payment, Subscription, InvoiceLineItem, MeterDefinition, QuotaDefinition, UsageRecord; NoTracking default; billing schema | TenantAwareDbContext base | |
| 117 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/BillingDbContextFactory.cs` | Design-time factory for EF Core migrations | Creates BillingDbContext with placeholder connection string and mock tenant context | IDesignTimeDbContextFactory | |
| 118 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/DesignTimeTenantContext.cs` | Mock ITenantContext for design-time migrations | Returns empty GUID TenantId and "design-time" name | ITenantContext | |
| 119 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/MeteringDbSeeder.cs` | Seeds default meter definitions and plan quotas | Seeds 4 meters (api.calls, storage.bytes, users.active, workflows.executions) and 9 plan quotas (free/pro/enterprise tiers) | BillingDbContext, MeterDefinition, QuotaDefinition | |

### Persistence - Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 120 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/InvoiceConfiguration.cs` | EF Core config for Invoice entity | Table "invoices"; StronglyTypedId conversion; OwnsOne for TotalAmount (Money); CustomFields as jsonb; unique index on TenantId+InvoiceNumber | StronglyTypedIdConverter, DictionaryValueComparer | |
| 121 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/InvoiceLineItemConfiguration.cs` | EF Core config for InvoiceLineItem entity | Table "invoice_line_items"; OwnsOne for UnitPrice and LineTotal (Money); FK to Invoice | StronglyTypedIdConverter | |
| 122 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs` | EF Core config for Payment entity | Table "payments"; OwnsOne for Amount (Money); CustomFields as jsonb; indexes on TenantId, InvoiceId, UserId, Status | StronglyTypedIdConverter, DictionaryValueComparer | |
| 123 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/SubscriptionConfiguration.cs` | EF Core config for Subscription entity | Table "subscriptions"; OwnsOne for Price (Money); CustomFields as jsonb; indexes on TenantId, UserId, Status | StronglyTypedIdConverter, DictionaryValueComparer | |
| 124 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/MeterDefinitionConfiguration.cs` | EF Core config for MeterDefinition entity | Table "meter_definitions"; unique index on Code; column mappings for all fields | StronglyTypedIdConverter | |
| 125 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/QuotaDefinitionConfiguration.cs` | EF Core config for QuotaDefinition entity | Table "quota_definitions"; unique filtered index on TenantId+MeterCode (where PlanCode IS NULL) | StronglyTypedIdConverter | |
| 126 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/UsageRecordConfiguration.cs` | EF Core config for UsageRecord entity | Table "usage_records"; unique composite index on TenantId+MeterCode+PeriodStart+PeriodEnd | StronglyTypedIdConverter | |

### Persistence - Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 127 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/InvoiceRepository.cs` | EF Core implementation of IInvoiceRepository | Compiled query for GetById; eager loading of LineItems; tenant-filtered queries | BillingDbContext | |
| 128 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/InvoiceRepositoryExtensions.cs` | Extension methods for custom field queries on invoices | FindByCustomField, FindByCustomFields, CustomFieldValueExists using EF.Functions.JsonContains on jsonb | DbSet, JsonSerializer | |
| 129 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/PaymentRepository.cs` | EF Core implementation of IPaymentRepository | Standard CRUD with tenant-filtered queries ordered by CreatedAt DESC | BillingDbContext | |
| 130 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/SubscriptionRepository.cs` | EF Core implementation of ISubscriptionRepository | Compiled query for GetById; GetActiveByUserId filters by Active status | BillingDbContext | |
| 131 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/MeterDefinitionRepository.cs` | EF Core implementation of IMeterDefinitionRepository | GetByCode for lookup, GetAll ordered by Code | BillingDbContext | |
| 132 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/QuotaDefinitionRepository.cs` | EF Core implementation of IQuotaDefinitionRepository | GetEffectiveQuota checks tenant override first then plan default; uses IgnoreQueryFilters for system-level quotas | BillingDbContext, ITenantContext | |
| 133 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/UsageRecordRepository.cs` | EF Core implementation of IUsageRecordRepository | GetHistory filters by tenant+meter+date range; GetForPeriod for exact period match (upsert support) | BillingDbContext, ITenantContext | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 134 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/ValkeyMeteringService.cs` | Valkey/Redis-based real-time metering service | IncrementAsync (fire-and-forget with index Set tracking), CheckQuotaAsync (reads counter + quota, raises threshold events), GetCurrentUsageAsync, GetUsageHistoryAsync | IConnectionMultiplexer, ITenantContext, repositories, IMessageBus, ISubscriptionQueryService | |
| 135 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/InvoiceQueryService.cs` | Dapper-based invoice query service | GetTotalRevenue, GetCount, GetPendingCount, GetOutstandingAmount using raw SQL on billing schema | BillingDbContext (for connection), Dapper, ITenantContext | |
| 136 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/InvoiceReportService.cs` | Dapper-based invoice report service | GetInvoicesAsync returns InvoiceReportRow list with raw SQL join | BillingDbContext (for connection), Dapper, ITenantContext | |
| 137 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/PaymentReportService.cs` | Dapper-based payment report service | GetPaymentsAsync returns PaymentReportRow list with raw SQL joining payments and invoices | BillingDbContext (for connection), Dapper, ITenantContext | |
| 138 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/RevenueReportService.cs` | Dapper-based revenue report service | GetRevenueAsync returns RevenueReportRow with aggregated gross/net revenue and refunds | BillingDbContext (for connection), Dapper, ITenantContext | |
| 139 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/SubscriptionQueryService.cs` | Service to get active plan code for a tenant | GetActivePlanCodeAsync finds active subscription and returns PlanName | ISubscriptionRepository | |
| 140 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/MeteringQueryService.cs` | Cross-module metering query service | CheckQuotaAsync reads effective quota + usage records from PostgreSQL, returns QuotaStatus | IUsageRecordRepository, IQuotaDefinitionRepository | |
| 141 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/UsageReportService.cs` | Usage report service for dashboards | GetUsageAsync joins UsageRecords with MeterDefinitions, groups by date/meter, returns UsageReportRow | BillingDbContext (LINQ) | |

### Workflows

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 142 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Workflows/InvoiceCreatedTrigger.cs` | Sample Elsa workflow activity for invoice creation | Placeholder ExecuteActivityAsync; ModuleName = "Billing" | WorkflowActivityBase (Elsa) | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 143 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/20260213182125_InitialCreate.cs` | Initial EF Core migration creating billing tables | Creates Invoices, InvoiceLineItems, Payments, Subscriptions tables with indexes | EF Core Migration | |
| 144 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/20260213182125_InitialCreate.Designer.cs` | Designer file for InitialCreate migration | Auto-generated model snapshot for migration | EF Core Migration | |
| 145 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/20260227201407_AbsorbMeteringEntities.cs` | Migration adding metering tables to billing schema | Creates meter_definitions, quota_definitions, usage_records tables | EF Core Migration | |
| 146 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/20260227201407_AbsorbMeteringEntities.Designer.cs` | Designer file for AbsorbMeteringEntities migration | Auto-generated model snapshot | EF Core Migration | |
| 147 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/20260303060302_AddTenantScopedInvoiceNumberIndex.cs` | Migration adding tenant-scoped unique invoice number index | Creates unique index on (TenantId, InvoiceNumber) | EF Core Migration | |
| 148 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/20260303060302_AddTenantScopedInvoiceNumberIndex.Designer.cs` | Designer file for AddTenantScopedInvoiceNumberIndex migration | Auto-generated model snapshot | EF Core Migration | |
| 149 | [ ] | `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/BillingDbContextModelSnapshot.cs` | Current model snapshot for BillingDbContext | Auto-generated snapshot of all entities | EF Core Migration | |

---

## Api Layer

### Contracts - Invoices

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 150 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Contracts/Invoices/AddLineItemRequest.cs` | API request to add a line item | Record with Description, UnitPrice, Quantity | None | |
| 151 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Contracts/Invoices/CreateInvoiceRequest.cs` | API request to create an invoice | Record with InvoiceNumber, Currency, DueDate, optional UserId (admin override) | None | |
| 152 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Contracts/Invoices/InvoiceResponse.cs` | API response for invoice data | InvoiceResponse record + nested InvoiceLineItemResponse record | None | |

### Contracts - Payments

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 153 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Contracts/Payments/ProcessPaymentRequest.cs` | API request to process a payment | Record with Amount, Currency, PaymentMethod | None | |
| 154 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Contracts/Payments/PaymentResponse.cs` | API response for payment data | Record with all payment fields including TransactionReference and FailureReason | None | |

### Contracts - Subscriptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 155 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Contracts/Subscriptions/CreateSubscriptionRequest.cs` | API request to create a subscription | Record with PlanName, Price, Currency, StartDate, PeriodEnd | None | |
| 156 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Contracts/Subscriptions/SubscriptionResponse.cs` | API response for subscription data | Record with all subscription fields including period dates and CancelledAt | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 157 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Controllers/InvoicesController.cs` | REST controller for invoice CRUD operations | GetAll, GetById, GetByUserId, Create (admin can target other users), AddLineItem, Issue, Cancel; maps DTOs to responses | IMessageBus, ICurrentUserService; permissions: InvoicesRead/Write | |
| 158 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Controllers/MetersController.cs` | REST controller for meter definitions | GetAll returns all meter definitions | IMessageBus; permission: BillingRead | |
| 159 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Controllers/PaymentsController.cs` | REST controller for payment operations | GetById, GetByInvoiceId, ProcessPayment; maps DTOs to responses | IMessageBus, ICurrentUserService; permissions: PaymentsRead/Write | |
| 160 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Controllers/QuotasController.cs` | REST controller for quota management | GetAll (tenant quotas), SetOverride (admin), RemoveOverride (admin); includes SetQuotaOverrideRequest inline record | IMessageBus; permissions: BillingRead/Manage | |
| 161 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Controllers/SubscriptionsController.cs` | REST controller for subscription operations | GetById, GetByUserId, Create, Cancel; maps DTOs to responses | IMessageBus, ICurrentUserService; permissions: SubscriptionsRead/Write | |
| 162 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Controllers/UsageController.cs` | REST controller for usage data | GetAll (optional period filter), GetByMeterCode, GetHistory (date range) | IMessageBus; permission: BillingRead | |

### Middleware

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 163 | [ ] | `src/Modules/Billing/Foundry.Billing.Api/Middleware/MeteringMiddleware.cs` | Request pipeline middleware for quota enforcement and API call counting | Checks quota with HybridCache (30s TTL), blocks with 429 if exceeded, adds rate limit headers, increments api.calls counter on success (status < 400) | IMeteringService, ITenantContext, IMessageBus, HybridCache | |

---

## Test Files

### Foundry.Billing.Tests - GlobalUsings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 164 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/GlobalUsings.cs` | Global using directives for test project | N/A - configuration file | |

### Foundry.Billing.Tests - Domain/Entities

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 165 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Domain/Entities/InvoiceTests.cs` | Unit tests for Invoice aggregate root | Invoice creation, state transitions, line item management, domain event raising | |
| 166 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Domain/Entities/PaymentTests.cs` | Unit tests for Payment aggregate root | Payment creation, Complete/Fail/Refund transitions, validation rules | |
| 167 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Domain/Entities/SubscriptionTests.cs` | Unit tests for Subscription aggregate root | Subscription creation, Renew/MarkPastDue/Cancel/Expire transitions | |

### Foundry.Billing.Tests - Domain/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 168 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Domain/Metering/MeterDefinitionTests.cs` | Unit tests for MeterDefinition entity | Create/Update validation, field assignment | |
| 169 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Domain/Metering/QuotaDefinitionTests.cs` | Unit tests for QuotaDefinition entity | CreatePlanQuota, CreateTenantOverride, UpdateLimit validation | |
| 170 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Domain/Metering/UsageRecordTests.cs` | Unit tests for UsageRecord entity | Create validation, AddValue behavior | |

### Foundry.Billing.Tests - Domain/ValueObjects

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 171 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Domain/ValueObjects/MoneyTests.cs` | Unit tests for Money value object | Create validation, Zero factory, operator+, currency mismatch, equality | |

### Foundry.Billing.Tests - Application/Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 172 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/AddLineItemHandlerTests.cs` | Unit tests for AddLineItemHandler | Line item addition, invoice not found, validation | |
| 173 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/CancelInvoiceHandlerTests.cs` | Unit tests for CancelInvoiceHandler | Invoice cancellation, not found handling | |
| 174 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/CancelSubscriptionHandlerTests.cs` | Unit tests for CancelSubscriptionHandler | Subscription cancellation, not found handling | |
| 175 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/CreateInvoiceHandlerTests.cs` | Unit tests for CreateInvoiceHandler | Invoice creation, duplicate number check, telemetry | |
| 176 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/CreateSubscriptionHandlerTests.cs` | Unit tests for CreateSubscriptionHandler | Subscription creation flow | |
| 177 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/InvoiceEventHandlerTests.cs` | Unit tests for invoice domain event handlers | InvoiceCreated, InvoicePaid, InvoiceOverdue event bridge behavior | |
| 178 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/IssueInvoiceHandlerTests.cs` | Unit tests for IssueInvoiceHandler | Invoice issuance, not found, invalid state | |
| 179 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/MeteringCommandHandlerTests.cs` | Unit tests for metering command handlers | IncrementMeter, SetQuotaOverride, RemoveQuotaOverride | |
| 180 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/PaymentEventHandlerTests.cs` | Unit tests for payment domain event handlers | PaymentCreated event bridge behavior | |
| 181 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Handlers/ProcessPaymentHandlerTests.cs` | Unit tests for ProcessPaymentHandler | Payment processing, overpayment check, auto-mark paid | |

### Foundry.Billing.Tests - Application/EventHandlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 182 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/EventHandlers/InvoiceCreatedDomainEventHandlerTests.cs` | Unit tests for InvoiceCreatedDomainEventHandler | Integration event publishing, telemetry recording | |
| 183 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/EventHandlers/InvoiceOverdueDomainEventHandlerTests.cs` | Unit tests for InvoiceOverdueDomainEventHandler | Integration event publishing with user email enrichment | |
| 184 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/EventHandlers/InvoicePaidDomainEventHandlerTests.cs` | Unit tests for InvoicePaidDomainEventHandler | Integration event publishing with invoice enrichment | |
| 185 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/EventHandlers/PaymentCreatedDomainEventHandlerTests.cs` | Unit tests for PaymentCreatedDomainEventHandler | Integration event publishing with user email | |

### Foundry.Billing.Tests - Application/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 186 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/GetCurrentUsageHandlerTests.cs` | Unit tests for GetCurrentUsageHandler | Usage summary retrieval, meter filtering, quota integration | |
| 187 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/GetMeterDefinitionsHandlerTests.cs` | Unit tests for GetMeterDefinitionsHandler | Meter definition listing and DTO mapping | |
| 188 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/GetQuotaStatusHandlerTests.cs` | Unit tests for GetQuotaStatusHandler | Quota status retrieval, override detection | |
| 189 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/GetUsageHistoryHandlerTests.cs` | Unit tests for GetUsageHistoryHandler | Historical usage retrieval and DTO mapping | |
| 190 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/QuotaThresholdReachedDomainEventHandlerTests.cs` | Unit tests for QuotaThresholdReachedDomainEventHandler | Integration event publishing for threshold alerts | |
| 191 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/RemoveQuotaOverrideHandlerTests.cs` | Unit tests for RemoveQuotaOverrideHandler | Override removal, not found handling | |
| 192 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/RemoveQuotaOverrideValidatorTests.cs` | Unit tests for RemoveQuotaOverrideValidator | Validation rules for TenantId and MeterCode | |
| 193 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/SetQuotaOverrideHandlerTests.cs` | Unit tests for SetQuotaOverrideHandler | Override creation, existing update, meter not found | |
| 194 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/SetQuotaOverrideValidatorTests.cs` | Unit tests for SetQuotaOverrideValidator | Validation rules for all command fields | |
| 195 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Metering/UsageFlushedDomainEventHandlerTests.cs` | Unit tests for UsageFlushedDomainEventHandler | Integration event publishing for flush events | |

### Foundry.Billing.Tests - Application/Queries

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 196 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Queries/GetAllInvoicesHandlerTests.cs` | Unit tests for GetAllInvoicesHandler | All invoices retrieval and mapping | |
| 197 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Queries/GetInvoiceByIdHandlerTests.cs` | Unit tests for GetInvoiceByIdHandler | Single invoice retrieval, not found | |
| 198 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Queries/GetInvoicesByUserIdHandlerTests.cs` | Unit tests for GetInvoicesByUserIdHandler | User invoices retrieval | |
| 199 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Queries/GetPaymentByIdHandlerTests.cs` | Unit tests for GetPaymentByIdHandler | Single payment retrieval, not found | |
| 200 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Queries/GetPaymentsByInvoiceIdHandlerTests.cs` | Unit tests for GetPaymentsByInvoiceIdHandler | Invoice payments retrieval | |
| 201 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Queries/GetSubscriptionByIdHandlerTests.cs` | Unit tests for GetSubscriptionByIdHandler | Single subscription retrieval, not found | |
| 202 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Queries/GetSubscriptionsByUserIdHandlerTests.cs` | Unit tests for GetSubscriptionsByUserIdHandler | User subscriptions retrieval | |

### Foundry.Billing.Tests - Application/Validators

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 203 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/AddLineItemValidatorTests.cs` | Unit tests for AddLineItemValidator | Validation of all AddLineItem command fields | |
| 204 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/BillingValidatorTests.cs` | Shared/cross-cutting validator tests | Common validation patterns across billing validators | |
| 205 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/CancelInvoiceValidatorTests.cs` | Unit tests for CancelInvoiceValidator | Validation of CancelInvoice command fields | |
| 206 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/CancelSubscriptionValidatorTests.cs` | Unit tests for CancelSubscriptionValidator | Validation of CancelSubscription command fields | |
| 207 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/CreateInvoiceValidatorTests.cs` | Unit tests for CreateInvoiceValidator | Validation of CreateInvoice command fields | |
| 208 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/CreateSubscriptionValidatorTests.cs` | Unit tests for CreateSubscriptionValidator | Validation of CreateSubscription command fields | |
| 209 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/IssueInvoiceValidatorTests.cs` | Unit tests for IssueInvoiceValidator | Validation of IssueInvoice command fields | |
| 210 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Application/Validators/ProcessPaymentValidatorTests.cs` | Unit tests for ProcessPaymentValidator | Validation of ProcessPayment command fields | |

### Foundry.Billing.Tests - Api/Contracts

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 211 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Contracts/RequestContractTests.cs` | Contract tests for API request records | Request record structure, field presence | |
| 212 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Contracts/ResponseContractTests.cs` | Contract tests for API response records | Response record structure, field presence | |

### Foundry.Billing.Tests - Api/Controllers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 213 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Controllers/InvoicesControllerTests.cs` | Unit tests for InvoicesController | All invoice endpoints, auth checks, DTO mapping | |
| 214 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Controllers/MetersControllerTests.cs` | Unit tests for MetersController | Meter definitions endpoint | |
| 215 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Controllers/PaymentsControllerTests.cs` | Unit tests for PaymentsController | Payment endpoints, auth checks | |
| 216 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Controllers/QuotasControllerTests.cs` | Unit tests for QuotasController | Quota status, set/remove override endpoints | |
| 217 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Controllers/SubscriptionsControllerTests.cs` | Unit tests for SubscriptionsController | Subscription endpoints, auth checks | |
| 218 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Controllers/UsageControllerTests.cs` | Unit tests for UsageController | Usage endpoints, period filtering, history | |

### Foundry.Billing.Tests - Api/Extensions

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 219 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Extensions/ResultExtensionsTests.cs` | Unit tests for Result-to-ActionResult extensions | ToActionResult mapping for success/failure/not-found/validation errors | |

### Foundry.Billing.Tests - Api/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 220 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Api/Metering/MeteringMiddlewareTests.cs` | Unit tests for MeteringMiddleware | Quota blocking (429), warning headers, rate limit headers, counter increment on success, non-API route skip | |

### Foundry.Billing.Tests - Infrastructure/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 221 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Metering/FlushUsageJobTests.cs` | Unit tests for FlushUsageJob | Key processing, atomic get-and-reset, upsert logic, event publishing | |
| 222 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Metering/FlushUsageJobAdditionalTests.cs` | Additional unit tests for FlushUsageJob edge cases | Invalid key formats, cancellation, error handling | |
| 223 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Metering/FlushUsageJobExceptionTests.cs` | Exception handling tests for FlushUsageJob | Error recovery, partial flush scenarios | |
| 224 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Metering/ValkeyMeteringServiceTests.cs` | Unit tests for ValkeyMeteringService | Increment, CheckQuota, GetCurrentUsage, threshold events, period key generation | |

### Foundry.Billing.Tests - Infrastructure/Persistence

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 225 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/BillingDbContextFactoryTests.cs` | Unit tests for BillingDbContextFactory | Design-time context creation | |
| 226 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/DesignTimeTenantContextTests.cs` | Unit tests for DesignTimeTenantContext | Mock tenant context property values | |
| 227 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/InvoiceRepositoryExtensionsTests.cs` | Unit tests for InvoiceRepositoryExtensions | Custom field JSON querying | |
| 228 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/InvoiceRepositoryTests.cs` | Unit tests for InvoiceRepository | CRUD operations, compiled query, line item loading | |
| 229 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/MeterDefinitionRepositoryTests.cs` | Unit tests for MeterDefinitionRepository | GetByCode, GetAll operations | |
| 230 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/MeteringDbSeederTests.cs` | Unit tests for MeteringDbSeeder | Seed idempotency, default meters and quotas | |
| 231 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/PaymentRepositoryTests.cs` | Unit tests for PaymentRepository | CRUD operations, invoice ID filtering | |
| 232 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/QuotaDefinitionRepositoryTests.cs` | Unit tests for QuotaDefinitionRepository | Effective quota resolution, tenant override priority | |
| 233 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Persistence/SubscriptionRepositoryTests.cs` | Unit tests for SubscriptionRepository | CRUD, GetActiveByUserId filtering | |

### Foundry.Billing.Tests - Infrastructure/Services

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 234 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/InvoiceQueryServiceTests.cs` | Unit tests for InvoiceQueryService | Dapper revenue/count/pending/outstanding queries | |
| 235 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/InvoiceReportServiceTests.cs` | Unit tests for InvoiceReportService | Dapper invoice report generation | |
| 236 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/MeteringQueryServiceTests.cs` | Unit tests for MeteringQueryService | Quota checking via PostgreSQL, period bounds | |
| 237 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/PaymentReportServiceTests.cs` | Unit tests for PaymentReportService | Dapper payment report generation | |
| 238 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/RevenueReportServiceTests.cs` | Unit tests for RevenueReportService | Dapper revenue aggregation | |
| 239 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/SubscriptionQueryServiceTests.cs` | Unit tests for SubscriptionQueryService | Active plan code lookup, error handling | |
| 240 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/UsageReportServiceTests.cs` | Unit tests for UsageReportService | Usage report LINQ query, meter join | |
| 241 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Services/ValkeyMeteringServiceAdditionalTests.cs` | Additional unit tests for ValkeyMeteringService | Edge cases, error paths, fire-and-forget behavior | |

### Foundry.Billing.Tests - Infrastructure/Workflows

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 242 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/Workflows/InvoiceCreatedTriggerTests.cs` | Unit tests for InvoiceCreatedTrigger | Workflow activity execution, module name | |

### Foundry.Billing.Tests - Infrastructure (Root)

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 243 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Infrastructure/DapperQueryTests.cs` | Tests for Dapper raw SQL queries | SQL correctness, parameter binding, tenant filtering | |

### Foundry.Billing.Tests - Integration/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 244 | [ ] | `tests/Modules/Billing/Foundry.Billing.Tests/Integration/Metering/UsageRecordRepositoryTests.cs` | Integration tests for UsageRecordRepository | Full DB round-trip for usage records with Testcontainers | |
