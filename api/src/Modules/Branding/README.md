# Branding Module

## Overview

The Branding module manages per-client visual customization for OAuth/OIDC applications. Each client application (registered via OpenIddict) can have its own display name, tagline, logo, and theme colors. Branding data is tenant-scoped and cached in a dedicated bounded memory cache.

## Key Features

- **Client branding as an org sub-resource**: registration creates each application's branding row; the owning organization reads and replaces it and removes the logo under `…/organizations/{orgId}/clients/{clientId}/branding`
- **Logo management**: Upload/replace/delete logos through `IStorageProvider`, whose default provider is `Local` (see `Storage/README.md`); S3-compatible backends such as GarageHQ are opt-in. Magic-byte validation for PNG, JPEG, and WebP
- **Curated theme validation**: JSON theme restricted to `primary` and `primaryForeground` per `light`/`dark` mode, colors validated as oklch or hex
- **Caching**: Keyed `IMemoryCache` ("BrandingCache") with 5-minute sliding expiration and 1000-entry size limit
- **Ownership enforcement**: the caller needs `OrganizationClientsManage`, a tenant matching the route organization (or the global-admin claim), and the client must be that organization's own application — resolved through `IOrganizationClientDirectory`, never OpenIddict
- **Reserved display name**: the end-user-facing display name may never case-insensitively equal the fork's app name (`ForkBrandingOptions`)
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

There is no anonymous read by client id. The sign-in screens get branding through Identity's
transaction-scoped `GET /v1/identity/auth/authorize-context`, which reads this module's row via
the `IClientBrandingProvider` contract in `Shared.Contracts` (its `FindAsync` shares the bounded
memory cache; the contract's display-name sync read deliberately bypasses it; no HTTP response
cache, so an edit shows on the next sign-in).

Management (org sub-resource): `/v1/identity/organizations/{orgId}/clients/{clientId}/branding`

| Method | Auth | Description |
|--------|------|-------------|
| `GET` | `OrganizationClientsManage` | The client's branding as its organization sees it |
| `PUT` | `OrganizationClientsManage` | Replace branding (multipart/form-data with optional logo) |
| `DELETE /logo` | `OrganizationClientsManage` | Remove the logo; the rest of the branding stays |

Logo uploads are validated for file type (magic bytes), size (max 2MB), and content type match.
A foreign organization, an unknown client and a service account are all answered 404.

## Integration Events

Consumed (Wolverine, in-memory):

| Event | Reaction |
|-------|----------|
| `ClientRegisteredEvent` | Creates the branding row for an application (display name defaults to the client name; a chosen initial branding wins) |
| `ClientDeletedEvent` | Removes the branding row and its logo |

Published:

| Event | Consumers |
|-------|-----------|
| `ClientBrandingUpdatedEvent` | Identity — writes the `ClientBrandingUpdated` audit row and syncs the OpenIddict application's display name by re-reading the current branding (the event is a trigger, not a payload) |

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, strongly-typed IDs, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | `IStorageProvider` interface |
| `Wallow.Shared.Infrastructure.Core` | Cross-cutting infrastructure — tenant-aware persistence, caching, messaging ([README](../../Shared/Wallow.Shared.Infrastructure.Core/README.md)) |
| `Wallow.Shared.Contracts` (Identity) | Client ownership via `IOrganizationClientDirectory` — the module never touches OpenIddict or Identity's persistence |

## Configuration

Uses the shared `DefaultConnection` connection string. No additional configuration required. Its schema is migrated inline only in the `Testing` environment; everywhere else `Wallow.MigrationService` applies migrations.

## Testing

```bash
./scripts/run-tests.sh branding
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project api/src/Modules/Branding/Wallow.Branding.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context BrandingDbContext
```

## Related Documentation

- Agent guide for this module: [`CLAUDE.md`](CLAUDE.md)
- Backend conventions and commands: [`api/CLAUDE.md`](../../../CLAUDE.md)
- Integration event catalogue: [`Wallow.Shared.Contracts/README.md`](../../Shared/Wallow.Shared.Contracts/README.md)
