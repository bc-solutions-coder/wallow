# Branding Module

## Overview

The Branding module manages per-client visual customization for OAuth/OIDC applications. Each client application (registered via OpenIddict) can have its own display name, tagline, logo, and theme colors. Branding data is tenant-scoped and cached in a dedicated bounded memory cache.

## Key Features

- **Client branding CRUD**: Upsert and delete branding for OAuth client applications
- **Logo management**: Upload/replace/delete logos via S3-compatible storage (GarageHQ), with magic-byte validation for PNG, JPEG, and WebP
- **Theme validation**: JSON theme with color values validated against oklch, hex, and rem patterns
- **Caching**: Keyed `IMemoryCache` ("BrandingCache") with 5-minute sliding expiration and 1000-entry size limit
- **Ownership enforcement**: Only the user who created the OAuth client can modify its branding
- **Multi-tenancy**: Automatic tenant isolation via EF Core query filters

## Architecture

```
src/Modules/Branding/
+-- Wallow.Branding.Domain         # ClientBranding entity, ClientBrandingId
+-- Wallow.Branding.Application    # DTOs, repository/service interfaces
+-- Wallow.Branding.Infrastructure # EF Core, repository, caching service, DI registration
+-- Wallow.Branding.Api            # Controller, request contracts
```

**Database Schema**: `branding` (PostgreSQL), table `client_brandings`

## Domain

### ClientBranding (Entity)

The sole entity in this module. Stores branding configuration for an OAuth client application.

| Property | Type | Description |
|----------|------|-------------|
| `ClientId` | `string` | OpenIddict client identifier (unique index) |
| `DisplayName` | `string` | Required display name |
| `Tagline` | `string?` | Optional tagline |
| `LogoStorageKey` | `string?` | S3 object key for the logo |
| `ThemeJson` | `string?` | JSON theme (stored as `jsonb`) |
| `TenantId` | `TenantId` | Tenant scope |

### ClientBrandingId (Strongly-Typed ID)

`readonly record struct` implementing `IStronglyTypedId<ClientBrandingId>`.

## API Endpoints

Route: `api/v1/identity/apps/{clientId}/branding`

| Method | Auth | Description |
|--------|------|-------------|
| `GET` | Anonymous | Get branding for a client (cached, 300s response cache) |
| `POST` | Required | Upsert branding (multipart/form-data with optional logo) |
| `DELETE` | Required | Delete branding and associated logo |

Logo uploads are validated for file type (magic bytes), size (max 2MB), and content type match.

## Integration Events

Defined in `Wallow.Shared.Contracts.Branding.Events`:

| Event | Description |
|-------|-------------|
| `ClientBrandingUpdatedEvent` | Published when branding is updated. Consumed by Identity and Web modules. |

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, strongly-typed IDs, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | `IStorageProvider` interface, integration events |
| `Wallow.Shared.Infrastructure.Core` | `TenantAwareDbContext`, `AddReadDbContext`, `AddTenantAwareScopedContext` |
| OpenIddict | Client ownership verification via `IOpenIddictApplicationManager` |

## Configuration

Uses the shared `DefaultConnection` connection string. Auto-migrates in Development and Testing environments. No additional configuration required.

## Testing

```bash
./scripts/run-tests.sh branding
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/Branding/Wallow.Branding.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BrandingDbContext
```
