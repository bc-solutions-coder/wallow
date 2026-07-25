# Announcements Module — Agent Guide

## Module Purpose

Manages tenant-scoped in-app announcements (with audience targeting and user dismissals) and a global public changelog. Two distinct sub-domains: Announcements and Changelogs.

## Key File Locations

| Area | Path |
|------|------|
| Domain entities | `Wallow.Announcements.Domain/Announcements/Entities/` and `Changelogs/Entities/` |
| Enums | `Wallow.Announcements.Domain/Announcements/Enums/` and `Changelogs/Enums/` |
| Strongly-typed IDs | `Wallow.Announcements.Domain/Announcements/Identity/` and `Changelogs/Identity/` |
| Commands & handlers | `Wallow.Announcements.Application/Announcements/Commands/` and `Changelogs/Commands/` |
| Queries & handlers | `Wallow.Announcements.Application/Announcements/Queries/` and `Changelogs/Queries/` |
| Repository interfaces | `Wallow.Announcements.Application/Announcements/Interfaces/` and `Changelogs/Interfaces/` |
| Targeting service | `Wallow.Announcements.Application/Announcements/Services/AnnouncementTargetingService.cs` |
| Repository implementations | `Wallow.Announcements.Infrastructure/Persistence/Repositories/` |
| EF configurations | `Wallow.Announcements.Infrastructure/Persistence/Configurations/` |
| Module registration | `Wallow.Announcements.Infrastructure/Extensions/AnnouncementsModuleExtensions.cs` |
| Controllers | `Wallow.Announcements.Api/Controllers/` |
| Response contracts | `Wallow.Announcements.Api/Contracts/Responses/AnnouncementResponse.cs` |
| Tests | `tests/Modules/Announcements/Wallow.Announcements.Tests/` |

## Cross-Module Communication

- **Publishes** `AnnouncementPublishedEvent` (defined in `Wallow.Shared.Contracts/Announcements/Events/`) via Wolverine in-memory messaging when an announcement is published
- The event includes target criteria (`Target`, `TargetValue`, `TargetUserIds`) so consuming modules (e.g., Notifications) can determine delivery
- **Does not consume** any integration events from other modules

## Important Patterns

- **Two sub-domains**: Announcements are tenant-scoped (`ITenantScoped`); Changelogs are global (no tenant scoping)
- **Targeting service**: `AnnouncementTargetingService` filters published announcements by target type (All, Tenant, Plan, Role) and excludes dismissed ones. The `ResolveTargetUsersAsync` method currently returns an empty list — actual user resolution is deferred to consuming modules
- **State transitions via aggregate methods**: Use `Publish()`, `Archive()`, `Expire()` — never set `Status` directly
- **HTML sanitization**: Controllers sanitize `Title` and `Content` via `IHtmlSanitizationService` before passing to commands
- **Wolverine handler discovery**: Handlers are plain classes with `Handle` methods — no interface implementation needed. Wolverine discovers them automatically
- **Request records in controller files**: `CreateAnnouncementRequest` and `UpdateAnnouncementRequest` are defined at the bottom of `AdminAnnouncementsController.cs`; `CreateChangelogEntryRequest` is in `AdminChangelogController.cs`

## Permissions

| Permission | Used By |
|------------|---------|
| `AnnouncementManage` | Admin announcement CRUD (all admin endpoints) |
| `AnnouncementRead` | User-facing read and dismiss |
| `ChangelogManage` | Admin changelog creation and publishing |

Public changelog endpoints (`/api/v1/changelog`) are `[AllowAnonymous]`.

## Database

- Schema: `announcements`
- Context: `AnnouncementsDbContext` (extends `TenantAwareDbContext`)
- Auto-migrates in Development/Testing environments only
- Tenant query filters apply to `Announcement` (via `ITenantScoped`); `ChangelogEntry` and `ChangelogItem` are not tenant-scoped

## Testing

```bash
./scripts/run-tests.sh announcements
```

Tests cover domain entities, all command/query handlers, validators, targeting service, repository persistence (using Testcontainers PostgreSQL), and controller behavior.
