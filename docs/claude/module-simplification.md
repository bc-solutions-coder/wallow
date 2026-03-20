# Module Simplification Design

> **HISTORICAL DOCUMENT** — This document describes a design proposal from 2026-02-27 that was partially implemented with a different outcome than planned. The actual result was **8 modules** (Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries, Showcases), not the 5 modules described here. Specifically: Communications was split into three separate modules (Notifications, Messaging, Announcements) rather than merged; the Configuration module was never built. The Shared Infrastructure capabilities (Auditing, Background Jobs, Workflows) were implemented as described. File paths and module names in this document reference the original design and do not match the current codebase.

**Date:** 2026-02-27
**Status:** Historical (partially superseded)
**Scope:** Reduce Wallow from 24 modules to 5 core modules, move 3 capabilities to shared infrastructure.

## Problem

Wallow had 24 modules. Most were thin, speculative, or unused. The complexity they created outweighed their value. The platform needed a solid, well-tested foundation before domain modules are built on top of it.

## Decision

Cut to 5 core modules. Move scheduling, auditing, and workflow orchestration to shared infrastructure. Domain building blocks (Catalog, Sales, Inventory, etc.) exist on a separate branch and will return once the foundation is ready.

## Module Inventory (Post-Refactor)

### Kept (5 Modules)

| Module | Change | Purpose |
|--------|--------|---------|
| **Identity** | None | Authentication, users, tenants, roles (Keycloak) |
| **Storage** | None | Raw file storage (S3, local filesystem) |
| **Communications** | Merge of Email + Notifications + Announcements | All outbound messaging and in-app notifications |
| **Billing** | Absorbs Metering | Invoicing, payments, subscriptions, metered usage |
| **Configuration** | None | Tenant settings, feature flags, app config |

### Moved to Shared Infrastructure (3)

| Capability | Former Module | New Home |
|------------|--------------|----------|
| **Auditing** | AuditLog | `Shared.Infrastructure/Auditing/` via Audit.NET |
| **Background Jobs** | Scheduler | `Shared.Infrastructure/BackgroundJobs/` via Hangfire |
| **Workflows** | Workflows | `Shared.Infrastructure/Workflows/` via Elsa 3 |

---

## Communications Module (Merge Design)

Three modules merge into one. Each becomes a subdomain within Communications.

### Structure

```
src/Modules/Communications/
├── Wallow.Communications.Domain/
│   ├── Channels/              — Email, InApp, Push (future)
│   ├── Announcements/         — System/tenant-wide broadcast messages
│   └── Preferences/           — Per-user channel preferences
├── Wallow.Communications.Application/
│   ├── Commands/
│   │   ├── SendEmail/
│   │   ├── SendNotification/
│   │   ├── CreateAnnouncement/
│   │   └── MarkAsRead/
│   └── Queries/
│       ├── GetNotifications/
│       └── GetAnnouncements/
├── Wallow.Communications.Infrastructure/
│   ├── Email/                 — MailKit SMTP, templates
│   ├── SignalR/               — Real-time notification push
│   └── Persistence/          — CommunicationsDbContext
└── Wallow.Communications.Api/
    ├── NotificationsController
    └── AnnouncementsController
```

### Behavior

Other modules publish integration events (e.g., `InvoiceOverdueEvent`). Communications subscribes to events it cares about and delivers messages through the appropriate channel based on event type and user preferences. Modules never call Communications directly.

---

## Billing Module (Metering Merge)

Metering becomes a subdomain of Billing. Usage tracking exists only to feed invoicing.

### Structure

```
src/Modules/Billing/
├── Wallow.Billing.Domain/
│   ├── Invoicing/             — Invoice, InvoiceLine, PaymentRecord
│   ├── Subscriptions/         — Plan, Subscription, BillingCycle
│   └── Metering/              — UsageRecord, UsageMeter, RatingRule
├── Wallow.Billing.Application/
│   ├── Commands/
│   │   ├── CreateInvoice/
│   │   ├── RecordPayment/
│   │   ├── RecordUsage/
│   │   └── RateUsage/         — Convert raw usage into billable lines
│   └── Queries/
│       ├── GetInvoice/
│       ├── GetUsageSummary/
│       └── GetSubscription/
├── Wallow.Billing.Infrastructure/
│   ├── Persistence/           — BillingDbContext (absorbs metering tables)
│   └── Jobs/                  — UsageAggregationJob, InvoiceGenerationJob
└── Wallow.Billing.Api/
    ├── InvoicesController
    ├── SubscriptionsController
    └── UsageController
```

### Flow

Modules report usage via integration events or a shared contract (`IUsageReporter`). Billing's Metering subdomain records and aggregates usage. At billing cycle end, rating rules convert raw usage into invoice lines.

---

## Shared Infrastructure Additions

Three capabilities move from standalone modules into `src/Shared/Wallow.Shared.Infrastructure/`.

### Auditing (Audit.NET)

```
Shared.Infrastructure/
└── Auditing/
    ├── AuditInterceptor.cs        — EF Core SaveChanges interceptor
    ├── AuditEntry.cs              — Entity for the audit table
    └── AuditingExtensions.cs      — services.AddWallowAuditing()
```

**How it works:** Audit.NET hooks into every `DbContext.SaveChanges()` call. It captures every insert, update, and delete with old values, new values, user ID, tenant ID, and timestamp. Modules do nothing — auditing happens automatically.

**Storage:** All audit records go to a shared `audit.audit_entries` table in PostgreSQL.

**Package:** `Audit.EntityFramework.Core` (NuGet).

### Background Jobs (Hangfire Abstraction)

```
Shared.Infrastructure/
└── BackgroundJobs/
    ├── IJobScheduler.cs           — Schedule, Enqueue, RecurringJob
    └── HangfireJobScheduler.cs    — Implementation
```

**How it works:** Any module injects `IJobScheduler` and schedules work directly. No intermediary module. Hangfire manages execution, retries, and persistence.

```csharp
// Any module can do this:
_jobScheduler.Enqueue(() => ProcessInvoice(invoiceId));
_jobScheduler.AddRecurring("usage-aggregation", "0 */6 * * *",
    () => AggregateUsage());
```

### Workflows (Elsa 3 Server)

```
Shared.Infrastructure/
└── Workflows/
    ├── ElsaExtensions.cs          — services.AddWallowWorkflows()
    └── WorkflowActivityBase.cs    — Base class for module activities
```

**How it works:** Elsa 3 runs as embedded infrastructure, similar to Marten or Wolverine. The runtime, persistence, and server APIs are registered in `Program.cs`. Each module registers its own activities and triggers in its `AddXxxModule()` method.

```csharp
// In Billing's AddBillingModule():
elsa.AddActivity<InvoiceOverdueActivity>();
elsa.AddTrigger<InvoiceCreatedTrigger>();
```

**UI:** Elsa Studio is a separate frontend application that talks to Elsa's server APIs. It is not part of any module — it's a deployment/hosting concern, like Scalar for API docs.

---

## Cleanup Scope

### WallowModules.cs

Remove all `Add*Module()` and `Initialize*ModuleAsync()` calls for deleted modules. Add `AddCommunicationsModule()` (replacing three separate calls). The remaining registrations:

```csharp
services.AddIdentityModule(configuration);
services.AddStorageModule(configuration);
services.AddCommunicationsModule(configuration);  // replaces Email + Notifications + Announcements
services.AddBillingModule(configuration);          // now includes Metering
services.AddConfigurationModule(configuration);
```

### Shared.Contracts

Remove integration events that only deleted modules published or consumed. Keep events published by the 5 remaining modules.

### Marten Configuration

Remove projections and event stores for Sales, Inventory, and Scheduling. None of the 5 remaining modules use Marten event sourcing.

### Test Projects

Remove test projects under `tests/Modules/` for all 14 deleted modules. Update architecture tests.

### Directory.Packages.props

Audit packages that become unused after deletion. Add `Audit.EntityFramework.Core`.

---

## What This Does Not Change

- **Clean Architecture per module** — Each module keeps its 4-project structure (Domain, Application, Infrastructure, Api).
- **Wolverine + RabbitMQ** — Message bus and handler discovery remain the same.
- **Multi-tenancy** — Tenant isolation patterns remain unchanged.
- **EF Core + Dapper** — Write/read split stays.
- **Keycloak integration** — Identity module is unchanged.
