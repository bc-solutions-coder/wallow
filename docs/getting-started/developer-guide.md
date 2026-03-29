# Wallow Developer Guide

---

## Prerequisites

- .NET 10 SDK
- Docker and Docker Compose
- Rider or Visual Studio 2022+

---

## Getting Started

### 1. Start Infrastructure

Wallow depends on PostgreSQL, Valkey (Redis-compatible cache), GarageHQ (S3-compatible object storage), and Mailpit. Docker Compose provisions all of them:

```bash
cd docker && docker compose up -d
```

To also start ClamAV for virus scanning on file uploads (optional):

```bash
cd docker && docker compose --profile clamav up -d
```

Authentication is handled by the embedded OpenIddict server (part of the Identity module), so no external identity provider container is needed.

### 2. Run the API

```bash
dotnet run --project src/Wallow.Api
```

The API starts on `http://localhost:5000`. Interactive API documentation is available at `http://localhost:5000/scalar/v1`.

### 3. Run Tests

```bash
# All tests
./scripts/run-tests.sh

# Specific module
./scripts/run-tests.sh billing

# Specific test project
./scripts/run-tests.sh tests/Modules/Billing/Wallow.Billing.Tests
```

The script outputs structured per-assembly pass/fail counts and lists individual failed test names. Supported shorthands: `identity`, `billing`, `storage`, `notifications`, `messaging`, `announcements`, `inquiries`, `branding`, `apikeys`, `auth`, `api`, `arch`, `shared`, `kernel`, `integration`.

Integration tests require Docker. Testcontainers spins up ephemeral Postgres and Valkey containers automatically.

### Local Services

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5000 | - |
| Scalar Docs | http://localhost:5000/scalar/v1 | - |
| OpenIddict Authorize | http://localhost:5000/connect/authorize | - |
| OpenIddict Token | http://localhost:5000/connect/token | - |
| GarageHQ (S3 API) | http://localhost:3900 | See `docker/.env` |
| GarageHQ (Admin API) | http://localhost:3903 | See `docker/.env` |
| Mailpit | http://localhost:8025 | - |
| PostgreSQL | localhost:5432 | See `docker/.env` |
| AsyncAPI Viewer | http://localhost:5000/asyncapi | Dev only |
| ClamAV (optional) | localhost:3310 | - |
| Grafana | http://localhost:3001 | admin / admin |

### Getting a Test Token

Use the OpenIddict token endpoint with client credentials or authorization code flow:

```bash
# Client credentials (service account)
curl -s -X POST http://localhost:5000/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=<your-client-id>" \
  -d "client_secret=<your-client-secret>" \
  -d "scope=openid profile email"
```

In development, the `ApiScopeSeeder` seeds default API scopes at startup. Use the `ClientsController` admin API (`/api/v1/identity/clients`) to register new OpenIddict applications and obtain client credentials.

### Resetting Infrastructure

```bash
cd docker && docker compose down -v && docker compose up -d
```

---

## Architecture Overview

Wallow is a modular monolith. Each module is an autonomous bounded context that follows Clean Architecture internally and communicates with other modules exclusively through integration events over Wolverine. Modules never reference each other directly.

**Modules:** Identity, Billing, Branding, Storage, Notifications, Messaging, Announcements, Inquiries, ApiKeys

**Shared libraries:**
- `Wallow.Shared.Contracts` -- Cross-module integration events and DTOs
- `Wallow.Shared.Kernel` -- Base classes, multi-tenancy primitives, shared abstractions
- `Wallow.Shared.Infrastructure` -- Cross-cutting infrastructure (auditing, background jobs, workflows)

---

## Shared Infrastructure

Cross-cutting capabilities that were previously separate modules now live in `Wallow.Shared.Infrastructure` and are registered centrally. Modules access these through DI -- they don't reference other modules.

### Auditing (`Shared.Infrastructure.Core/Auditing/`)

An EF Core `SaveChangesInterceptor` that automatically captures all entity changes (inserts, updates, deletes) across every module's DbContext. Audit entries include the entity type, primary key, old/new values (serialized JSON), the acting user, tenant, and timestamp. Entries are stored in a dedicated `audit` schema via `AuditDbContext`.

**Registration:** `services.AddWallowAuditing(configuration)` registers the `AuditDbContext` and `AuditInterceptor` singleton. Module DbContexts pick up auditing automatically by adding the interceptor to their options (via `options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>())`).

### Background Jobs (`Shared.Infrastructure.BackgroundJobs/`)

A thin `IJobScheduler` abstraction (defined in `Shared.Kernel/BackgroundJobs/`) over Hangfire for fire-and-forget and recurring jobs. Modules inject `IJobScheduler` to enqueue work without depending on Hangfire directly.

**Registration:** `services.AddWallowBackgroundJobs()` registers `HangfireJobScheduler` as the `IJobScheduler` implementation.

### Workflows (`Shared.Infrastructure.Workflows/`)

Elsa 3 workflow engine integration for long-running, multi-step business processes. Elsa stores workflow definitions and runtime state in PostgreSQL via EF Core. Modules define custom workflow activities by extending `WorkflowActivityBase`, which adds module-scoped logging and execution context. Activities are auto-discovered from all `Wallow.*` assemblies at startup.

**Registration:** `services.AddWallowWorkflows(configuration)` registers Elsa with PostgreSQL persistence, scheduling, HTTP activities, and email integration.

---

## Project Structure

```
src/
  Wallow.Api/                        # Host -- wires all modules together
  Modules/
    Identity/
      Wallow.Identity.Domain/
      Wallow.Identity.Application/
      Wallow.Identity.Infrastructure/
      Wallow.Identity.Api/
    Billing/                          # Same four-layer pattern
    Storage/
    Notifications/
    Messaging/
    Announcements/
    Inquiries/
  Shared/
    Wallow.Shared.Contracts/                     # Cross-module events and DTOs
    Wallow.Shared.Kernel/                        # Base classes, multi-tenancy, shared abstractions
    Wallow.Shared.Infrastructure/                # Settings and shared infrastructure
    Wallow.Shared.Infrastructure.Core/           # Auditing, caching, messaging middleware
    Wallow.Shared.Infrastructure.Plugins/        # Plugin loading and lifecycle
    Wallow.Shared.Infrastructure.BackgroundJobs/ # IJobScheduler / Hangfire
    Wallow.Shared.Infrastructure.Workflows/      # Elsa 3 workflow engine

tests/
  Wallow.Api.Tests/
  Wallow.Architecture.Tests/
  Wallow.Shared.Kernel.Tests/
  Wallow.Shared.Infrastructure.Tests/
  Wallow.Tests.Common/               # Shared test utilities, fixtures, factories
  Modules/
    {Module}/
      {Module}.Domain.Tests/          # Unit tests for domain layer
      {Module}.Application.Tests/     # Unit tests for application layer
      {Module}.Infrastructure.Tests/  # Unit tests for infrastructure layer
      Wallow.{Module}.IntegrationTests/  # Integration tests (optional)
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
dotnet new classlib -o src/Modules/Tickets/Wallow.Tickets.Domain
dotnet new classlib -o src/Modules/Tickets/Wallow.Tickets.Application
dotnet new classlib -o src/Modules/Tickets/Wallow.Tickets.Infrastructure
dotnet new classlib -o src/Modules/Tickets/Wallow.Tickets.Api
```

Set project references:
- `Application` -> `Domain`
- `Infrastructure` -> `Application`, `Shared.Kernel`, `Shared.Contracts`
- `Api` -> `Application`, `Shared.Kernel`

### Step 2: Define Domain Entities

Place entities in `Wallow.{Module}.Domain/Entities/`. Inherit from `Entity` or `AggregateRoot`:

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
// src/Modules/Tickets/Wallow.Tickets.Infrastructure/Extensions/TicketsModuleExtensions.cs
using Wallow.Tickets.Application.Extensions;
using Wallow.Tickets.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wallow.Tickets.Infrastructure.Extensions;

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

### Step 6: Register in WallowModules.cs

Add the module to `src/Wallow.Api/WallowModules.cs`:

```csharp
// Add using directive at top
using Wallow.Tickets.Infrastructure.Extensions;

// In AddWallowModules():
services.AddTicketsModule(configuration);

// In InitializeWallowModulesAsync():
await app.InitializeTicketsModuleAsync();
```

### Step 7: Create Initial Migration

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/Tickets/Wallow.Tickets.Infrastructure \
    --startup-project src/Wallow.Api \
    --context TicketsDbContext
```

### Step 8: Add Tests

Create test projects under `tests/Modules/Tickets/`:
- `Tickets.Domain.Tests/` -- Unit tests for domain entities and value objects
- `Tickets.Application.Tests/` -- Unit tests for handlers and validators
- `Tickets.Infrastructure.Tests/` -- Unit tests for repositories (optional)

### Handler Discovery

Wolverine automatically discovers handlers in all `Wallow.*` assemblies. No manual registration needed. Just create handlers following Wolverine conventions:

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

### Message Routing

Wolverine uses in-memory transport for all module-to-module messaging. Messages are routed automatically by type -- no manual routing configuration is needed.

### Module Type Examples

#### Standard Module (with EF Core persistence)
See the Billing module: `src/Modules/Billing/Wallow.Billing.Infrastructure/Extensions/BillingModuleExtensions.cs`

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

Modules communicate through integration events published over Wolverine (in-memory bus). Events are defined in `Shared.Contracts`.

### Defining an Event

In `src/Shared/Wallow.Shared.Contracts/Billing/Events/`:

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

`TenantResolutionMiddleware` (in `Identity.Infrastructure/MultiTenancy/`) reads the `org_id` claim from the JWT and populates `ITenantContext` for the request scope.

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
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext

dotnet ef database update \
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext
```

Migrations also run automatically at startup via `Initialize{Module}ModuleAsync()`.

> **Note:** Auto-migration at startup only applies in Development and Testing environments. In production and staging, a dedicated init container applies migrations before the app starts. See [Database Migrations](../development/database-migrations.md#production-migrations) for details.

### Write vs. Read Strategy

- **Writes**: EF Core through repositories.
- **Complex reads**: Dapper for performance-sensitive or join-heavy queries.

---

## Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Projects | `Wallow.{Module}.{Layer}` | `Wallow.Billing.Domain` |
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

Wallow uses **xUnit** as the test framework, **FluentAssertions** for readable assertions, and **Testcontainers** for integration tests that need real infrastructure (PostgreSQL, Valkey).

Shared test utilities live in `tests/Wallow.Tests.Common/`, including:
- `WallowApiFactory` -- `WebApplicationFactory` configured with Testcontainers
- `DatabaseFixture`, `RedisFixture` -- reusable xUnit fixtures
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

Use `WallowApiFactory` with Testcontainers:

```csharp
public class InvoicesControllerTests : IClassFixture<WallowApiFactory>
{
    private readonly HttpClient _client;

    public InvoicesControllerTests(WallowApiFactory factory)
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

`Wallow.Architecture.Tests` validates structural rules (e.g., modules do not reference each other, dependency direction is correct). These run as part of the standard test suite.

---

## Technology Stack

| Purpose | Technology |
|---------|------------|
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| ORM | EF Core + Dapper |
| CQRS & Messaging | Wolverine (mediator + in-memory bus) |
| Logging | Serilog |
| Real-time | SignalR |
| Validation | FluentValidation |
| Identity / Auth | ASP.NET Core Identity + OpenIddict |
| Caching | Valkey (Redis-compatible) |
| Object Storage | GarageHQ (S3-compatible) |
| Testing | xUnit, Testcontainers, FluentAssertions |

---

## Troubleshooting

**DB connection failures**: Verify Postgres is running with `docker compose ps`. Check connection strings in `appsettings.Development.json`.

**GarageHQ issues**: Check admin API at http://localhost:3903. Verify the bucket exists with `docker exec wallow-garage garage bucket list`. If the init script failed, restart with `docker compose restart garage`.

**Tests failing**: Integration tests need Docker. Run `docker ps` to verify. Testcontainers creates ephemeral containers; ensure Docker has enough resources.

**Reset everything**: `cd docker && docker compose down -v && docker compose up -d`
