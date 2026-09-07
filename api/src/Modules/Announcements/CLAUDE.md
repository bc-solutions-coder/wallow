# Announcements module

- `AnnouncementPublishedEvent.TargetUserIds` is ALWAYS empty — `ResolveTargetUsersAsync` is an
  unimplemented TODO returning `[]`; Notifications does broadcast delivery instead.
- Controllers sanitize `Title`/`Content` via `IHtmlSanitizationService` BEFORE dispatching
  commands — new endpoints must copy that ordering.
