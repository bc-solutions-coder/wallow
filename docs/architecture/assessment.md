# Architecture Assessment: DDD & Clean Architecture

This document assesses Wallow's implementation of Domain-Driven Design (DDD) and Clean Architecture patterns, providing guidance for building new modules consistently.

---

## Executive Summary

| Dimension | Score | Verdict |
|-----------|-------|---------|
| **Clean Architecture** | 9/10 | Excellent — textbook layer separation |
| **DDD** | 7/10 | Good foundations, gaps in consistency |
| **Overall Maturity** | 8/10 | Intermediate-to-Advanced |

The codebase demonstrates solid foundational patterns with excellent consistency across most modules. The Notifications module is the gold standard. Strategic gaps exist in domain services and event-sourced module consistency.

---

## Table of Contents

1. [Clean Architecture Implementation](#1-clean-architecture-implementation)
2. [DDD Implementation](#2-ddd-implementation)
3. [Two Module Patterns](#3-two-module-patterns)
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

Strong in traditional modules (Notifications, Storage, Announcements). Aggregates protect invariants and raise domain events.

### Entities vs Value Objects (8/10)

**EmailAddress** in Notifications is an excellent Value Object example: immutable, with input normalization and a factory method that enforces validation via regex.

### Domain Events (7/10)

**Good:**
- Past-tense naming (`NotificationCreatedDomainEvent`)
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

## 3. Two Module Patterns

Wallow uses two distinct architectural patterns. Understanding these is essential before building new modules.

### Pattern 1: Traditional DDD

**Used by:** Notifications, Announcements, Storage, Inquiries.

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
| **Notifications** | Traditional | 9/10 | Reference implementation. Multi-channel delivery, Value Objects (`EmailAddress`, `EmailContent`), domain events, provider pattern. |

### Tier 2: Production Ready

| Module | Pattern | DDD Score | Notes |
|--------|---------|-----------|-------|
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
└── NO → Use Traditional DDD Pattern (like Notifications)
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

### Gold Standard: Notifications Notification Aggregate

```csharp
public sealed class Notification : AggregateRoot<NotificationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public bool IsArchived { get; private set; }

    public static Notification Create(
        TenantId tenantId, Guid userId, NotificationType type,
        string title, string message, TimeProvider timeProvider,
        string? actionUrl = null, string? sourceModule = null,
        DateTime? expiresAt = null)
    {
        return new Notification(tenantId, userId, type, title, message,
            actionUrl, sourceModule, expiresAt, timeProvider);
    }

    public void MarkAsRead(TimeProvider timeProvider)
    {
        IsRead = true;
        ReadAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new NotificationReadDomainEvent(Id.Value, UserId));
    }

    public void Archive(TimeProvider timeProvider)
    {
        IsArchived = true;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
```

### Gold Standard: EmailAddress Value Object

```csharp
public sealed partial class EmailAddress : ValueObject
{
    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static EmailAddress Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidEmailAddressException("Email address cannot be empty");

        email = email.Trim();

        if (!EmailRegex().IsMatch(email))
            throw new InvalidEmailAddressException($"'{email}' is not a valid email address");

        return new EmailAddress(email);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(EmailAddress email) => email.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();
}
```

### Repository Interface (Application Layer)

```csharp
public interface INotificationRepository
{
    void Add(Notification notification);
    Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default);
    Task<PagedResult<Notification>> GetByUserIdPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, DateTime readAt, CancellationToken cancellationToken = default);
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
| Aggregate design (Notifications) | Exemplary |
| Command/Query pattern | Clean |
| Repository pattern | Proper |

### Gaps

| Aspect | Status | Priority |
|--------|--------|----------|
| Domain Services layer | Missing | High |
| Value Objects | Expanding (Notifications, Storage) | Low |
| Event dispatch visibility | Implicit | Low |

### Bottom Line

**Use Notifications as your template for traditional DDD modules.** For external system integrations, follow the Identity module's adapter pattern.

---

*This assessment covers the 7 core modules in the Wallow platform: Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, and Branding. Notifications is the gold standard for traditional DDD with strong Value Object adoption. Identity demonstrates the External Adapter pattern. Cross-cutting capabilities (Auditing, Background Jobs) live in separate Shared.Infrastructure projects. See the [Module Creation Guide](module-creation.md) for step-by-step module creation instructions.*
