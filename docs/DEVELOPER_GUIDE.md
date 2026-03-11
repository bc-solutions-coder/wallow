# Foundry Developer Guide

---

## Prerequisites

- .NET 10 SDK
- Docker and Docker Compose
- Rider or Visual Studio 2022+

---

## Getting Started

### 1. Start Infrastructure

Foundry depends on PostgreSQL, RabbitMQ, Mailpit, Valkey (Redis-compatible cache), and Keycloak. Docker Compose provisions all of them:

```bash
cd docker && docker compose up -d
```

Keycloak takes 1-2 minutes on first boot. The `foundry` realm is auto-provisioned with clients, roles, and a test user.

### 2. Run the API

```bash
dotnet run --project src/Foundry.Api
```

The API starts on `http://localhost:5000`. Interactive API documentation is available at `http://localhost:5000/scalar/v1`.

### 3. Run Tests

```bash
# All tests
dotnet test

# Specific test project
dotnet test tests/Modules/Billing/Billing.Domain.Tests

# All Billing tests (using filter)
dotnet test --filter "FullyQualifiedName~Billing"

# By category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

Integration tests require Docker. Testcontainers spins up ephemeral Postgres, RabbitMQ, and Valkey containers automatically.

### Local Services

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5000 | - |
| Scalar Docs | http://localhost:5000/scalar/v1 | - |
| Keycloak Admin | http://localhost:8080 | See `docker/.env` |
| Keycloak Account | http://localhost:8080/realms/foundry/account | admin@foundry.dev / Admin123! |
| RabbitMQ | http://localhost:15672 | See `docker/.env` |
| Mailpit | http://localhost:8025 | - |
| PostgreSQL | localhost:5432 | See `docker/.env` |
| AsyncAPI Viewer | http://localhost:5000/asyncapi | Dev only |
| Grafana | http://localhost:3000 | admin / admin |

### Getting a Test Token

```bash
curl -s -X POST http://localhost:8080/realms/foundry/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=foundry-api" \
  -d "client_secret=foundry-api-secret" \
  -d "username=admin@foundry.dev" \
  -d "password=Admin123!"
```

### Resetting Infrastructure

```bash
cd docker && docker compose down -v && docker compose up -d
```

---

## Architecture Overview

Foundry is a modular monolith. Each module is an autonomous bounded context that follows Clean Architecture internally and communicates with other modules exclusively through integration events over RabbitMQ. Modules never reference each other directly.

**Modules:** Identity, Storage, Communications, Billing

**Shared libraries:**
- `Foundry.Shared.Contracts` -- Cross-module integration events and DTOs
- `Foundry.Shared.Kernel` -- Base classes, multi-tenancy primitives, shared abstractions
- `Foundry.Shared.Infrastructure` -- Cross-cutting infrastructure (auditing, background jobs, workflows)

---

## Shared Infrastructure

Cross-cutting capabilities that were previously separate modules now live in `Foundry.Shared.Infrastructure` and are registered centrally. Modules access these through DI -- they don't reference other modules.

### Auditing (`Shared.Infrastructure/Auditing/`)

An EF Core `SaveChangesInterceptor` that automatically captures all entity changes (inserts, updates, deletes) across every module's DbContext. Audit entries include the entity type, primary key, old/new values (serialized JSON), the acting user, tenant, and timestamp. Entries are stored in a dedicated `audit` schema via `AuditDbContext`.

**Registration:** `services.AddFoundryAuditing(configuration)` registers the `AuditDbContext` and `AuditInterceptor` singleton. Module DbContexts pick up auditing automatically by adding the interceptor to their options (via `options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>())`).

### Background Jobs (`Shared.Infrastructure/BackgroundJobs/`)

A thin `IJobScheduler` abstraction (defined in `Shared.Kernel/BackgroundJobs/`) over Hangfire for fire-and-forget and recurring jobs. Modules inject `IJobScheduler` to enqueue work without depending on Hangfire directly.

```csharp
public interface IJobScheduler
{
    string Enqueue(Expression<Func<Task>> job);
    string Enqueue<T>(Expression<Func<T, Task>> job);
    void AddRecurring(string id, string cron, Expression<Func<Task>> job);
    void RemoveRecurring(string id);
}
```

**Registration:** `services.AddFoundryBackgroundJobs()` registers `HangfireJobScheduler` as the `IJobScheduler` implementation.

### Workflows (`Shared.Infrastructure/Workflows/`)

Elsa 3 workflow engine integration for long-running, multi-step business processes. Elsa stores workflow definitions and runtime state in PostgreSQL via EF Core. Modules define custom workflow activities by extending `WorkflowActivityBase`, which adds module-scoped logging and execution context. Activities are auto-discovered from all `Foundry.*` assemblies at startup.

```csharp
public class SendWelcomeEmailActivity : WorkflowActivityBase
{
    public override string ModuleName => "Communications";

    protected override async ValueTask ExecuteActivityAsync(ActivityExecutionContext context)
    {
        // Activity logic here
    }
}
```

**Registration:** `services.AddFoundryWorkflows(configuration)` registers Elsa with PostgreSQL persistence, scheduling, HTTP activities, and email integration.

---

## Project Structure

```
src/
  Foundry.Api/                        # Host -- wires all modules together
  Modules/
    Identity/
      Foundry.Identity.Domain/
      Foundry.Identity.Application/
      Foundry.Identity.Infrastructure/
      Foundry.Identity.Api/
    Billing/                          # Same four-layer pattern
    Storage/
    Communications/
  Shared/
    Foundry.Shared.Contracts/         # Cross-module events and DTOs
    Foundry.Shared.Kernel/            # Base classes, multi-tenancy, shared abstractions
    Foundry.Shared.Infrastructure/    # Cross-cutting infrastructure
      Auditing/                       # EF Core audit interceptor
      BackgroundJobs/                 # IJobScheduler / Hangfire
      Workflows/                      # Elsa 3 workflow engine

tests/
  Foundry.Api.Tests/
  Foundry.Architecture.Tests/
  Foundry.Shared.Kernel.Tests/
  Foundry.Shared.Infrastructure.Tests/
  Foundry.Tests.Common/               # Shared test utilities, fixtures, factories
  Messaging.IntegrationTests/
  Modules/
    {Module}/
      {Module}.Domain.Tests/          # Unit tests for domain layer
      {Module}.Application.Tests/     # Unit tests for application layer
      {Module}.Infrastructure.Tests/  # Unit tests for infrastructure layer
      Foundry.{Module}.IntegrationTests/  # Integration tests (optional)
```

---

## Module Architecture

Every module follows Clean Architecture with four layers:

```
Domain         -> Entities, Value Objects, Domain Events. Zero dependencies.
Application    -> Commands, Queries, Handlers, DTOs, Interfaces. Depends on Domain.
Infrastructure -> EF Core, Dapper, Consumers, external services. Implements Application interfaces.
Api            -> Controllers, request/response contracts. Depends on Application.
```

### Dependency Rules

- **Domain** references nothing.
- **Application** references Domain only.
- **Infrastructure** references Application and Domain.
- **Api** references Application only.
- Modules never reference each other directly. Cross-module communication goes through `Shared.Contracts` events.
- **Api** never references **Infrastructure** directly. It consumes Application interfaces.

---

## Adding a New Module

This guide walks through creating a new standard (EF Core) module using extension methods.

### Step 1: Create the Projects

Under `src/Modules/{Module}/`, create four class libraries. Example for a "Tickets" module:

```bash
dotnet new classlib -o src/Modules/Tickets/Foundry.Tickets.Domain
dotnet new classlib -o src/Modules/Tickets/Foundry.Tickets.Application
dotnet new classlib -o src/Modules/Tickets/Foundry.Tickets.Infrastructure
dotnet new classlib -o src/Modules/Tickets/Foundry.Tickets.Api
```

Set project references:
- `Application` -> `Domain`
- `Infrastructure` -> `Application`, `Shared.Kernel`, `Shared.Contracts`
- `Api` -> `Application`, `Shared.Kernel`

### Step 2: Define Domain Entities

Place entities in `Foundry.{Module}.Domain/Entities/`. Inherit from `Entity` or `AggregateRoot`:

```csharp
public class Ticket : AggregateRoot, ITenantScoped
{
    public string Title { get; private set; }
    public TicketStatus Status { get; private set; }
    public TenantId TenantId { get; set; }
}
```

### Step 3: Create Application Services

Define commands, queries, and handlers:

```csharp
// Commands/CreateTicket/CreateTicketCommand.cs
public record CreateTicketCommand(string Title, string Description);

// Commands/CreateTicket/CreateTicketHandler.cs
public static class CreateTicketHandler
{
    public static async Task<Result<TicketDto>> HandleAsync(
        CreateTicketCommand command,
        ITicketRepository repository,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

Create the Application extension:

```csharp
// Extensions/ApplicationExtensions.cs
public static class ApplicationExtensions
{
    public static IServiceCollection AddTicketsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

### Step 4: Create Infrastructure Layer

Create the DbContext:

```csharp
// Persistence/TicketsDbContext.cs
public sealed class TicketsDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public TicketsDbContext(
        DbContextOptions<TicketsDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tickets");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketsDbContext).Assembly);
        modelBuilder.Entity<Ticket>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }
}
```

Create Infrastructure extension:

```csharp
// Extensions/InfrastructureExtensions.cs
public static class InfrastructureExtensions
{
    public static IServiceCollection AddTicketsInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<TicketsDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tickets");
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<ITicketRepository, TicketRepository>();
        return services;
    }
}
```

### Step 5: Create Module Extension Methods

Create the module extension methods in Infrastructure:

```csharp
// src/Modules/Tickets/Foundry.Tickets.Infrastructure/Extensions/TicketsModuleExtensions.cs
using Foundry.Tickets.Application.Extensions;
using Foundry.Tickets.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Foundry.Tickets.Infrastructure.Extensions;

public static class TicketsModuleExtensions
{
    public static IServiceCollection AddTicketsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTicketsApplication();
        services.AddTicketsInfrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> InitializeTicketsModuleAsync(
        this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("TicketsModule");
            logger.LogWarning(ex, "Tickets module startup failed. Ensure PostgreSQL is running.");
        }

        return app;
    }
}
```

### Step 6: Register in FoundryModules.cs

Add the module to `src/Foundry.Api/FoundryModules.cs`:

```csharp
// Add using directive at top
using Foundry.Tickets.Infrastructure.Extensions;

// In AddFoundryModules():
services.AddTicketsModule(configuration);

// In InitializeFoundryModulesAsync():
await app.InitializeTicketsModuleAsync();
```

### Step 7: Create Initial Migration

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/Tickets/Foundry.Tickets.Infrastructure \
    --startup-project src/Foundry.Api \
    --context TicketsDbContext
```

### Step 8: Add Tests

Create test projects under `tests/Modules/Tickets/`:
- `Tickets.Domain.Tests/` -- Unit tests for domain entities and value objects
- `Tickets.Application.Tests/` -- Unit tests for handlers and validators
- `Tickets.Infrastructure.Tests/` -- Unit tests for repositories (optional)

### Handler Discovery

Wolverine automatically discovers handlers in all `Foundry.*` assemblies. No manual registration needed. Just create handlers following Wolverine conventions:

```csharp
public static class CreateInvoiceHandler
{
    public static async Task<Result<InvoiceDto>> HandleAsync(
        CreateInvoiceCommand command,
        IInvoiceRepository repository,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

### RabbitMQ Routing

Wolverine's `UseConventionalRouting()` automatically creates queues and exchanges for message types. No manual routing configuration needed.

### Module Type Examples

#### Standard Module (with EF Core persistence)
See the Billing module: `src/Modules/Billing/Foundry.Billing.Infrastructure/Extensions/BillingModuleExtensions.cs`

#### Stateless Module (no persistence)
```csharp
public static class ExampleModuleExtensions
{
    public static IServiceCollection AddExampleModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddExampleInfrastructure(configuration);
        return services;
    }

    public static Task<WebApplication> InitializeExampleModuleAsync(
        this WebApplication app) => Task.FromResult(app);
}
```

---

## Cross-Module Communication

Modules communicate through integration events published over Wolverine + RabbitMQ. Events are defined in `Shared.Contracts`.

### Defining an Event

In `src/Shared/Foundry.Shared.Contracts/Billing/Events/`:

```csharp
public record InvoicePaidEvent(Guid InvoiceId, Guid CustomerId, DateTime OccurredAt);
```

Events are facts. Name them in past tense. They are not commands.

### Publishing

From any handler:

```csharp
await bus.PublishAsync(new InvoicePaidEvent(invoice.Id, invoice.CustomerId, DateTime.UtcNow));
```

### Consuming

In the consuming module's Infrastructure layer, create a handler. Wolverine discovers it by convention:

```csharp
public static class InvoicePaidEventHandler
{
    public static async Task HandleAsync(
        InvoicePaidEvent @event,
        IEmailService emailService,
        CancellationToken ct)
    {
        // React to the event
    }
}
```

---

## Commands and Queries

Wolverine acts as the CQRS mediator. No marker interfaces required.

### Command (Write)

`Application/Commands/CreateInvoice/CreateInvoiceCommand.cs`:
```csharp
public record CreateInvoiceCommand(Guid CustomerId, List<LineItemDto> Items);
```

`Application/Commands/CreateInvoice/CreateInvoiceHandler.cs`:
```csharp
public static class CreateInvoiceHandler
{
    public static async Task<Result<InvoiceDto>> HandleAsync(
        CreateInvoiceCommand command,
        IInvoiceRepository repository,
        CancellationToken ct)
    {
        // ...
    }
}
```

### Query (Read)

Same pattern in `Application/Queries/`. For complex reads, use Dapper directly in the handler.

### Validation

FluentValidation validators are auto-discovered by Wolverine middleware:

```csharp
public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
    }
}
```

### Controller

```csharp
[HttpPost]
public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
{
    var result = await _bus.InvokeAsync<Result<InvoiceDto>>(
        new CreateInvoiceCommand(request.CustomerId, request.Items));
    return result.ToActionResult();
}
```

---

## Multi-Tenancy

Tenant isolation is enforced at three layers.

### 1. Middleware

`TenantResolutionMiddleware` reads the organization claim from the Keycloak JWT and populates `ITenantContext` for the request scope.

### 2. Entity Marking

Domain entities that are tenant-scoped implement `ITenantScoped` from `Shared.Kernel`:

```csharp
public interface ITenantScoped
{
    TenantId TenantId { get; set; }
}
```

### 3. Query Filters

Each DbContext applies EF Core global query filters on tenant-scoped entities:

```csharp
modelBuilder.Entity<Invoice>()
    .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
```

This ensures all queries are automatically scoped to the current tenant. To bypass (admin scenarios), use `.IgnoreQueryFilters()`.

### 4. Save Interceptor

`TenantSaveChangesInterceptor` automatically stamps `TenantId` on new entities and prevents modification of `TenantId` on updates.

### Dapper Queries

When using Dapper, you must filter by tenant manually:

```csharp
WHERE tenant_id = @TenantId
```

Pass `_tenantContext.TenantId.Value` as the parameter.

---

## Database

Each module owns its own schema. Migrations are per-module.

### Running Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/{Module}/Foundry.{Module}.Infrastructure \
    --startup-project src/Foundry.Api \
    --context {Module}DbContext

dotnet ef database update \
    --project src/Modules/{Module}/Foundry.{Module}.Infrastructure \
    --startup-project src/Foundry.Api \
    --context {Module}DbContext
```

Migrations also run automatically at startup via `Use{Module}ModuleAsync()`.

### Write vs. Read Strategy

- **Writes**: EF Core through repositories.
- **Complex reads**: Dapper for performance-sensitive or join-heavy queries.

---

## Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Projects | `Foundry.{Module}.{Layer}` | `Foundry.Billing.Domain` |
| Commands | Verb + Noun + Command | `CreateInvoiceCommand` |
| Queries | Get + Noun + Query | `GetInvoiceByIdQuery` |
| Handlers | Command/Query name + Handler | `CreateInvoiceHandler` |
| Events | Noun + PastTense + Event | `InvoicePaidEvent` |
| DTOs | Noun + Dto | `InvoiceDto` |
| Requests | Verb + Noun + Request | `CreateInvoiceRequest` |
| Responses | Noun + Response | `InvoiceResponse` |
| DB Schemas | snake_case module name | `billing`, `identity` |

One class per file. File name matches class name. Commands and queries get their own folder:

```
Commands/
  CreateInvoice/
    CreateInvoiceCommand.cs
    CreateInvoiceHandler.cs
```

---

## Request Flow

```
HTTP Request
  -> Controller (Api)
  -> Command/Query (Application)
  -> Wolverine IMessageBus
  -> Handler (Application)
  -> Repository (Infrastructure, via interface)
  -> DTO (Application)
  -> Response (Api)
  -> HTTP Response
```

Validation runs automatically before handlers via Wolverine's FluentValidation middleware.

---

## Testing

### Test Infrastructure

Foundry uses **xUnit** as the test framework, **FluentAssertions** for readable assertions, and **Testcontainers** for integration tests that need real infrastructure (PostgreSQL, RabbitMQ, Valkey).

Shared test utilities live in `tests/Foundry.Tests.Common/`, including:
- `FoundryApiFactory` -- `WebApplicationFactory` configured with Testcontainers
- `DatabaseFixture`, `RabbitMqFixture`, `RedisFixture`, `KeycloakFixture` -- reusable xUnit fixtures
- `Builders/`, `Fakes/`, `Helpers/` -- test data builders and utilities

### Unit Tests

Test handlers in isolation. Mock repositories and services:

```csharp
[Fact]
public async Task Should_create_invoice()
{
    // Arrange
    var repo = Substitute.For<IInvoiceRepository>();
    var command = new CreateInvoiceCommand(tenantId, customerId, items);

    // Act
    var result = await CreateInvoiceHandler.HandleAsync(command, repo, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    await repo.Received(1).SaveChangesAsync();
}
```

### Integration Tests

Use `FoundryApiFactory` with Testcontainers:

```csharp
public class InvoicesControllerTests : IClassFixture<FoundryApiFactory>
{
    private readonly HttpClient _client;

    public InvoicesControllerTests(FoundryApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateInvoice_returns_201()
    {
        var response = await _client.PostAsJsonAsync("/api/invoices", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

### Architecture Tests

`Foundry.Architecture.Tests` validates structural rules (e.g., modules do not reference each other, dependency direction is correct). These run as part of the standard test suite.

---

## Technology Stack

| Purpose | Technology |
|---------|------------|
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| ORM | EF Core + Dapper |
| CQRS & Messaging | Wolverine (mediator + RabbitMQ transport) |
| Logging | Serilog |
| Real-time | SignalR |
| Validation | FluentValidation |
| Identity Provider | Keycloak 26 |
| Caching | Valkey (Redis-compatible) |
| Testing | xUnit, Testcontainers, FluentAssertions |

---

## Troubleshooting

**Keycloak not responding**: Wait 1-2 minutes after first `docker compose up`. Check `docker compose logs keycloak`.

**DB connection failures**: Verify Postgres is running with `docker compose ps`. Check connection strings in `appsettings.Development.json`.

**RabbitMQ issues**: Check management UI at http://localhost:15672. Look for unroutable messages.

**Tests failing**: Integration tests need Docker. Run `docker ps` to verify. Testcontainers creates ephemeral containers; ensure Docker has enough resources.

**Reset everything**: `cd docker && docker compose down -v && docker compose up -d`
