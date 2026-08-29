# Announcements module

- `AnnouncementPublishedEvent.TargetUserIds` is ALWAYS empty — `ResolveTargetUsersAsync` is an
  unimplemented TODO returning `[]`; Notifications does broadcast delivery instead.
- The Changelogs sub-domain is global: `ChangelogEntry`/`ChangelogItem` carry no `ITenantScoped`
  and no tenant filter (announcements themselves are tenant-scoped).
- Controllers sanitize `Title`/`Content` via `IHtmlSanitizationService` BEFORE dispatching
  commands — new endpoints must copy that ordering.
