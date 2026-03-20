# Phase 15: Showcases Module

**Scope:** Complete Showcases module - Domain, Application, Infrastructure, Api layers + Domain tests + Application tests
**Status:** Not Started
**Files:** 26 source files, 10 test files

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
| 1 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Domain/Entities/Showcase.cs` | Aggregate root for portfolio showcase items | Factory `Create()` validates title (required, max 200), requires at least one URL (demo/GitHub/video), returns `Result<Showcase>`; `Update()` applies same validation and replaces all fields including tags collection | `AggregateRoot<ShowcaseId>`, `Result`, Domain Enums, Domain Identity | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 2 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Domain/Enums/ShowcaseCategory.cs` | Category classification for showcases | Enum values: WebApp, Api, Mobile, Library, Tool | None | |

### Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 3 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Domain/Identity/ShowcaseId.cs` | Strongly-typed ID for Showcase entity | Readonly record struct implementing `IStronglyTypedId<ShowcaseId>` with `Create()` and `New()` factory methods | `IStronglyTypedId` (Shared.Kernel) | |

---

## Application Layer

### Commands / CreateShowcase

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 4 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Commands/CreateShowcase/CreateShowcaseHandler.cs` | Command + handler for creating showcases | Contains both `CreateShowcaseCommand` record and `CreateShowcaseHandler` class; delegates validation to domain `Showcase.Create()`, persists via repository, returns `ShowcaseId` | `IShowcaseRepository`, Domain entities | |
| 5 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Commands/CreateShowcase/CreateShowcaseValidator.cs` | FluentValidation for CreateShowcaseCommand | Validates Title (required, max 200), at least one URL must be present, Category must be valid enum | `FluentValidation` | |

### Commands / DeleteShowcase

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 6 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Commands/DeleteShowcase/DeleteShowcaseHandler.cs` | Command + handler for deleting showcases | Contains `DeleteShowcaseCommand` record (takes ShowcaseId) and handler; checks existence before deleting, returns NotFound error if missing | `IShowcaseRepository` | |

### Commands / UpdateShowcase

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 7 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Commands/UpdateShowcase/UpdateShowcaseHandler.cs` | Command + handler for updating showcases | Contains `UpdateShowcaseCommand` record and handler; loads existing showcase, delegates to domain `Update()` method, persists if successful | `IShowcaseRepository`, Domain entities | |
| 8 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Commands/UpdateShowcase/UpdateShowcaseValidator.cs` | FluentValidation for UpdateShowcaseCommand | Validates ShowcaseId (not empty GUID), Title (required, max 200), at least one URL, valid Category enum | `FluentValidation` | |

### Contracts

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 9 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Contracts/IShowcaseRepository.cs` | Repository interface for Showcase persistence | Methods: GetByIdAsync, GetAllAsync (with category/tag filters), AddAsync, UpdateAsync, DeleteAsync | Domain Entities, Enums, Identity | |
| 10 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Contracts/ShowcaseDto.cs` | Data transfer object for showcase responses | Record with ShowcaseId, Title, Description, Category, DemoUrl, GitHubUrl, VideoUrl, Tags, DisplayOrder, IsPublished | Domain Enums, Domain Identity | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 11 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Extensions/ApplicationExtensions.cs` | DI registration for Application layer | Registers FluentValidation validators from assembly | `FluentValidation`, `Microsoft.Extensions.DependencyInjection` | |

### Queries / GetShowcase

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 12 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Queries/GetShowcase/GetShowcaseQuery.cs` | Query record for fetching single showcase | Takes `ShowcaseId` (strongly-typed) | Domain Identity | |
| 13 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Queries/GetShowcase/GetShowcaseHandler.cs` | Handles single showcase lookup by ID | Loads showcase, returns NotFound if missing, manually maps entity to `ShowcaseDto` | `IShowcaseRepository`, `ShowcaseDto` | |

### Queries / GetShowcases

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 14 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Queries/GetShowcases/GetShowcasesQuery.cs` | Query record for listing showcases | Takes optional `ShowcaseCategory?` and `string? Tag` filters | Domain Enums | |
| 15 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Application/Queries/GetShowcases/GetShowcasesHandler.cs` | Handles listing showcases with filters | Delegates filtering to repository (category + tag), maps results to `ShowcaseDto` list | `IShowcaseRepository`, `ShowcaseDto` | |

---

## Infrastructure Layer

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 16 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Extensions/ShowcasesModuleExtensions.cs` | Top-level module registration and initialization | `AddShowcasesModule()` composes Application + Persistence (DbContext with Npgsql, retry, "showcases" schema, `ShowcaseRepository`); `InitializeShowcasesModuleAsync()` runs EF migrations in Development only | Application Extensions, `ShowcasesDbContext`, `ShowcaseRepository` | |

### Persistence / Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Persistence/Configurations/ShowcaseConfiguration.cs` | EF Core entity configuration for Showcase | Maps to "showcases" table with snake_case columns, value conversion for ShowcaseId, `text[]` PostgreSQL array for tags via field access mode, ignores CreatedBy/UpdatedBy | `IEntityTypeConfiguration<Showcase>` | |

### Persistence

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 18 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Persistence/ShowcasesDbContext.cs` | EF Core DbContext for Showcases module | Default schema "showcases", NoTracking by default, auto-applies configurations from assembly | `DbContext`, `Showcase` entity | |
| 19 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Persistence/ShowcasesDbContextFactory.cs` | Design-time DbContext factory for EF migrations | `IDesignTimeDbContextFactory` with env-variable password fallback to "wallow" | `ShowcasesDbContext` | |

### Persistence / Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 20 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Persistence/Repositories/ShowcaseRepository.cs` | EF Core repository implementation for Showcase | Uses `EF.CompileAsyncQuery` for GetById (with AsTracking); GetAllAsync filters by category and tag (using `EF.Property` for backing field), orders by DisplayOrder then Title; Add/Update/Delete all call SaveChangesAsync | `ShowcasesDbContext`, `IShowcaseRepository` | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 21 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Migrations/20260306215720_Initial.cs` | Initial migration creating showcases table | Creates "showcases" schema and table with columns (uuid PK, varchar title, text description, integer category, text URLs, integer display_order, boolean is_published, text[] tags, timestamps) | EF Core Migrations | |
| 22 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Migrations/20260306215720_Initial.Designer.cs` | Auto-generated migration designer metadata | EF Core generated - snapshot of model at migration time | EF Core Migrations | |
| 23 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Migrations/ShowcasesDbContextModelSnapshot.cs` | Auto-generated model snapshot | EF Core generated - current model state for migration diffing | EF Core Migrations | |

---

## Api Layer

### Contracts / Requests

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 24 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Api/Contracts/Requests/CreateShowcaseRequest.cs` | API request DTO for creating a showcase | Sealed record with Title, Description, Category, DemoUrl, GitHubUrl, VideoUrl, Tags, DisplayOrder, IsPublished | `ShowcaseCategory` enum | |
| 25 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Api/Contracts/Requests/UpdateShowcaseRequest.cs` | API request DTO for updating a showcase | Sealed record with same fields as CreateShowcaseRequest | `ShowcaseCategory` enum | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 26 | [ ] | `src/Modules/Showcases/Wallow.Showcases.Api/Controllers/ShowcasesController.cs` | REST API controller for showcase CRUD | 5 endpoints: GET all (AllowAnonymous, with category/tag filters), GET by ID (AllowAnonymous), POST (requires ShowcasesManage permission, returns 201 Created), PUT (ShowcasesManage), DELETE (ShowcasesManage, returns 204 NoContent); uses Wolverine `IMessageBus` for CQRS dispatch | Wolverine, Application Commands/Queries, `HasPermission` attribute, `Shared.Api.Extensions` | |

---

## Test Files

### Domain Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 27 | [ ] | `tests/Modules/Showcases/Showcases.Domain.Tests/ShowcaseTests.cs` | Unit tests for Showcase aggregate root | `ShowcaseCreateTests`: 8 tests covering Create with valid data (all fields set), single URL variants (demo-only, GitHub-only, video-only), empty/whitespace/null title failure, title exceeding 200 chars, no URLs failure, default display order. `ShowcaseUpdateTests`: 4 tests covering Update with valid data (all fields updated), empty title failure, long title failure, no URLs failure | |
| 28 | [ ] | `tests/Modules/Showcases/Showcases.Domain.Tests/ShowcasePublishTests.cs` | Unit tests for Showcase publish behavior | Tests covering publish/unpublish state transitions on the Showcase aggregate | |
| 29 | [ ] | `tests/Modules/Showcases/Showcases.Domain.Tests/ShowcaseUpdateEdgeCaseTests.cs` | Edge case tests for Showcase Update | Additional edge cases for `Update()` beyond the core happy/failure paths in ShowcaseTests | |

### Application Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 30 | [ ] | `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/CreateShowcase/CreateShowcaseHandlerTests.cs` | Unit tests for CreateShowcaseHandler | Handler behavior: successful creation, domain failure propagation, repository interactions | |
| 31 | [ ] | `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/CreateShowcase/CreateShowcaseValidatorTests.cs` | Unit tests for CreateShowcaseValidator | FluentValidation rules: title required/length, URL presence, category validity | |
| 32 | [ ] | `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/DeleteShowcase/DeleteShowcaseHandlerTests.cs` | Unit tests for DeleteShowcaseHandler | Handler behavior: successful delete, not-found when showcase missing | |
| 33 | [ ] | `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/UpdateShowcase/UpdateShowcaseHandlerTests.cs` | Unit tests for UpdateShowcaseHandler | Handler behavior: successful update, not-found, domain failure propagation | |
| 34 | [ ] | `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/UpdateShowcase/UpdateShowcaseValidatorTests.cs` | Unit tests for UpdateShowcaseValidator | FluentValidation rules: ShowcaseId not empty, title required/length, URL presence, category validity | |
| 35 | [ ] | `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Queries/GetShowcase/GetShowcaseHandlerTests.cs` | Unit tests for GetShowcaseHandler | Handler behavior: returns ShowcaseDto on success, not-found when missing | |
| 36 | [ ] | `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Queries/GetShowcases/GetShowcasesHandlerTests.cs` | Unit tests for GetShowcasesHandler | Handler behavior: returns list, filters by category and tag, empty list when none match | |
