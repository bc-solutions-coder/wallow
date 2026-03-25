# Architecture Assessment: DDD & Clean Architecture

This document assesses Wallow's implementation of Domain-Driven Design (DDD) and Clean Architecture patterns, providing guidance for building new modules consistently.

**Last Updated:** February 2026

---

## Executive Summary

| Dimension | Score | Verdict |
|-----------|-------|---------|
| **Clean Architecture** | 9/10 | Excellent - textbook layer separation |
| **DDD** | 7/10 | Good foundations, gaps in consistency |
| **Overall Maturity** | 8/10 | Intermediate-to-Advanced |

The codebase demonstrates solid foundational patterns with excellent consistency across most modules. The Billing module is the gold standard. Strategic gaps exist in domain services and event-sourced module consistency.

---

## Table of Contents

1. [Clean Architecture Implementation](#1-clean-architecture-implementation)
2. [DDD Implementation](#2-ddd-implementation)
3. [Three Module Patterns](#3-three-module-patterns)
4. [Module Maturity Assessment](#4-module-maturity-assessment)
5. [Key Gaps & Recommendations](#5-key-gaps--recommendations)
6. [Decision Tree: Choosing a Pattern](#6-decision-tree-choosing-a-pattern)
7. [Code Examples](#7-code-examples)

---

## 1. Clean Architecture Implementation

### Layer Structure (9/10)

The dependency direction is textbook correct:

```
┌─────────────────────────────────────────┐
│ API (Controllers, HTTP contracts)       │  Depends on ↓
├─────────────────────────────────────────┤
│ Application (Commands, Queries, DTOs)   │  Depends on ↓
├─────────────────────────────────────────┤
│ Domain (Entities, VOs, Events)          │  Depends on nothing*
├─────────────────────────────────────────┤
│ Infrastructure (EF, Repos, Services)    │  Implements Application interfaces
└─────────────────────────────────────────┘
*except Shared.Kernel
```

### What's Working

| Aspect | Implementation | Grade |
|--------|---------------|-------|
| **Dependency Direction** | Outer layers depend on inner | ✓ |
| **No Infrastructure Leaks** | Domain and Application are framework-free | ✓ |
| **Interface Segregation** | Interfaces in Application, implementations in Infrastructure | ✓ |
| **Use Cases** | Commands/Queries represent distinct use cases | ✓ |
| **DTOs** | Separate request/response contracts per layer | ✓ |

### Project Reference Rules

```xml
<!-- Domain: Zero external dependencies -->
<ProjectReference Include="Wallow.Shared.Kernel" />

<!-- Application: Depends on Domain + Shared -->
<ProjectReference Include="Wallow.{Module}.Domain" />
<ProjectReference Include="Wallow.Shared.Kernel" />
<ProjectReference Include="Wallow.Shared.Contracts" />
<!-- NO EntityFrameworkCore, NO HttpClient -->

<!-- Infrastructure: Implements Application interfaces -->
<ProjectReference Include="Wallow.{Module}.Application" />
<ProjectReference Include="Wallow.{Module}.Domain" />
<!-- HAS EntityFrameworkCore - but Application doesn't know -->

<!-- Api: Composes everything -->
<ProjectReference Include="Wallow.{Module}.Application" />
<ProjectReference Include="Wallow.{Module}.Infrastructure" />
```

---

## 2. DDD Implementation

### Aggregates (8/10)

**Strong in traditional modules** (Billing, Notifications, Storage). Aggregates protect invariants and raise domain events.

### Entities vs Value Objects (8/10)

**Money** in Billing is an excellent Value Object example:
- Immutable
- Operator overloading for domain language
- Factory method enforces validation

**Gap:** Value Objects could be used more broadly across modules.

### Domain Events (7/10)

**Good:**
- Past-tense naming (`InvoiceCreatedDomainEvent`)
- Raised from aggregates
- Handlers bridge to integration events

**Gap:**
- Event dispatch mechanism is implicit (hidden by Wolverine)
- No event versioning strategy

### Repositories (9/10)

**Excellent pattern:**
- Interfaces in Application layer
- Implementations in Infrastructure layer
- Work with Aggregate Roots, not individual entities
- Explicit `SaveChangesAsync`

### Domain Services (6/10)

**Gap:** No explicit Domain Services layer. Cross-aggregate logic sometimes lives in Application layer services rather than Domain layer.

### Bounded Context Enforcement (8/10)

**Strong:**
- No cross-module project references
- Communication via `Shared.Contracts` events
- Each module owns its database schema
- Violations caught at compile time

---

## 3. Three Module Patterns

Wallow uses three distinct architectural patterns. Understanding these is essential before building new modules.

### Pattern 1: Traditional DDD

**Used by:** Billing, Notifications, Messaging, Announcements, Storage, Inquiries.

```
Domain:        Aggregates with behavior, Value Objects, Domain Events
Application:   Commands, Queries, Handlers, Repository interfaces
Infrastructure: EF Core, Repositories implement interfaces
```

**Characteristics:**
- Rich domain model with behavior
- Aggregates protect invariants
- EF Core for persistence
- Dapper for complex queries (optional)

**Use when:**
- CRUD-heavy operations
- Complex business invariants
- Traditional business logic
- No need for full audit trail

### Pattern 2: External Adapter

**Used by:** Identity

```
Domain:        Thin entities (validation only)
Application:   Light command handlers
Infrastructure: Heavy services wrapping external system
```

**Characteristics:**
- Domain model is intentionally thin
- Most logic lives in Infrastructure services
- Clear interface boundary to external system
- Domain events still published

**Use when:**
- Wrapping an external system (IdP, payment gateway, workflow engine)
- External system owns the "truth"
- Business logic lives in external system

**Examples:**
- **Identity**: ASP.NET Core Identity owns user/role management

---

## 4. Module Maturity Assessment

### Tier 1: Gold Standard

| Module | Pattern | DDD Score | Notes |
|--------|---------|-----------|-------|
| **Billing** | Traditional | 9/10 | Reference implementation. Perfect aggregates, Money VO, domain events. |

### Tier 2: Production Ready

| Module | Pattern | DDD Score | Notes |
|--------|---------|-----------|-------|
| **Notifications** | Traditional | 8/10 | Email delivery via MailKit. Good Value Objects (`EmailAddress`, `EmailContent`). |
| **Messaging** | Traditional | 7/10 | In-app real-time messaging via SignalR. |
| **Announcements** | Traditional | 7/10 | Broadcast announcements with targeting rules. |
| **Storage** | Traditional (simple) | 6/10 | Raw file abstraction. `RetentionPolicy` Value Object. |

### Tier 3: Pragmatic Trade-offs

| Module | Pattern | DDD Score | Notes |
|--------|---------|-----------|-------|
| **Identity** | External Adapter | 4/10 | Intentionally thin domain. ASP.NET Core Identity owns the logic. |
| **Inquiries** | Traditional | 7/10 | Contact/inquiry form processing. |

### Shared Infrastructure Capabilities

| Capability | Location | Notes |
|------------|----------|-------|
| **Auditing** | `Shared.Infrastructure/Auditing/` | Audit.NET EF Core interceptor. Cross-cutting. |
| **Background Jobs** | `Shared.Infrastructure/BackgroundJobs/` | IJobScheduler over Hangfire. |
| **Workflows** | `Shared.Infrastructure/Workflows/` | Elsa 3 workflow engine integration. |

---

## 5. Key Gaps & Recommendations

### Gap 1: Missing Domain Services Layer

**Problem:** Cross-aggregate logic sometimes lives in Application layer rather than Domain layer.

**Recommendation:** Create explicit Domain Services for cross-aggregate operations when business rules span multiple aggregates.

**Priority:** High

---

### Gap 2: Value Objects Adoption Expanding

**Progress:** Value Objects are now used in multiple modules beyond Billing:

| Module | Value Objects |
|--------|--------------|
| Billing | `Money` (currency, arithmetic) |
| Notifications | `EmailAddress` (validation), `EmailContent` |
| Storage | `RetentionPolicy` |

**Recommendation:** Continue extracting Value Objects when:
- Field has validation rules
- Field has domain meaning
- Field could have behavior (formatting, comparison)

**Priority:** Low (good progress made)

---

### Gap 3: Event Dispatch Visibility

**Problem:** How domain events become integration events is implicit.

```csharp
// Handler exists but orchestration hidden
public sealed class InvoiceCreatedDomainEventHandler
{
    public async Task Handle(InvoiceCreatedDomainEvent domainEvent, ...)
    // When/where is this called? Hidden by Wolverine.
}
```

**Recommendation:** Document the event pipeline. Add explicit `IEventDispatcher` interface for visibility.

**Priority:** Low (documentation issue)

---

### Gap 4: No Event Versioning Strategy

**Problem:** Events lack versioning for schema evolution.

**Recommendation:** Before going to production, define:
- How events will be versioned
- How old events will be migrated/upcasted
- Schema registry (optional)

**Priority:** Medium (pre-production)

---

## 6. Decision Tree: Choosing a Pattern

```
Is this module wrapping an external system?
│
├── YES → Use External Adapter Pattern (like Identity)
│         • Thin domain
│         • Heavy infrastructure services
│         • Clear interface boundary
│
└── NO → Use Traditional DDD Pattern (like Billing)
         • Full aggregate design
         • EF Core writes, Dapper reads
         • Rich Value Objects
```

### Additional Considerations

| Question | If YES |
|----------|--------|
| Is the domain model simple CRUD? | Traditional (simplified) |
| Does an external system own the data? | External Adapter |
| Are there complex business invariants? | Traditional with rich aggregates |
| Is this cross-cutting infrastructure (audit, jobs)? | Shared Infrastructure capability |

---

## 7. Code Examples

### Gold Standard: Billing Invoice Aggregate

```csharp
public sealed class Invoice : AggregateRoot<InvoiceId>, ITenantScoped
{
    // Private collection, exposed as read-only
    private readonly List<InvoiceLineItem> _lineItems = [];
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    // All state changes through domain methods
    public void AddLineItem(string description, Money unitPrice, int quantity, Guid updatedByUserId)
    {
        // Invariant enforcement
        if (Status != InvoiceStatus.Draft)
            throw new InvalidInvoiceException("Can only add line items to draft invoices");
        if (quantity <= 0)
            throw new BusinessRuleException("Billing.InvalidQuantity", "Quantity must be greater than zero");

        // Encapsulated state change
        var lineItem = InvoiceLineItem.Create(Id, description, unitPrice, quantity);
        _lineItems.Add(lineItem);
        RecalculateTotal();  // Maintains aggregate consistency
    }

    // Domain events raised from aggregate
    public void MarkAsPaid(Guid paymentId, Guid updatedByUserId)
    {
        if (Status != InvoiceStatus.Issued && Status != InvoiceStatus.Overdue)
            throw new InvalidInvoiceException("Can only mark issued or overdue invoices as paid");

        Status = InvoiceStatus.Paid;
        PaidAt = DateTime.UtcNow;
        SetUpdated(updatedByUserId);

        RaiseDomainEvent(new InvoicePaidDomainEvent(Id.Value, paymentId, PaidAt.Value));
    }
}
```

### Gold Standard: Money Value Object

```csharp
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    // Factory method enforces immutability
    public static Money Create(decimal amount, string currency)
    {
        if (amount < 0)
            throw new BusinessRuleException("Billing.NegativeAmount", "Amount cannot be negative");
        if (currency.Length != 3)
            throw new BusinessRuleException("Billing.InvalidCurrency", "Currency must be 3-letter ISO code");
        return new Money(amount, currency.ToUpperInvariant());
    }

    // Operator overloading for domain language
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new BusinessRuleException("Billing.CurrencyMismatch", "Cannot add money with different currencies");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    // Equality by value
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

### Command Handler Pattern

```csharp
public sealed class CreateInvoiceHandler
{
    private readonly IInvoiceRepository _invoiceRepository;

    public CreateInvoiceHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<InvoiceDto>> Handle(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validation (application-level)
        var exists = await _invoiceRepository.ExistsByInvoiceNumberAsync(
            command.InvoiceNumber, cancellationToken);
        if (exists)
            return Result.Failure<InvoiceDto>(
                Error.Conflict($"Invoice '{command.InvoiceNumber}' already exists"));

        // 2. Domain creates the aggregate (domain-level)
        var invoice = Invoice.Create(
            command.UserId,
            command.InvoiceNumber,
            command.Currency,
            command.UserId,
            command.DueDate);

        // 3. Persist
        _invoiceRepository.Add(invoice);
        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        // 4. Return DTO
        return invoice.ToDto();
    }
}
```

### Repository Interface (Application Layer)

```csharp
public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByIdWithLineItemsAsync(InvoiceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    void Add(Invoice invoice);
    void Update(Invoice invoice);
    void Remove(Invoice invoice);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

---

## Summary

### Strengths

| Aspect | Status |
|--------|--------|
| Layer separation | Excellent |
| Dependency direction | Correct |
| Module isolation | Strong |
| Aggregate design (Billing) | Exemplary |
| Command/Query pattern | Clean |
| Repository pattern | Proper |

### Gaps

| Aspect | Status | Priority |
|--------|--------|----------|
| Domain Services layer | Missing | High |
| Value Objects | Expanding (Billing, Notifications, Storage) | Low |
| Event versioning | Not defined | Medium |
| Event dispatch visibility | Implicit | Low |

### Bottom Line

**Use Billing as your template for traditional DDD modules.** For external system integrations, follow the Identity module's adapter pattern.

---

*This assessment covers the 8 core modules in the Wallow platform. Billing remains the gold standard for traditional DDD. Notifications demonstrates good Value Object adoption. Identity demonstrates the External Adapter pattern. Cross-cutting capabilities (Auditing, Background Jobs, Workflows) live in Shared.Infrastructure. See [Module Creation Guide](module-creation.md) for step-by-step module creation instructions.*
