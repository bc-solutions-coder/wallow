# Wallow

Wallow is a production-ready .NET 10 modular monolith base platform with 7 core modules. Products are built by forking this repository and adding domain-specific modules on top of the base. The platform provides: Identity (Keycloak), Billing, Storage, Notifications, Messaging, Announcements, and Inquiries — all with multi-tenancy, messaging infrastructure, observability, background jobs, workflow automation, and auditing built in.

---

## Architecture

Modular monolith. Each module is an autonomous bounded context with four layers following Clean Architecture:

```
Domain         -> Entities, Value Objects, Domain Events
                  No external dependencies. Pure business logic.

Application    -> Commands, Queries, Handlers, DTOs, Interfaces
                  Depends on Domain only.

Infrastructure -> EF Core DbContexts, Dapper queries, RabbitMQ consumers, external service clients
                  Implements Application interfaces. Depends on Application and Domain.

Api            -> Controllers, Request/Response contracts
                  Depends on Application only. Never references Infrastructure directly.
```

Modules never reference each other. Cross-module communication happens exclusively through integration events defined in `Shared.Contracts` and routed via RabbitMQ.

### Project Layout

```
src/
  Wallow.Api/                          # Host — wires modules, middleware pipeline, Program.cs
  Modules/
    Identity/
      Wallow.Identity.Domain/
      Wallow.Identity.Application/
      Wallow.Identity.Infrastructure/
      Wallow.Identity.Api/
    Billing/            (same four-layer structure)
    Storage/            (same four-layer structure)
    Notifications/      (same four-layer structure)
    Messaging/          (same four-layer structure)
    Announcements/      (same four-layer structure)
    Inquiries/          (same four-layer structure)
  Shared/
    Wallow.Shared.Contracts/           # Integration events and cross-module DTOs
    Wallow.Shared.Kernel/              # Base classes: Entity, AggregateRoot, ValueObject,
                                        # Result, ITenantContext, TenantSaveChangesInterceptor
    Wallow.Shared.Infrastructure/      # Cross-cutting infrastructure (Auditing, BackgroundJobs, Workflows)
```

### Dependency Rules

- **Domain** depends on nothing.
- **Application** depends on Domain.
- **Infrastructure** depends on Application and Domain.
- **Api** depends on Application.
- **No module references another module.** Events only.
- **Api never references Infrastructure.** Uses Application interfaces; DI wires the implementations.

### Request Pipeline

```
HTTP Request
  -> Controller (Api)
  -> Command/Query record (Application)
  -> Wolverine IMessageBus.InvokeAsync
  -> FluentValidation middleware (automatic)
  -> Handler (Application)
  -> Repository/Service (Infrastructure, via interface)
  -> DTO (Application)
  -> HTTP Response
```

---

## Technology Stack

| Purpose              | Technology                              |
|----------------------|-----------------------------------------|
| Framework            | .NET 10                                 |
| Database             | PostgreSQL 18                           |
| ORM (writes)         | EF Core                                 |
| Queries (complex)    | Dapper                                  |
| CQRS & Messaging     | Wolverine (mediator + RabbitMQ transport) |
| Message Broker       | RabbitMQ                                |
| Cache / SignalR backplane | Valkey (Redis-compatible)          |
| Real-time            | SignalR                                 |
| Identity Provider    | Keycloak 26                             |
| Workflow Engine      | Elsa 3.x (Shared.Infrastructure)        |
| Validation           | FluentValidation                        |
| Logging              | Serilog (console + OpenTelemetry)       |
| Background Jobs      | Hangfire (PostgreSQL storage, Shared.Infrastructure) |
| Auditing             | Audit.NET (Shared.Infrastructure)       |
| Observability        | OpenTelemetry -> Grafana                |
| Cloud Storage        | AWS S3 SDK                              |
| Testing              | xUnit, Testcontainers, FluentAssertions, NSubstitute, Bogus |
| Architecture Tests   | NetArchTest                             |
| API Documentation    | OpenAPI + Scalar                        |

---

## Multi-Tenancy

Tenants map to Keycloak Organizations within a single `wallow` realm. Data isolation uses EF Core global query filters on `TenantId`.

### How It Works

1. **TenantResolutionMiddleware** runs after authentication. Reads the organization claim from the Keycloak JWT and populates a scoped `ITenantContext` for the request.

2. **EF Core global query filters** — Each module's `DbContext` applies `HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)` on all entities implementing `ITenantScoped`.

3. **TenantSaveChangesInterceptor** — Automatically stamps `TenantId` on new entities at save time. Prevents modification of `TenantId` on existing entities.

4. **RabbitMQ messages** carry tenant context through Wolverine's message headers.

### Key Types (Shared.Kernel)

| Type                          | Purpose                                           |
|-------------------------------|---------------------------------------------------|
| `ITenantContext`              | Request-scoped. Exposes `TenantId`, `TenantName`, `IsResolved`. |
| `TenantContext`               | Concrete implementation, populated by middleware.  |
| `ITenantScoped`               | Marker interface for entities requiring tenant isolation. |
| `TenantId`                    | Strongly-typed ID (same pattern as all IDs in the system). |
| `TenantSaveChangesInterceptor`| EF Core interceptor — sets TenantId on insert, blocks TenantId changes on update. |

### Superadmin Override

Users with the `admin` realm role can pass an `X-Tenant-Id` header to impersonate a tenant. Admin queries can use `.IgnoreQueryFilters()` when cross-tenant access is needed.

---

## Module Inventory

Wallow contains 7 core modules plus shared infrastructure capabilities.

### Modules

#### Identity

Thin adapter around Keycloak. No local user database — all user data lives in Keycloak.

**Provides:**
- OIDC JWT validation against Keycloak JWKS endpoint
- `PermissionExpansionMiddleware` — maps Keycloak role claims (`admin`, `manager`, `user`) to fine-grained `PermissionType` claims at request time
- `TenantResolutionMiddleware` — resolves tenant from JWT org claim
- `KeycloakAdminService` — wraps Keycloak Admin REST API for user CRUD and role assignment
- `KeycloakOrganizationService` — wraps Admin API for organization/membership management
- Controllers: `UsersController`, `RolesController`, `OrganizationsController`

**Publishes:** `UserRegisteredEvent`, `UserRoleChangedEvent`, `OrganizationCreatedEvent`, `OrganizationMemberAddedEvent`, `OrganizationMemberRemovedEvent`

**NuGet packages:** `Keycloak.AuthServices.Authentication`, `Keycloak.AuthServices.Sdk`

#### Billing

Invoicing, payment tracking, and usage metering. Tenant-scoped.

**Publishes:** `InvoiceCreatedEvent`, `PaymentReceivedEvent`, `InvoicePaidEvent`, `InvoiceOverdueEvent`

#### Storage

Raw file storage abstraction (S3, local filesystem). Provides upload, download, and URL generation.

#### Notifications

In-app and push notifications with delivery preferences. Consumes events from Identity and Billing to create notifications automatically.

#### Messaging

User-to-user conversations and threaded messaging.

#### Announcements

System-wide announcements, banners, and changelog entries.

#### Inquiries

Inquiry and question submission workflows.

### Shared Infrastructure Capabilities

Cross-cutting concerns that live in `Shared.Infrastructure` and are available to all modules:

| Capability | Location | Technology |
|------------|----------|------------|
| **Auditing** | `Shared.Infrastructure/Auditing/` | Audit.NET — EF Core SaveChangesInterceptor |
| **Background Jobs** | `Shared.Infrastructure/BackgroundJobs/` | Hangfire — IJobScheduler abstraction |
| **Workflows** | `Shared.Infrastructure/Workflows/` | Elsa 3 — WorkflowActivityBase for custom activities |

---

## Cross-Module Communication

Modules communicate through integration events. No module ever imports another module's types.

### Event Contract

All events implement `IIntegrationEvent` (defined in `Shared.Contracts`):

```csharp
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
```

Events use primitive types only — plain `Guid` for IDs, no strongly-typed domain IDs. This keeps serialization simple across module boundaries.

### Transport

Wolverine handles both in-process mediation (commands/queries) and RabbitMQ transport (integration events).

**Publishing:** Handlers call `IMessageBus.PublishAsync(new SomeEvent(...))`. Wolverine routes the event to a named RabbitMQ exchange based on configuration in `Program.cs`.

**Consuming:** Convention-based handler classes in the consuming module's Infrastructure layer. Wolverine discovers them automatically. Example:

```csharp
// In Notifications.Application
public static class UserRegisteredEventHandler
{
    public static async Task HandleAsync(
        UserRegisteredEvent @event,
        INotificationService notifications,
        CancellationToken ct)
    {
        // Create welcome notification
    }
}
```

### Exchange Topology

Wolverine uses conventional routing with automatic queue/exchange discovery. Common patterns:

| Exchange          | Events                                                    | Consumers                  |
|-------------------|-----------------------------------------------------------|----------------------------|
| `identity-events` | UserRegistered, UserRoleChanged, OrganizationCreated, OrganizationMemberAdded/Removed | Notifications, Billing |
| `billing-events`  | InvoiceCreated, PaymentReceived, InvoicePaid, InvoiceOverdue | Notifications |

Consumer queues follow the pattern `{module}-inbox` (e.g., `notifications-inbox`, `billing-inbox`).

### Durability

Wolverine's PostgreSQL-backed durable outbox ensures events are not lost if RabbitMQ is temporarily unavailable. Messages are persisted in a `wolverine` schema and retried with configurable error handling policies.

---

## Fork Workflow

Wallow is a base platform. To build a product:

1. **Fork** the repository.
2. **Rename** — Update solution name, namespaces, Docker image names, Keycloak realm/clients as needed.
3. **Add modules** — Create new modules under `src/Modules/` following the four-layer structure. Register them in `WallowModules.cs`.
4. **Define events** — Add integration events to `Shared.Contracts` for your new modules. Wolverine handles routing automatically.
5. **Configure tenants** — Set up Keycloak Organizations for your customers. The multi-tenancy infrastructure (query filters, save interceptor, middleware) works automatically for any entity implementing `ITenantScoped`.
6. **Deploy** — Docker Compose configurations are in `docker/` (local development) and `deploy/` (staging/production). CI/CD publishes images to GitHub Container Registry on version tags.

### Adding a Module Checklist

1. Create `src/Modules/{Name}/Wallow.{Name}.Domain` (class library)
2. Create `src/Modules/{Name}/Wallow.{Name}.Application` (class library, references Domain)
3. Create `src/Modules/{Name}/Wallow.{Name}.Infrastructure` (class library, references Application)
4. Create `src/Modules/{Name}/Wallow.{Name}.Api` (class library, references Application)
5. Add integration events to `Shared.Contracts/{Name}/Events/`
6. Create module extension methods in Infrastructure (`Add{Name}Module`, `Initialize{Name}ModuleAsync`)
7. Register module in `src/Wallow.Api/WallowModules.cs`
8. Create test project under `tests/Modules/{Name}/`

**Note:** Wolverine automatically discovers handlers in all `Wallow.*` assemblies and uses conventional RabbitMQ routing — no manual exchange/queue configuration needed.

---

## Infrastructure Services

| Service     | Dev URL                    | Credentials       | Purpose                     |
|-------------|----------------------------|--------------------|-----------------------------|
| API         | http://localhost:5000       | -                  | Application                 |
| API Docs    | http://localhost:5000/scalar/v1 | -              | Scalar OpenAPI UI           |
| Keycloak    | http://localhost:8080       | See `docker/.env`  | Identity provider admin     |
| RabbitMQ    | http://localhost:15672      | See `docker/.env`  | Message broker management   |
| Mailpit     | http://localhost:8025       | -                  | Email capture (dev only)    |
| PostgreSQL  | localhost:5432              | See `docker/.env`  | Database                    |
| Grafana     | http://localhost:3001       | admin / admin      | Observability dashboards    |
| Hangfire    | http://localhost:5000/hangfire | -               | Background job dashboard    |

---

## Technology Rationale

**Wolverine over MediatR** -- Wolverine serves as both the in-process mediator (replacing MediatR) and the RabbitMQ transport. One library handles CQRS dispatch, durable outbox, retry policies, and message routing. This eliminates the MediatR + MassTransit split that most .NET projects carry.

**EF Core + Dapper** -- EF Core handles writes through the repository pattern, giving us change tracking, migrations, and the `SaveChanges` interceptor for tenant stamping. Dapper handles complex read queries where EF-generated SQL would be inefficient or unreadable. The two coexist in the same DbContext connection.

**Keycloak over custom auth** -- Offloads password storage, MFA, token lifecycle, social login, and SAML/LDAP federation to battle-tested infrastructure. The Identity module becomes a thin adapter rather than a security liability.

**PostgreSQL schemas over separate databases** -- Each module gets its own schema (`billing`, `notifications`, etc.) in a single database. This keeps local development simple (one connection string) while still enforcing module data ownership. A tenant can be promoted to a dedicated database later if scale demands it.

**Testcontainers over mocked infrastructure** -- Integration tests spin up real Postgres, RabbitMQ, and Redis containers. This catches configuration and query issues that mocks would miss, at the cost of slightly slower test runs.

**SignalR with Redis backplane** -- Enables real-time notifications that scale horizontally. Redis acts as the backplane for multi-instance deployments.

---

## Further Reading

- **Developer guide:** `docs/DEVELOPER_GUIDE.md` -- Day-to-day development workflows, coding patterns, and conventions.
- **Forking guide:** `docs/FORKING_GUIDE.md` -- Step-by-step instructions for creating a new product from Wallow.
- **Developer guide:** `docs/DEVELOPER_GUIDE.md` -- Day-to-day development workflows, coding patterns, and conventions.
