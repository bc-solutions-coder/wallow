# Announcements Module

## Overview

The Announcements module manages in-app announcements. Announcements are tenant-scoped, support audience targeting (by tenant, plan, or role), and can be dismissed by individual users.

The module follows Clean Architecture with CQRS patterns, using Wolverine for command/query handling and integration event publishing.

## Key Features

- **Announcement Lifecycle**: Draft, scheduled, published, expired, and archived states
- **Audience Targeting**: Target announcements to all users, a specific tenant, plan, or role
- **User Dismissals**: Users can dismiss individual announcements; dismissed announcements are filtered from active queries
- **Pinning and Expiration**: Announcements can be pinned (sorted first) and auto-expire after a date
- **Call-to-Action**: Optional action URL/label and image for rich announcements
- **HTML Sanitization**: Title and content are sanitized on input via `IHtmlSanitizationService`
- **Multi-tenancy**: Announcements are tenant-scoped via EF Core query filters

## Architecture

```
src/Modules/Announcements/
+-- Wallow.Announcements.Domain         # Entities, Enums, Strongly-typed IDs
+-- Wallow.Announcements.Application    # Commands, Queries, Handlers, DTOs, Services
+-- Wallow.Announcements.Infrastructure # EF Core, Repositories, Module Registration
+-- Wallow.Announcements.Api            # Controllers, Request/Response Contracts
```

**Database Schema**: `announcements` (PostgreSQL)

## Domain Model

### Announcement (Aggregate Root)

Tenant-scoped announcement with lifecycle management and audience targeting.

**State Machine**:
```
Draft --> Published --> Expired
         Published --> Archived
Scheduled --> Published
```

### AnnouncementDismissal (Entity)

Tracks which users have dismissed which announcements. Keyed by announcement ID and user ID.

## Enums

| Enum | Values |
|------|--------|
| `AnnouncementStatus` | Draft, Scheduled, Published, Expired, Archived |
| `AnnouncementType` | Feature, Update, Maintenance, Alert, Tip |
| `AnnouncementTarget` | All, Tenant, Plan, Role |

## Commands

| Command | Description |
|---------|-------------|
| `CreateAnnouncementCommand` | Create a new announcement (Draft or Scheduled) |
| `UpdateAnnouncementCommand` | Update an existing announcement |
| `PublishAnnouncementCommand` | Publish an announcement and emit integration event |
| `ArchiveAnnouncementCommand` | Archive an announcement |
| `DismissAnnouncementCommand` | Dismiss an announcement for a specific user |

## Queries

| Query | Returns |
|-------|---------|
| `GetAllAnnouncementsQuery` | All announcements (admin) |
| `GetActiveAnnouncementsQuery` | Published, non-expired, targeted, non-dismissed announcements for a user |

## Integration Events

Published via Wolverine in-memory messaging. Defined in `Wallow.Shared.Contracts`.

| Event | When |
|-------|------|
| `AnnouncementPublishedEvent` | Announcement is published (carries the target criteria) |

> [!NOTE]
> The event record declares a `TargetUserIds` property, but targeting resolution is unimplemented:
> `AnnouncementTargetingService.ResolveTargetUsersAsync` is a `TODO` that always returns an empty
> list, so the collection is always empty on the wire. Notifications handles broadcast delivery
> instead.

## API Endpoints

### Admin Announcements (`/v1/admin/announcements`) — requires `AnnouncementManage` permission

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/admin/announcements` | List all announcements |
| `POST` | `/v1/admin/announcements` | Create announcement |
| `PUT` | `/v1/admin/announcements/{id}` | Update announcement |
| `POST` | `/v1/admin/announcements/{id}/publish` | Publish announcement |
| `DELETE` | `/v1/admin/announcements/{id}` | Archive announcement |

### User Announcements (`/v1/announcements`) — requires `AnnouncementRead` permission

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/announcements` | Get active announcements for current user |
| `POST` | `/v1/announcements/{id}/dismiss` | Dismiss an announcement |

## Configuration

Uses the shared `DefaultConnection` connection string. No additional configuration required. Its schema is migrated inline only in the `Testing` environment; everywhere else `Wallow.MigrationService` applies migrations.

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, strongly-typed IDs, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | Integration event definitions (`AnnouncementPublishedEvent`) |

## Testing

```bash
./scripts/run-tests.sh announcements
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project api/src/Modules/Announcements/Wallow.Announcements.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context AnnouncementsDbContext
```

## Related Documentation

- Agent guide for this module: [`CLAUDE.md`](CLAUDE.md)
- Backend conventions and commands: [`api/CLAUDE.md`](../../../CLAUDE.md)
- Integration event catalogue: [`Wallow.Shared.Contracts/README.md`](../../Shared/Wallow.Shared.Contracts/README.md)
