# Branding module

No CQRS (deliberate — see `api/CLAUDE.md`); consumes and publishes integration events through
Wolverine: `ClientRegisteredEvent` creates the branding row, `ClientDeletedEvent` removes it,
and the org branding endpoints publish `ClientBrandingUpdatedEvent` on every write (Identity
audits it and syncs the OpenIddict application's display name — as a trigger: the sync pulls the
CURRENT name via `IClientBrandingProvider`, never the event's payload, so reordering/redelivery
are harmless). Controller/service writes that publish must go through
`IClientBrandingRepository.SaveChangesAndPublishAsync` — it rides Wolverine's durable outbox so
the event commits or rolls back with the rows; a separate save-then-publish reopens the crash
window that drops the event. (Wolverine handlers don't use it — the transaction middleware
already supplies their outbox and save.)

- `[FromKeyedServices("BrandingCache")] IMemoryCache` is a bounded cache: always set `Size = 1`
  on entries and call `brandingService.InvalidateCache(clientId)` after every mutation — a new
  endpoint silently violates this otherwise.
- Client ownership is checked through `IOrganizationClientDirectory` (`Shared.Contracts`) —
  never OpenIddict, never Identity's persistence. The org sub-resource admits the callers the
  parent client surface admits: the org's own tenant, the global admin, or a membership that
  grants client management (`CanManageClientsAsync` on the same contract).
- `ClientRegisteredHandler` must NOT inject `IOrganizationClientDirectory` — it is backed by
  Identity's DbContext and the one-DbContext-per-chain rule makes that a runtime codegen
  failure. The event carries `Kind` so the handler never has to ask.
- Rows the module creates for another organization need an explicit
  `IClientBrandingRepository.UseTenant(TenantId.Create(orgId))` BEFORE `Add` — the ambient
  tenant is the caller's (or the event publisher's), not necessarily the owner's, and the
  unit of work snapshots it at creation, so mutating `ITenantContext` after the repository
  exists does nothing.
- The display name may never case-insensitively equal the fork's app name
  (`ForkBrandingOptions`, section `Branding`, default `Wallow`).
