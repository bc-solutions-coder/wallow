# Notifications module

- The Application layer is organized **by channel** (`Email`, `InApp`, `Push`, `Sms`), each with
  its own `Commands/`/`Queries/`/`DTOs/`; integration-event handlers go in the flat
  `EventHandlers/` directory at the Application root, not inside channels.
- Handler naming: `{Event}NotificationHandler` (email), `{Event}InAppHandler` (in-app),
  `{Event}SseHandler` (SSE).
- Check `INotificationPreferenceChecker` before sending.
- `RetryFailedEmailsJob` is registered scoped but invoked externally — not auto-scheduled here.
- Publishes nothing: `NotificationCreatedEvent` in `Shared.Contracts` has ZERO publishers and
  zero consumers — a glob over the contracts suggests otherwise.
