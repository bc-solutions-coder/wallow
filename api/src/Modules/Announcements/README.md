# Announcements Module

## Overview

The Announcements module manages in-app announcements and a public changelog. Announcements are tenant-scoped, support audience targeting (by tenant, plan, or role), and can be dismissed by individual users. The changelog tracks versioned release notes and is publicly accessible without authentication.

The module follows Clean Architecture with CQRS patterns, using Wolverine for command/query handling and integration event publishing.

## Key Features

- **Announcement Lifecycle**: Draft, scheduled, published, expired, and archived states
- **Audience Targeting**: Target announcements to all users, a specific tenant, plan, or role
- **User Dismissals**: Users can dismiss individual announcements; dismissed announcements are filtered from active queries
- **Pinning and Expiration**: Announcements can be pinned (sorted first) and auto-expire after a date
- **Call-to-Action**: Optional action URL/label and image for rich announcements
- **Changelog Management**: Versioned release entries with categorized change items
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

### ChangelogEntry (Aggregate Root)

A versioned release note. Not tenant-scoped (global). Contains a collection of `ChangelogItem` entities.

### ChangelogItem (Entity)

An individual change within a changelog entry, categorized by `ChangeType`.

## Enums

| Enum | Values |
|------|--------|
| `AnnouncementStatus` | Draft, Scheduled, Published, Expired, Archived |
| `AnnouncementType` | Feature, Update, Maintenance, Alert, Tip |
| `AnnouncementTarget` | All, Tenant, Plan, Role |
| `ChangeType` | Feature, Improvement, Fix, Breaking, Security, Deprecated |

## Commands

| Command | Description |
|---------|-------------|
| `CreateAnnouncementCommand` | Create a new announcement (Draft or Scheduled) |
| `UpdateAnnouncementCommand` | Update an existing announcement |
| `PublishAnnouncementCommand` | Publish an announcement and emit integration event |
| `ArchiveAnnouncementCommand` | Archive an announcement |
| `DismissAnnouncementCommand` | Dismiss an announcement for a specific user |
| `CreateChangelogEntryCommand` | Create a new changelog entry |
| `PublishChangelogEntryCommand` | Publish a changelog entry |

## Queries

| Query | Returns |
|-------|---------|
| `GetAllAnnouncementsQuery` | All announcements (admin) |
| `GetActiveAnnouncementsQuery` | Published, non-expired, targeted, non-dismissed announcements for a user |
| `GetChangelogQuery` | Published changelog entries (paginated by limit) |
| `GetChangelogByVersionQuery` | Single changelog entry by version string |
| `GetLatestChangelogQuery` | Most recent published changelog entry |

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

### Admin Changelog (`/v1/admin/changelog`) — requires `ChangelogManage` permission

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/admin/changelog` | Create changelog entry |
| `POST` | `/v1/admin/changelog/{id}/publish` | Publish changelog entry |

### Public Changelog (`/v1/changelog`) — anonymous access

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/changelog` | List published changelog entries |
| `GET` | `/v1/changelog/{version}` | Get changelog entry by version |
| `GET` | `/v1/changelog/latest` | Get latest changelog entry |

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
