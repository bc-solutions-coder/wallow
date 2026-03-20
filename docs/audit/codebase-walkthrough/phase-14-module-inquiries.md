# Phase 14: Inquiries Module

**Scope:** Complete Inquiries module - Domain, Application, Infrastructure, Api layers plus Tests
**Status:** Not Started
**Files:** 40 source files, 19 test files

## How to Use This Document
- Work through layers bottom-up: Domain -> Application -> Infrastructure -> Api -> Tests
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

---

## Domain Layer

### Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Entities/Inquiry.cs` | Aggregate root for inquiry submissions | Factory method `Create()` sets initial state and raises `InquirySubmittedDomainEvent`; `TransitionTo()` enforces valid status transitions (New->Reviewed->Contacted->Closed) via pattern matching | `AggregateRoot<InquiryId>`, Domain Enums, Domain Events, Domain Exceptions | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 2 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/BudgetRange.cs` | Budget range options for inquiries | Enum values: Under5K, From5KTo15K, From15KTo50K, Over50K, NotSure | None | |
| 3 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/InquiryStatus.cs` | Status lifecycle for inquiries | Enum values: New, Reviewed, Contacted, Closed | None | |
| 4 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/ProjectType.cs` | Project type classification | Enum values: WebApplication, MobileApplication, ApiIntegration, Consulting, Other | None | |
| 5 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/Timeline.cs` | Expected project timeline options | Enum values: Asap, OneToThreeMonths, ThreeToSixMonths, SixPlusMonths, Flexible | None | |

### Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 6 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Events/InquiryStatusChangedDomainEvent.cs` | Domain event raised when inquiry status transitions | Record with InquiryId, OldStatus, NewStatus | `DomainEvent` (Shared.Kernel) | |
| 7 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Events/InquirySubmittedDomainEvent.cs` | Domain event raised when new inquiry is created | Record with InquiryId, Name, Email, Company, ProjectType, BudgetRange, Timeline, Message | `DomainEvent` (Shared.Kernel) | |

### Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 8 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Exceptions/InvalidInquiryStatusTransitionException.cs` | Business rule exception for invalid status transitions | Extends `BusinessRuleException` with error code "Inquiries.InvalidStatusTransition" | `BusinessRuleException` (Shared.Kernel) | |

### Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 9 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Domain/Identity/InquiryId.cs` | Strongly-typed ID for Inquiry entity | Readonly record struct implementing `IStronglyTypedId<InquiryId>` with `Create()` and `New()` factory methods | `IStronglyTypedId` (Shared.Kernel) | |

---

## Application Layer

### Commands / SubmitInquiry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 10 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryCommand.cs` | Command record for submitting a new inquiry | Includes honeypot field for bot detection | None | |
| 11 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryHandler.cs` | Handles inquiry submission with anti-spam measures | Silently discards honeypot submissions (returns fake success); checks Valkey rate limiting by IP; creates Inquiry aggregate and persists | `IInquiryRepository`, `IRateLimitService`, `InquiryMappings` | |
| 12 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryValidator.cs` | FluentValidation for SubmitInquiryCommand | Validates Name (required, max 200), Email (required, valid format, max 254), ProjectType/BudgetRange/Timeline (required, max 100), Message (required, max 5000), SubmitterIpAddress (required) | `FluentValidation` | |

### Commands / UpdateInquiryStatus

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 13 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusCommand.cs` | Command record for changing inquiry status | Takes InquiryId (Guid) and NewStatus (InquiryStatus enum) | `InquiryStatus` enum | |
| 14 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusHandler.cs` | Handles status transition on existing inquiry | Loads inquiry by ID, calls `TransitionTo()` on aggregate, persists update | `IInquiryRepository`, `InquiryMappings` | |
| 15 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusValidator.cs` | FluentValidation for UpdateInquiryStatusCommand | Validates InquiryId (not empty) and NewStatus (valid enum value) | `FluentValidation` | |

### DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 16 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/DTOs/InquiryDto.cs` | Data transfer object for inquiry responses | Sealed record with Id, Name, Email, Company, ProjectType, BudgetRange, Timeline, Message, Status, SubmitterIpAddress, CreatedAt | None | |

### EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/EventHandlers/InquiryStatusChangedDomainEventHandler.cs` | Handles domain event when inquiry status changes | Handles `InquiryStatusChangedDomainEvent`; performs any side effects needed on status transition | `IInquiryRepository`, Domain Events | |
| 18 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/EventHandlers/InquirySubmittedDomainEventHandler.cs` | Publishes integration event when inquiry is submitted | Handles `InquirySubmittedDomainEvent`, loads inquiry, publishes `InquirySubmittedEvent` (Shared.Contracts) via Wolverine `IMessageBus` for cross-module communication | `IInquiryRepository`, `IMessageBus` (Wolverine), `Shared.Contracts.Inquiries.Events` | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 19 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Extensions/ApplicationExtensions.cs` | DI registration for Application layer | Registers FluentValidation validators from assembly | `FluentValidation`, `Microsoft.Extensions.DependencyInjection` | |

### Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 20 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Interfaces/IInquiryRepository.cs` | Repository interface for Inquiry persistence | Methods: GetByIdAsync, GetAllAsync, AddAsync, UpdateAsync | Domain Entities, Domain Identity | |
| 21 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Interfaces/IRateLimitService.cs` | Rate limiting abstraction | Single method `IsAllowedAsync(key)` returns bool | None | |

### Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 22 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Mappings/InquiryMappings.cs` | Extension method to map Inquiry entity to DTO | `ToDto()` extension on `Inquiry` entity | `InquiryDto`, `Inquiry` entity | |

### Queries / GetInquiries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 23 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiries/GetInquiriesQuery.cs` | Query record for listing inquiries | Optional `InquiryStatus?` filter parameter | `InquiryStatus` enum | |
| 24 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiries/GetInquiriesHandler.cs` | Handles listing all inquiries with optional status filter | Loads all inquiries, filters by status in-memory if specified, maps to DTOs | `IInquiryRepository`, `InquiryMappings` | |

### Queries / GetInquiryById

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 25 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiryById/GetInquiryByIdQuery.cs` | Query record for fetching single inquiry | Takes InquiryId as Guid | None | |
| 26 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiryById/GetInquiryByIdHandler.cs` | Handles single inquiry lookup by ID | Loads inquiry by strongly-typed ID, returns NotFound if missing, maps to DTO | `IInquiryRepository`, `InquiryMappings` | |

---

## Infrastructure Layer

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 27 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Extensions/InquiriesInfrastructureExtensions.cs` | DI registration for Infrastructure layer | Registers `InquiriesDbContext` with Npgsql (retry, timeout, migrations history in "inquiries" schema), registers `InquiryRepository` (scoped) and `ValkeyRateLimitService` (singleton) | `IInquiryRepository`, `IRateLimitService`, EF Core | |
| 28 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Extensions/InquiriesModuleExtensions.cs` | Top-level module registration and initialization | `AddInquiriesModule()` composes Application + Infrastructure; `InitializeInquiriesModuleAsync()` runs EF migrations in Development/Testing environments with error logging | Application Extensions, `InquiriesDbContext` | |

### Persistence / Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 29 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/Configurations/InquiryConfiguration.cs` | EF Core entity configuration for Inquiry | Maps to "inquiries" table with snake_case columns, `StronglyTypedIdConverter` for InquiryId, indexes on Status, CreatedAt, Email; ignores DomainEvents navigation | `IEntityTypeConfiguration<Inquiry>`, `StronglyTypedIdConverter` | |

### Persistence

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 30 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/InquiriesDbContext.cs` | EF Core DbContext for Inquiries module | Default schema "inquiries", NoTracking by default, auto-applies configurations from assembly | `DbContext`, `Inquiry` entity | |
| 31 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/InquiriesDbContextFactory.cs` | Design-time DbContext factory for EF migrations | `IDesignTimeDbContextFactory` with env-variable password fallback to "wallow" | `InquiriesDbContext` | |

### Persistence / Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/Repositories/InquiryRepository.cs` | EF Core repository implementation for Inquiry | GetByIdAsync uses AsTracking for mutation support; GetAllAsync orders by CreatedAt descending; AddAsync/UpdateAsync delegate to DbContext | `InquiriesDbContext`, `IInquiryRepository` | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 33 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Services/ValkeyRateLimitService.cs` | Valkey/Redis-based IP rate limiting | Sliding window: StringIncrement on IP key, sets 15-min expiry on first request, allows max 5 requests per window | `IConnectionMultiplexer` (StackExchange.Redis), `IRateLimitService` | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 34 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Migrations/20260306182826_InitialCreate.cs` | Initial migration creating inquiries table | Creates "inquiries" schema and table with all columns (uuid PK, varchar fields, integer status), adds indexes on status, created_at, email | EF Core Migrations | |
| 35 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Migrations/20260306182826_InitialCreate.Designer.cs` | Auto-generated migration designer metadata | EF Core generated - snapshot of model at migration time | EF Core Migrations | |
| 36 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Migrations/InquiriesDbContextModelSnapshot.cs` | Auto-generated model snapshot | EF Core generated - current model state for migration diffing | EF Core Migrations | |

---

## Api Layer

### Contracts

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 37 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/InquiryResponse.cs` | API response DTO for inquiry data | Sealed record with Id, Name, Email, Company, Phone, Subject, Message, Status, CreatedAt, UpdatedAt | None | |
| 38 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/SubmitInquiryRequest.cs` | API request DTO for submitting inquiry | Sealed record with Name, Email, Company, Phone, Subject, Message | None | |
| 39 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/UpdateInquiryStatusRequest.cs` | API request DTO for status update | Sealed record with NewStatus (string) | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 40 | [ ] | `src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs` | REST API controller for inquiry CRUD | 4 endpoints: POST (AllowAnonymous, extracts IP), GET all (with status filter), GET by ID, PUT status; uses Wolverine `IMessageBus` for CQRS dispatch; maps DTOs to API response format | Wolverine, Application Commands/Queries, `Shared.Api.Extensions` | |

---

## Test Files

### Domain / Entities

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 41 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Domain/Entities/InquiryCreateTests.cs` | Inquiry aggregate creation via factory method | |
| 42 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Domain/Entities/InquiryDomainEventTests.cs` | Domain events raised during Inquiry lifecycle | |
| 43 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Domain/Entities/InquiryStatusTransitionTests.cs` | Valid status transition paths | |
| 44 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Domain/Entities/InquiryTransitionTests.cs` | Additional transition edge cases | |

### Domain / Exceptions

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 45 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Domain/Exceptions/InvalidInquiryStatusTransitionExceptionTests.cs` | Exception thrown for invalid status transitions | |

### Domain / ValueObjects

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 46 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Domain/ValueObjects/InquiryValueObjectTests.cs` | Strongly-typed ID and value object behavior | |

### Application / Commands / SubmitInquiry

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 47 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Commands/SubmitInquiry/SubmitInquiryBoundaryTests.cs` | Boundary/edge cases for inquiry submission | |
| 48 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Commands/SubmitInquiry/SubmitInquiryHandlerTests.cs` | Handler logic: honeypot rejection, rate limiting, persistence | |
| 49 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Commands/SubmitInquiry/SubmitInquiryValidatorTests.cs` | Validator rules for SubmitInquiryCommand | |

### Application / Commands / UpdateInquiryStatus

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 50 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusAllTransitionsTests.cs` | All valid and invalid status transition combinations | |
| 51 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusHandlerTests.cs` | Handler logic for status update | |
| 52 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusValidatorTests.cs` | Validator rules for UpdateInquiryStatusCommand | |

### Application / EventHandlers

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 53 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/EventHandlers/InquiryStatusChangedDomainEventHandlerTests.cs` | Handler behavior on InquiryStatusChangedDomainEvent | |
| 54 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/EventHandlers/InquirySubmittedDomainEventHandlerTests.cs` | Integration event publication on inquiry submission | |

### Application / Mappings

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 55 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Mappings/InquiryMappingsTests.cs` | ToDto() mapping correctness | |

### Application / Queries / GetInquiries

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 56 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Queries/GetInquiries/GetInquiriesHandlerEdgeCaseTests.cs` | Edge cases for GetInquiries query (empty list, filter mismatches) | |
| 57 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Queries/GetInquiries/GetInquiriesHandlerTests.cs` | Normal GetInquiries handler behavior with and without status filter | |

### Application / Queries / GetInquiryById

| # | Status | File | What It Tests | Your Notes |
|---|--------|------|---------------|------------|
| 58 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Queries/GetInquiryById/GetInquiryByIdHandlerTests.cs` | Successful single inquiry retrieval | |
| 59 | [ ] | `tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Queries/GetInquiryById/GetInquiryByIdNotFoundTests.cs` | NotFound result when inquiry does not exist | |
