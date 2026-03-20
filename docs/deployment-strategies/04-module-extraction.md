# Module Extraction Guide

**Status:** Advanced Strategy
**Audience:** Experienced teams with strong operational capabilities
**Risk Level:** High

---

## Table of Contents

1. [Overview](#1-overview)
2. [Prerequisites and Warnings](#2-prerequisites-and-warnings)
3. [Wallow's Modular Architecture](#3-wallows-modular-architecture)
4. [Extraction Process](#4-extraction-process)
5. [Example: Extracting the Billing Module](#5-example-extracting-the-billing-module)
6. [Handling Shared Code](#6-handling-shared-code)
7. [Service Discovery and Communication](#7-service-discovery-and-communication)
8. [Deployment Strategies](#8-deployment-strategies)
9. [Monitoring Distributed Services](#9-monitoring-distributed-services)
10. [Reverting Extraction](#10-reverting-extraction)
11. [Case Studies](#11-case-studies)
12. [Checklist](#12-checklist)

---

## 1. Overview

### What Module Extraction Means

Module extraction is the process of taking a bounded context that currently runs inside the Wallow monolith and deploying it as an independent, separately-scalable service. The extracted module becomes a standalone application that:

- Runs in its own process/container
- Has its own deployment lifecycle
- Can be scaled independently
- Communicates with other modules only via RabbitMQ events

This is the path from modular monolith toward microservices, done incrementally and deliberately.

### Why Wallow's Architecture Makes This Possible

Wallow was designed from day one with extraction in mind. The modular monolith architecture provides several guarantees that make extraction feasible:

1. **Module Isolation**: Each module (Identity, Billing, Communications, Storage, Configuration) has four separate projects:
   - `Wallow.{Module}.Domain` - Zero external dependencies
   - `Wallow.{Module}.Application` - Depends only on Domain and abstractions
   - `Wallow.{Module}.Infrastructure` - Implements Application interfaces
   - `Wallow.{Module}.Api` - Controllers and HTTP contracts

2. **Event-Driven Communication**: Modules never call each other directly. All cross-module communication flows through RabbitMQ using integration events defined in `Wallow.Shared.Contracts`. This means extracting a module requires no code changes to other modules.

3. **Database Schema Isolation**: Each module owns its own PostgreSQL schema (e.g., `billing`, `communications`, `storage`). There are no cross-schema joins or foreign keys.

4. **Shared Contracts Only**: The only shared dependency between modules is `Wallow.Shared.Contracts`, which contains only DTOs and event definitions with zero NuGet dependencies.

### When (and When NOT) to Do This

**Extract a module when:**
- A single module consumes disproportionate resources (CPU, memory, database connections)
- Different teams need to own different modules with independent release cycles
- Compliance requirements mandate isolation (e.g., PCI-DSS for payment processing)
- A module needs fundamentally different infrastructure (different database, different runtime)

**Do NOT extract a module when:**
- You think "microservices are better" (they're not inherently better, just different trade-offs)
- You want to try microservices for learning (use a side project instead)
- You haven't exhausted simpler scaling strategies (vertical scaling, read replicas, caching)
- The module has unclear boundaries or tight coupling with other modules

---

## 2. Prerequisites and Warnings

### 2.1 When to Consider Module Extraction

#### Single Module Resource Consumption

If your monitoring shows one module consuming 80%+ of resources while others are idle, extraction may be warranted. Common scenarios:

```
Before extraction:
┌─────────────────────────────────────────┐
│ Wallow Monolith (4 CPU, 8GB RAM)       │
│ ┌───────┐ ┌───────┐ ┌───────┐          │
│ │Identity│ │Billing│ │Comms  │          │
│ │  5%   │ │  85%  │ │  10%  │          │
│ └───────┘ └───────┘ └───────┘          │
└─────────────────────────────────────────┘

After extracting Billing:
┌────────────────────────┐ ┌─────────────────────┐
│ Wallow (2 CPU, 4GB)   │ │ Billing (4 CPU, 8GB)│
│ ┌───────┐ ┌───────┐   │ │     ┌───────┐       │
│ │Identity│ │Comms  │   │ │     │Billing│       │
│ │  10%  │ │  20%  │   │ │     │  60%  │       │
│ └───────┘ └───────┘   │ │     └───────┘       │
└────────────────────────┘ └─────────────────────┘
```

#### Team Scaling

When your organization grows beyond 2-3 teams working on Wallow, extraction allows:
- Independent deployment schedules
- Different on-call rotations
- Separate code review and approval processes
- Technology autonomy within modules

#### Compliance/Security Isolation

Certain compliance requirements may mandate process-level isolation:
- **PCI-DSS**: Payment card data handling in isolated environment
- **HIPAA**: Healthcare data in separate process with audit controls
- **SOC 2**: Separation of duties across deployment boundaries

#### Different Technology Requirements

While Wallow is .NET 10, you might need:
- A module in a different language (Python for ML features)
- A different database engine (TimescaleDB for time-series data)
- Edge deployment capabilities

### 2.2 When NOT to Extract

#### "Because Microservices are Better"

Microservices are a trade-off, not an upgrade. The modular monolith gives you:
- Simpler deployment (one artifact)
- Lower latency (in-process communication)
- Easier debugging (single process, single log stream)
- Simpler transactions (can use database transactions across modules if needed)

Only trade these away when you have a compelling reason.

#### Premature Optimization

Signs you're optimizing too early:
- No production traffic yet
- No evidence of scaling issues
- "We might need it later" reasoning
- Following industry trends rather than your own data

#### Before Trying Simpler Strategies

Before extraction, try:
1. **Vertical scaling**: Add more CPU/RAM to the monolith
2. **Database optimization**: Indexes, query optimization, connection pooling
3. **Caching**: Add Valkey/Redis caching for hot paths
4. **Read replicas**: Offload read queries to replicas
5. **Multiple instances**: Run multiple monolith instances behind a load balancer
6. **Background processing**: Move heavy work to Hangfire jobs

#### When Modules Have Tight Coupling

Red flags that indicate coupling problems:
- Module A frequently needs data from Module B's database
- Multiple modules need to update in a single transaction
- Circular event dependencies (A -> B -> A)
- Shared domain concepts that span module boundaries

Fix the coupling before extracting, or you'll have distributed coupling (much worse).

### 2.3 Cost of Extraction

Be honest about what you're signing up for:

#### Operational Complexity

| Monolith | After Extraction |
|----------|------------------|
| 1 deployment | N deployments |
| 1 monitoring target | N monitoring targets |
| 1 log stream | N log streams to correlate |
| 1 health check | N health checks + dependency health |

#### Network Latency

In the monolith, cross-module calls are essentially free (nanoseconds). After extraction:
- Same data center: 0.5-2ms per hop
- Across availability zones: 2-5ms per hop
- With retries and circuit breakers: 10-100ms worst case

A request that touched 3 modules might add 5-10ms of latency.

#### Distributed Debugging

When something fails:
- **Monolith**: Look at one log, one stack trace
- **Distributed**: Correlate logs across services using trace IDs, check RabbitMQ for stuck messages, verify network connectivity

#### Data Consistency Challenges

Transactions that used to be ACID become eventually consistent:
- What happens if Billing fails after Identity succeeds?
- How do you handle partial failures?
- How long is "eventually"?

---

## 3. Wallow's Modular Architecture

### 3.1 Current Module Boundaries

Each Wallow module is already isolated at multiple levels:

#### Project Structure

```
src/Modules/Billing/
├── Wallow.Billing.Domain/           # Entities, value objects, domain events
│   ├── Entities/
│   │   ├── Invoice.cs               # Aggregate root
│   │   ├── InvoiceLineItem.cs
│   │   ├── Payment.cs
│   │   └── Subscription.cs
│   ├── ValueObjects/
│   │   └── Money.cs
│   ├── Events/
│   │   └── InvoiceCreatedDomainEvent.cs
│   └── Enums/
│       └── InvoiceStatus.cs
│
├── Wallow.Billing.Application/      # CQRS handlers, interfaces
│   ├── Commands/
│   │   ├── CreateInvoice/
│   │   │   ├── CreateInvoiceCommand.cs
│   │   │   └── CreateInvoiceHandler.cs
│   │   └── ProcessPayment/
│   ├── Queries/
│   │   └── GetInvoiceById/
│   └── Interfaces/
│       └── IInvoiceRepository.cs
│
├── Wallow.Billing.Infrastructure/   # EF Core, repositories
│   ├── Persistence/
│   │   ├── BillingDbContext.cs
│   │   └── Repositories/
│   │       └── InvoiceRepository.cs
│   └── Extensions/
│       └── InfrastructureExtensions.cs
│
└── Wallow.Billing.Api/              # Controllers, contracts
    ├── Controllers/
    │   └── InvoicesController.cs
    ├── Contracts/
    │   └── CreateInvoiceRequest.cs
    └── Extensions/
        └── BillingModuleExtensions.cs
```

#### Event-Only Communication

Modules communicate exclusively through integration events:

```csharp
// In Billing module - publishes event after invoice creation
public class InvoiceCreatedDomainEventHandler : IHandler<InvoiceCreatedDomainEvent>
{
    private readonly IMessageBus _bus;

    public async Task HandleAsync(InvoiceCreatedDomainEvent domainEvent, CancellationToken ct)
    {
        // Convert domain event to integration event
        var integrationEvent = new InvoiceCreatedEvent
        {
            InvoiceId = domainEvent.InvoiceId,
            UserId = domainEvent.UserId,
            Amount = domainEvent.Amount,
            // ... other properties
        };

        // Publish to RabbitMQ via Wolverine
        await _bus.PublishAsync(integrationEvent);
    }
}

// In Communications module - consumes event
public class InvoiceCreatedEventHandler : IHandler<InvoiceCreatedEvent>
{
    private readonly INotificationService _notifications;

    public async Task HandleAsync(InvoiceCreatedEvent e, CancellationToken ct)
    {
        await _notifications.SendAsync(new SendNotificationCommand
        {
            UserId = e.UserId,
            Title = "Invoice Created",
            Message = $"Invoice {e.InvoiceNumber} for {e.Amount} {e.Currency}"
        });
    }
}
```

#### Shared.Contracts Structure

All cross-module DTOs and events live in `Wallow.Shared.Contracts`:

```
src/Shared/Wallow.Shared.Contracts/
├── IIntegrationEvent.cs              # Base interface
├── Identity/
│   └── Events/
│       ├── UserRegisteredEvent.cs
│       ├── OrganizationCreatedEvent.cs
│       └── ...
├── Billing/
│   └── Events/
│       ├── InvoiceCreatedEvent.cs
│       ├── PaymentReceivedEvent.cs
│       ├── InvoicePaidEvent.cs
│       └── InvoiceOverdueEvent.cs
├── Communications/
│   └── Events/
│       └── NotificationCreatedEvent.cs
└── Realtime/
    ├── IRealtimeDispatcher.cs
    └── RealtimeEnvelope.cs
```

**Key constraint**: `Shared.Contracts` has zero NuGet dependencies. It's pure C# records and interfaces, making it trivially shareable.

#### Database Schema Separation

Each module owns its PostgreSQL schema:

```sql
-- Billing module
CREATE SCHEMA billing;
CREATE TABLE billing.invoices (...);
CREATE TABLE billing.payments (...);
CREATE TABLE billing.subscriptions (...);

-- Communications module
CREATE SCHEMA communications;
CREATE TABLE communications.notifications (...);
CREATE TABLE communications.email_messages (...);

-- Storage module
CREATE SCHEMA storage;
CREATE TABLE storage.files (...);
```

There are NO cross-schema foreign keys. Modules store only IDs from other modules, never joins.

### 3.2 Module Dependencies Map

Understanding event flow is critical before extraction. Here's the current Wallow event topology:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        EVENT FLOW DIAGRAM                               │
└─────────────────────────────────────────────────────────────────────────┘

                            PUBLISHES                    CONSUMES
┌──────────────┐
│   Identity   │──────> UserRegisteredEvent ──────────> Communications
│              │──────> OrganizationCreatedEvent ─────> Billing
│              │──────> OrganizationMemberAddedEvent ─> Communications
│              │──────> UserRoleChangedEvent ─────────> Communications
└──────────────┘

┌──────────────┐
│   Billing    │──────> InvoiceCreatedEvent ──────────> Communications
│              │──────> PaymentReceivedEvent ─────────> Communications
│              │──────> InvoicePaidEvent ─────────────> Communications
│              │──────> InvoiceOverdueEvent ──────────> Communications
└──────────────┘

┌────────────────┐
│Communications  │──────> NotificationCreatedEvent ───> (SignalR realtime)
│                │──────> EmailSentEvent ──────────────> (audit trail)
│                │        (Fan-out consumer for notifications and email)
└────────────────┘

┌──────────────┐
│   Storage    │──────> FileUploadedEvent ────────────> (internal)
│              │        (No cross-module consumers)
└──────────────┘

┌──────────────┐
│Configuration │──────> (No outbound events)
│              │        (Consumed via direct queries)
└──────────────┘
```

#### RabbitMQ Exchange/Queue Topology

```
Exchanges (fanout):
├── identity-events
├── billing-events
└── communications-events

Queues (with bindings):
├── communications-inbox ─> identity-events, billing-events
├── billing-inbox ────────> identity-events (for OrganizationCreatedEvent)
└── storage-inbox ────────> (internal routing)
```

**Critical insight**: Communications consumes events from most other modules. If you extract Communications, you must ensure all event publishers can reach it via RabbitMQ.

---

## 4. Extraction Process

### 4.1 Step 1: Identify Extraction Candidate

Good extraction candidates share these characteristics:

| Criteria | Why It Matters | How to Measure |
|----------|---------------|----------------|
| High resource usage | Independent scaling needed | APM tools, container metrics |
| Clear bounded context | Minimal change to other modules | Event flow analysis, no direct references |
| Few inbound dependencies | Other modules don't need to change | Count consumers of this module's events |
| Well-defined events | Clean integration contract | Review Shared.Contracts |
| Stable API surface | Extraction won't break clients | API versioning history |

**Ranking Wallow modules by extraction suitability:**

| Module | Resource Intensity | Dependencies | Inbound Events | Extraction Ease |
|--------|-------------------|--------------|----------------|-----------------|
| Communications | Medium | Low (fan-out consumer) | Many (consumer) | Easy |
| Storage | Medium | Low | None | Easy |
| Billing | High | Medium | Low | Medium |
| Configuration | Low | Low | None | Easy |
| Identity | Medium | Very High | Very High | Very Hard |

### 4.2 Step 2: Verify Event Contracts

Before extraction, ensure all cross-module communication is via events:

#### Check for Direct Code References

```bash
# From repository root, search for direct module references
# This should return NO results for a well-isolated module

# Check if any module imports Billing domain directly
grep -r "using Wallow.Billing.Domain" src/Modules --include="*.cs" | grep -v "Billing/"

# Check if any module imports Billing application directly
grep -r "using Wallow.Billing.Application" src/Modules --include="*.cs" | grep -v "Billing/"

# Check for any direct service injections
grep -r "IBillingService\|IInvoiceRepository\|IPaymentRepository" src/Modules --include="*.cs" | grep -v "Billing/"
```

If any results appear, you have coupling that must be resolved before extraction.

#### Run Architecture Tests

Wallow includes architecture tests that enforce module boundaries:

```csharp
// tests/Architecture/ModuleBoundaryTests.cs
[Fact]
public void Billing_Should_Not_Have_Direct_Dependencies_On_Other_Modules()
{
    var billingAssemblies = Types.InAssembly(typeof(Invoice).Assembly)
        .And(Types.InAssembly(typeof(CreateInvoiceCommand).Assembly))
        .And(Types.InAssembly(typeof(BillingDbContext).Assembly));

    var otherModules = new[]
    {
        "Wallow.Identity",
        "Wallow.Communications",
        "Wallow.Storage",
        "Wallow.Configuration"
    };

    var result = billingAssemblies
        .ShouldNot()
        .HaveDependencyOnAny(otherModules)
        .GetResult();

    result.IsSuccessful.Should().BeTrue(result.FailingTypeNames?.FirstOrDefault());
}
```

Run the architecture tests before proceeding:

```bash
dotnet test tests/Architecture
```

#### Verify Event Flow

Confirm all communication paths use Shared.Contracts events:

```csharp
// List all events published by Billing (should all be in Shared.Contracts)
var billingEvents = typeof(InvoiceCreatedEvent).Assembly
    .GetTypes()
    .Where(t => t.Namespace?.Contains("Billing.Events") == true)
    .ToList();

// Expected: InvoiceCreatedEvent, PaymentReceivedEvent, InvoicePaidEvent, InvoiceOverdueEvent
```

### 4.3 Step 3: Create Standalone Service

Create a new project that hosts the extracted module as an independent service.

#### Project Structure

```
extracted-services/
└── Wallow.Billing.Service/
    ├── Wallow.Billing.Service.csproj
    ├── Program.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── appsettings.Production.json
    ├── Dockerfile
    └── docker-compose.yml
```

#### New Project File (Wallow.Billing.Service.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Wallow.Billing.Service</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core framework -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />

    <!-- Wolverine for CQRS and messaging -->
    <PackageReference Include="WolverineFx" />
    <PackageReference Include="WolverineFx.RabbitMQ" />
    <PackageReference Include="WolverineFx.FluentValidation" />
    <PackageReference Include="WolverineFx.EntityFrameworkCore" />
    <PackageReference Include="WolverineFx.Postgresql" />

    <!-- Validation -->
    <PackageReference Include="FluentValidation" />

    <!-- Database -->
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />

    <!-- Observability -->
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />

    <!-- Health checks -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" />
    <PackageReference Include="AspNetCore.HealthChecks.RabbitMQ" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference the existing module projects -->
    <ProjectReference Include="..\..\src\Modules\Billing\Wallow.Billing.Api\Wallow.Billing.Api.csproj" />
    <ProjectReference Include="..\..\src\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
    <ProjectReference Include="..\..\src\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
  </ItemGroup>

</Project>
```

#### Program.cs for Standalone Service

```csharp
using Wallow.Billing.Api.Extensions;
using Wallow.Billing.Application.Commands.CreateInvoice;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Billing.Events;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Extensions;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Wallow Billing Service");

    var builder = WebApplication.CreateBuilder(args);

    // ===========================================
    // SERILOG CONFIGURATION
    // ===========================================
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Wallow.Billing.Service")
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.OpenTelemetry(options =>
        {
            var otlpEndpoint = context.Configuration["OpenTelemetry:OtlpEndpoint"]
                ?? "http://localhost:4318";
            options.Endpoint = otlpEndpoint + "/v1/logs";
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = "wallow-billing",
                ["service.namespace"] = "Wallow",
                ["deployment.environment"] = context.HostingEnvironment.EnvironmentName
            };
        }));

    // ===========================================
    // WOLVERINE CONFIGURATION
    // ===========================================
    builder.Host.UseWolverine(opts =>
    {
        // PostgreSQL persistence for durable outbox/inbox
        var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");
        opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine_billing");

        // EF Core transaction integration
        opts.UseEntityFrameworkCoreTransactions();

        // Error handling
        opts.ConfigureStandardErrorHandling();

        // FluentValidation middleware
        opts.UseFluentValidation();

        // Discover handlers from Billing Application assembly
        opts.Discovery.IncludeAssembly(typeof(CreateInvoiceCommand).Assembly);

        // RabbitMQ transport
        var rabbitMqConnection = builder.Configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException("RabbitMQ connection string not configured");

        opts.UseRabbitMq(new Uri(rabbitMqConnection))
            .AutoProvision();

        // Publish Billing events to billing-events exchange
        opts.PublishMessage<InvoiceCreatedEvent>()
            .ToRabbitExchange("billing-events");
        opts.PublishMessage<PaymentReceivedEvent>()
            .ToRabbitExchange("billing-events");
        opts.PublishMessage<InvoicePaidEvent>()
            .ToRabbitExchange("billing-events");
        opts.PublishMessage<InvoiceOverdueEvent>()
            .ToRabbitExchange("billing-events");

        // Consume from billing-inbox queue
        opts.ListenToRabbitQueue("billing-inbox");

        // Durable outbox for reliability
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
    });

    // ===========================================
    // OPENTELEMETRY CONFIGURATION
    // ===========================================
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: "wallow-billing",
                serviceNamespace: "Wallow",
                serviceVersion: "1.0.0"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("Wolverine")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(
                    builder.Configuration["OpenTelemetry:OtlpEndpoint"]
                    ?? "http://localhost:4317");
            }));

    // ===========================================
    // HEALTH CHECKS
    // ===========================================
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgresql",
            tags: new[] { "ready" })
        .AddRabbitMQ(
            builder.Configuration.GetConnectionString("RabbitMq")!,
            name: "rabbitmq",
            tags: new[] { "ready" });

    // ===========================================
    // APPLICATION SERVICES
    // ===========================================
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddControllers();
    builder.Services.AddSharedKernel();

    // Register the Billing module (same as in monolith)
    builder.Services.AddBillingModule(builder.Configuration);

    // OpenAPI documentation
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    // ===========================================
    // BUILD AND CONFIGURE APP
    // ===========================================
    var app = builder.Build();

    // Module initialization (database migrations)
    await app.UseBillingModuleAsync();

    // Exception handling
    app.UseExceptionHandler("/error");
    app.UseSerilogRequestLogging();

    // Health checks
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    error = e.Value.Exception?.Message
                })
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // Service info endpoint
    app.MapGet("/", () => Results.Ok(new
    {
        Name = "Wallow Billing Service",
        Version = "1.0.0",
        Environment = app.Environment.EnvironmentName,
        Health = "/health"
    })).ExcludeFromDescription();

    // Map controllers (InvoicesController, PaymentsController, SubscriptionsController)
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Billing Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
```

#### Configuration (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=postgres;Password=postgres;Include Error Detail=true",
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  },
  "OpenTelemetry": {
    "OtlpEndpoint": "http://localhost:4317",
    "ServiceName": "wallow-billing"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Wolverine": "Information"
      }
    }
  }
}
```

#### Production Configuration (appsettings.Production.json)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

### 4.4 Step 4: Database Separation

You have three options for database deployment, each with different trade-offs:

#### Option A: Same Database, Same Schema (Simplest)

The extracted service connects to the same PostgreSQL instance and uses the same `billing` schema.

**Pros:**
- Zero migration effort
- Shared connection pooling
- Simple backup/restore

**Cons:**
- No isolation
- Database becomes SPOF
- Can't scale database independently

**Configuration:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Database=wallow;Username=wallow;Password=***;Search Path=billing"
  }
}
```

#### Option B: Same Database, Explicit Schema (Current)

The extracted service uses the same PostgreSQL instance but explicitly targets its schema:

```csharp
// In BillingDbContext
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("billing");
    // ... entity configurations
}
```

**Pros:**
- Schema-level isolation
- Separate migration paths
- Easy to move to Option C later

**Cons:**
- Database still SPOF
- Shared resource contention

#### Option C: Separate Database (Full Isolation)

Create a dedicated database for the Billing service:

**Migration steps:**

1. **Create new database:**
```sql
CREATE DATABASE wallow_billing;
CREATE USER billing_service WITH PASSWORD 'secure_password';
GRANT ALL PRIVILEGES ON DATABASE wallow_billing TO billing_service;
```

2. **Export existing data:**
```bash
pg_dump -h localhost -U postgres -d wallow \
  --schema=billing \
  --no-owner \
  > billing_data.sql
```

3. **Import to new database:**
```bash
psql -h localhost -U billing_service -d wallow_billing < billing_data.sql
```

4. **Update connection string:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=billing-db;Database=wallow_billing;Username=billing_service;Password=***"
  }
}
```

**Pros:**
- Full isolation
- Independent scaling
- Separate backup schedules

**Cons:**
- More operational complexity
- Data migration required
- Separate connection pools

### 4.5 Step 5: Configure RabbitMQ Routing

The extracted service must participate in the same RabbitMQ topology as the monolith.

#### Exchange Configuration

Billing publishes to `billing-events` exchange (fanout):

```csharp
// In Wolverine configuration
opts.PublishMessage<InvoiceCreatedEvent>()
    .ToRabbitExchange("billing-events", exchange =>
    {
        exchange.ExchangeType = ExchangeType.Fanout;
        exchange.IsDurable = true;
    });
```

#### Queue Configuration

Billing consumes from `billing-inbox`:

```csharp
opts.ListenToRabbitQueue("billing-inbox", queue =>
{
    queue.IsDurable = true;
    queue.IsExclusive = false;
    queue.AutoDelete = false;

    // Dead letter configuration
    queue.DeadLetterExchange = "billing-dlx";
    queue.DeadLetterRoutingKey = "billing-dead";
});
```

#### Dead Letter Handling

Configure dead letter queues for failed messages:

```csharp
// Dead letter exchange and queue
opts.UseRabbitMq(uri)
    .DeclareExchange("billing-dlx", exchange =>
    {
        exchange.ExchangeType = ExchangeType.Direct;
        exchange.IsDurable = true;
    })
    .DeclareQueue("billing-dead-letters", queue =>
    {
        queue.IsDurable = true;
        queue.BindExchange("billing-dlx", "billing-dead");
    });
```

### 4.6 Step 6: Update Main Application

Remove the extracted module from the monolith.

#### Remove Project Reference

Edit `Wallow.Api.csproj`:

```xml
<!-- BEFORE -->
<ProjectReference Include="..\Modules\Billing\Wallow.Billing.Api\Wallow.Billing.Api.csproj" />

<!-- AFTER: Remove this line entirely -->
```

#### Remove Service Registration

Edit `Program.cs`:

```csharp
// BEFORE
builder.Services.AddBillingModule(builder.Configuration);

// AFTER: Remove this line

// BEFORE
await app.UseBillingModuleAsync();

// AFTER: Remove this line

// BEFORE
opts.Discovery.IncludeAssembly(typeof(CreateInvoiceCommand).Assembly);

// AFTER: Remove this line
```

#### Keep Event Publishing (Important!)

Even though Billing is extracted, the monolith may still need to publish events that Billing consumes. Keep the RabbitMQ routing:

```csharp
// Keep these if Identity events need to reach Billing service
opts.PublishMessage<OrganizationCreatedEvent>()
    .ToRabbitExchange("identity-events");
```

The extracted Billing service will consume these events from its `billing-inbox` queue.

---

## 5. Example: Extracting the Billing Module

### 5.1 Why Billing is a Good Candidate

Billing module is a strong extraction candidate for several reasons:

| Factor | Assessment |
|--------|------------|
| Bounded context | Clear: invoices, payments, subscriptions |
| Inbound dependencies | Low: only consumes OrganizationCreatedEvent |
| Outbound dependencies | Medium: publishes 4 event types |
| Resource usage | High: payment processing is CPU-intensive |
| Compliance | PCI-DSS may require isolation |
| Team ownership | Often separate billing/finance team |

### 5.2 Complete Implementation

#### Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and restore
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["extracted-services/Wallow.Billing.Service/Wallow.Billing.Service.csproj", "extracted-services/Wallow.Billing.Service/"]
COPY ["src/Modules/Billing/Wallow.Billing.Api/Wallow.Billing.Api.csproj", "src/Modules/Billing/Wallow.Billing.Api/"]
COPY ["src/Modules/Billing/Wallow.Billing.Application/Wallow.Billing.Application.csproj", "src/Modules/Billing/Wallow.Billing.Application/"]
COPY ["src/Modules/Billing/Wallow.Billing.Infrastructure/Wallow.Billing.Infrastructure.csproj", "src/Modules/Billing/Wallow.Billing.Infrastructure/"]
COPY ["src/Modules/Billing/Wallow.Billing.Domain/Wallow.Billing.Domain.csproj", "src/Modules/Billing/Wallow.Billing.Domain/"]
COPY ["src/Shared/Wallow.Shared.Kernel/Wallow.Shared.Kernel.csproj", "src/Shared/Wallow.Shared.Kernel/"]
COPY ["src/Shared/Wallow.Shared.Contracts/Wallow.Shared.Contracts.csproj", "src/Shared/Wallow.Shared.Contracts/"]

RUN dotnet restore "extracted-services/Wallow.Billing.Service/Wallow.Billing.Service.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/extracted-services/Wallow.Billing.Service"
RUN dotnet build "Wallow.Billing.Service.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Wallow.Billing.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Create non-root user
RUN addgroup -g 1000 wallow && adduser -u 1000 -G wallow -D wallow
USER wallow

COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Wallow.Billing.Service.dll"]
```

#### Docker Compose for Billing Service (docker-compose.billing.yml)

```yaml
version: '3.8'

services:
  billing-service:
    build:
      context: ../..
      dockerfile: extracted-services/Wallow.Billing.Service/Dockerfile
    container_name: wallow-billing-service
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=wallow;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Include Error Detail=true"
      ConnectionStrings__RabbitMq: "amqp://${RABBITMQ_USER}:${RABBITMQ_PASSWORD}@rabbitmq:5672"
      OpenTelemetry__OtlpEndpoint: "http://grafana-lgtm:4317"
    ports:
      - "8081:8080"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/health/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
    networks:
      - wallow
    restart: unless-stopped

networks:
  wallow:
    external: true
```

#### Modified Main Docker Compose (add to existing docker-compose.yml)

When running both the monolith and extracted service during transition:

```yaml
# docker-compose.override.yml for development with extracted Billing
version: '3.8'

services:
  # The main Wallow app (without Billing module)
  app:
    build:
      context: .
      dockerfile: src/Wallow.Api/Dockerfile
    container_name: wallow-app
    environment:
      # Billing is now external, configure if needed for API gateway
      Billing__ServiceUrl: "http://billing-service:8080"
    depends_on:
      billing-service:
        condition: service_healthy
      # ... other dependencies

  # Extracted Billing service
  billing-service:
    build:
      context: .
      dockerfile: extracted-services/Wallow.Billing.Service/Dockerfile
    container_name: wallow-billing
    ports:
      - "8081:8080"
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=wallow;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      ConnectionStrings__RabbitMq: "amqp://${RABBITMQ_USER}:${RABBITMQ_PASSWORD}@rabbitmq:5672"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
```

### 5.3 Testing the Extraction

#### 1. Verify Events Flow Correctly

Create a test that publishes an event from Identity and verifies Billing receives it:

```csharp
[Fact]
public async Task OrganizationCreated_Should_Reach_Billing_Service()
{
    // Arrange
    var orgId = Guid.NewGuid();
    var @event = new OrganizationCreatedEvent
    {
        OrganizationId = orgId,
        Name = "Test Org",
        OccurredAt = DateTimeOffset.UtcNow
    };

    // Act - Publish from monolith
    await _monolithMessageBus.PublishAsync(@event);

    // Assert - Verify Billing service processed it
    // This requires checking Billing's database or a test endpoint
    await WaitForConditionAsync(async () =>
    {
        var response = await _billingHttpClient.GetAsync($"/api/billing/organizations/{orgId}/status");
        return response.IsSuccessStatusCode;
    }, timeout: TimeSpan.FromSeconds(10));
}
```

#### 2. Test Failure Scenarios

```csharp
[Fact]
public async Task Billing_Failure_Should_Not_Break_Monolith()
{
    // Arrange - Stop Billing service
    await _billingContainer.StopAsync();

    // Act - Create invoice request (should fail gracefully or queue)
    var response = await _client.PostAsJsonAsync("/api/billing/invoices", new CreateInvoiceRequest
    {
        UserId = Guid.NewGuid(),
        Amount = 100m,
        Currency = "USD"
    });

    // Assert - Monolith continues to function
    var healthResponse = await _client.GetAsync("/health");
    healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task Billing_Should_Process_Backlog_After_Recovery()
{
    // Arrange - Queue messages while Billing is down
    await _billingContainer.StopAsync();

    for (int i = 0; i < 10; i++)
    {
        await _monolithMessageBus.PublishAsync(new OrganizationCreatedEvent
        {
            OrganizationId = Guid.NewGuid(),
            Name = $"Org {i}"
        });
    }

    // Act - Restart Billing
    await _billingContainer.StartAsync();
    await Task.Delay(TimeSpan.FromSeconds(5)); // Allow processing

    // Assert - All messages processed
    var queueInfo = await _rabbitMqAdmin.GetQueueInfoAsync("billing-inbox");
    queueInfo.MessageCount.Should().Be(0);
}
```

#### 3. Performance Comparison

```csharp
[Fact]
public async Task Extracted_Service_Should_Have_Acceptable_Latency()
{
    // Measure latency to Billing endpoint
    var stopwatch = Stopwatch.StartNew();

    var response = await _billingHttpClient.GetAsync("/api/billing/invoices");

    stopwatch.Stop();

    // Assert
    response.IsSuccessStatusCode.Should().BeTrue();
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
        "Billing service response should be under 100ms");
}
```

---

## 6. Handling Shared Code

### 6.1 Shared.Contracts

`Wallow.Shared.Contracts` is the only code shared between modules. After extraction, you have two options:

#### Option A: Keep as Shared Project Reference

If the extracted service is in the same repository:

```xml
<ProjectReference Include="..\..\src\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
```

**Pros:** Simple, no versioning overhead
**Cons:** Requires same repo, rebuild all on changes

#### Option B: Publish as NuGet Package

For truly independent services:

```bash
# Create NuGet package
dotnet pack src/Shared/Wallow.Shared.Contracts -c Release -o ./packages

# Publish to private feed
dotnet nuget push ./packages/Wallow.Shared.Contracts.1.0.0.nupkg --source https://your-nuget-feed
```

**Versioning Strategy:**

```xml
<!-- Wallow.Shared.Contracts.csproj -->
<PropertyGroup>
  <Version>1.0.0</Version>
  <PackageId>Wallow.Shared.Contracts</PackageId>
  <Authors>Your Team</Authors>
  <Description>Shared contracts for Wallow platform services</Description>
</PropertyGroup>
```

**Breaking Change Handling:**

1. **Additive changes** (new events, new properties): Minor version bump (1.0.0 -> 1.1.0)
2. **Breaking changes** (removed properties, renamed events): Major version bump (1.0.0 -> 2.0.0)
3. **Always support N-1**: Keep backward compatibility for at least one major version

```csharp
// Example: Adding property without breaking consumers
public sealed record InvoiceCreatedEvent : IntegrationEvent
{
    public required Guid InvoiceId { get; init; }
    public required Guid UserId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }

    // New property - nullable for backward compatibility
    public string? PaymentTerms { get; init; }
}
```

### 6.2 Shared.Kernel

`Wallow.Shared.Kernel` contains common utilities:
- Base entities and value objects
- Multi-tenancy interfaces
- Result pattern
- Domain event interfaces

**Decision: Duplicate or Share?**

| Content | Recommendation |
|---------|---------------|
| `ITenantScoped`, `TenantId` | Share (NuGet package) |
| `Result<T>` pattern | Share (NuGet package) |
| Base entity classes | Can duplicate |
| Domain event interfaces | Share (NuGet package) |
| Extension methods | Can duplicate |

For extracted services, consider a minimal shared package:

```csharp
// Wallow.Shared.Kernel.Minimal - for extracted services
namespace Wallow.Shared.Kernel;

public interface ITenantScoped
{
    Guid TenantId { get; }
}

public readonly record struct TenantId(Guid Value);

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    // ... minimal implementation
}
```

### 6.3 Database Migrations

When a module is extracted, migration coordination becomes critical.

#### Scenario: Shared Database with Schema Isolation

Both services can run migrations independently if they only touch their own schema:

```csharp
// Billing service startup
await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

// Only migrates billing schema
await db.Database.MigrateAsync();
```

#### Scenario: Separate Databases

Each service manages its own migrations:

```bash
# Generate migration for extracted Billing service
dotnet ef migrations add InitialCreate \
    --project extracted-services/Wallow.Billing.Service \
    --startup-project extracted-services/Wallow.Billing.Service \
    --context BillingDbContext
```

#### Migration Ordering

If migrations have dependencies (rare with proper isolation):

1. **Deploy migration to shared DB first**
2. **Wait for verification**
3. **Deploy service update**

```yaml
# CI/CD pipeline
stages:
  - name: migrate-database
    script: dotnet ef database update --connection $DB_CONNECTION

  - name: verify-migration
    script: ./scripts/verify-schema.sh

  - name: deploy-service
    script: kubectl apply -f k8s/billing-service.yaml
    needs: [migrate-database, verify-migration]
```

---

## 7. Service Discovery and Communication

### 7.1 Event-Driven Communication (Recommended)

Wallow's architecture already uses event-driven communication via RabbitMQ. This continues to work after extraction with no changes:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Wallow API    │    │    RabbitMQ     │    │ Billing Service │
│  (Monolith)     │    │                 │    │   (Extracted)   │
│                 │    │                 │    │                 │
│  Identity ──────┼───>│ identity-events │───>│ billing-inbox   │
│  Module         │    │   (exchange)    │    │   (queue)       │
│                 │    │                 │    │                 │
│  Communications │<───┼─billing-events ─┼────│ InvoiceCreated  │
│  Module         │    │   (exchange)    │    │ PaymentReceived │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

**Key principle**: Services communicate only through events. No service knows another service's URL.

### 7.2 Direct API Calls (If Absolutely Needed)

Sometimes you need synchronous communication. Use sparingly and with proper resilience.

#### Service-to-Service Authentication

Option 1: **Shared API Keys**

```csharp
// In calling service
public class BillingApiClient
{
    private readonly HttpClient _client;

    public BillingApiClient(HttpClient client, IConfiguration config)
    {
        _client = client;
        _client.BaseAddress = new Uri(config["Services:Billing:Url"]);
        _client.DefaultRequestHeaders.Add("X-Service-Key", config["Services:Billing:ApiKey"]);
    }

    public async Task<InvoiceDto?> GetInvoiceAsync(Guid invoiceId)
    {
        var response = await _client.GetAsync($"/api/billing/invoices/{invoiceId}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<InvoiceDto>()
            : null;
    }
}

// In Billing service - validate service key
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Service-Key", out var key))
    {
        if (key == _config["ServiceKeys:Internal"])
        {
            // Trusted internal caller
            await next();
            return;
        }
    }
    // Fall through to normal auth
    await next();
});
```

Option 2: **JWT with Service Identity**

```csharp
// Request a service token
var tokenResponse = await _tokenClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
{
    Address = $"{_keycloakUrl}/realms/wallow/protocol/openid-connect/token",
    ClientId = "wallow-api-service",
    ClientSecret = _config["Keycloak:ClientSecret"],
    Scope = "billing:read billing:write"
});

_client.SetBearerToken(tokenResponse.AccessToken);
```

#### Retry Policies

Use Microsoft.Extensions.Resilience (Polly-based):

```csharp
builder.Services.AddHttpClient<BillingApiClient>()
    .AddStandardResilienceHandler(options =>
    {
        // Retry 3 times with exponential backoff
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        options.Retry.UseJitter = true;
        options.Retry.BackoffType = DelayBackoffType.Exponential;

        // Circuit breaker
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

        // Timeout
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
    });
```

#### Circuit Breakers

Circuit breaker prevents cascading failures:

```csharp
// Configuration
services.Configure<CircuitBreakerOptions>("billing", options =>
{
    options.BreakDuration = TimeSpan.FromSeconds(30);
    options.FailureRatio = 0.5;
    options.MinimumThroughput = 10;
});

// Usage in handler
public async Task<Result<InvoiceDto>> GetInvoice(Guid id)
{
    try
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            var invoice = await _billingClient.GetInvoiceAsync(id);
            return Result.Success(invoice);
        });
    }
    catch (BrokenCircuitException)
    {
        // Billing service is down, use cached data or queue for later
        return Result.Failure<InvoiceDto>("Billing service temporarily unavailable");
    }
}
```

---

## 8. Deployment Strategies

### 8.1 Parallel Deployment (Strangler Fig)

Run both the monolith (with Billing) and extracted Billing service simultaneously:

```
                    ┌─────────────────────────┐
                    │      Load Balancer       │
                    │    /api/billing/* →      │
                    └─────────┬───────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               │
    ┌─────────────────┐  ┌──────────────┐    │
    │  Wallow API    │  │   Billing    │    │
    │  (with Billing) │  │   Service    │    │
    │   10% traffic   │  │  90% traffic │    │
    └─────────────────┘  └──────────────┘    │
              │               │               │
              └───────────────┴───────────────┘
                              │
                    ┌─────────┴─────────┐
                    │     RabbitMQ      │
                    │  (shared events)  │
                    └───────────────────┘
```

**Steps:**

1. Deploy extracted Billing service alongside monolith
2. Configure load balancer to route small percentage to new service
3. Monitor for errors and performance
4. Gradually increase traffic to new service
5. Remove Billing from monolith once 100% migrated

**Nginx configuration example:**

```nginx
upstream billing_backends {
    server monolith:8080 weight=1;    # 10%
    server billing-service:8080 weight=9;  # 90%
}

server {
    location /api/billing/ {
        proxy_pass http://billing_backends;
    }

    location / {
        proxy_pass http://monolith:8080;
    }
}
```

### 8.2 Feature Flags

Use feature flags to control routing at the application level:

```csharp
// In API Gateway or main app
public class BillingRoutingMiddleware
{
    private readonly IFeatureManager _features;
    private readonly HttpClient _billingServiceClient;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/api/billing"))
        {
            if (await _features.IsEnabledAsync("use-extracted-billing"))
            {
                // Proxy to extracted service
                await ProxyToBillingService(context);
                return;
            }
        }

        await next(context);
    }

    private async Task ProxyToBillingService(HttpContext context)
    {
        var requestMessage = CreateProxyRequest(context);
        var response = await _billingServiceClient.SendAsync(requestMessage);
        await CopyResponseToContext(response, context);
    }
}
```

**Benefits:**
- Instant rollback (just flip the flag)
- A/B testing capabilities
- Gradual rollout by user/tenant

**Configuration:**

```json
{
  "FeatureManagement": {
    "use-extracted-billing": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": {
            "Value": 50
          }
        }
      ]
    }
  }
}
```

---

## 9. Monitoring Distributed Services

### 9.1 Distributed Tracing

OpenTelemetry configuration for cross-service trace correlation:

```csharp
// In both monolith and extracted service
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: builder.Environment.ApplicationName,
            serviceNamespace: "Wallow",
            serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        // Capture incoming requests
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
        })
        // Capture outgoing HTTP calls
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        // Capture EF Core queries
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
        })
        // Capture Wolverine message handling
        .AddSource("Wolverine")
        // Export to Grafana/Tempo
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(config["OpenTelemetry:OtlpEndpoint"]);
        }));
```

**Trace propagation through RabbitMQ:**

Wolverine automatically propagates trace context through RabbitMQ messages. The trace ID flows from the publishing service through the message broker to the consuming service.

```
Trace ID: abc123
├── Wallow.Api: POST /api/identity/organizations
│   └── Span: CreateOrganization
│       └── Span: PublishEvent (OrganizationCreatedEvent)
│           │
│           ▼ (via RabbitMQ)
│
└── Wallow.Billing.Service: HandleEvent
    └── Span: OrganizationCreatedEventHandler
        └── Span: CreateBillingAccount
            └── Span: BillingDbContext.SaveChanges
```

### 9.2 Centralized Logging

All services should log to the same destination with consistent metadata:

```csharp
// Serilog configuration for consistent logging
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    // Correlate with traces
    .Enrich.WithSpan()
    // Write to OpenTelemetry (Loki via OTLP)
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = $"{config["OpenTelemetry:OtlpEndpoint"]}/v1/logs";
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = context.HostingEnvironment.ApplicationName,
            ["service.namespace"] = "Wallow"
        };
    }));
```

**Log correlation in Grafana:**

```promql
# Find all logs for a specific trace
{service_namespace="Wallow"} | json | trace_id="abc123"

# Find errors across all services
{service_namespace="Wallow"} | json | level="error"

# Logs from Billing service with latency info
{service_name="wallow-billing"} | json | latency > 100
```

### 9.3 Health Monitoring

#### Individual Service Health

Each service exposes health endpoints:

```csharp
builder.Services.AddHealthChecks()
    // Database connectivity
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready", "db"])
    // RabbitMQ connectivity
    .AddRabbitMQ(rabbitMqConnectionString, name: "rabbitmq", tags: ["ready", "messaging"])
    // Custom checks
    .AddCheck<BillingServiceHealthCheck>("billing-processing", tags: ["ready"]);

// Custom health check
public class BillingServiceHealthCheck : IHealthCheck
{
    private readonly IInvoiceRepository _invoices;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            // Verify we can query invoices
            await _invoices.CountAsync(ct);
            return HealthCheckResult.Healthy("Billing processing is operational");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Billing processing failed", ex);
        }
    }
}
```

#### Dependency Health

The monolith should check extracted service health:

```csharp
builder.Services.AddHealthChecks()
    // ... existing checks ...
    .AddUrlGroup(
        new Uri($"{config["Services:Billing:Url"]}/health/ready"),
        name: "billing-service",
        tags: ["ready", "external"],
        timeout: TimeSpan.FromSeconds(5));
```

#### Grafana Dashboard

Create a dashboard showing:

1. **Service Status** - Health check results for all services
2. **Request Latency** - P50, P95, P99 by service
3. **Error Rate** - 5xx responses per service
4. **Message Queue Depth** - RabbitMQ queue sizes
5. **Database Connections** - Active connections per service

```json
{
  "panels": [
    {
      "title": "Service Health",
      "type": "stat",
      "targets": [
        {
          "expr": "up{service_namespace=\"Wallow\"}",
          "legendFormat": "{{service_name}}"
        }
      ]
    },
    {
      "title": "Request Latency P95",
      "type": "timeseries",
      "targets": [
        {
          "expr": "histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket{service_namespace=\"Wallow\"}[5m]))",
          "legendFormat": "{{service_name}}"
        }
      ]
    },
    {
      "title": "RabbitMQ Queue Depth",
      "type": "timeseries",
      "targets": [
        {
          "expr": "rabbitmq_queue_messages{queue=~\".*-inbox\"}",
          "legendFormat": "{{queue}}"
        }
      ]
    }
  ]
}
```

---

## 10. Reverting Extraction

Sometimes extraction doesn't work out. Here's how to bring a module back.

### When to Consider Reverting

- Operational complexity exceeds benefits
- Latency impact is unacceptable
- Team can't support distributed system
- Coupling discovered post-extraction
- Cost of running separate infrastructure too high

### Step-by-Step Reverting Process

#### 1. Stop Traffic to Extracted Service

Update load balancer or feature flags:

```nginx
upstream billing_backends {
    server monolith:8080 weight=10;    # 100%
    # server billing-service:8080;      # Disabled
}
```

#### 2. Drain RabbitMQ Queues

Ensure all pending messages are processed:

```bash
# Monitor queue depth
rabbitmqctl list_queues name messages

# Wait until billing-inbox is empty
while [ $(rabbitmqctl list_queues name messages | grep billing-inbox | awk '{print $2}') -gt 0 ]; do
    sleep 5
done
```

#### 3. Re-Add Module to Monolith

```xml
<!-- Wallow.Api.csproj -->
<ProjectReference Include="..\Modules\Billing\Wallow.Billing.Api\Wallow.Billing.Api.csproj" />
```

```csharp
// Program.cs
builder.Services.AddBillingModule(builder.Configuration);
// ...
await app.UseBillingModuleAsync();
// ...
opts.Discovery.IncludeAssembly(typeof(CreateInvoiceCommand).Assembly);
```

#### 4. Database Migration (If Separated)

If you moved to a separate database, migrate data back:

```bash
# Export from billing database
pg_dump -h billing-db -U billing_service -d wallow_billing \
  --schema=billing --no-owner > billing_data.sql

# Import to main database
psql -h postgres -U wallow -d wallow < billing_data.sql
```

#### 5. Deploy Updated Monolith

```bash
# Deploy new monolith version with Billing module
docker compose up -d --build app
```

#### 6. Verify and Cleanup

```bash
# Verify billing endpoints work
curl -f https://api.yourdomain.com/api/billing/invoices

# Remove extracted service
docker compose stop billing-service
docker compose rm billing-service
```

---

## 11. Case Studies

### 11.1 Extract Communications Module

**Difficulty: Easy**

Communications is an excellent first extraction candidate:

| Factor | Assessment |
|--------|------------|
| Dependencies | None outbound, consumes from many |
| Database | Simple: notifications, email messages, announcements |
| Complexity | Low: receive event, create notification, push to SignalR, send emails |
| Risk | Low: failures don't break business processes |

**Special considerations:**

1. **SignalR**: Communications uses SignalR for real-time push. The extracted service needs its own SignalR hub.

2. **Redis backplane**: Both monolith and extracted Communications must share Redis for SignalR clustering:

```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("Wallow");
        options.ConnectionFactory = async writer =>
        {
            return await ConnectionMultiplexer.ConnectAsync(redisConnectionString, writer);
        };
    });
```

3. **IRealtimeDispatcher**: The abstraction continues to work; implementation just points to local SignalR hub.

4. **Email delivery**: The extracted service needs SMTP configuration (MailKit) for sending emails.

### 11.2 Extract Billing Module

**Difficulty: Medium**

Billing is a strong extraction candidate, especially for PCI-DSS compliance:

| Factor | Assessment |
|--------|------------|
| Bounded context | Clear: invoices, payments, subscriptions, metering |
| Inbound events | Low: only consumes OrganizationCreatedEvent from Identity |
| Outbound events | Medium: publishes 4 event types consumed by Communications |
| Risk | Medium: payment processing failures need careful handling |

**Special considerations:**

1. **PCI-DSS compliance**: Extracting Billing into its own service enables process-level isolation for payment card data handling.

2. **Hangfire jobs**: Billing uses recurring background jobs (e.g., usage flushing, invoice generation) that must continue running in the extracted service:

```csharp
services.AddHangfireServer(options =>
{
    options.Queues = new[] { "billing", "default" };
    options.WorkerCount = 4;
});
```

3. **Database migration**: If separating databases, export the billing schema:

```bash
pg_dump -h localhost -U postgres -d wallow \
  --schema=billing \
  --no-owner \
  > billing_data.sql
```

4. **Dapper read queries**: Billing uses Dapper for complex reporting queries that must be migrated along with the EF Core write side.

---

## 12. Checklist

Use this checklist when extracting a module:

### Pre-Extraction Verification

- [ ] Confirm module has clear bounded context with well-defined responsibilities
- [ ] Run architecture tests to verify no direct cross-module references
- [ ] Map all inbound and outbound event dependencies
- [ ] Document current resource usage (CPU, memory, DB connections)
- [ ] Verify RabbitMQ exchange/queue topology
- [ ] Review Shared.Contracts for all event types used
- [ ] Check for any shared database queries or joins
- [ ] Identify saga dependencies (for event-sourced modules)
- [ ] Estimate operational complexity increase
- [ ] Get team buy-in on maintenance responsibilities

### Code Changes

- [ ] Create standalone service project structure
- [ ] Configure Program.cs with Wolverine, Serilog, OpenTelemetry
- [ ] Set up health checks (database, RabbitMQ)
- [ ] Configure RabbitMQ publishing and consuming
- [ ] Set up service-to-service authentication (if needed)
- [ ] Add retry policies and circuit breakers (if using HTTP calls)
- [ ] Update Shared.Contracts versioning strategy
- [ ] Create Dockerfile for extracted service
- [ ] Update docker-compose files

### Infrastructure Setup

- [ ] Provision separate infrastructure (if needed)
- [ ] Configure database (same, separate schema, or separate DB)
- [ ] Set up RabbitMQ exchanges and queues
- [ ] Configure load balancer routing
- [ ] Set up SSL/TLS certificates
- [ ] Configure secrets management
- [ ] Set up CI/CD pipeline for new service

### Testing

- [ ] Unit tests pass for extracted module
- [ ] Integration tests verify event flow
- [ ] Test failure scenarios (service down, network partition)
- [ ] Performance test comparing monolith vs extracted
- [ ] Load test extracted service independently
- [ ] Test saga coordination (if applicable)
- [ ] Verify tracing works across service boundary
- [ ] Test rollback procedure

### Deployment

- [ ] Deploy extracted service to staging
- [ ] Run parallel with monolith (strangler fig pattern)
- [ ] Gradually shift traffic (10% -> 50% -> 100%)
- [ ] Monitor error rates during transition
- [ ] Monitor latency during transition
- [ ] Verify RabbitMQ queue depths remain stable
- [ ] Complete cutover to extracted service
- [ ] Remove module from monolith

### Post-Deployment Monitoring

- [ ] Grafana dashboards configured for new service
- [ ] Alerts set up for error rate, latency, queue depth
- [ ] Log aggregation includes new service
- [ ] Trace correlation verified across services
- [ ] Health check monitoring in place
- [ ] On-call rotation updated (if separate team)

### Documentation Update

- [ ] Architecture diagrams updated
- [ ] Runbook created for new service
- [ ] Event flow documentation updated
- [ ] Deployment guide updated
- [ ] Disaster recovery procedure documented
- [ ] Team responsibilities documented

---

## Summary

Module extraction is a powerful technique for scaling specific parts of your system independently. Wallow's modular monolith architecture makes this possible by enforcing:

1. **Project isolation**: Each module has separate Domain, Application, Infrastructure, and Api projects
2. **Event-driven communication**: Modules communicate only via RabbitMQ events
3. **Database schema separation**: No cross-module database dependencies
4. **Shared contracts only**: The only shared code is `Wallow.Shared.Contracts`

However, extraction comes with significant operational complexity. Before extracting:

1. **Exhaust simpler scaling strategies** (vertical scaling, caching, read replicas)
2. **Verify module isolation** (no direct dependencies, clean event contracts)
3. **Prepare for distributed systems challenges** (network latency, eventual consistency, distributed debugging)
4. **Plan the deployment strategy** (parallel deployment, feature flags, rollback plan)

When done right, extraction allows you to:
- Scale high-load modules independently
- Enable team autonomy with separate deployment cycles
- Meet compliance requirements through process isolation
- Adopt different technologies where beneficial

When done wrong, you get all the complexity of microservices with none of the benefits.

**Start with the modular monolith. Extract only when the data demands it.**
