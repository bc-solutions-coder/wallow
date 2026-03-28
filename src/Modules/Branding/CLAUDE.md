# Branding Module - Agent Guide

## Module Purpose

Per-client OAuth application branding (display name, tagline, logo, theme colors). This is a simple module with one entity and no CQRS -- it uses a service/repository pattern directly from the controller.

## Key File Locations

- **Entity**: `Wallow.Branding.Domain/Entities/ClientBranding.cs`
- **Strongly-typed ID**: `Wallow.Branding.Domain/Identity/ClientBrandingId.cs`
- **Interfaces**: `Wallow.Branding.Application/Interfaces/` (repository + service)
- **DTO**: `Wallow.Branding.Application/DTOs/ClientBrandingDto.cs`
- **Controller**: `Wallow.Branding.Api/Controllers/ClientBrandingController.cs`
- **Request contract**: `Wallow.Branding.Api/Contracts/Requests/UpsertClientBrandingRequest.cs`
- **Repository**: `Wallow.Branding.Infrastructure/Repositories/ClientBrandingRepository.cs`
- **Caching service**: `Wallow.Branding.Infrastructure/Services/ClientBrandingService.cs`
- **DI registration**: `Wallow.Branding.Infrastructure/Extensions/BrandingInfrastructureExtensions.cs`
- **Module startup**: `Wallow.Branding.Infrastructure/Extensions/BrandingModuleExtensions.cs`
- **EF config**: `Wallow.Branding.Infrastructure/Persistence/Configurations/ClientBrandingConfiguration.cs`
- **DbContext**: `Wallow.Branding.Infrastructure/Persistence/BrandingDbContext.cs`
- **Integration event**: `src/Shared/Wallow.Shared.Contracts/Branding/Events/ClientBrandingUpdatedEvent.cs`
- **Tests**: `tests/Modules/Branding/Wallow.Branding.Tests/`

## Patterns and Conventions

- **No CQRS/Wolverine handlers**: Unlike most modules, Branding uses a direct service/repository pattern. The controller calls `IClientBrandingRepository` and `IClientBrandingService` directly.
- **Keyed memory cache**: Uses `[FromKeyedServices("BrandingCache")] IMemoryCache` -- a dedicated bounded cache (size limit 1000) separate from the global `IMemoryCache`. Always set `Size = 1` on cache entries.
- **Ownership via OpenIddict**: Client ownership is checked by reading the `creatorUserId` property from the OpenIddict application descriptor. The controller injects `IOpenIddictApplicationManager`.
- **Logo storage**: Logos stored via `IStorageProvider` (from `Wallow.Shared.Contracts.Storage`) at key `client-logos/{clientId}/{guid}.{ext}`. Old logos are deleted on replacement.
- **Logo validation**: Magic-byte validation for PNG/JPEG/WebP, 2MB max size. Validation logic is in the controller.
- **Theme validation**: ThemeJson validated as JSON with color properties matching `oklch(...)`, `#hex`, or `rem` patterns via source-generated regex.
- **Database schema**: `branding`, table `client_brandings`. Uses snake_case column names.
- **Tenant isolation**: `BrandingDbContext` extends `TenantAwareDbContext` with automatic query filters. `TenantSaveChangesInterceptor` auto-sets tenant on save.

## Cross-Module Communication

- **Publishes**: `ClientBrandingUpdatedEvent` (via Wolverine in-memory messaging) -- consumed by Identity and Web modules
- **Depends on**: `IStorageProvider` from the Storage module (via `Wallow.Shared.Contracts.Storage`)

## Things to Watch

- The controller is `partial` (source-generated regex for color validation)
- GET endpoint is `[AllowAnonymous]` with 300s response cache -- branding is public data
- POST endpoint uses `[Consumes("multipart/form-data")]` for logo upload
- Cache invalidation must be called after any mutation (`brandingService.InvalidateCache(clientId)`)
- `QueryTrackingBehavior.NoTracking` is set by default on the DbContext -- mutations must attach/track entities explicitly (the repository's `Add`/`Remove` handle this)

## Running Tests

```bash
./scripts/run-tests.sh branding
```
