# Wallow Developer Onboarding Guide

Welcome to Wallow! This guide will get you from zero to productive in about 30 minutes. We'll start with getting the app running, then explore the codebase architecture, and finally equip you with the patterns you'll use daily.

---

## 1. Quick Start (5 Minutes)

### Prerequisites

Before you begin, make sure you have:

- **Docker Desktop** (or Docker Engine + Docker Compose)
- **.NET 10 SDK** ([download here](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Your favorite IDE** (Rider, VS Code with C# Dev Kit, or Visual Studio)
- **Git** (obviously, since you're reading this)

### Get It Running

```bash
# 1. Clone the repo (if you haven't already)
git clone https://github.com/your-org/wallow.git
cd wallow

# 2. Start infrastructure services
cd docker
docker compose up -d

# 3. Run the API (from repo root)
cd ..
dotnet run --project src/Wallow.Api

# You should see:
# [12:34:56 INF] [Api] Now listening on: http://localhost:5000
```

### Verify Everything Works

Open these URLs in your browser:

| Service | URL | Credentials |
|---------|-----|-------------|
| **API Swagger** | http://localhost:5000/scalar/v1 | N/A (explore the endpoints!) |
| **Keycloak Admin** | http://localhost:8080 | Username: `admin` / Password: `admin` |
| **Keycloak Realm** | Realm: `wallow` | Username: `admin@wallow.dev` / Password: `Admin123!` |
| **RabbitMQ Management** | http://localhost:15672 | Username: `guest` / Password: `guest` |
| **Mailpit (Email Sink)** | http://localhost:8025 | N/A |
| **Grafana (Observability)** | http://localhost:3000 | Username: `admin` / Password: `admin` |

If all URLs load, you're good to go! 🎉

---

## 2. Architecture Overview (10 Minutes)

### What Is a Modular Monolith?

Wallow is a **modular monolith** — a single deployable application (one API) internally organized into autonomous modules that communicate like microservices, but without the operational overhead of distributed systems.

**Think of it as:**
- A single ASP.NET Core web app
- With 8 independent "mini-apps" inside (modules)
- Each module owns its own database schema, domain logic, and API endpoints
- Modules communicate via **events** over RabbitMQ (never direct method calls)

**Benefits:**
- ✅ Simple deployment (one Docker container)
- ✅ Shared infrastructure (one PostgreSQL, one RabbitMQ)
- ✅ Atomic transactions within a module
- ✅ Easy to extract into microservices later if needed
- ✅ No network latency for local operations

### Module Structure: Clean Architecture

Every module follows the same 4-layer pattern:

```
📁 src/Modules/{Module}/
├── Wallow.{Module}.Domain           ← Entities, Value Objects, Domain Events (zero dependencies)
├── Wallow.{Module}.Application      ← Commands, Queries, Handlers, DTOs (depends on Domain)
├── Wallow.{Module}.Infrastructure   ← EF Core, Dapper, RabbitMQ Consumers (implements Application)
└── Wallow.{Module}.Api              ← Controllers, Request/Response DTOs (depends on Application)
```

**Dependency Rule:** Dependencies point inward. Infrastructure and Api depend on Application, Application depends on Domain, Domain depends on nothing.

**Example module:** `Billing`
```
Wallow.Billing.Domain
  ├── Entities: Invoice, Payment, Subscription
  ├── Value Objects: Money
  ├── Domain Events: InvoiceCreated, InvoicePaid
  └── Exceptions: InvalidInvoiceException

Wallow.Billing.Application
  ├── Commands: CreateInvoice, ProcessPayment
  ├── Queries: GetInvoiceById, GetInvoicesByUser
  └── Handlers: CreateInvoiceHandler (static class with HandleAsync)

Wallow.Billing.Infrastructure
  ├── Persistence: BillingDbContext (EF Core)
  ├── Consumers: InvoiceCreatedEventConsumer (RabbitMQ)
  └── Services: InvoiceQueryService (Dapper)

Wallow.Billing.Api
  └── Controllers: InvoicesController (thin wrapper around Wolverine IMessageBus)
```

### Key Architectural Concepts

#### 1. CQRS (Wolverine)

We use **Wolverine** as both a mediator (for commands/queries) and a message bus (for events).

- **Commands:** Write operations (`CreateInvoice`, `ProcessPayment`)
- **Queries:** Read operations (`GetInvoiceById`, `GetAllInvoices`)
- **Handlers:** Static classes with `HandleAsync` methods

**No MediatR. No manual registration. Wolverine auto-discovers handlers.**

#### 2. Shared Infrastructure

Cross-cutting capabilities live in `Shared.Infrastructure` and are available to all modules:
- **Auditing** (`Shared.Infrastructure/Auditing/`) — EF Core SaveChangesInterceptor via Audit.NET
- **Background Jobs** (`Shared.Infrastructure/BackgroundJobs/`) — IJobScheduler abstraction over Hangfire
- **Workflows** (`Shared.Infrastructure/Workflows/`) — Elsa 3 workflow engine integration

#### 3. Multi-Tenancy

Every entity (except a few global ones like `StorageBucket`) is **tenant-scoped**.

**How it works:**
1. JWT contains `org` claim (Keycloak organization ID)
2. `TenantResolutionMiddleware` extracts it and stores in `ITenantContext`
3. `TenantSaveChangesInterceptor` auto-stamps `TenantId` on new entities
4. `TenantQueryExtensions` auto-filters queries to current tenant

**You rarely think about tenancy — it's automatic.**

#### 4. Module Communication (Events via RabbitMQ)

Modules **never** reference each other's Domain/Application/Infrastructure layers. They communicate via:
- **Integration Events** (defined in `Wallow.Shared.Contracts`)
- **RabbitMQ** (Wolverine handles routing)

**Example flow:**
```
Identity module:
  1. User registers
  2. Publishes UserRegisteredEvent to RabbitMQ

Notifications module:
  3. Consumes UserRegisteredEvent
  4. Creates "Welcome!" in-app notification
```

**No direct calls. Ever.**

---

## 3. Codebase Exploration Checklist

Work through this checklist to build a mental map of the codebase. Check off each item as you complete it.

### 🎯 Startup & Infrastructure

- [ ] **Read `src/Wallow.Api/Program.cs`** (lines 1-150)
  - See the middleware pipeline: Exception handler → Auth → Tenant resolution → Permission expansion → Authorization
  - See Wolverine setup (handler discovery, RabbitMQ, durable outbox)
  - See Hangfire, SignalR, health checks

- [ ] **Read `src/Wallow.Api/WallowModules.cs`**
  - See explicit module registration (no magic auto-discovery)
  - Notice the groupings: Platform Modules and Feature Modules
  - Each module has `AddXxxModule()` and `InitializeXxxModuleAsync()` extension methods

### 🏗️ Module Deep Dive: Billing (EF Core Example)

Billing is the **gold standard DDD module**. Start here.

- [ ] **Domain: `src/Modules/Billing/Wallow.Billing.Domain/Entities/Invoice.cs`**
  - See the state machine: Draft → Issued → Paid/Overdue/Cancelled
  - See domain events: `InvoiceCreated`, `InvoicePaid`
  - See business rules: `Issue()`, `MarkAsPaid()`, `Cancel()`

- [ ] **Application: `src/Modules/Billing/Wallow.Billing.Application/Commands/CreateInvoice.cs`**
  - See the command record
  - See FluentValidation validator
  - See the handler: `CreateInvoiceHandler` (static class)

- [ ] **Infrastructure: `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContext.cs`**
  - See schema name: `billing`
  - See multi-tenancy: `builder.HasTenantQueryFilter<Invoice>()`
  - See JSONB configuration: `DictionaryValueComparer` for CustomFields

- [ ] **API: `src/Modules/Billing/Wallow.Billing.Api/Controllers/InvoicesController.cs`**
  - See thin controller: just validation + `await _messageBus.InvokeAsync(command)`
  - No business logic here!

### 🔐 Authentication & Multi-Tenancy

- [ ] **Middleware: `src/Modules/Identity/Wallow.Identity.Infrastructure/Middleware/TenantResolutionMiddleware.cs`**
  - See how JWT `org` claim becomes `ITenantContext.TenantId`

- [ ] **Middleware: `src/Modules/Identity/Wallow.Identity.Infrastructure/Middleware/PermissionExpansionMiddleware.cs`**
  - See how roles → permissions expansion works
  - User has `billing:admin` role → gets `billing:invoice:create` permission

- [ ] **Interceptor: `src/Shared/Wallow.Shared.Kernel/MultiTenancy/TenantSaveChangesInterceptor.cs`**
  - See auto-stamping of `TenantId` on new entities
  - See tampering prevention (can't change `TenantId` on updates)

### 📬 Integration Events

- [ ] **Browse: `src/Shared/Wallow.Shared.Contracts/`**
  - See integration events organized by module: `Identity/`, `Billing/`, `Storage/`, `Notifications/`, `Messaging/`, `Announcements/`, `Inquiries/`, `Showcases/`
  - These are module-to-module contracts (never change breaking fields!)

- [ ] **Example Handler: `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/`**
  - See how to consume an event from another module
  - Just a static class with `HandleAsync(UserRegisteredEvent evt)`

### 🏃 Running Tests

- [ ] **Run the test suite**
  ```bash
  dotnet test
  ```
  - Watch for Testcontainers spinning up Postgres/RabbitMQ/Valkey
  - Notice separate test projects: Domain.Tests, Infrastructure.Tests, Api.Tests

---

## 4. Common Patterns & Conventions

### Creating a New Module

Follow this step-by-step recipe:

#### Step 1: Create Projects

```bash
# From repo root
dotnet new classlib -n Wallow.YourModule.Domain -o src/Modules/YourModule/Wallow.YourModule.Domain
dotnet new classlib -n Wallow.YourModule.Application -o src/Modules/YourModule/Wallow.YourModule.Application
dotnet new classlib -n Wallow.YourModule.Infrastructure -o src/Modules/YourModule/Wallow.YourModule.Infrastructure
dotnet new classlib -n Wallow.YourModule.Api -o src/Modules/YourModule/Wallow.YourModule.Api

# Add project references
dotnet add src/Modules/YourModule/Wallow.YourModule.Application/Wallow.YourModule.Application.csproj reference src/Modules/YourModule/Wallow.YourModule.Domain/Wallow.YourModule.Domain.csproj

dotnet add src/Modules/YourModule/Wallow.YourModule.Infrastructure/Wallow.YourModule.Infrastructure.csproj reference src/Modules/YourModule/Wallow.YourModule.Application/Wallow.YourModule.Application.csproj

dotnet add src/Modules/YourModule/Wallow.YourModule.Api/Wallow.YourModule.Api.csproj reference src/Modules/YourModule/Wallow.YourModule.Application/Wallow.YourModule.Application.csproj
```

#### Step 2: Create Module Extension Methods

Create `src/Modules/YourModule/Wallow.YourModule.Infrastructure/Extensions/YourModuleExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.YourModule.Infrastructure.Extensions;

public static class YourModuleExtensions
{
    public static IServiceCollection AddYourModuleModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<YourModuleDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "yourmodule")));

        // Register application services
        services.AddYourModuleApplication();
        services.AddYourModuleInfrastructure(configuration);

        return services;
    }

    public static async Task<WebApplication> InitializeYourModuleAsync(
        this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YourModuleDbContext>();
        await db.Database.MigrateAsync();
        return app;
    }
}
```

#### Step 3: Register in WallowModules.cs

Edit `src/Wallow.Api/WallowModules.cs`:

```csharp
// Add using
using Wallow.YourModule.Infrastructure.Extensions;

// In AddWallowModules():
services.AddYourModuleModule(configuration);

// In InitializeWallowModulesAsync():
await app.InitializeYourModuleAsync();
```

#### Step 4: Create First Migration

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/YourModule/Wallow.YourModule.Infrastructure \
    --startup-project src/Wallow.Api \
    --context YourModuleDbContext
```

### Adding a Command Handler

**Example:** Create a new invoice

#### 1. Define the command (Application layer)

`src/Modules/Billing/Wallow.Billing.Application/Commands/CreateInvoice.cs`:

```csharp
using FluentValidation;

namespace Wallow.Billing.Application.Commands;

public record CreateInvoiceCommand(
    Guid UserId,
    DateOnly DueDate,
    string Currency,
    List<LineItemDto> LineItems);

public record LineItemDto(string Description, decimal Amount);

public class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DueDate).GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.LineItems).NotEmpty();
    }
}
```

#### 2. Create the handler (Application layer)

`src/Modules/Billing/Wallow.Billing.Application/Handlers/CreateInvoiceHandler.cs`:

```csharp
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Kernel;
using Wolverine;

namespace Wallow.Billing.Application.Handlers;

public static class CreateInvoiceHandler
{
    public static async Task<Result<Guid>> HandleAsync(
        CreateInvoiceCommand command,
        BillingDbContext db,
        IMessageBus bus,
        CancellationToken ct)
    {
        var invoice = Invoice.Create(command.UserId, command.DueDate, command.Currency);

        foreach (var item in command.LineItems)
        {
            invoice.AddLineItem(item.Description, new Money(item.Amount, command.Currency));
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        // Publish domain events as integration events
        await bus.PublishAsync(new InvoiceCreatedEvent(invoice.Id, invoice.UserId));

        return Result<Guid>.Success(invoice.Id);
    }
}
```

**That's it!** Wolverine auto-discovers the handler. No registration needed.

### Publishing an Integration Event

Integration events are defined in `Wallow.Shared.Contracts`.

#### 1. Define the event (Shared.Contracts)

`src/Shared/Wallow.Shared.Contracts/Events/InvoiceCreatedEvent.cs`:

```csharp
namespace Wallow.Shared.Contracts.Events;

public record InvoiceCreatedEvent(
    Guid InvoiceId,
    Guid UserId,
    string UserEmail) : IntegrationEvent;
```

#### 2. Publish it (from handler)

```csharp
await _messageBus.PublishAsync(new InvoiceCreatedEvent(invoice.Id, invoice.UserId, "user@example.com"));
```

Wolverine + RabbitMQ automatically:
- Routes the event to the correct exchange
- Delivers to all consumer queues
- Handles retries and dead-letter queue on failure

### Consuming an Event from Another Module

#### Create a consumer (Application or Infrastructure layer)

```csharp
// In the consuming module's Application or Infrastructure layer
public static class InvoiceCreatedEventHandler
{
    public static async Task HandleAsync(
        InvoiceCreatedEvent evt,
        INotificationService notifications,
        CancellationToken ct)
    {
        // React to the event from another module
        await notifications.CreateAsync(evt.UserId, "Invoice created", ct);
    }
}
```

**No queue setup needed.** Wolverine's `UseConventionalRouting()` auto-creates the queue.

### Adding a Dapper Read Query

For complex queries, use **Dapper** instead of EF Core LINQ.

`src/Modules/Billing/Wallow.Billing.Infrastructure/Services/InvoiceReportService.cs`:

```csharp
using Dapper;
using Npgsql;

namespace Wallow.Billing.Infrastructure.Services;

public class InvoiceReportService
{
    private readonly string _connectionString;

    public InvoiceReportService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }

    public async Task<decimal> GetTotalRevenueAsync(Guid tenantId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var sql = @"
            SELECT COALESCE(SUM(total_amount), 0)
            FROM billing.invoices
            WHERE tenant_id = @TenantId
              AND status = 'Paid'";

        return await conn.ExecuteScalarAsync<decimal>(sql, new { TenantId = tenantId });
    }
}
```

### Adding a Background Job (Hangfire)

#### 1. Create the job class

`src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/OverdueInvoiceJob.cs`:

```csharp
using Wallow.Billing.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Jobs;

public class OverdueInvoiceJob
{
    private readonly BillingDbContext _db;

    public OverdueInvoiceJob(BillingDbContext db) => _db = db;

    public async Task ExecuteAsync()
    {
        var overdueInvoices = await _db.Invoices
            .Where(i => i.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) && i.Status == InvoiceStatus.Issued)
            .ToListAsync();

        foreach (var invoice in overdueInvoices)
        {
            invoice.MarkAsOverdue();
        }

        await _db.SaveChangesAsync();
    }
}
```

#### 2. Register the recurring job

`src/Modules/Billing/Wallow.Billing.Infrastructure/Extensions/BillingModuleExtensions.cs`:

```csharp
public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration config)
{
    // ... existing registrations

    // Register Hangfire job
    services.AddScoped<OverdueInvoiceJob>();

    return services;
}

// In your startup (or via IRecurringJobRegistration interface)
RecurringJob.AddOrUpdate<OverdueInvoiceJob>(
    "billing-overdue-invoices",
    job => job.ExecuteAsync(),
    Cron.Daily);
```

### Multi-Tenancy: ITenantScoped

**Every entity must implement `ITenantScoped`** (unless it's intentionally global).

```csharp
public class Invoice : AggregateRoot<Guid>, ITenantScoped
{
    public Guid TenantId { get; private set; } // Required by ITenantScoped

    // ... rest of entity
}
```

**That's it.** The interceptor and query filters handle the rest automatically.

### Result<T> Error Handling

**Never throw exceptions for business rule violations.** Use `Result<T>`.

```csharp
public Result<Invoice> Issue()
{
    if (Status != InvoiceStatus.Draft)
        return Result<Invoice>.Failure("Cannot issue an invoice that is not in Draft status");

    if (LineItems.Count == 0)
        return Result<Invoice>.Failure("Cannot issue an invoice with no line items");

    Status = InvoiceStatus.Issued;
    IssuedAt = DateTime.UtcNow;

    return Result<Invoice>.Success(this);
}
```

In the handler:

```csharp
var result = invoice.Issue();
if (!result.IsSuccess)
    return Result<Guid>.Failure(result.Error);

await db.SaveChangesAsync(ct);
return Result<Guid>.Success(invoice.Id);
```

---

## 5. Points of Interest

Things you'll wonder about — here are the answers:

### Why doesn't Identity use CQRS?

**Short answer:** Keycloak is the source of truth.

The Identity module interacts with Keycloak's Admin API for users, orgs, and roles. There's no local database for these entities. CQRS would add ceremony without benefit.

**Exception:** Service Accounts DO use CQRS because they have local state (metadata, status, tracking).

### Where is Email handling?

Email sending is handled within the **Notifications** module. The Notifications module manages in-app and push notifications, consuming events from Identity and Billing.

### Which module is the best example?

**Billing module** is the gold standard.

Why?
- Rich domain model (textbook DDD)
- Full CQRS implementation
- FluentValidation on all commands
- Strong state machine (Invoice: Draft → Issued → Paid/Overdue)
- Value Objects (`Money` with currency-safe arithmetic)
- Strongly-typed IDs (`InvoiceId`, `PaymentId`)
- Integration events properly published
- Comprehensive tests

**Start here when learning a new pattern.**

### How do the Notifications and Announcements modules work?

The **Notifications** module handles in-app and push notifications, consuming integration events from Identity and Billing.

**How it consumes events:**
```
UserRegisteredEvent (Identity) → Create welcome notification
InvoicePaidEvent (Billing) → Create payment confirmation notification
```

The **Announcements** module handles system-wide announcements and changelog entries. The **Messaging** module handles user-to-user conversations.

---

## 6. Testing Guide

### Unit Tests (Domain Logic)

Test domain entities and value objects in isolation.

**Example:** `tests/Modules/Billing/Billing.Domain.Tests/InvoiceTests.cs`

```csharp
[Fact]
public void Issue_WithNoLineItems_ShouldFail()
{
    // Arrange
    var invoice = Invoice.Create(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), "USD");

    // Act
    var result = invoice.Issue();

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("no line items");
}
```

**Run:**
```bash
dotnet test --filter "Category!=Integration"
```

### Integration Tests (API + Database)

Use **Testcontainers** to spin up real PostgreSQL/RabbitMQ/Valkey in Docker.

**Example:** `tests/Modules/Billing/Billing.Api.Tests/InvoicesControllerTests.cs`

```csharp
public class InvoicesControllerTests : IClassFixture<WallowApiFactory>
{
    private readonly HttpClient _client;

    public InvoicesControllerTests(WallowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateInvoice_ShouldReturn201()
    {
        // Arrange
        var command = new CreateInvoiceCommand(
            UserId: Guid.NewGuid(),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Currency: "USD",
            LineItems: [new LineItemDto("Consulting", 1000m)]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/invoices", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

**Run:**
```bash
dotnet test --filter "Category=Integration"
```

### Architecture Tests (Enforce Rules)

Use **NetArchTest.Rules** to enforce architectural constraints.

**Example:** `tests/Wallow.Architecture.Tests/DependencyTests.cs`

```csharp
[Fact]
public void Domain_ShouldNotDependOnApplication()
{
    var result = Types.InAssembly(typeof(Invoice).Assembly)
        .Should()
        .NotHaveDependencyOn("Wallow.Billing.Application")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

**Run:**
```bash
dotnet test tests/Wallow.Architecture.Tests
```

---

## 7. FAQ

### Q: Where do I add a new API endpoint?

**A:** In the module's `Api` project. Controllers should be thin — just validate and delegate to Wolverine.

```csharp
[HttpPost]
public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceCommand command)
{
    var result = await _messageBus.InvokeAsync<Result<Guid>>(command);
    return result.IsSuccess
        ? CreatedAtAction(nameof(GetInvoice), new { id = result.Value }, result.Value)
        : UnprocessableEntity(result.Error);
}
```

### Q: How do I test RabbitMQ message consumption?

**A:** Use Testcontainers to spin up RabbitMQ, then publish an event and assert the side effect.

```csharp
[Fact]
public async Task InvoiceCreated_ShouldCreateNotification()
{
    // Arrange
    var evt = new InvoiceCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "user@example.com");

    // Act
    await _messageBus.PublishAsync(evt);
    await Task.Delay(1000); // Wait for async processing

    // Assert
    var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.UserId == evt.UserId);
    notification.Should().NotBeNull();
}
```

### Q: How do I debug a failing background job?

**A:** Open Hangfire dashboard at http://localhost:5000/hangfire, find the job, and check the exception details.

### Q: How do I add a new migration?

**A:**

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext
```

### Q: How do I reset my local database?

**A:**

```bash
cd docker
docker compose down -v  # Delete volumes
docker compose up -d    # Recreate
dotnet run --project src/Wallow.Api  # Re-run migrations
```

### Q: Can I query across modules?

**Short answer:** No.

**Long answer:** Modules are autonomous. If Module A needs data from Module B:
1. Module B publishes an event
2. Module A consumes it and stores a local copy (eventual consistency)

**Example:** Notifications module doesn't query Billing directly. It listens to `InvoicePaidEvent` and creates a notification.

### Q: What if I need real-time cross-module data?

Use **Shared.Contracts query service interfaces** (e.g., `IInvoiceQueryService`).

These are implemented in the module's Infrastructure layer and injected via DI. The interface is in Shared.Contracts so other modules can reference it without coupling to the implementation.

**Trade-off:** This creates a direct dependency (not event-driven). Use sparingly.

### Q: How do I add a new strongly-typed ID?

```csharp
public readonly record struct InvoiceId(Guid Value);
```

Then configure EF Core:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.Entity<Invoice>()
        .Property(e => e.Id)
        .HasConversion(
            id => id.Value,
            value => new InvoiceId(value));
}
```

---

## 8. Useful Links

### Local Development URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5000 | N/A |
| API Docs (Scalar) | http://localhost:5000/scalar/v1 | N/A |
| Keycloak Admin | http://localhost:8080 | See `docker/.env` |
| Keycloak Realm | wallow | admin@wallow.dev / Admin123! |
| RabbitMQ Management | http://localhost:15672 | See `docker/.env` |
| Mailpit | http://localhost:8025 | N/A |
| Grafana | http://localhost:3000 | admin / admin |
| Hangfire Dashboard | http://localhost:5000/hangfire | (auth via JWT) |

### Design Documents

Core architecture:
- **Architecture Reference:** `docs/WALLOW.md` — Single architecture and design reference
- **Developer Guide:** `docs/DEVELOPER_GUIDE.md` — How to work in the codebase
- **Deployment Guide:** `docs/DEPLOYMENT_GUIDE.md` — Server setup, CI/CD, Docker

### Audit Reports

Recent codebase audit:
- **Consolidated Findings:** `docs/audit/CONSOLIDATED-FINDINGS.md` — Executive summary, critical issues

---

## Next Steps

Now that you're oriented:

1. **Pick a small issue** from the backlog and assign yourself
2. **Ask questions** in Slack/Teams (architecture, patterns, "why is X done this way?")
3. **Pair with a teammate** on your first PR
4. **Read the Billing module** end-to-end (it's the reference implementation)
5. **Run the tests** and see what breaks when you change things

Welcome aboard! 🚀
