# Phase 12: Announcements Module

**Scope:** `src/Modules/Announcements/`
**Status:** Not Started
**Files:** 68 source files, 21 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

---

## Domain Layer

### Announcements Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 1 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Announcements/Entities/Announcement.cs` | Core aggregate root for announcements | `Create` factory sets status to `Draft` or `Scheduled` based on presence of `PublishAt`; `Publish`, `Expire`, `Archive` methods transition status; `Update` mutates all fields; implements `ITenantScoped` | `AnnouncementId`, `AnnouncementStatus`, `AnnouncementTarget`, `AnnouncementType`, `AggregateRoot<T>`, `ITenantScoped`, `TenantId` | |
| 2 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Announcements/Entities/AnnouncementDismissal.cs` | Records a user dismissing an announcement | `Create` factory captures `AnnouncementId`, `UserId`, and `DismissedAt` timestamp; no state transitions; child entity not an aggregate | `AnnouncementDismissalId`, `AnnouncementId`, `UserId`, `Entity<T>` | |

### Changelog Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 3 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Changelogs/Entities/ChangelogEntry.cs` | Aggregate root for a versioned changelog entry | `Create` factory validates version/title/content; `Publish`/`Unpublish` toggle `IsPublished`; `AddItem`/`RemoveItem` manage the `_items` collection; `Update` mutates all fields | `ChangelogEntryId`, `ChangelogItemId`, `ChangelogItem`, `ChangeType`, `AggregateRoot<T>` | |
| 4 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Changelogs/Entities/ChangelogItem.cs` | Individual change line item within a `ChangelogEntry` | `Create` validates description; `Update` mutates description and type; child entity owned by `ChangelogEntry` | `ChangelogItemId`, `ChangelogEntryId`, `ChangeType`, `Entity<T>` | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 5 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Announcements/Enums/AnnouncementStatus.cs` | Lifecycle states for an announcement | Values: `Draft=0`, `Scheduled=1`, `Published=2`, `Expired=3`, `Archived=4` | None | |
| 6 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Announcements/Enums/AnnouncementTarget.cs` | Audience targeting scope | Values: `All=0`, `Tenant=1`, `Plan=2`, `Role=3` | None | |
| 7 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Announcements/Enums/AnnouncementType.cs` | Category of announcement | Values: `Feature=0`, `Update=1`, `Maintenance=2`, `Alert=3`, `Tip=4` | None | |
| 8 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Changelogs/Enums/ChangeType.cs` | Category of a changelog line item | Values: `Feature=0`, `Improvement=1`, `Fix=2`, `Breaking=3`, `Security=4`, `Deprecated=5` | None | |

### Identity / Strongly-Typed IDs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 9 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Announcements/Identity/AnnouncementId.cs` | Strongly-typed ID wrapper for `Announcement` | Wraps `Guid`; provides `New()` and `Create(Guid)` factory methods | `Shared.Kernel` strongly-typed ID base | |
| 10 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Announcements/Identity/AnnouncementDismissalId.cs` | Strongly-typed ID wrapper for `AnnouncementDismissal` | Same pattern as `AnnouncementId` | `Shared.Kernel` strongly-typed ID base | |
| 11 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Changelogs/Identity/ChangelogEntryId.cs` | Strongly-typed ID wrapper for `ChangelogEntry` | Same pattern as `AnnouncementId` | `Shared.Kernel` strongly-typed ID base | |
| 12 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/Changelogs/Identity/ChangelogItemId.cs` | Strongly-typed ID wrapper for `ChangelogItem` | Same pattern as `AnnouncementId` | `Shared.Kernel` strongly-typed ID base | |

### Marker Interface

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 13 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Domain/IAnnouncementsDomainMarker.cs` | Assembly marker for Wolverine handler discovery and architecture tests | Empty interface used as type-scan anchor | None | |

---

## Application Layer

### Announcements Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 14 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/ArchiveAnnouncement/ArchiveAnnouncementCommand.cs` | Archives an announcement by ID | Loads announcement, calls `Archive(timeProvider)`, saves; returns `NotFound` if missing | `IAnnouncementRepository`, `TimeProvider`, `Result` | |
| 15 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/ArchiveAnnouncement/ArchiveAnnouncementValidator.cs` | Validates `ArchiveAnnouncementCommand` | Validates that `Id` is non-empty Guid | FluentValidation | |
| 16 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/CreateAnnouncement/CreateAnnouncementCommand.cs` | Creates a new announcement in the current tenant | Calls `Announcement.Create` with all fields; resolves tenant from `ITenantContext`; maps result to `AnnouncementDto` | `IAnnouncementRepository`, `ITenantContext`, `TimeProvider`, `AnnouncementDto` | |
| 17 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/CreateAnnouncement/CreateAnnouncementValidator.cs` | Validates `CreateAnnouncementCommand` | Validates `Title` and `Content` non-empty; URL format checks for optional fields | FluentValidation | |
| 18 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/DismissAnnouncement/DismissAnnouncementCommand.cs` | Records a user's dismissal of an announcement | Checks announcement exists and is dismissible; idempotent (no error if already dismissed); creates `AnnouncementDismissal` | `IAnnouncementRepository`, `IAnnouncementDismissalRepository`, `TimeProvider` | |
| 19 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/DismissAnnouncement/DismissAnnouncementValidator.cs` | Validates `DismissAnnouncementCommand` | Validates `AnnouncementId` and `UserId` are non-empty Guids | FluentValidation | |
| 20 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/PublishAnnouncement/PublishAnnouncementCommand.cs` | Publishes a draft/scheduled announcement | Calls `Publish(timeProvider)`; resolves target users via `IAnnouncementTargetingService`; publishes `AnnouncementPublishedEvent` via Wolverine `IMessageBus` | `IAnnouncementRepository`, `IAnnouncementTargetingService`, `IMessageBus`, `TimeProvider`, `AnnouncementPublishedEvent` | |
| 21 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/PublishAnnouncement/PublishAnnouncementValidator.cs` | Validates `PublishAnnouncementCommand` | Validates `Id` is non-empty Guid | FluentValidation | |
| 22 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/UpdateAnnouncement/UpdateAnnouncementCommand.cs` | Updates all mutable fields of an existing announcement | Loads by ID; calls `announcement.Update(...)` with full field set; maps to `AnnouncementDto` | `IAnnouncementRepository`, `TimeProvider`, `AnnouncementDto` | |
| 23 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Commands/UpdateAnnouncement/UpdateAnnouncementValidator.cs` | Validates `UpdateAnnouncementCommand` | Validates `Id`, `Title`, `Content`, URL formats | FluentValidation | |

### Announcements Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 24 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Queries/GetActiveAnnouncements/GetActiveAnnouncementsQuery.cs` | Returns active, visible announcements for the requesting user | Delegates entirely to `IAnnouncementTargetingService.GetActiveAnnouncementsForUserAsync` with a `UserContext` built from query params | `IAnnouncementTargetingService`, `UserContext`, `UserId`, `TenantId` | |
| 25 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Queries/GetAllAnnouncements/GetAllAnnouncementsQuery.cs` | Returns all announcements regardless of status (admin use) | Calls `repository.GetAllAsync`; maps to DTOs ordered by `CreatedAt` desc | `IAnnouncementRepository`, `AnnouncementDto` | |

### Changelog Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 26 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/Commands/CreateChangelogEntry/CreateChangelogEntryCommand.cs` | Creates a new changelog entry in draft state | Calls `ChangelogEntry.Create`; maps items to `ChangelogEntryDto` | `IChangelogRepository`, `TimeProvider`, `ChangelogEntryDto` | |
| 27 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/Commands/CreateChangelogEntry/CreateChangelogEntryValidator.cs` | Validates `CreateChangelogEntryCommand` | Validates `Version`, `Title`, `Content` non-empty; `ReleasedAt` is valid date | FluentValidation | |
| 28 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/Commands/PublishChangelogEntry/PublishChangelogEntryCommand.cs` | Publishes a changelog entry by ID | Loads entry by ID, calls `entry.Publish(timeProvider)`, saves | `IChangelogRepository`, `TimeProvider` | |
| 29 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/Commands/PublishChangelogEntry/PublishChangelogEntryValidator.cs` | Validates `PublishChangelogEntryCommand` | Validates `Id` is non-empty Guid | FluentValidation | |

### Changelog Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 30 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/Queries/GetChangelog/GetChangelogQuery.cs` | Returns paginated list of published changelog entries | Calls `repository.GetPublishedAsync(limit)`; default limit 50; maps with items | `IChangelogRepository`, `ChangelogEntryDto` | |
| 31 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/Queries/GetChangelogEntry/GetChangelogEntryQuery.cs` | Returns a single changelog entry by version string or the latest published entry | Two handlers: `GetChangelogByVersionHandler` (version lookup) and `GetLatestChangelogHandler` (most recent by `ReleasedAt`); both return `NotFound` if unpublished | `IChangelogRepository`, `ChangelogEntryDto` | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 32 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Services/AnnouncementTargetingService.cs` | Filters published announcements to those relevant for a specific user | `GetActiveAnnouncementsForUserAsync`: filters by `Published` status, expiry, target match (All/Tenant/Plan/Role), and dismissal state; orders pinned first then by `CreatedAt`; `ResolveTargetUsersAsync` returns empty list (Notifications module handles broadcast delivery) | `IAnnouncementRepository`, `IAnnouncementDismissalRepository`, `TimeProvider`, `UserContext` | |

### DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 33 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/DTOs/AnnouncementDto.cs` | Immutable DTO for announcement data crossing application/API boundary | Record with all announcement fields including `Status` and `CreatedAt` | `AnnouncementStatus`, `AnnouncementTarget`, `AnnouncementType` | |
| 34 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/DTOs/ChangelogEntryDto.cs` | Immutable DTO for changelog entry including nested items | Record; includes `IReadOnlyList<ChangelogItemDto>`; nested `ChangelogItemDto` record also defined here | `ChangeType` | |

### Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 35 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Interfaces/IAnnouncementRepository.cs` | Repository contract for `Announcement` aggregate | Methods: `GetByIdAsync`, `GetPublishedAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync` | `Announcement`, `AnnouncementId` | |
| 36 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Announcements/Interfaces/IAnnouncementDismissalRepository.cs` | Repository contract for `AnnouncementDismissal` | Methods: `GetByUserIdAsync`, `ExistsAsync`, `AddAsync` | `AnnouncementDismissal`, `AnnouncementId`, `UserId` | |
| 37 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/Changelogs/Interfaces/IChangelogRepository.cs` | Repository contract for `ChangelogEntry` aggregate | Methods: `GetByIdAsync`, `GetByVersionAsync`, `GetLatestPublishedAsync`, `GetPublishedAsync`, `AddAsync`, `UpdateAsync` | `ChangelogEntry`, `ChangelogEntryId` | |

### Marker Interface

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 38 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Application/IAnnouncementsApplicationMarker.cs` | Assembly marker for handler/validator discovery | Empty interface; scanned by Wolverine and architecture tests | None | |

---

## Infrastructure Layer

### Persistence — DbContext

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 39 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/AnnouncementsDbContext.cs` | EF Core DbContext for the Announcements module | Extends `TenantAwareDbContext`; schema `announcements`; sets `NoTracking` globally; calls `ApplyTenantQueryFilters` and discovers configurations from assembly | `TenantAwareDbContext`, `ITenantContext`, `Announcement`, `AnnouncementDismissal`, `ChangelogEntry`, `ChangelogItem` | |

### Persistence — Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 40 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/Configurations/AnnouncementConfiguration.cs` | EF Core fluent configuration for `Announcement` | Maps all scalar properties with snake_case column names; indexes on `TenantId`, `Status`, `Target`, `PublishAt`, `ExpiresAt`; uses `StronglyTypedIdConverter` for ID columns | `Announcement`, `AnnouncementId`, `TenantId` | |
| 41 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/Configurations/AnnouncementDismissalConfiguration.cs` | EF Core fluent configuration for `AnnouncementDismissal` | Maps columns; composite unique index on `(announcement_id, user_id)` to prevent duplicate dismissals | `AnnouncementDismissal`, `AnnouncementDismissalId`, `AnnouncementId`, `UserId` | |
| 42 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/Configurations/ChangelogEntryConfiguration.cs` | EF Core fluent configuration for `ChangelogEntry` | Maps scalar fields; `HasMany(Items)` with cascade delete; `UsePropertyAccessMode(Field)` for private `_items` collection; unique index on `Version` | `ChangelogEntry`, `ChangelogEntryId`, `ChangelogItem` | |
| 43 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/Configurations/ChangelogItemConfiguration.cs` | EF Core fluent configuration for `ChangelogItem` | Maps columns; FK to `ChangelogEntry`; uses `StronglyTypedIdConverter` | `ChangelogItem`, `ChangelogItemId`, `ChangelogEntryId` | |

### Persistence — Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 44 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/Repositories/AnnouncementRepository.cs` | EF Core implementation of `IAnnouncementRepository` | `GetByIdAsync` uses `AsTracking()`; `GetPublishedAsync` filters by `Published` status, `PublishAt <= now`, `ExpiresAt > now`; ordered pinned-first then by `PublishAt` desc | `AnnouncementsDbContext`, `TimeProvider`, `AnnouncementStatus` | |
| 45 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/Repositories/AnnouncementDismissalRepository.cs` | EF Core implementation of `IAnnouncementDismissalRepository` | `GetByUserIdAsync` filters by user; `ExistsAsync` uses `AnyAsync` for efficiency; `AddAsync` persists and saves | `AnnouncementsDbContext` | |
| 46 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Persistence/Repositories/ChangelogRepository.cs` | EF Core implementation of `IChangelogRepository` | All read methods `Include(e => e.Items)`; `GetPublishedAsync` uses `AsSplitQuery()` to avoid cartesian explosion; ordered by `ReleasedAt` desc | `AnnouncementsDbContext` | |

### Persistence — Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 47 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Migrations/20260312201519_InitialCreate.cs` | Initial EF Core migration creating all Announcements tables | Creates `announcements`, `announcement_dismissals`, `changelog_entries`, `changelog_items` tables in `announcements` schema | EF Core migrations | |
| 48 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Migrations/20260312201519_InitialCreate.Designer.cs` | Auto-generated designer file for initial migration | EF Core snapshot metadata; do not edit manually | EF Core migrations | |
| 49 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Migrations/AnnouncementsDbContextModelSnapshot.cs` | EF Core model snapshot for migration diffing | Auto-generated; represents current model state | EF Core migrations | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 50 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Extensions/AnnouncementsModuleExtensions.cs` | DI registration and startup initialization for the Announcements module | `AddAnnouncementsModule`: registers `AnnouncementsDbContext` with Npgsql (retry-on-failure, 30s timeout), all three repositories, and `AnnouncementTargetingService`; `InitializeAnnouncementsModuleAsync`: runs EF migrations in Development/Testing; structured log on failure | `IServiceCollection`, `WebApplication`, `AnnouncementsDbContext`, all repositories, `IAnnouncementTargetingService` | |

---

## Api Layer

### Contracts

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 51 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Api/Contracts/Responses/AnnouncementResponse.cs` | API response contracts for announcements and changelog | Defines `AnnouncementResponse`, `ChangelogEntryResponse`, and `ChangelogItemResponse` records; type enums serialized as strings | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 52 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Api/Controllers/AdminAnnouncementsController.cs` | Admin CRUD + lifecycle endpoints for announcements | Route `api/v1/admin/announcements`; requires `PermissionType.AnnouncementManage`; GET all, POST create, PUT update, POST publish, DELETE archive; sanitizes `Title`/`Content` via `IHtmlSanitizationService`; also defines `CreateAnnouncementRequest` and `UpdateAnnouncementRequest` records | `IMessageBus`, `IHtmlSanitizationService`, `HasPermission`, Wolverine | |
| 53 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Api/Controllers/AdminChangelogController.cs` | Admin create + publish endpoints for changelog | Route `api/v1/admin/changelog`; requires `PermissionType.ChangelogManage`; POST create entry, POST publish entry; sanitizes `Title`/`Content`; defines `CreateChangelogEntryRequest` record | `IMessageBus`, `IHtmlSanitizationService`, `HasPermission`, Wolverine | |
| 54 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Api/Controllers/AnnouncementsController.cs` | User-facing announcement endpoints | Route `api/v1/announcements`; requires `Authorize` + `PermissionType.AnnouncementRead`; GET active announcements (tenant/plan/role context extracted from claims); POST dismiss; extracts `plan` claim and `ClaimTypes.Role` from JWT | `IMessageBus`, `ITenantContext`, `ICurrentUserService`, Wolverine | |
| 55 | [ ] | `src/Modules/Announcements/Wallow.Announcements.Api/Controllers/ChangelogController.cs` | Public changelog read endpoints | Route `api/v1/changelog`; `AllowAnonymous`; GET paginated changelog (limit param), GET by version string, GET latest | `IMessageBus`, Wolverine | |

---

## Test Files

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 56 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/AnnouncementsTestsMarker.cs` | Assembly marker for test project | Enables assembly-level test discovery and shared configuration | |
| 57 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Domain/Entities/AnnouncementCreateTests.cs` | Domain unit tests for `Announcement.Create` | Validates draft/scheduled status based on `PublishAt`; all optional parameters; `CreatedAt` timestamp; unique ID generation; tenant ID assignment; argument validation for null/empty title and content | |
| 58 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Domain/Entities/AnnouncementStateTransitionTests.cs` | Domain unit tests for `Announcement` lifecycle transitions | `Publish`, `Expire`, `Archive` transitions; idempotent publish (no-op if already published); `Update` mutation; invariant enforcement | |
| 59 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/ArchiveAnnouncement/ArchiveAnnouncementHandlerTests.cs` | Unit tests for `ArchiveAnnouncementHandler` | Success path archives announcement; `NotFound` returned when announcement missing | |
| 60 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/ArchiveAnnouncement/ArchiveAnnouncementValidatorTests.cs` | Unit tests for `ArchiveAnnouncementValidator` | Validates `Id` must be non-empty Guid | |
| 61 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/CreateAnnouncement/CreateAnnouncementHandlerTests.cs` | Unit tests for `CreateAnnouncementHandler` | Creates announcement; persists via repository; returns correct DTO | |
| 62 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/CreateAnnouncement/CreateAnnouncementValidatorTests.cs` | Unit tests for `CreateAnnouncementValidator` | Title/content required; URL format validation | |
| 63 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/DismissAnnouncement/DismissAnnouncementHandlerTests.cs` | Unit tests for `DismissAnnouncementHandler` | Dismissal created for dismissible announcement; idempotent (no error on second dismiss); `NotFound` for missing announcement; validation error for non-dismissible announcement | |
| 64 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/DismissAnnouncement/DismissAnnouncementValidatorTests.cs` | Unit tests for `DismissAnnouncementValidator` | `AnnouncementId` and `UserId` must be non-empty Guids | |
| 65 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/PublishAnnouncement/PublishAnnouncementHandlerTests.cs` | Unit tests for `PublishAnnouncementHandler` | Publishes announcement; fires `AnnouncementPublishedEvent`; `NotFound` when missing | |
| 66 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/PublishAnnouncement/PublishAnnouncementValidatorTests.cs` | Unit tests for `PublishAnnouncementValidator` | `Id` must be non-empty Guid | |
| 67 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/UpdateAnnouncement/UpdateAnnouncementHandlerTests.cs` | Unit tests for `UpdateAnnouncementHandler` | Updates all fields; `NotFound` when missing | |
| 68 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/UpdateAnnouncement/UpdateAnnouncementValidatorTests.cs` | Unit tests for `UpdateAnnouncementValidator` | `Id`, `Title`, `Content` required; URL format checks | |
| 69 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/Changelogs/CreateChangelogEntry/CreateChangelogEntryHandlerTests.cs` | Unit tests for `CreateChangelogEntryHandler` | Creates entry in unpublished state; persists; returns DTO | |
| 70 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/Changelogs/CreateChangelogEntry/CreateChangelogEntryValidatorTests.cs` | Unit tests for `CreateChangelogEntryValidator` | Version, title, content required; date validation | |
| 71 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/Changelogs/PublishChangelogEntry/PublishChangelogEntryHandlerTests.cs` | Unit tests for `PublishChangelogEntryHandler` | Sets `IsPublished=true`; `NotFound` when entry missing | |
| 72 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Commands/Changelogs/PublishChangelogEntry/PublishChangelogEntryValidatorTests.cs` | Unit tests for `PublishChangelogEntryValidator` | `Id` must be non-empty Guid | |
| 73 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Queries/GetActiveAnnouncements/GetActiveAnnouncementsHandlerTests.cs` | Unit tests for `GetActiveAnnouncementsHandler` | Delegates to targeting service; returns filtered DTO list | |
| 74 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Queries/GetAllAnnouncements/GetAllAnnouncementsHandlerTests.cs` | Unit tests for `GetAllAnnouncementsHandler` | Returns all announcements; maps to DTOs | |
| 75 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Queries/GetChangelog/GetChangelogHandlerTests.cs` | Unit tests for `GetChangelogHandler` | Returns published entries ordered by release date; respects limit | |
| 76 | [ ] | `tests/Modules/Announcements/Wallow.Announcements.Tests/Application/Queries/GetChangelogEntry/GetChangelogByVersionHandlerTests.cs` | Unit tests for `GetChangelogByVersionHandler` and `GetLatestChangelogHandler` | Version lookup returns correct entry; unpublished entry returns `NotFound`; latest handler returns most recent | |
