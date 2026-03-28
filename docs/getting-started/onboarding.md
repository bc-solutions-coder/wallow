# Wallow Developer Onboarding

This guide gets you from zero to productive. For the full architecture reference, see the [Developer Guide](developer-guide.md).

---

## 1. Quick Start

### Prerequisites

- **Docker Desktop** (or Docker Engine + Docker Compose)
- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Your preferred IDE** (Rider, VS Code with C# Dev Kit, or Visual Studio)
- **Git**

### Get It Running

```bash
# 1. Clone the repo
git clone https://github.com/your-org/wallow.git
cd wallow

# 2. Start infrastructure services
cd docker && docker compose up -d

# 3. Run the API (from repo root)
cd ..
dotnet run --project src/Wallow.Api

# You should see:
# [12:34:56 INF] [Api] Now listening on: http://localhost:5000
```

### Verify Everything Works

| Service | URL | Credentials |
|---------|-----|-------------|
| API Docs (Scalar) | http://localhost:5000/scalar/v1 | N/A |
| Mailpit (Email Sink) | http://localhost:8025 | N/A |
| Grafana (Observability) | http://localhost:3001 | admin / See `docker/.env` |

If all URLs load, you are ready.

---

## 2. Architecture at a Glance

Wallow is a **modular monolith** -- a single deployable application internally organized into autonomous modules. Each module owns its own database schema, domain logic, and API endpoints. Modules communicate via **Wolverine in-memory events**, never direct references.

### Module Structure

Every module follows Clean Architecture with four layers:

```
src/Modules/{Module}/
  Wallow.{Module}.Domain           -- Entities, Value Objects, Domain Events (zero dependencies)
  Wallow.{Module}.Application      -- Commands, Queries, Handlers, DTOs (depends on Domain)
  Wallow.{Module}.Infrastructure   -- EF Core, Dapper, Consumers (implements Application interfaces)
  Wallow.{Module}.Api              -- Controllers, Request/Response DTOs (depends on Application)
```

Dependencies point inward. Domain depends on nothing. Infrastructure and Api depend on Application. Application depends on Domain.

### Key Concepts

**CQRS via Wolverine.** Wolverine acts as both mediator and message bus. Handlers are static classes with `HandleAsync` methods, auto-discovered from all `Wallow.*` assemblies. No manual registration needed.

**Multi-tenancy.** The JWT contains an `org_id` claim. `TenantResolutionMiddleware` extracts it into `ITenantContext`. `TenantSaveChangesInterceptor` auto-stamps `TenantId` on new entities. EF Core global query filters scope all reads to the current tenant. You rarely need to think about tenancy -- it is automatic.

**Module communication.** Modules never reference each other directly. They communicate via integration events defined in `Wallow.Shared.Contracts`, published and consumed through Wolverine.

**Shared infrastructure.** Cross-cutting capabilities live in separate shared projects:
- **Auditing** (`Shared.Infrastructure.Core/Auditing/`) -- EF Core `SaveChangesInterceptor` for change tracking
- **Background Jobs** (`Shared.Infrastructure.BackgroundJobs/`) -- `IJobScheduler` abstraction over Hangfire
- **Workflows** (`Shared.Infrastructure.Workflows/`) -- Elsa 3 workflow engine integration

---

## 3. Codebase Exploration Checklist

Work through this list to build a mental map of the codebase.

### Startup and Infrastructure

- [ ] **Read `src/Wallow.Api/Program.cs`** -- See the middleware pipeline (exception handler, auth, tenant resolution, permission expansion, authorization), Wolverine setup, Hangfire, SignalR, and health checks.
- [ ] **Read `src/Wallow.Api/WallowModules.cs`** -- See explicit module registration via `IFeatureManager`. Identity is always registered; all other modules are behind feature flags.

### Module Deep Dive: Billing

Billing is the reference implementation for DDD patterns. Start here.

- [ ] **Domain:** `src/Modules/Billing/Wallow.Billing.Domain/Entities/` -- State machines, domain events, business rules.
- [ ] **Application:** `src/Modules/Billing/Wallow.Billing.Application/Commands/` -- Command records, FluentValidation validators, static handler classes.
- [ ] **Infrastructure:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContext.cs` -- Schema name, multi-tenancy query filters, JSONB configuration.
- [ ] **API:** `src/Modules/Billing/Wallow.Billing.Api/Controllers/` -- Thin controllers that delegate to Wolverine `IMessageBus`.

### Authentication and Multi-Tenancy

- [ ] **`src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs`** -- How JWT `org_id` claim becomes `ITenantContext.TenantId`.
- [ ] **`src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs`** -- How roles expand to permission claims.
- [ ] **`src/Shared/Wallow.Shared.Kernel/MultiTenancy/TenantSaveChangesInterceptor.cs`** -- Auto-stamping of `TenantId` on new entities and tampering prevention.

### Integration Events

- [ ] **Browse `src/Shared/Wallow.Shared.Contracts/`** -- Integration events organized by module. These are module-to-module contracts.
- [ ] **Browse `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/`** -- How to consume events from other modules.

### Run the Tests

```bash
./scripts/run-tests.sh
```

Watch for Testcontainers spinning up Postgres and Valkey.

---

## 4. Common Patterns

### Result Pattern

Never throw exceptions for business rule violations. Use `Result<T>` from `Shared.Kernel`.

### Adding a New Command

1. Define the command record in the Application layer
2. Add a FluentValidation validator
3. Create a static handler class with `HandleAsync` -- Wolverine discovers it automatically
4. Add a thin controller action that dispatches via `IMessageBus`

For the full step-by-step guide to creating a new module, see the [Developer Guide](developer-guide.md#adding-a-new-module).

---

## 5. Points of Interest

**Why doesn't Identity use CQRS?** ASP.NET Core Identity is the source of truth for user accounts. CQRS would add ceremony without benefit. Exception: Service Accounts do use CQRS because they have local state.

**Where is email handling?** In the Notifications module. It consumes events from Identity and Billing to send transactional emails.

**Which module is the best example?** Billing. Rich domain model, full CQRS, FluentValidation, state machine, value objects, strongly-typed IDs, integration events, and comprehensive tests.

---

## 6. Testing

Run all tests with `./scripts/run-tests.sh`. Run a specific module with `./scripts/run-tests.sh billing`.

**Unit tests** test domain entities and handlers in isolation with mocked dependencies. **Integration tests** use Testcontainers to spin up real Postgres and Valkey. **Architecture tests** (`Wallow.Architecture.Tests`) enforce structural rules such as module isolation and dependency direction.

For detailed testing patterns and examples, see the [Developer Guide](developer-guide.md#testing).

---

## 7. FAQ

**Where do I add a new API endpoint?** In the module's `Api` project. Controllers should be thin -- validate and delegate to Wolverine.

**How do I add a new migration?**
```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext
```

**How do I reset my local database?**
```bash
cd docker && docker compose down -v && docker compose up -d
dotnet run --project src/Wallow.Api  # Re-runs migrations
```

**Can I query across modules?** No. Modules are autonomous. If Module A needs data from Module B, Module B publishes an event and Module A stores a local copy (eventual consistency). For rare cases requiring synchronous cross-module reads, `Shared.Contracts` defines query service interfaces (e.g., `IInvoiceQueryService`) implemented in the owning module's Infrastructure layer.

---

## 8. Useful Links

| Resource | Location |
|----------|----------|
| API Docs (Scalar) | http://localhost:5000/scalar/v1 |
| Mailpit | http://localhost:8025 |
| Grafana | http://localhost:3001 |
| Hangfire Dashboard | http://localhost:5000/hangfire |
| AsyncAPI Viewer | http://localhost:5000/asyncapi |
| Developer Guide | [developer-guide.md](developer-guide.md) |
| Architecture Assessment | [../architecture/assessment.md](../architecture/assessment.md) |
| Deployment Guide | [../operations/deployment.md](../operations/deployment.md) |

---

## Next Steps

1. Pick a small issue from the backlog
2. Read the Billing module end-to-end (it is the reference implementation)
3. Pair with a teammate on your first PR
4. Run the tests and see what breaks when you change things
