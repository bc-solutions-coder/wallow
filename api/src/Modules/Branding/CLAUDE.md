# Branding Module — Agent Guide

## Module Purpose

Per-client OAuth application branding (display name, tagline, logo, theme colors). One entity
(`ClientBranding`), no CQRS — service/repository pattern directly from the controller.

## Patterns and Conventions

- **No CQRS/Wolverine handlers**: `ClientBrandingController` calls `IClientBrandingRepository`
  and `IClientBrandingService` directly.
- **Keyed memory cache**: `[FromKeyedServices("BrandingCache")] IMemoryCache` — a dedicated
  bounded cache (size limit 1000) separate from the global `IMemoryCache`. Always set `Size = 1`
  on cache entries, and call `brandingService.InvalidateCache(clientId)` after any mutation.
- **Ownership via OpenIddict**: client ownership is checked by reading the `creatorUserId`
  property from the OpenIddict application descriptor (`IOpenIddictApplicationManager`).
- **Logo storage**: via `IStorageProvider` (`Wallow.Shared.Contracts.Storage`) at key
  `client-logos/{clientId}/{guid}.{ext}`; old logos are deleted on replacement.
- **Logo validation**: magic-byte validation for PNG/JPEG/WebP, 2MB max — in the controller.
- **Theme validation**: ThemeJson validated as JSON with color properties matching `oklch(...)`,
  `#hex`, or `rem` patterns via source-generated regex (hence the `partial` controller).

## Cross-Module Communication

- **Publishes**: nothing — no `Branding` namespace exists in `Wallow.Shared.Contracts`.
- **Depends on**: `IStorageProvider` from Storage (via `Shared.Contracts`).

## Things to Watch

- GET endpoint is `[AllowAnonymous]` with a 300s response cache — branding is public data.
- POST uses `[Consumes("multipart/form-data")]` for logo upload.

## Database

Schema: `branding`, table `client_brandings` (snake_case columns); context: `BrandingDbContext`.

## Testing

`./scripts/run-tests.sh branding`
