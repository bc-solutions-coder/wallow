# Phase 9: Billing Module

**Scope:** Complete Billing module - Domain, Application, Infrastructure, Api layers + all tests
**Status:** Not Started
**Files:** 192 source files, 83 test files

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
| 1 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Entities/Invoice.cs` | Invoice aggregate root with state machine (Draft->Issued->Paid/Overdue/Cancelled) | Create, AddLineItem, RemoveLineItem, Issue, MarkAsPaid, MarkAsOverdue, Cancel; RecalculateTotal on line item changes | AggregateRoot, ITenantScoped, IHasCustomFields, Money, InvoiceLineItem, domain events | |
| 2 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Entities/InvoiceLineItem.cs` | Child entity of Invoice representing a single billable line | Create factory with validation; computes LineTotal = UnitPrice * Quantity | Entity, Money, InvoiceId | |
| 3 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Entities/Payment.cs` | Payment aggregate root tracking transactions against invoices | Create, Complete, Fail, Refund; state machine Pending->Completed/Failed, Completed->Refunded | AggregateRoot, ITenantScoped, IHasCustomFields, Money, PaymentMethod/Status enums, domain events | |
| 4 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Entities/Subscription.cs` | Subscription aggregate root for recurring billing plans | Create, Renew, MarkPastDue, Cancel, Expire; state machine Active->PastDue/Cancelled/Expired | AggregateRoot, ITenantScoped, IHasCustomFields, Money, SubscriptionStatus enum, domain events | |

### Value Objects

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 5 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/ValueObjects/Money.cs` | Immutable money value object with currency enforcement | Create (non-negative, 3-letter ISO), Zero factory, operator+ (currency mismatch throws), GetEqualityComponents | ValueObject base from Shared.Kernel | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 6 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Enums/InvoiceStatus.cs` | Invoice lifecycle states | Draft=0, Issued=1, Paid=2, Overdue=3, Cancelled=4 | None | |
| 7 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Enums/PaymentMethod.cs` | Payment method types | CreditCard=0, BankTransfer=1, PayPal=2 | None | |
| 8 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Enums/PaymentStatus.cs` | Payment lifecycle states | Pending=0, Completed=1, Failed=2, Refunded=3 | None | |
| 9 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Enums/SubscriptionStatus.cs` | Subscription lifecycle states | Active=0, PastDue=1, Cancelled=2, Expired=3 | None | |

### Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 10 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Events/InvoiceCreatedDomainEvent.cs` | Raised when invoice is created | Record with InvoiceId, UserId, TotalAmount, Currency | DomainEvent base | |
| 11 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Events/InvoiceOverdueDomainEvent.cs` | Raised when invoice is marked overdue | Record with InvoiceId, UserId, DueDate | DomainEvent base | |
| 12 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Events/InvoicePaidDomainEvent.cs` | Raised when invoice is paid | Record with InvoiceId, PaymentId, PaidAt | DomainEvent base | |
| 13 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Events/PaymentCreatedDomainEvent.cs` | Raised when payment is created | Record with PaymentId, InvoiceId, Amount, Currency, UserId | DomainEvent base | |
| 14 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Events/PaymentFailedDomainEvent.cs` | Raised when payment fails | Record with PaymentId, InvoiceId, Reason, UserId | DomainEvent base | |
| 15 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Events/SubscriptionCancelledDomainEvent.cs` | Raised when subscription is cancelled | Record with SubscriptionId, UserId, CancelledAt | DomainEvent base | |
| 16 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Events/SubscriptionCreatedDomainEvent.cs` | Raised when subscription is created | Record with SubscriptionId, UserId, PlanName, Amount, Currency | DomainEvent base | |

### Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Exceptions/InvalidInvoiceException.cs` | Domain exception for invalid invoice operations | Wraps message with code "Billing.InvalidInvoice" | DomainException base | |
| 18 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Exceptions/InvalidPaymentException.cs` | Domain exception for invalid payment operations | Wraps message with code "Billing.InvalidPayment" | DomainException base | |
| 19 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Exceptions/InvalidSubscriptionStatusTransitionException.cs` | Domain exception for invalid subscription state transitions | Formats from/to status in message | DomainException base | |

### Identity (Strongly-Typed IDs)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 20 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Identity/InvoiceId.cs` | Strongly-typed ID for Invoice | readonly record struct with Create and New factory methods | IStronglyTypedId | |
| 21 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Identity/InvoiceLineItemId.cs` | Strongly-typed ID for InvoiceLineItem | readonly record struct with Create and New factory methods | IStronglyTypedId | |
| 22 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Identity/PaymentId.cs` | Strongly-typed ID for Payment | readonly record struct with Create and New factory methods | IStronglyTypedId | |
| 23 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Identity/SubscriptionId.cs` | Strongly-typed ID for Subscription | readonly record struct with Create and New factory methods | IStronglyTypedId | |

### CustomFields - Entity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 24 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/CustomFields/Entities/CustomFieldDefinition.cs` | Aggregate root defining a tenant-configured custom field for an entity type | Create with validation (entity type via CustomFieldRegistry, snake_case field key max 50, display name max 100); Update/Deactivate/Activate methods; ValidationRules and Options stored as JSON; raises Created/Deactivated events | AggregateRoot, ITenantScoped, CustomFieldType, FieldValidationRules, CustomFieldOption, domain events | |

### CustomFields - Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 25 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/CustomFields/Events/CustomFieldDefinitionCreatedEvent.cs` | Raised when a custom field definition is created | Record with DefinitionId, TenantId, EntityType, FieldKey, DisplayName, FieldType | DomainEvent base | |
| 26 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/CustomFields/Events/CustomFieldDefinitionDeactivatedEvent.cs` | Raised when a custom field definition is deactivated | Record with DefinitionId, TenantId, EntityType, FieldKey | DomainEvent base | |

### CustomFields - Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 27 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/CustomFields/Exceptions/CustomFieldException.cs` | Domain exception for custom field validation violations | Wraps message with code "Billing.CustomField" | BusinessRuleException base | |

### CustomFields - Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 28 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/CustomFields/Identity/CustomFieldDefinitionId.cs` | Strongly-typed ID for CustomFieldDefinition | readonly record struct with Create and New factory methods | IStronglyTypedId | |

### Metering - Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 29 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Entities/MeterDefinition.cs` | Defines a trackable/limitable meter (e.g., api.calls, storage.bytes) | Create with validation, Update method; Code, DisplayName, Unit, Aggregation, IsBillable, ValkeyKeyPattern | AuditableEntity, MeterAggregation enum | |
| 30 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Entities/QuotaDefinition.cs` | Defines usage limits per plan or tenant override | CreatePlanQuota, CreateTenantOverride, UpdateLimit; tenant overrides take precedence | AuditableEntity, ITenantScoped, QuotaPeriod/QuotaAction enums | |
| 31 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Entities/UsageRecord.cs` | Billing-grade usage record flushed from Valkey to PostgreSQL | Create with period validation, AddValue for upsert operations | Entity, ITenantScoped | |

### Metering - Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Enums/MeterAggregation.cs` | Aggregation strategy for meter values | Sum=0 (total calls), Max=1 (peak storage) | None | |
| 33 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Enums/QuotaAction.cs` | Action when quota exceeded | Block=0 (429 response), Warn=1 (warning header), Throttle=2 (rate limit) | None | |
| 34 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Enums/QuotaPeriod.cs` | Time period for quota evaluation | Hourly=0, Daily=1, Monthly=2 | None | |

### Metering - Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 35 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Events/QuotaThresholdReachedEvent.cs` | Raised when usage hits quota threshold (80%, 90%, 100%) | Record with TenantId, MeterCode, MeterDisplayName, CurrentUsage, Limit, PercentUsed | DomainEvent base | |
| 36 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Events/UsageFlushedEvent.cs` | Raised when usage is flushed from Valkey to PostgreSQL | Record with FlushedAt, RecordCount | DomainEvent base | |

### Metering - Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 37 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Identity/MeterDefinitionId.cs` | Strongly-typed ID for MeterDefinition | readonly record struct with Create and New | IStronglyTypedId | |
| 38 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Identity/QuotaDefinitionId.cs` | Strongly-typed ID for QuotaDefinition | readonly record struct with Create and New | IStronglyTypedId | |
| 39 | [ ] | `src/Modules/Billing/Wallow.Billing.Domain/Metering/Identity/UsageRecordId.cs` | Strongly-typed ID for UsageRecord | readonly record struct with Create and New | IStronglyTypedId | |

---

## Application Layer

### Commands - AddLineItem

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 40 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/AddLineItem/AddLineItemCommand.cs` | Command record to add a line item to an invoice | Fields: InvoiceId, Description, UnitPrice, Quantity, UpdatedByUserId | None | |
| 41 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/AddLineItem/AddLineItemHandler.cs` | Wolverine handler for adding line items | Loads invoice with line items, creates Money, calls invoice.AddLineItem, saves | IInvoiceRepository, TimeProvider | |
| 42 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/AddLineItem/AddLineItemValidator.cs` | FluentValidation validator for AddLineItemCommand | Validates InvoiceId, Description (max 500), UnitPrice >= 0, Quantity > 0, UpdatedByUserId | FluentValidation | |

### Commands - CancelInvoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 43 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CancelInvoice/CancelInvoiceCommand.cs` | Command record to cancel an invoice | Fields: InvoiceId, CancelledByUserId | None | |
| 44 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CancelInvoice/CancelInvoiceHandler.cs` | Wolverine handler for cancelling invoices | Loads invoice, calls invoice.Cancel, saves | IInvoiceRepository, TimeProvider | |
| 45 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CancelInvoice/CancelInvoiceValidator.cs` | FluentValidation validator for CancelInvoiceCommand | Validates InvoiceId and CancelledByUserId not empty | FluentValidation | |

### Commands - CancelSubscription

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 46 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CancelSubscription/CancelSubscriptionCommand.cs` | Command record to cancel a subscription | Fields: SubscriptionId, CancelledByUserId | None | |
| 47 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CancelSubscription/CancelSubscriptionHandler.cs` | Wolverine handler for cancelling subscriptions | Loads subscription, calls subscription.Cancel, saves | ISubscriptionRepository, TimeProvider | |
| 48 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CancelSubscription/CancelSubscriptionValidator.cs` | FluentValidation validator for CancelSubscriptionCommand | Validates SubscriptionId and CancelledByUserId not empty | FluentValidation | |

### Commands - CreateInvoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 49 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CreateInvoice/CreateInvoiceCommand.cs` | Command record to create an invoice | Fields: UserId, InvoiceNumber, Currency, DueDate, CustomFields | None | |
| 50 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CreateInvoice/CreateInvoiceHandler.cs` | Wolverine handler for creating invoices | Checks duplicate invoice number, calls Invoice.Create, saves; OTel activity tracing | IInvoiceRepository, TimeProvider, BillingModuleTelemetry | |
| 51 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CreateInvoice/CreateInvoiceValidator.cs` | FluentValidation validator for CreateInvoiceCommand | Validates UserId, InvoiceNumber (max 50), Currency (exactly 3 chars) | FluentValidation | |

### Commands - CreateSubscription

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 52 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CreateSubscription/CreateSubscriptionCommand.cs` | Command record to create a subscription | Fields: UserId, PlanName, Price, Currency, StartDate, PeriodEnd, CustomFields | None | |
| 53 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CreateSubscription/CreateSubscriptionHandler.cs` | Wolverine handler for creating subscriptions | Creates Money, calls Subscription.Create, saves | ISubscriptionRepository, TimeProvider | |
| 54 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/CreateSubscription/CreateSubscriptionValidator.cs` | FluentValidation validator for CreateSubscriptionCommand | Validates UserId, PlanName (max 100), Price >= 0, Currency (3 chars), PeriodEnd > StartDate | FluentValidation | |

### Commands - IssueInvoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 55 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/IssueInvoice/IssueInvoiceCommand.cs` | Command record to issue (activate) an invoice | Fields: InvoiceId, IssuedByUserId | None | |
| 56 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/IssueInvoice/IssueInvoiceHandler.cs` | Wolverine handler for issuing invoices | Loads invoice with line items, calls invoice.Issue, saves | IInvoiceRepository, TimeProvider | |
| 57 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/IssueInvoice/IssueInvoiceValidator.cs` | FluentValidation validator for IssueInvoiceCommand | Validates InvoiceId and IssuedByUserId not empty | FluentValidation | |

### Commands - ProcessPayment

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 58 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/ProcessPayment/ProcessPaymentCommand.cs` | Command record to process a payment | Fields: InvoiceId, UserId, Amount, Currency, PaymentMethod, CustomFields | None | |
| 59 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/ProcessPayment/ProcessPaymentHandler.cs` | Wolverine handler for processing payments | Validates overpayment/currency mismatch, creates Payment, auto-marks invoice paid if fully covered | IPaymentRepository, IInvoiceRepository, TimeProvider | |
| 60 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Commands/ProcessPayment/ProcessPaymentValidator.cs` | FluentValidation validator for ProcessPaymentCommand | Validates InvoiceId, UserId, Amount > 0, Currency (3 chars), PaymentMethod not empty | FluentValidation | |

### DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 61 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/DTOs/InvoiceDto.cs` | DTO for Invoice with line items and custom fields | Record with all invoice fields including LineItems list and CustomFields | None | |
| 62 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/DTOs/InvoiceLineItemDto.cs` | DTO for InvoiceLineItem | Record with Id, Description, UnitPrice, Currency, Quantity, LineTotal | None | |
| 63 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/DTOs/PaymentDto.cs` | DTO for Payment with custom fields | Record with all payment fields including CustomFields | None | |
| 64 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/DTOs/SubscriptionDto.cs` | DTO for Subscription with custom fields | Record with all subscription fields including CustomFields | None | |

### EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 65 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/InvoiceCreatedDomainEventHandler.cs` | Bridges InvoiceCreatedDomainEvent to integration event | Enriches with invoice data, publishes InvoiceCreatedEvent via IMessageBus, records telemetry metrics | IInvoiceRepository, IMessageBus, BillingModuleTelemetry | |
| 66 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/InvoiceOverdueDomainEventHandler.cs` | Bridges InvoiceOverdueDomainEvent to integration event | Enriches with user email via IUserQueryService, publishes InvoiceOverdueEvent | IInvoiceRepository, IUserQueryService, IMessageBus | |
| 67 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/InvoicePaidDomainEventHandler.cs` | Bridges InvoicePaidDomainEvent to integration event | Enriches with invoice data, publishes InvoicePaidEvent | IInvoiceRepository, IMessageBus | |
| 68 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/EventHandlers/PaymentCreatedDomainEventHandler.cs` | Bridges PaymentCreatedDomainEvent to integration event | Gets user email, publishes PaymentReceivedEvent via IMessageBus | IMessageBus, ITenantContext, IUserQueryService | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 69 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Extensions/ApplicationExtensions.cs` | DI registration for Application layer | AddBillingApplication registers FluentValidation validators from assembly | FluentValidation, IServiceCollection | |

### Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 70 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Interfaces/IInvoiceRepository.cs` | Repository interface for Invoice aggregate | GetById, GetByIdWithLineItems, GetByUserId, GetAll, ExistsByInvoiceNumber, Add, Update, Remove, SaveChanges | Invoice, InvoiceId | |
| 71 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Interfaces/IPaymentRepository.cs` | Repository interface for Payment aggregate | GetById, GetByInvoiceId, GetByUserId, GetAll, Add, Update, SaveChanges | Payment, PaymentId, InvoiceId | |
| 72 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Interfaces/ISubscriptionRepository.cs` | Repository interface for Subscription aggregate | GetById, GetByUserId, GetAll, GetActiveByUserId, Add, Update, SaveChanges | Subscription, SubscriptionId | |

### Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 73 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Mappings/InvoiceMappings.cs` | Extension methods mapping Invoice/InvoiceLineItem to DTOs | ToDto() on Invoice and InvoiceLineItem; maps Money to separate Amount/Currency fields | InvoiceDto, InvoiceLineItemDto | |
| 74 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Mappings/PaymentMappings.cs` | Extension method mapping Payment to DTO | ToDto() on Payment; maps Money, enums to strings | PaymentDto | |
| 75 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Mappings/SubscriptionMappings.cs` | Extension method mapping Subscription to DTO | ToDto() on Subscription; maps Money, enums to strings | SubscriptionDto | |

### Queries - Invoice

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 76 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetAllInvoices/GetAllInvoicesQuery.cs` | Query record to get all invoices | Empty record (parameterless) | None | |
| 77 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetAllInvoices/GetAllInvoicesHandler.cs` | Handler returning all invoices as DTOs | Gets all from repository, maps to DTOs | IInvoiceRepository | |
| 78 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetInvoiceById/GetInvoiceByIdQuery.cs` | Query record to get invoice by ID | Record with InvoiceId field | None | |
| 79 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetInvoiceById/GetInvoiceByIdHandler.cs` | Handler returning single invoice with line items | Gets by ID with line items, returns NotFound or DTO | IInvoiceRepository | |
| 80 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetInvoicesByUserId/GetInvoicesByUserIdQuery.cs` | Query record to get invoices by user | Record with UserId field | None | |
| 81 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetInvoicesByUserId/GetInvoicesByUserIdHandler.cs` | Handler returning user's invoices as DTOs | Gets by user ID from repository, maps to DTOs | IInvoiceRepository | |

### Queries - Payment

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 82 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetPaymentById/GetPaymentByIdQuery.cs` | Query record to get payment by ID | Record with PaymentId field | None | |
| 83 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetPaymentById/GetPaymentByIdHandler.cs` | Handler returning single payment as DTO | Gets by ID, returns NotFound or DTO | IPaymentRepository | |
| 84 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetPaymentsByInvoiceId/GetPaymentsByInvoiceIdQuery.cs` | Query record to get payments by invoice | Record with InvoiceId field | None | |
| 85 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetPaymentsByInvoiceId/GetPaymentsByInvoiceIdHandler.cs` | Handler returning invoice's payments as DTOs | Gets by invoice ID from repository, maps to DTOs | IPaymentRepository | |

### Queries - Subscription

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 86 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetSubscriptionById/GetSubscriptionByIdQuery.cs` | Query record to get subscription by ID | Record with SubscriptionId field | None | |
| 87 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetSubscriptionById/GetSubscriptionByIdHandler.cs` | Handler returning single subscription as DTO | Gets by ID, returns NotFound or DTO | ISubscriptionRepository | |
| 88 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetSubscriptionsByUserId/GetSubscriptionsByUserIdQuery.cs` | Query record to get subscriptions by user | Record with UserId field | None | |
| 89 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Queries/GetSubscriptionsByUserId/GetSubscriptionsByUserIdHandler.cs` | Handler returning user's subscriptions as DTOs | Gets by user ID from repository, maps to DTOs | ISubscriptionRepository | |

### Telemetry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 90 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Telemetry/BillingModuleTelemetry.cs` | OTel instrumentation for Billing module | ActivitySource "Billing", Meter "Billing"; InvoicesCreatedTotal counter, InvoiceAmount histogram | Shared.Kernel.Diagnostics | |

### Metering - Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 91 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/IncrementMeter/IncrementMeterCommand.cs` | Command record to increment a meter counter | Record with MeterCode and Value (default 1) | None | |
| 92 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/IncrementMeter/IncrementMeterHandler.cs` | Handler delegating to IMeteringService | Calls meteringService.IncrementAsync; swallows exceptions with warning log | IMeteringService | |
| 93 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/SetQuotaOverride/SetQuotaOverrideCommand.cs` | Command record to set tenant-specific quota override | Record with TenantId, MeterCode, Limit, Period, OnExceeded | QuotaPeriod, QuotaAction enums | |
| 94 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/SetQuotaOverride/SetQuotaOverrideHandler.cs` | Handler for setting quota overrides | Validates meter exists, upserts quota (updates existing or creates new tenant override) | IQuotaDefinitionRepository, IMeterDefinitionRepository | |
| 95 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/SetQuotaOverride/SetQuotaOverrideValidator.cs` | FluentValidation validator for SetQuotaOverrideCommand | Validates TenantId, MeterCode (max 100), Limit >= 0, Period/OnExceeded are valid enums | FluentValidation | |
| 96 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/RemoveQuotaOverride/RemoveQuotaOverrideCommand.cs` | Command record to remove tenant quota override | Record with TenantId, MeterCode | None | |
| 97 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/RemoveQuotaOverride/RemoveQuotaOverrideHandler.cs` | Handler for removing quota overrides | Finds tenant override, removes it; returns NotFound if not exists | IQuotaDefinitionRepository | |
| 98 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Commands/RemoveQuotaOverride/RemoveQuotaOverrideValidator.cs` | FluentValidation validator for RemoveQuotaOverrideCommand | Validates TenantId and MeterCode (max 100) not empty | FluentValidation | |

### Metering - DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 99 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/DTOs/MeterDefinitionDto.cs` | DTO for meter definitions | Record with Id, Code, DisplayName, Unit, Aggregation, IsBillable | None | |
| 100 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/DTOs/QuotaCheckResult.cs` | Result of a quota check operation | IsAllowed, CurrentUsage, Limit, PercentUsed, ActionIfExceeded; static Unlimited factory | QuotaAction enum | |
| 101 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/DTOs/QuotaStatusDto.cs` | Status of a quota for a tenant | Record with MeterCode, CurrentUsage, Limit, PercentUsed, Period, OnExceeded, IsOverride | None | |
| 102 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/DTOs/UsageRecordDto.cs` | DTO for historical usage records | Record with Id, TenantId, MeterCode, PeriodStart, PeriodEnd, Value, FlushedAt | None | |
| 103 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/DTOs/UsageSummaryDto.cs` | Summary of current usage across meters | Record with MeterCode, DisplayName, Unit, CurrentValue, Limit, PercentUsed, Period | None | |

### Metering - EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 104 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/EventHandlers/QuotaThresholdReachedDomainEventHandler.cs` | Bridges QuotaThresholdReachedEvent to integration event | Publishes Shared.Contracts.Metering.Events.QuotaThresholdReachedEvent for Notifications module | IMessageBus | |
| 105 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/EventHandlers/UsageFlushedDomainEventHandler.cs` | Bridges UsageFlushedEvent to integration event | Publishes Shared.Contracts.Metering.Events.UsageFlushedEvent | IMessageBus | |

### Metering - Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 106 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Interfaces/IMeterDefinitionRepository.cs` | Repository interface for MeterDefinition | GetById, GetByCode, GetAll, Add, SaveChanges | MeterDefinition, MeterDefinitionId | |
| 107 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Interfaces/IQuotaDefinitionRepository.cs` | Repository interface for QuotaDefinition | GetById, GetEffectiveQuota (tenant override > plan default), GetTenantOverride, GetAllForTenant, Add, Remove, SaveChanges | QuotaDefinition, QuotaDefinitionId | |
| 108 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Interfaces/IUsageRecordRepository.cs` | Repository interface for UsageRecord | GetById, GetHistory, GetForPeriod, Add, Update, SaveChanges | UsageRecord, UsageRecordId | |

### Metering - Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 109 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetCurrentUsage/GetCurrentUsageQuery.cs` | Query for current usage optionally filtered by meter/period | Record with optional MeterCode and Period | QuotaPeriod enum | |
| 110 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetCurrentUsage/GetCurrentUsageHandler.cs` | Handler returning current usage summaries | Iterates meters, gets current value from Valkey + quota check, builds UsageSummaryDto list | IMeterDefinitionRepository, IMeteringService | |
| 111 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetMeterDefinitions/GetMeterDefinitionsQuery.cs` | Query for all meter definitions | Empty record (parameterless) | None | |
| 112 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetMeterDefinitions/GetMeterDefinitionsHandler.cs` | Handler returning all meter definitions as DTOs | Gets all from repository, maps to MeterDefinitionDto | IMeterDefinitionRepository | |
| 113 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetQuotaStatus/GetQuotaStatusQuery.cs` | Query for quota status of current tenant | Empty record (parameterless) | None | |
| 114 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetQuotaStatus/GetQuotaStatusHandler.cs` | Handler returning quota status for all meters with configured quotas | Iterates meters, checks quotas via IMeteringService, checks for tenant overrides | IQuotaDefinitionRepository, IMeterDefinitionRepository, IMeteringService | |
| 115 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetUsageHistory/GetUsageHistoryQuery.cs` | Query for historical usage records | Record with MeterCode, From, To | None | |
| 116 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Queries/GetUsageHistory/GetUsageHistoryHandler.cs` | Handler returning historical usage records as DTOs | Gets from repository by meter/date range, maps to UsageRecordDto | IUsageRecordRepository | |

### Metering - Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 117 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Metering/Services/IMeteringService.cs` | Core metering service interface | IncrementAsync, CheckQuotaAsync, GetCurrentUsageAsync (Valkey), GetUsageHistoryAsync (PostgreSQL) | QuotaCheckResult, UsageRecordDto, QuotaPeriod | |

### CustomFields - Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 118 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/CreateCustomFieldDefinition/CreateCustomFieldDefinitionCommand.cs` | Command + handler to create a custom field definition | Checks FieldKeyExists, calls CustomFieldDefinition.Create with optional description/required/rules/options, saves; returns Result<CustomFieldDefinitionDto> | ICustomFieldDefinitionRepository, ITenantContext, ICurrentUserService, TimeProvider | |
| 119 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/CreateCustomFieldDefinition/CreateCustomFieldDefinitionValidator.cs` | FluentValidation validator for CreateCustomFieldDefinitionCommand | Validates EntityType, FieldKey, DisplayName, FieldType | FluentValidation | |
| 120 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/DeactivateCustomFieldDefinition/DeactivateCustomFieldDefinitionCommand.cs` | Command + handler to deactivate a custom field definition | Looks up by ID, calls definition.Deactivate, saves | ICustomFieldDefinitionRepository, ICurrentUserService, TimeProvider | |
| 121 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/DeactivateCustomFieldDefinition/DeactivateCustomFieldDefinitionValidator.cs` | FluentValidation validator for DeactivateCustomFieldDefinitionCommand | Validates Id not empty | FluentValidation | |
| 122 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/ReorderCustomFields/ReorderCustomFieldsCommand.cs` | Command + handler to reorder custom field definitions | Sets DisplayOrder by position in provided FieldIds list for given EntityType | ICustomFieldDefinitionRepository, ICurrentUserService, TimeProvider | |
| 123 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/ReorderCustomFields/ReorderCustomFieldsValidator.cs` | FluentValidation validator for ReorderCustomFieldsCommand | Validates EntityType and FieldIds not empty | FluentValidation | |
| 124 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/UpdateCustomFieldDefinition/UpdateCustomFieldDefinitionCommand.cs` | Command + handler to update a custom field definition | Updates DisplayName, Description, IsRequired, DisplayOrder, ValidationRules, Options selectively | ICustomFieldDefinitionRepository, ICurrentUserService, TimeProvider | |
| 125 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Commands/UpdateCustomFieldDefinition/UpdateCustomFieldDefinitionValidator.cs` | FluentValidation validator for UpdateCustomFieldDefinitionCommand | Validates Id not empty; optional field constraints | FluentValidation | |

### CustomFields - DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 126 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/DTOs/CustomFieldDefinitionDto.cs` | DTO for custom field definitions | Record with Id, EntityType, FieldKey, DisplayName, Description, FieldType, DisplayOrder, IsRequired, IsActive, ValidationRules, Options, timestamps | CustomFieldType, FieldValidationRules, CustomFieldOption | |
| 127 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/DTOs/CustomFieldDefinitionMapper.cs` | Extension method mapping CustomFieldDefinition to DTO | ToDto() on CustomFieldDefinition; deserializes ValidationRulesJson and OptionsJson | CustomFieldDefinitionDto | |

### CustomFields - Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 128 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Interfaces/ICustomFieldDefinitionRepository.cs` | Repository interface for CustomFieldDefinition | GetByIdAsync, GetByEntityTypeAsync, FieldKeyExistsAsync, AddAsync, UpdateAsync, SaveChangesAsync | CustomFieldDefinition, CustomFieldDefinitionId | |

### CustomFields - Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 129 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Queries/GetCustomFieldDefinitionById/GetCustomFieldDefinitionByIdQuery.cs` | Query record to get a single custom field definition | Record with Id (Guid) | None | |
| 130 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Queries/GetCustomFieldDefinitionById/GetCustomFieldDefinitionByIdHandler.cs` | Handler returning single definition as DTO | Gets by ID, returns null if not found | ICustomFieldDefinitionRepository | |
| 131 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Queries/GetCustomFieldDefinitions/GetCustomFieldDefinitionsQuery.cs` | Query record to get definitions by entity type | Record with EntityType, IncludeInactive | None | |
| 132 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Queries/GetCustomFieldDefinitions/GetCustomFieldDefinitionsHandler.cs` | Handler returning definitions for an entity type | Gets by entity type from repository, maps to DTOs | ICustomFieldDefinitionRepository | |

### CustomFields - Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 133 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/CustomFields/Services/CustomFieldValidator.cs` | Service validating custom field values on entities | ValidateAsync<T> checks values against definitions for tenant; validates required fields, type constraints, validation rules | ICustomFieldDefinitionRepository, ITenantContext | |

### Settings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 134 | [ ] | `src/Modules/Billing/Wallow.Billing.Application/Settings/BillingSettingKeys.cs` | Declares billing module setting definitions | DefaultCurrency (USD), InvoicePrefix (INV-), DateFormat (YYYY-MM-DD), PaymentRetryAttempts (3) | SettingRegistryBase, SettingDefinition | |

---

## Infrastructure Layer

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 135 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Extensions/BillingInfrastructureExtensions.cs` | DI registration for Infrastructure layer | Registers BillingDbContext (Npgsql with retry/timeout), all repositories, all services (query/report/metering), FlushUsageJob | All repository and service interfaces | |
| 136 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Extensions/BillingModuleExtensions.cs` | Top-level module registration and initialization | AddBillingModule (DI), InitializeBillingModuleAsync (auto-migrate in Dev/Testing, seed metering data) | BillingDbContext, MeteringDbSeeder | |

### Jobs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 137 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs` | Hangfire job flushing Valkey counters to PostgreSQL | Enumerates meter index Set, atomic get-and-reset per key, upserts UsageRecord, publishes UsageFlushedEvent | IConnectionMultiplexer, IUsageRecordRepository, IMessageBus, ITenantContextFactory, TimeProvider | |

### Persistence - DbContext

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 138 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContext.cs` | EF Core DbContext for billing schema | DbSets for Invoice, Payment, Subscription, InvoiceLineItem, MeterDefinition, QuotaDefinition, UsageRecord; NoTracking default; billing schema | TenantAwareDbContext base | |
| 139 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContextFactory.cs` | Design-time factory for EF Core migrations | Creates BillingDbContext with placeholder connection string and mock tenant context | IDesignTimeDbContextFactory | |
| 140 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/DesignTimeTenantContext.cs` | Mock ITenantContext for design-time migrations | Returns empty GUID TenantId and "design-time" name | ITenantContext | |
| 141 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/MeteringDbSeeder.cs` | Seeds default meter definitions and plan quotas | Seeds 4 meters (api.calls, storage.bytes, users.active, workflows.executions) and 9 plan quotas (free/pro/enterprise tiers) | BillingDbContext, MeterDefinition, QuotaDefinition | |

### Persistence - Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 142 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/InvoiceConfiguration.cs` | EF Core config for Invoice entity | Table "invoices"; StronglyTypedId conversion; OwnsOne for TotalAmount (Money); CustomFields as jsonb; unique index on TenantId+InvoiceNumber | StronglyTypedIdConverter, DictionaryValueComparer | |
| 143 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/InvoiceLineItemConfiguration.cs` | EF Core config for InvoiceLineItem entity | Table "invoice_line_items"; OwnsOne for UnitPrice and LineTotal (Money); FK to Invoice | StronglyTypedIdConverter | |
| 144 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs` | EF Core config for Payment entity | Table "payments"; OwnsOne for Amount (Money); CustomFields as jsonb; indexes on TenantId, InvoiceId, UserId, Status | StronglyTypedIdConverter, DictionaryValueComparer | |
| 145 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/SubscriptionConfiguration.cs` | EF Core config for Subscription entity | Table "subscriptions"; OwnsOne for Price (Money); CustomFields as jsonb; indexes on TenantId, UserId, Status | StronglyTypedIdConverter, DictionaryValueComparer | |
| 146 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/MeterDefinitionConfiguration.cs` | EF Core config for MeterDefinition entity | Table "meter_definitions"; unique index on Code; column mappings for all fields | StronglyTypedIdConverter | |
| 147 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/QuotaDefinitionConfiguration.cs` | EF Core config for QuotaDefinition entity | Table "quota_definitions"; unique filtered index on TenantId+MeterCode (where PlanCode IS NULL) | StronglyTypedIdConverter | |
| 148 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/UsageRecordConfiguration.cs` | EF Core config for UsageRecord entity | Table "usage_records"; unique composite index on TenantId+MeterCode+PeriodStart+PeriodEnd | StronglyTypedIdConverter | |
| 149 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/CustomFieldDefinitionConfiguration.cs` | EF Core config for CustomFieldDefinition entity | Table "custom_field_definitions"; unique index on TenantId+EntityType+FieldKey; ValidationRulesJson and OptionsJson as nvarchar | StronglyTypedIdConverter | |

### Persistence - Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 150 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/InvoiceRepository.cs` | EF Core implementation of IInvoiceRepository | Compiled query for GetById; eager loading of LineItems; tenant-filtered queries | BillingDbContext | |
| 151 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/InvoiceRepositoryExtensions.cs` | Extension methods for custom field queries on invoices | FindByCustomField, FindByCustomFields, CustomFieldValueExists using EF.Functions.JsonContains on jsonb | DbSet, JsonSerializer | |
| 152 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/PaymentRepository.cs` | EF Core implementation of IPaymentRepository | Standard CRUD with tenant-filtered queries ordered by CreatedAt DESC | BillingDbContext | |
| 153 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/SubscriptionRepository.cs` | EF Core implementation of ISubscriptionRepository | Compiled query for GetById; GetActiveByUserId filters by Active status | BillingDbContext | |
| 154 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/MeterDefinitionRepository.cs` | EF Core implementation of IMeterDefinitionRepository | GetByCode for lookup, GetAll ordered by Code | BillingDbContext | |
| 155 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/QuotaDefinitionRepository.cs` | EF Core implementation of IQuotaDefinitionRepository | GetEffectiveQuota checks tenant override first then plan default; uses IgnoreQueryFilters for system-level quotas | BillingDbContext, ITenantContext | |
| 156 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/UsageRecordRepository.cs` | EF Core implementation of IUsageRecordRepository | GetHistory filters by tenant+meter+date range; GetForPeriod for exact period match (upsert support) | BillingDbContext, ITenantContext | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 157 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/ValkeyMeteringService.cs` | Valkey/Redis-based real-time metering service | IncrementAsync (fire-and-forget with index Set tracking), CheckQuotaAsync (reads counter + quota, raises threshold events), GetCurrentUsageAsync, GetUsageHistoryAsync | IConnectionMultiplexer, ITenantContext, repositories, IMessageBus, ISubscriptionQueryService | |
| 158 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/InvoiceQueryService.cs` | Dapper-based invoice query service | GetTotalRevenue, GetCount, GetPendingCount, GetOutstandingAmount using raw SQL on billing schema | BillingDbContext (for connection), Dapper, ITenantContext | |
| 159 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/InvoiceReportService.cs` | Dapper-based invoice report service | GetInvoicesAsync returns InvoiceReportRow list with raw SQL join | BillingDbContext (for connection), Dapper, ITenantContext | |
| 160 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/PaymentReportService.cs` | Dapper-based payment report service | GetPaymentsAsync returns PaymentReportRow list with raw SQL joining payments and invoices | BillingDbContext (for connection), Dapper, ITenantContext | |
| 161 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/RevenueReportService.cs` | Dapper-based revenue report service | GetRevenueAsync returns RevenueReportRow with aggregated gross/net revenue and refunds | BillingDbContext (for connection), Dapper, ITenantContext | |
| 162 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/SubscriptionQueryService.cs` | Service to get active plan code for a tenant | GetActivePlanCodeAsync finds active subscription and returns PlanName | ISubscriptionRepository | |
| 163 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/MeteringQueryService.cs` | Cross-module metering query service | CheckQuotaAsync reads effective quota + usage records from PostgreSQL, returns QuotaStatus | IUsageRecordRepository, IQuotaDefinitionRepository | |
| 164 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/UsageReportService.cs` | Usage report service for dashboards | GetUsageAsync joins UsageRecords with MeterDefinitions, groups by date/meter, returns UsageReportRow | BillingDbContext (LINQ) | |

### Workflows

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 165 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Workflows/InvoiceCreatedTrigger.cs` | Sample Elsa workflow activity for invoice creation | Placeholder ExecuteActivityAsync; ModuleName = "Billing" | WorkflowActivityBase (Elsa) | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 166 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260213182125_InitialCreate.cs` | Initial EF Core migration creating billing tables | Creates Invoices, InvoiceLineItems, Payments, Subscriptions tables with indexes | EF Core Migration | |
| 167 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260213182125_InitialCreate.Designer.cs` | Designer file for InitialCreate migration | Auto-generated model snapshot for migration | EF Core Migration | |
| 168 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260227201407_AbsorbMeteringEntities.cs` | Migration adding metering tables to billing schema | Creates meter_definitions, quota_definitions, usage_records tables | EF Core Migration | |
| 169 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260227201407_AbsorbMeteringEntities.Designer.cs` | Designer file for AbsorbMeteringEntities migration | Auto-generated model snapshot | EF Core Migration | |
| 170 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260303060302_AddTenantScopedInvoiceNumberIndex.cs` | Migration adding tenant-scoped unique invoice number index | Creates unique index on (TenantId, InvoiceNumber) | EF Core Migration | |
| 171 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260303060302_AddTenantScopedInvoiceNumberIndex.Designer.cs` | Designer file for AddTenantScopedInvoiceNumberIndex migration | Auto-generated model snapshot | EF Core Migration | |
| 172 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260310203331_AddSettingsTables.cs` | Migration adding settings tables to billing schema | Creates billing settings tables for tenant/user settings | EF Core Migration | |
| 173 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260310203331_AddSettingsTables.Designer.cs` | Designer file for AddSettingsTables migration | Auto-generated model snapshot | EF Core Migration | |
| 174 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260311000849_AddCustomFieldDefinition.cs` | Migration adding custom_field_definitions table | Creates table with all CustomFieldDefinition columns including ValidationRulesJson and OptionsJson | EF Core Migration | |
| 175 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/20260311000849_AddCustomFieldDefinition.Designer.cs` | Designer file for AddCustomFieldDefinition migration | Auto-generated model snapshot | EF Core Migration | |
| 176 | [ ] | `src/Modules/Billing/Wallow.Billing.Infrastructure/Migrations/BillingDbContextModelSnapshot.cs` | Current model snapshot for BillingDbContext | Auto-generated snapshot of all entities | EF Core Migration | |

---

## Api Layer

### Contracts - Invoices

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 177 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Contracts/Invoices/AddLineItemRequest.cs` | API request to add a line item | Record with Description, UnitPrice, Quantity | None | |
| 178 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Contracts/Invoices/CreateInvoiceRequest.cs` | API request to create an invoice | Record with InvoiceNumber, Currency, DueDate, optional UserId (admin override) | None | |
| 179 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Contracts/Invoices/InvoiceResponse.cs` | API response for invoice data | InvoiceResponse record + nested InvoiceLineItemResponse record | None | |

### Contracts - Payments

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 180 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Contracts/Payments/ProcessPaymentRequest.cs` | API request to process a payment | Record with Amount, Currency, PaymentMethod | None | |
| 181 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Contracts/Payments/PaymentResponse.cs` | API response for payment data | Record with all payment fields including TransactionReference and FailureReason | None | |

### Contracts - Subscriptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 182 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Contracts/Subscriptions/CreateSubscriptionRequest.cs` | API request to create a subscription | Record with PlanName, Price, Currency, StartDate, PeriodEnd | None | |
| 183 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Contracts/Subscriptions/SubscriptionResponse.cs` | API response for subscription data | Record with all subscription fields including period dates and CancelledAt | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 184 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/InvoicesController.cs` | REST controller for invoice CRUD operations | GetAll, GetById, GetByUserId, Create (admin can target other users), AddLineItem, Issue, Cancel; maps DTOs to responses | IMessageBus, ICurrentUserService; permissions: InvoicesRead/Write | |
| 185 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/MetersController.cs` | REST controller for meter definitions | GetAll returns all meter definitions | IMessageBus; permission: BillingRead | |
| 186 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/PaymentsController.cs` | REST controller for payment operations | GetById, GetByInvoiceId, ProcessPayment; maps DTOs to responses | IMessageBus, ICurrentUserService; permissions: PaymentsRead/Write | |
| 187 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/QuotasController.cs` | REST controller for quota management | GetAll (tenant quotas), SetOverride (admin), RemoveOverride (admin); includes SetQuotaOverrideRequest inline record | IMessageBus; permissions: BillingRead/Manage | |
| 188 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/SubscriptionsController.cs` | REST controller for subscription operations | GetById, GetByUserId, Create, Cancel; maps DTOs to responses | IMessageBus, ICurrentUserService; permissions: SubscriptionsRead/Write | |
| 189 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/UsageController.cs` | REST controller for usage data | GetAll (optional period filter), GetByMeterCode, GetHistory (date range) | IMessageBus; permission: BillingRead | |
| 190 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/BillingSettingsController.cs` | REST controller for billing module settings | GetConfig, GetTenantSettings, GetUserSettings, UpsertTenantSetting, DeleteTenantSetting, UpsertUserSetting, DeleteUserSetting; validates keys against registry | ISettingsService (keyed "billing"), ISettingRegistry, ITenantContext; BillingManage permission for tenant ops | |
| 191 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Controllers/CustomFieldDefinitionsController.cs` | REST controller for custom field definition CRUD | GetByEntityType, GetById, Create, Update, Deactivate, Reorder; inline request records (CreateCustomFieldRequest, UpdateCustomFieldRequest, ReorderFieldsRequest) | IMessageBus; permission: ConfigurationManage | |

### Middleware

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 192 | [ ] | `src/Modules/Billing/Wallow.Billing.Api/Middleware/MeteringMiddleware.cs` | Request pipeline middleware for quota enforcement and API call counting | Checks quota with HybridCache (30s TTL), blocks with 429 if exceeded, adds rate limit headers, increments api.calls counter on success (status < 400) | IMeteringService, ITenantContext, IMessageBus, HybridCache | |

---

## Test Files

### Wallow.Billing.Tests - GlobalUsings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 193 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/GlobalUsings.cs` | Global using directives for test project | N/A - configuration file | |

### Wallow.Billing.Tests - Domain/Entities

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 194 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Domain/Entities/InvoiceTests.cs` | Unit tests for Invoice aggregate root | Invoice creation, state transitions, line item management, domain event raising | |
| 195 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Domain/Entities/PaymentTests.cs` | Unit tests for Payment aggregate root | Payment creation, Complete/Fail/Refund transitions, validation rules | |
| 196 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Domain/Entities/SubscriptionTests.cs` | Unit tests for Subscription aggregate root | Subscription creation, Renew/MarkPastDue/Cancel/Expire transitions | |

### Wallow.Billing.Tests - Domain/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 197 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Domain/Metering/MeterDefinitionTests.cs` | Unit tests for MeterDefinition entity | Create/Update validation, field assignment | |
| 198 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Domain/Metering/QuotaDefinitionTests.cs` | Unit tests for QuotaDefinition entity | CreatePlanQuota, CreateTenantOverride, UpdateLimit validation | |
| 199 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Domain/Metering/UsageRecordTests.cs` | Unit tests for UsageRecord entity | Create validation, AddValue behavior | |

### Wallow.Billing.Tests - Domain/ValueObjects

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 200 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Domain/ValueObjects/MoneyTests.cs` | Unit tests for Money value object | Create validation, Zero factory, operator+, currency mismatch, equality | |

### Wallow.Billing.Tests - Application/Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 201 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/AddLineItemHandlerTests.cs` | Unit tests for AddLineItemHandler | Line item addition, invoice not found, validation | |
| 202 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/CancelInvoiceHandlerTests.cs` | Unit tests for CancelInvoiceHandler | Invoice cancellation, not found handling | |
| 203 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/CancelSubscriptionHandlerTests.cs` | Unit tests for CancelSubscriptionHandler | Subscription cancellation, not found handling | |
| 204 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/CreateInvoiceHandlerTests.cs` | Unit tests for CreateInvoiceHandler | Invoice creation, duplicate number check, telemetry | |
| 205 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/CreateSubscriptionHandlerTests.cs` | Unit tests for CreateSubscriptionHandler | Subscription creation flow | |
| 206 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/InvoiceEventHandlerTests.cs` | Unit tests for invoice domain event handlers | InvoiceCreated, InvoicePaid, InvoiceOverdue event bridge behavior | |
| 207 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/IssueInvoiceHandlerTests.cs` | Unit tests for IssueInvoiceHandler | Invoice issuance, not found, invalid state | |
| 208 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/MeteringCommandHandlerTests.cs` | Unit tests for metering command handlers | IncrementMeter, SetQuotaOverride, RemoveQuotaOverride | |
| 209 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/PaymentEventHandlerTests.cs` | Unit tests for payment domain event handlers | PaymentCreated event bridge behavior | |
| 210 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Handlers/ProcessPaymentHandlerTests.cs` | Unit tests for ProcessPaymentHandler | Payment processing, overpayment check, auto-mark paid | |

### Wallow.Billing.Tests - Application/EventHandlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 211 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/EventHandlers/InvoiceCreatedDomainEventHandlerTests.cs` | Unit tests for InvoiceCreatedDomainEventHandler | Integration event publishing, telemetry recording | |
| 212 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/EventHandlers/InvoiceOverdueDomainEventHandlerTests.cs` | Unit tests for InvoiceOverdueDomainEventHandler | Integration event publishing with user email enrichment | |
| 213 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/EventHandlers/InvoicePaidDomainEventHandlerTests.cs` | Unit tests for InvoicePaidDomainEventHandler | Integration event publishing with invoice enrichment | |
| 214 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/EventHandlers/PaymentCreatedDomainEventHandlerTests.cs` | Unit tests for PaymentCreatedDomainEventHandler | Integration event publishing with user email | |

### Wallow.Billing.Tests - Application/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 215 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/GetCurrentUsageHandlerTests.cs` | Unit tests for GetCurrentUsageHandler | Usage summary retrieval, meter filtering, quota integration | |
| 216 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/GetMeterDefinitionsHandlerTests.cs` | Unit tests for GetMeterDefinitionsHandler | Meter definition listing and DTO mapping | |
| 217 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/GetQuotaStatusHandlerTests.cs` | Unit tests for GetQuotaStatusHandler | Quota status retrieval, override detection | |
| 218 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/GetUsageHistoryHandlerTests.cs` | Unit tests for GetUsageHistoryHandler | Historical usage retrieval and DTO mapping | |
| 219 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/QuotaThresholdReachedDomainEventHandlerTests.cs` | Unit tests for QuotaThresholdReachedDomainEventHandler | Integration event publishing for threshold alerts | |
| 220 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/RemoveQuotaOverrideHandlerTests.cs` | Unit tests for RemoveQuotaOverrideHandler | Override removal, not found handling | |
| 221 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/RemoveQuotaOverrideValidatorTests.cs` | Unit tests for RemoveQuotaOverrideValidator | Validation rules for TenantId and MeterCode | |
| 222 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/SetQuotaOverrideHandlerTests.cs` | Unit tests for SetQuotaOverrideHandler | Override creation, existing update, meter not found | |
| 223 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/SetQuotaOverrideValidatorTests.cs` | Unit tests for SetQuotaOverrideValidator | Validation rules for all command fields | |
| 224 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Metering/UsageFlushedDomainEventHandlerTests.cs` | Unit tests for UsageFlushedDomainEventHandler | Integration event publishing for flush events | |

### Wallow.Billing.Tests - Application/Queries

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 225 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Queries/GetAllInvoicesHandlerTests.cs` | Unit tests for GetAllInvoicesHandler | All invoices retrieval and mapping | |
| 226 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Queries/GetInvoiceByIdHandlerTests.cs` | Unit tests for GetInvoiceByIdHandler | Single invoice retrieval, not found | |
| 227 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Queries/GetInvoicesByUserIdHandlerTests.cs` | Unit tests for GetInvoicesByUserIdHandler | User invoices retrieval | |
| 228 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Queries/GetPaymentByIdHandlerTests.cs` | Unit tests for GetPaymentByIdHandler | Single payment retrieval, not found | |
| 229 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Queries/GetPaymentsByInvoiceIdHandlerTests.cs` | Unit tests for GetPaymentsByInvoiceIdHandler | Invoice payments retrieval | |
| 230 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Queries/GetSubscriptionByIdHandlerTests.cs` | Unit tests for GetSubscriptionByIdHandler | Single subscription retrieval, not found | |
| 231 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Queries/GetSubscriptionsByUserIdHandlerTests.cs` | Unit tests for GetSubscriptionsByUserIdHandler | User subscriptions retrieval | |

### Wallow.Billing.Tests - Application/Validators

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 232 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/AddLineItemValidatorTests.cs` | Unit tests for AddLineItemValidator | Validation of all AddLineItem command fields | |
| 233 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/BillingValidatorTests.cs` | Shared/cross-cutting validator tests | Common validation patterns across billing validators | |
| 234 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/CancelInvoiceValidatorTests.cs` | Unit tests for CancelInvoiceValidator | Validation of CancelInvoice command fields | |
| 235 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/CancelSubscriptionValidatorTests.cs` | Unit tests for CancelSubscriptionValidator | Validation of CancelSubscription command fields | |
| 236 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/CreateInvoiceValidatorTests.cs` | Unit tests for CreateInvoiceValidator | Validation of CreateInvoice command fields | |
| 237 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/CreateSubscriptionValidatorTests.cs` | Unit tests for CreateSubscriptionValidator | Validation of CreateSubscription command fields | |
| 238 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/IssueInvoiceValidatorTests.cs` | Unit tests for IssueInvoiceValidator | Validation of IssueInvoice command fields | |
| 239 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Application/Validators/ProcessPaymentValidatorTests.cs` | Unit tests for ProcessPaymentValidator | Validation of ProcessPayment command fields | |

### Wallow.Billing.Tests - Api/Contracts

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 240 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Contracts/RequestContractTests.cs` | Contract tests for API request records | Request record structure, field presence | |
| 241 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Contracts/ResponseContractTests.cs` | Contract tests for API response records | Response record structure, field presence | |

### Wallow.Billing.Tests - Api/Controllers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 242 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Controllers/InvoicesControllerTests.cs` | Unit tests for InvoicesController | All invoice endpoints, auth checks, DTO mapping | |
| 243 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Controllers/MetersControllerTests.cs` | Unit tests for MetersController | Meter definitions endpoint | |
| 244 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Controllers/PaymentsControllerTests.cs` | Unit tests for PaymentsController | Payment endpoints, auth checks | |
| 245 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Controllers/QuotasControllerTests.cs` | Unit tests for QuotasController | Quota status, set/remove override endpoints | |
| 246 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Controllers/SubscriptionsControllerTests.cs` | Unit tests for SubscriptionsController | Subscription endpoints, auth checks | |
| 247 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Controllers/UsageControllerTests.cs` | Unit tests for UsageController | Usage endpoints, period filtering, history | |
| 248 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Settings/BillingSettingsControllerTests.cs` | Unit tests for BillingSettingsController | GetConfig, tenant/user settings CRUD, key validation, permission checks | |

### Wallow.Billing.Tests - Api/Extensions

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 249 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Extensions/ResultExtensionsTests.cs` | Unit tests for Result-to-ActionResult extensions | ToActionResult mapping for success/failure/not-found/validation errors | |

### Wallow.Billing.Tests - Api/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 250 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Api/Metering/MeteringMiddlewareTests.cs` | Unit tests for MeteringMiddleware | Quota blocking (429), warning headers, rate limit headers, counter increment on success, non-API route skip | |

### Wallow.Billing.Tests - Infrastructure/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 251 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Metering/FlushUsageJobTests.cs` | Unit tests for FlushUsageJob | Key processing, atomic get-and-reset, upsert logic, event publishing | |
| 252 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Metering/FlushUsageJobAdditionalTests.cs` | Additional unit tests for FlushUsageJob edge cases | Invalid key formats, cancellation, error handling | |
| 253 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Metering/FlushUsageJobExceptionTests.cs` | Exception handling tests for FlushUsageJob | Error recovery, partial flush scenarios | |
| 254 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Metering/ValkeyMeteringServiceTests.cs` | Unit tests for ValkeyMeteringService | Increment, CheckQuota, GetCurrentUsage, threshold events, period key generation | |

### Wallow.Billing.Tests - Infrastructure/Persistence

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 255 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/BillingDbContextFactoryTests.cs` | Unit tests for BillingDbContextFactory | Design-time context creation | |
| 256 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/DesignTimeTenantContextTests.cs` | Unit tests for DesignTimeTenantContext | Mock tenant context property values | |
| 257 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/InvoiceRepositoryExtensionsTests.cs` | Unit tests for InvoiceRepositoryExtensions | Custom field JSON querying | |
| 258 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/InvoiceRepositoryTests.cs` | Unit tests for InvoiceRepository | CRUD operations, compiled query, line item loading | |
| 259 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/MeterDefinitionRepositoryTests.cs` | Unit tests for MeterDefinitionRepository | GetByCode, GetAll operations | |
| 260 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/MeteringDbSeederTests.cs` | Unit tests for MeteringDbSeeder | Seed idempotency, default meters and quotas | |
| 261 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/PaymentRepositoryTests.cs` | Unit tests for PaymentRepository | CRUD operations, invoice ID filtering | |
| 262 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/QuotaDefinitionRepositoryTests.cs` | Unit tests for QuotaDefinitionRepository | Effective quota resolution, tenant override priority | |
| 263 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Persistence/SubscriptionRepositoryTests.cs` | Unit tests for SubscriptionRepository | CRUD, GetActiveByUserId filtering | |

### Wallow.Billing.Tests - Infrastructure/Services

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 264 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/InvoiceQueryServiceTests.cs` | Unit tests for InvoiceQueryService | Dapper revenue/count/pending/outstanding queries | |
| 265 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/InvoiceReportServiceTests.cs` | Unit tests for InvoiceReportService | Dapper invoice report generation | |
| 266 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/MeteringQueryServiceTests.cs` | Unit tests for MeteringQueryService | Quota checking via PostgreSQL, period bounds | |
| 267 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/PaymentReportServiceTests.cs` | Unit tests for PaymentReportService | Dapper payment report generation | |
| 268 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/RevenueReportServiceTests.cs` | Unit tests for RevenueReportService | Dapper revenue aggregation | |
| 269 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/SubscriptionQueryServiceTests.cs` | Unit tests for SubscriptionQueryService | Active plan code lookup, error handling | |
| 270 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/UsageReportServiceTests.cs` | Unit tests for UsageReportService | Usage report LINQ query, meter join | |
| 271 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Services/ValkeyMeteringServiceAdditionalTests.cs` | Additional unit tests for ValkeyMeteringService | Edge cases, error paths, fire-and-forget behavior | |

### Wallow.Billing.Tests - Infrastructure/Workflows

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 272 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/Workflows/InvoiceCreatedTriggerTests.cs` | Unit tests for InvoiceCreatedTrigger | Workflow activity execution, module name | |

### Wallow.Billing.Tests - Infrastructure (Root)

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 273 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Infrastructure/DapperQueryTests.cs` | Tests for Dapper raw SQL queries | SQL correctness, parameter binding, tenant filtering | |

### Wallow.Billing.Tests - Integration/Metering

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 274 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Integration/Metering/UsageRecordRepositoryTests.cs` | Integration tests for UsageRecordRepository | Full DB round-trip for usage records with Testcontainers | |

### Wallow.Billing.Tests - Integration/Settings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 275 | [ ] | `tests/Modules/Billing/Wallow.Billing.Tests/Integration/Settings/BillingSettingsTests.cs` | Integration tests for billing settings | Settings persistence, tenant/user scoping, default values | |
