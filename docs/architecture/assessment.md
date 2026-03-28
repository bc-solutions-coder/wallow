# Architecture Assessment: DDD & Clean Architecture

This document assesses Wallow's implementation of Domain-Driven Design (DDD) and Clean Architecture patterns, providing guidance for building new modules consistently.

---

## Executive Summary

| Dimension | Score | Verdict |
|-----------|-------|---------|
| **Clean Architecture** | 9/10 | Excellent — textbook layer separation |
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
| **Dependency Direction** | Outer layers depend on inner | Pass |
| **No Infrastructure Leaks** | Domain and Application are framework-free | Pass |
| **Interface Segregation** | Interfaces in Application, implementations in Infrastructure | Pass |
| **Use Cases** | Commands/Queries represent distinct use cases | Pass |
| **DTOs** | Separate request/response contracts per layer | Pass |

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

Strong in traditional modules (Billing, Notifications, Storage). Aggregates protect invariants and raise domain events.

### Entities vs Value Objects (8/10)

**Money** in Billing is an excellent Value Object example: immutable, with operator overloading for domain language and a factory method that enforces validation.

### Domain Events (7/10)

**Good:**
- Past-tense naming (`InvoiceCreatedDomainEvent`)
- Raised from aggregates
- Handlers bridge to integration events via Wolverine

**Gap:**
- Event dispatch mechanism is implicit (hidden by Wolverine auto-discovery)

### Repositories (9/10)

Interfaces live in the Application layer; implementations in Infrastructure. Repositories work with Aggregate Roots and expose explicit `SaveChangesAsync`.

### Domain Services (6/10)

No explicit Domain Services layer. Cross-aggregate logic sometimes lives in Application layer services rather than Domain layer.

### Bounded Context Enforcement (8/10)

- No cross-module project references
- Communication via `Shared.Contracts` events dispatched through Wolverine
- Each module owns its database schema
- Violations caught at compile time

---

## 3. Three Module Patterns

Wallow uses two distinct architectural patterns. Understanding these is essential before building new modules.

### Pattern 1: Traditional DDD

**Used by:** Billing, Notifications, Messaging, Announcements, Storage, Inquiries.

```
Domain:         Aggregates with behavior, Value Objects, Domain Events
Application:    Commands, Queries, Handlers, Repository interfaces
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

### Pattern 2: External Adapter

**Used by:** Identity

```
Domain:         Thin entities (validation only)
Application:    Light command handlers
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

**Example:** Identity wraps ASP.NET Core Identity for user/role management.

---

## 4. Module Maturity Assessment

### Tier 1: Gold Standard

| Module | Pattern | DDD Score | Notes |
|--------|---------|-----------|-------|
| **Billing** | Traditional | 9/10 | Reference implementation. Proper aggregates, Money VO, domain events. |

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
| **Auditing** | `Shared.Infrastructure.Core/Auditing/` | Custom EF Core `SaveChanges` interceptor. Cross-cutting. |
| **Background Jobs** | `Shared.Infrastructure.BackgroundJobs/` | `IJobScheduler` over Hangfire. |
| **Workflows** | `Shared.Infrastructure.Workflows/` | Elsa workflow engine integration. |

---

## 5. Key Gaps & Recommendations

### Gap 1: Missing Domain Services Layer

**Problem:** Cross-aggregate logic sometimes lives in Application layer rather than Domain layer.

**Recommendation:** Create explicit Domain Services for cross-aggregate operations when business rules span multiple aggregates.

**Priority:** High

---

### Gap 2: Value Objects Adoption Expanding

Value Objects are used in multiple modules:

| Module | Value Objects |
|--------|--------------|
| Billing | `Money` (currency, arithmetic) |
| Notifications | `EmailAddress` (validation), `EmailContent` |
| Storage | `RetentionPolicy` |

**Recommendation:** Continue extracting Value Objects when a field has validation rules, domain meaning, or behavior (formatting, comparison).

**Priority:** Low (good progress made)

---

### Gap 3: Event Dispatch Visibility

**Problem:** How domain events become integration events is implicit. Wolverine auto-discovers handlers, so the event pipeline is not immediately obvious from the code.

**Recommendation:** Document the event pipeline clearly so new developers understand the flow.

**Priority:** Low (documentation issue)

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
public sealed class Invoice : AggregateRoot<InvoiceId>, ITenantScoped, IHasCustomFields
{
    private readonly List<InvoiceLineItem> _lineItems = [];
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    public void AddLineItem(string description, Money unitPrice, int quantity,
        Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidInvoiceException("Can only add line items to draft invoices");
        if (quantity <= 0)
            throw new BusinessRuleException("Billing.InvalidQuantity",
                "Quantity must be greater than zero");

        InvoiceLineItem lineItem = InvoiceLineItem.Create(Id, description, unitPrice, quantity);
        _lineItems.Add(lineItem);
        RecalculateTotal();
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void MarkAsPaid(Guid paymentId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Issued && Status != InvoiceStatus.Overdue)
            throw new InvalidInvoiceException(
                "Can only mark issued or overdue invoices as paid");

        Status = InvoiceStatus.Paid;
        PaidAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);

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

    public static Money Create(decimal amount, string currency)
    {
        if (amount < 0)
            throw new BusinessRuleException("Billing.InvalidMoney",
                "Money amount cannot be negative");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new BusinessRuleException("Billing.InvalidMoney",
                "Currency must be a 3-letter ISO code");
        return new Money(amount, currency.ToUpperInvariant());
    }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new BusinessRuleException("Billing.InvalidMoney",
                $"Cannot add money with different currencies: {left.Currency} and {right.Currency}");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
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
    Task<IReadOnlyList<Invoice>> GetAllAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);
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
| Event dispatch visibility | Implicit | Low |

### Bottom Line

**Use Billing as your template for traditional DDD modules.** For external system integrations, follow the Identity module's adapter pattern.

---

*This assessment covers the 7 core modules in the Wallow platform. Billing remains the gold standard for traditional DDD. Notifications demonstrates good Value Object adoption. Identity demonstrates the External Adapter pattern. Cross-cutting capabilities (Auditing, Background Jobs, Workflows) live in separate Shared.Infrastructure projects. See the [Module Creation Guide](module-creation.md) for step-by-step module creation instructions.*
