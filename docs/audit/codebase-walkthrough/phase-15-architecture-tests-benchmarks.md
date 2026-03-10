# Phase 15: Architecture Tests, Benchmarks & Test Infrastructure

**Scope:** `tests/Foundry.Architecture.Tests/`, `tests/Benchmarks/`, `tests/Foundry.Messaging.IntegrationTests/`, `tests/Foundry.Tests.Common/`
**Status:** Not Started
**Files:** 0 source files, 49 test/infrastructure files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Test Files

### Architecture Tests (`tests/Foundry.Architecture.Tests/`)

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 1 | [ ] | `tests/Foundry.Architecture.Tests/GlobalUsings.cs` | Global using for FluentAssertions | N/A - infrastructure |
| 2 | [ ] | `tests/Foundry.Architecture.Tests/TestConstants.cs` | Auto-discovers all module names by scanning for `Foundry.*.Domain.dll` at runtime. Provides `AllModules` array used by all architecture tests. | Test data provider for parameterized architecture tests |
| 3 | [ ] | `tests/Foundry.Architecture.Tests/CleanArchitectureTests.cs` | Enforces Clean Architecture layer dependency rules per module using NetArchTest. Verifies Domain has no forbidden dependencies, Application depends only on Domain, etc. | Domain/Application/Infrastructure/Api layer isolation for all modules |
| 4 | [ ] | `tests/Foundry.Architecture.Tests/ApiConventionTests.cs` | Enforces API controller naming and attribute conventions across all modules using NetArchTest. | Controller inheritance, naming, and attribute conventions |
| 5 | [ ] | `tests/Foundry.Architecture.Tests/ApiVersioningTests.cs` | Verifies all controllers have `ApiVersion` attributes for proper API versioning. | `[ApiVersion]` attribute presence on all controllers |
| 6 | [ ] | `tests/Foundry.Architecture.Tests/CqrsConventionTests.cs` | Enforces CQRS naming conventions (Command/Query suffixes, Handler naming) and validates FluentValidation validator pairing. | Command/Query/Handler naming patterns, validator coverage |
| 7 | [ ] | `tests/Foundry.Architecture.Tests/ModuleIsolationTests.cs` | Verifies no module references another module directly. Parameterized across all modules and layers. | Cross-module dependency prohibition (only Shared.Contracts allowed) |
| 8 | [ ] | `tests/Foundry.Architecture.Tests/ModuleRegistrationTests.cs` | Verifies `FoundryModules.cs` registers all modules by scanning its source code for `AddXxxModule` and `InitializeXxxModuleAsync` calls. | Module registration completeness in composition root |
| 9 | [ ] | `tests/Foundry.Architecture.Tests/MultiTenancyArchitectureTests.cs` | Enforces multi-tenancy patterns: tenant-scoped entities implement `ITenantScoped`, DbContexts apply tenant query filters, repositories inject `ITenantContext`. | `ITenantScoped`, tenant query filters, `ITenantContext` injection |
| 10 | [ ] | `tests/Foundry.Architecture.Tests/SharedContractsTests.cs` | Verifies `Shared.Contracts` and `Shared.Kernel` have no dependencies on any module. Ensures integration events inherit from `IntegrationEvent`. | Shared library independence, event base class inheritance |
| 11 | [ ] | `tests/Foundry.Architecture.Tests/WolverineConventionTests.cs` | Verifies all Wolverine message handlers follow conventions: have `Handle`/`HandleAsync` methods, live in correct namespaces. | Handler method presence and naming conventions |
| 12 | [ ] | `tests/Foundry.Architecture.Tests/Modules/ModuleToggleTests.cs` | Tests that disabling a module via `Foundry:Modules:X = false` prevents its services from being registered. | Module toggle configuration behavior |
| 13 | [ ] | `tests/Foundry.Architecture.Tests/MultiTenancy/TenantAwareDbContextTests.cs` | Tests tenant query filter application using an in-memory SQLite DbContext. Verifies `ITenantScoped` entities get automatic `WHERE TenantId = @tenantId` filters. | `ApplyTenantQueryFilters` on a test DbContext |

### Benchmarks (`tests/Benchmarks/`)

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 14 | [ ] | `tests/Benchmarks/Program.cs` | Entry point for BenchmarkDotNet. Uses `BenchmarkSwitcher` to run benchmarks from the assembly. | N/A - benchmark runner |
| 15 | [ ] | `tests/Benchmarks/QueryBenchmarks.cs` | Performance benchmarks for repository queries across Identity, Billing, Configuration, and Storage modules using in-memory SQLite. | Repository query performance (memory + time) via `[MemoryDiagnoser]` and `[ShortRunJob]` |

### Messaging Integration Tests - Fixtures & Helpers (`tests/Foundry.Messaging.IntegrationTests/`)

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 16 | [ ] | `tests/Foundry.Messaging.IntegrationTests/GlobalUsings.cs` | Global using for FluentAssertions | N/A - infrastructure |
| 17 | [ ] | `tests/Foundry.Messaging.IntegrationTests/Fixtures/MessagingTestFixture.cs` | Extends `FoundryApiFactory` with messaging-specific services (message tracker, cross-module tracker, RabbitMQ transport). Configures Wolverine to discover test handlers. | Test fixture for end-to-end messaging tests |
| 18 | [ ] | `tests/Foundry.Messaging.IntegrationTests/Helpers/MessageWaiter.cs` | Polling utility that waits for async message processing to complete with configurable timeout and interval. | Async message delivery verification |

### Messaging Integration Tests - Test Events & Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 19 | [ ] | `tests/Foundry.Messaging.IntegrationTests/TestEvents/TestEvent.cs` | Defines test integration event records: `TestEvent`, `TestEventThatFails` (configurable retry), `TestEventThatFailsImmediately`. | N/A - test data contracts |
| 20 | [ ] | `tests/Foundry.Messaging.IntegrationTests/TestHandlers/IMessageTracker.cs` | Interface for tracking processed test events and retry attempt counts. | N/A - test infrastructure contract |
| 21 | [ ] | `tests/Foundry.Messaging.IntegrationTests/TestHandlers/MessageTracker.cs` | Thread-safe implementation of `IMessageTracker`. Records processed events and attempt counts with `Lock`. | N/A - test infrastructure |
| 22 | [ ] | `tests/Foundry.Messaging.IntegrationTests/TestHandlers/ICrossModuleEventTracker.cs` | Interface for tracking cross-module event handler executions by module name and event type. | N/A - test infrastructure contract |
| 23 | [ ] | `tests/Foundry.Messaging.IntegrationTests/TestHandlers/CrossModuleEventTracker.cs` | Thread-safe implementation of `ICrossModuleEventTracker`. Records which modules handled which events. | N/A - test infrastructure |
| 24 | [ ] | `tests/Foundry.Messaging.IntegrationTests/TestHandlers/TestEventHandler.cs` | Wolverine handler for test events. Handles `TestEvent` (records), `TestEventThatFails` (fails N times then succeeds), `TestEventThatFailsImmediately` (always throws). | N/A - test handler for messaging scenarios |
| 25 | [ ] | `tests/Foundry.Messaging.IntegrationTests/TestHandlers/UserRegisteredEventTestHandler.cs` | Test handler for `UserRegisteredEvent` integration event. Records execution in cross-module tracker to verify event propagation. | N/A - test handler for cross-module events |

### Messaging Integration Tests - Test Classes

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 26 | [ ] | `tests/Foundry.Messaging.IntegrationTests/Tests/MessagePublishConsumeTests.cs` | End-to-end tests for publishing and consuming messages via Wolverine + RabbitMQ. | Basic message publish/consume, batch processing |
| 27 | [ ] | `tests/Foundry.Messaging.IntegrationTests/Tests/MessageRetryTests.cs` | Tests Wolverine's message retry behavior with configurable failure counts. | Retry logic for transient failures |
| 28 | [ ] | `tests/Foundry.Messaging.IntegrationTests/Tests/MessageDeadLetterTests.cs` | Tests that permanently failing messages are moved to the dead letter queue. | Dead letter queue behavior for unrecoverable failures |
| 29 | [ ] | `tests/Foundry.Messaging.IntegrationTests/Tests/CrossModuleEventPropagationTests.cs` | Tests that integration events (e.g., `UserRegisteredEvent`) are delivered to handlers across module boundaries. | Cross-module event routing via Shared.Contracts |

### Shared Test Infrastructure - Bases (`tests/Foundry.Tests.Common/`)

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 30 | [ ] | `tests/Foundry.Tests.Common/GlobalUsings.cs` | Global usings for FluentAssertions, NSubstitute, and xUnit | N/A - infrastructure |
| 31 | [ ] | `tests/Foundry.Tests.Common/Bases/DbContextIntegrationTestBase.cs` | Abstract base class for DbContext integration tests. Shares a PostgreSQL container via collection fixture, creates fresh DbContext per class with tenant isolation. | N/A - reusable test base for repository/Dapper tests |
| 32 | [ ] | `tests/Foundry.Tests.Common/Bases/FoundryIntegrationTestBase.cs` | Abstract base class for API integration tests using `FoundryApiFactory`. Handles HttpClient creation with default auth, scoped service lifecycle, and cleanup. | N/A - reusable test base for API-level tests |

### Shared Test Infrastructure - Builders

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 33 | [ ] | `tests/Foundry.Tests.Common/Builders/InvoiceBuilder.cs` | Fluent builder for creating `Invoice` test entities with configurable state (draft, issued, paid, overdue). Clears domain events after building. | N/A - test data builder for Billing module |

### Shared Test Infrastructure - Factories

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 34 | [ ] | `tests/Foundry.Tests.Common/Factories/FoundryApiFactory.cs` | `WebApplicationFactory<Program>` subclass that spins up Testcontainers (PostgreSQL, RabbitMQ, Redis), replaces auth with `TestAuthHandler`, and swaps external services with fakes. | N/A - full API pipeline test factory |

### Shared Test Infrastructure - Fakes

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 35 | [ ] | `tests/Foundry.Tests.Common/Fakes/FakeInvoiceQueryService.cs` | No-op `IInvoiceQueryService` returning zero revenue and counts. Used to avoid Billing dependencies in non-billing tests. | N/A - test double |
| 36 | [ ] | `tests/Foundry.Tests.Common/Fakes/FakeKeycloakAdminService.cs` | No-op `IKeycloakAdminService` returning random GUIDs and nulls. Avoids real Keycloak dependency in tests. | N/A - test double |
| 37 | [ ] | `tests/Foundry.Tests.Common/Fakes/FakeMeteringQueryService.cs` | No-op `IMeteringQueryService` returning null quota status. | N/A - test double |
| 38 | [ ] | `tests/Foundry.Tests.Common/Fakes/FakeUserQueryService.cs` | No-op `IUserQueryService` returning placeholder email and zero counts. | N/A - test double |
| 39 | [ ] | `tests/Foundry.Tests.Common/Fakes/NoOpHybridCache.cs` | Pass-through `HybridCache` implementation that always invokes the factory function without caching. Ensures test isolation. | N/A - test double |

### Shared Test Infrastructure - Fixtures

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 40 | [ ] | `tests/Foundry.Tests.Common/Fixtures/DatabaseFixture.cs` | Standalone PostgreSQL Testcontainer fixture (`postgres:18-alpine`) for repository-only tests. | N/A - container lifecycle |
| 41 | [ ] | `tests/Foundry.Tests.Common/Fixtures/FoundryTestCollection.cs` | xUnit collection definition for sharing `FoundryApiFactory` across integration test classes. | N/A - test infrastructure |
| 42 | [ ] | `tests/Foundry.Tests.Common/Fixtures/KeycloakFixture.cs` | Keycloak Testcontainer fixture with realm import. Used only for Identity module OAuth2 flow tests. | N/A - container lifecycle with realm config |
| 43 | [ ] | `tests/Foundry.Tests.Common/Fixtures/PostgresContainerFixture.cs` | Lightweight PostgreSQL Testcontainer fixture for `DbContextIntegrationTestBase`. Identical config to `DatabaseFixture`. | N/A - container lifecycle |
| 44 | [ ] | `tests/Foundry.Tests.Common/Fixtures/RabbitMqFixture.cs` | RabbitMQ Testcontainer fixture (`rabbitmq:4.2-management-alpine`) for messaging tests. | N/A - container lifecycle |
| 45 | [ ] | `tests/Foundry.Tests.Common/Fixtures/RedisFixture.cs` | Valkey/Redis Testcontainer fixture (`valkey/valkey:8-alpine`) for cache and presence tests. | N/A - container lifecycle |

### Shared Test Infrastructure - Helpers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 46 | [ ] | `tests/Foundry.Tests.Common/Helpers/JwtTokenHelper.cs` | Generates and parses test tokens in `test-token:{userId}:{roles}` format for `TestAuthHandler`. | N/A - auth test utility |
| 47 | [ ] | `tests/Foundry.Tests.Common/Helpers/LoggerAssertionExtensions.cs` | NSubstitute-based assertion extensions for verifying log messages on `ILogger` mocks. | N/A - logging assertion utility |
| 48 | [ ] | `tests/Foundry.Tests.Common/Helpers/TestAuthHandler.cs` | Fake `AuthenticationHandler` for integration tests. Default admin user; customizable via `X-Test-User-Id`, `X-Test-Roles`, and `X-Test-Auth-Skip` headers. | N/A - auth bypass for tests |
| 49 | [ ] | `tests/Foundry.Tests.Common/Helpers/TestConstants.cs` | Fixed GUIDs for test isolation: `AdminUserId`, `TestOrgId`, `TestTenantId`. | N/A - deterministic test data |
