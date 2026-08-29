# Branding module

No CQRS (deliberate — see `api/CLAUDE.md`); publishes nothing.

- `[FromKeyedServices("BrandingCache")] IMemoryCache` is a bounded cache: always set `Size = 1`
  on entries and call `brandingService.InvalidateCache(clientId)` after every mutation — a new
  endpoint silently violates this otherwise.
- Client ownership is checked by reading the `creatorUserId` property off the OpenIddict
  application descriptor (`IOpenIddictApplicationManager`).
