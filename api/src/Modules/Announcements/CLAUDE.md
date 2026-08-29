# Announcements Module — Agent Guide

## Module Purpose

Tenant-scoped in-app announcements (audience targeting and user dismissals) plus a global public
changelog — two distinct sub-domains, Announcements and Changelogs, each with its own
`Entities/`, `Commands/`, `Queries/` and `Interfaces/` trees under the Domain/Application projects.

## Cross-Module Communication

- **Publishes** `AnnouncementPublishedEvent` (`Wallow.Shared.Contracts/Announcements/Events/`)
  when an announcement is published. The event carries target criteria (`Target`, `TargetValue`)
  so consumers (e.g. Notifications) determine delivery. It also declares `TargetUserIds`, but that
  collection is **always empty** — `ResolveTargetUsersAsync` is an unimplemented `TODO`.
- **Consumes** no integration events.

## Important Patterns

- **Two sub-domains**: Announcements are tenant-scoped (`ITenantScoped`); Changelogs are global —
  `ChangelogEntry`/`ChangelogItem` carry no tenant filter.
- **Targeting**: `AnnouncementTargetingService` (Application) filters published announcements by
  target type (All, Tenant, Plan, Role) and excludes dismissed ones. `ResolveTargetUsersAsync`
  returns an empty list — user resolution is deferred to consuming modules.
- **Aggregate state methods**: `Publish()`, `Archive()`, `Expire()`.
- **HTML sanitization**: controllers sanitize `Title` and `Content` via
  `IHtmlSanitizationService` before passing to commands.
- **Request records live in controller files**: `CreateAnnouncementRequest` and
  `UpdateAnnouncementRequest` at the bottom of `AdminAnnouncementsController.cs`;
  `CreateChangelogEntryRequest` in `AdminChangelogController.cs`.

## Permissions

| Permission | Used By |
|------------|---------|
| `AnnouncementManage` | Admin announcement CRUD |
| `AnnouncementRead` | User-facing read and dismiss |
| `ChangelogManage` | Admin changelog creation and publishing |

Public changelog endpoints (`/v1/changelog`) are `[AllowAnonymous]`.

## Database

Schema: `announcements`; context: `AnnouncementsDbContext`.

## Testing

`./scripts/run-tests.sh announcements`
