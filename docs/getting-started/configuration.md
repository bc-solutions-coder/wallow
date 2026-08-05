# Configuration Guide

This guide explains how to configure Wallow modules using the Options Pattern and environment-specific settings.

## Overview

Wallow uses the **Microsoft.Extensions.Options pattern** for type-safe configuration. Each module defines its own Options class that binds to a section in `appsettings.json`. This provides:

- **Type safety** - Compile-time checking instead of magic strings
- **Testability** - Easy to mock `IOptions<T>` in unit tests
- **Documentation** - The class itself documents available settings
- **Validation** - Can add validation attributes or custom validators

## Configuration Reference

This section documents the configuration sections a fork is most likely to change. It is not an exhaustive dump of `api/src/Wallow.Api/appsettings.json` — that file also ships `FeatureManagement` (the `Modules.*` toggles), `Plugins`, `Database`, `ApiKeys` and `Performance` sections, and the OpenIddict/authentication wiring lives in code rather than configuration. Read `appsettings.json` itself when you need the complete set. See the "Quick Start" section below for how to create your own module configuration.

### Branding

Branding controls the user-facing identity of the React apps -- auth screens (login, register, password reset), the dashboard shell, and the document head. It is **not** an `appsettings.json` section: it lives in its own file, **`packages/styles/branding.json`**.

That file is the single source of fork identity. `packages/styles` (`@bc-solutions-coder/styles`, `src/branding.ts`) owns the canonical schema, imports `packages/styles/branding.json` statically at build time, and emits the color tokens as CSS custom properties. Both React apps (`apps/wallow-auth`, `apps/wallow-web`) consume it from there, so rebranding a fork needs no source changes -- just edit the JSON.

**Top-level keys:**

| Key | Type | Description |
|-----|------|-------------|
| `appName` | `string` | Product name shown in page titles, headings, and the landing page |
| `appIcon` | `string` | Brand asset reference. A bare filename (`"piggy-icon.svg"`) is resolved to a root-relative URL so it loads from any route depth |
| `tagline` | `string` | Sub-heading shown under the app name |
| `repositoryUrl` | `string` | Optional. The "GitHub"/fork-attribution link target. Falls back to the upstream Wallow repository |
| `docsUrl` | `string` | Optional. The "Docs" link target. Falls back to the upstream documentation site |
| `landingPage` | `object` | `{ "enabled": boolean }` -- see below |
| `theme` | `object` | `defaultMode` plus the `light` and `dark` color sets |

**`landingPage.enabled`** gates the public marketing page at `/` in `apps/wallow-web`. When `true`, an unauthenticated visitor sees the landing page. When `false`, they are sent straight to the BFF login (a forced OIDC challenge). Authenticated visitors are redirected to the dashboard either way.

**`theme`:**

| Key | Type | Description |
|-----|------|-------------|
| `defaultMode` | `string` | Color scheme applied when the document does not pick one: `"light"` or `"dark"` |
| `light` | `object` | Color tokens for light mode |
| `dark` | `object` | Color tokens for dark mode |

Each color set is a map of camelCase token names to CSS values. The tokens the shipped `packages/styles/branding.json` defines are:

`background`, `foreground`, `card`, `cardForeground`, `popover`, `popoverForeground`, `primary`, `primaryForeground`, `secondary`, `secondaryForeground`, `muted`, `mutedForeground`, `accent`, `accentForeground`, `destructive`, `destructiveForeground`, `sidebar`, `sidebarForeground`, `sidebarAccent`, `success`, `successForeground`, `warning`, `warningForeground`, `border`, `input`, `ring`, `radius`

All except `radius` are OKLCH colors; `radius` is a CSS length (`0.5rem`). The map is open-ended -- unknown keys are passed through as CSS custom properties, so a fork can add its own tokens.

The `sidebar*`, `success*` and `warning*` tokens were added after the original set, so all seven resolve through a two-level fallback (`--color-sidebar: var(--sidebar, var(--foreground))`, `--color-warning: var(--warning, var(--primary))`) rather than the plain `var(--x)` the older tokens use. Because `packages/styles/branding.json` is `merge=ours` in `.gitattributes`, a fork whose copy predates these keys never receives them from an upstream merge -- the fallback lands it on a colour its palette already carries instead of on nothing. The `sidebar*` family is the theme's general inverted-surface family, not solely a dashboard sidebar.

**`defaultMode` is only the starting point.** It is the scheme applied when neither the visitor nor their OS states a preference; the frontends resolve the active scheme at load time as persisted choice, then OS `prefers-color-scheme`, then this value, and a visitor can change it at any time through the shared theme toggle. See [Dark Mode](../development/frontend-setup.md#dark-mode).

**Example `packages/styles/branding.json`:**

```json
{
  "appName": "YourProduct",
  "appIcon": "your-icon.svg",
  "tagline": "Your tagline here",
  "landingPage": {
    "enabled": true
  },
  "theme": {
    "defaultMode": "dark",
    "light": {
      "primary": "oklch(0.52 0.12 45)",
      "primaryForeground": "oklch(0.96 0.01 60)",
      "background": "oklch(0.96 0.01 60)",
      "foreground": "oklch(0.20 0.03 55)",
      "radius": "0.5rem"
    },
    "dark": {
      "primary": "oklch(0.62 0.13 45)",
      "primaryForeground": "oklch(0.14 0.015 50)",
      "background": "oklch(0.16 0.02 50)",
      "foreground": "oklch(0.88 0.02 55)",
      "radius": "0.5rem"
    }
  }
}
```

**The two outbound links can be overridden per deployment.** `repositoryUrl` and `docsUrl` are the only branding values a running container can change, because they are the only ones that legitimately differ between deployments of the same image -- a staging stack pointing at a staging docs site, a private mirror instead of the public repository. Both React apps resolve them per request:

| Variable | Overrides |
|-----|-----|
| `WALLOW_REPOSITORY_URL` | `repositoryUrl` |
| `WALLOW_DOCS_URL` | `docsUrl` |

Resolution order for each link, independently: the environment variable, then `packages/styles/branding.json`, then the upstream URL. A variable set to an empty string counts as unset -- an unsubstituted `WALLOW_DOCS_URL=` in a compose file leaves the fork's own link in place rather than rendering a blank `href`. `docker/docker-compose.production.yml` already passes both through to `wallow-auth` and `wallow-web`; see `docker/.env.production.example`.

The variables are read on the SERVER, in each app's request middleware, and the resolved pair is published into the document so the browser renders the same links after hydration. They are therefore **not** `VITE_*` variables and are never baked into a client bundle: the same image serves different links in different environments.

Every other branding value is imported at build time rather than read at runtime, so changing it requires rebuilding (or restarting the dev server for) the frontends.

Per-OAuth-client branding is a separate, runtime concern: the Branding module serves `GET /v1/identity/apps/{clientId}/branding`, and a client's display name, tagline, logo, and theme are overlaid on top of the fork's when a `client_id` is present.

### Session Limits

Wallow enforces a per-user concurrent session limit. When a user exceeds the limit, the oldest active session is automatically evicted before the new one is created.

#### How it works

1. **Session creation** -- on every successful login, `SessionService` counts the user's active, non-revoked, non-expired sessions.
2. **Eviction** -- if the count is at or above the limit (default: **5**), the oldest session is revoked. A `UserSessionEvictedEvent` is published over the Wolverine bus so other modules can react.
3. **Redis revocation** -- the evicted (or manually revoked) session token is written to Redis under the key `session:revoked:{token}` with a 24-hour TTL.
4. **Request-time enforcement** -- `SessionRevocationMiddleware` checks every authenticated request against Redis. If the session token in the `wallow.session` cookie exists in the revoked set, the middleware clears the cookie and returns `401 Unauthorized` with `{"error":"session_revoked"}` before the request reaches any handler.
5. **Activity tracking** -- `SessionActivityMiddleware` updates the `last_activity_at` timestamp on each session. Updates are throttled to once per 60 seconds per session (via a Redis NX key) to avoid write amplification.
6. **Pruning** -- `SessionPruningJob` periodically deletes expired and revoked session rows from the database.

#### Session limit

The concurrent session limit is a compile-time constant (`MaxSessions = 5`) in `SessionService`. It applies globally across all users and tenants. To change the limit, update the constant and redeploy.

#### Session management API

Users can inspect and revoke their own sessions via the Identity module API (requires authentication):

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/identity/sessions` | List all active sessions for the authenticated user |
| `DELETE` | `/v1/identity/sessions/{sessionId}` | Revoke a specific session by ID |

**List active sessions response:**

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "createdAt": "2025-01-15T10:30:00Z",
    "lastActivityAt": "2025-01-15T14:22:00Z",
    "expiresAt": "2025-01-16T10:30:00Z"
  }
]
```

**Revoke a session:**

```http
DELETE /v1/identity/sessions/3fa85f64-5717-4562-b3fc-2c963f66afa6
Authorization: Bearer {token}
```

Returns `204 No Content` on success. A session can only be revoked by its owner -- attempting to revoke another user's session returns an error.

#### Redis requirements

Session revocation requires a working Redis connection. Configure `ConnectionStrings__Redis` (see [Connection Strings](#connection-strings) below).

Revoked and evicted session tokens are stored with a 24-hour TTL. Redis keys use these patterns:

```
session:revoked:{token}   -- value "revoked" (manual) or "evicted" (auto-evicted)
session:touched:{token}   -- NX key throttling activity updates (60s TTL)
```

If Redis is unreachable, `SessionRevocationMiddleware` will fail to check revocation, so revoked sessions may temporarily pass through. Ensure Redis availability matches your security requirements.

#### Session duration

Sessions expire 24 hours after creation. The `SessionPruningJob` removes expired and revoked rows from the database on a periodic schedule.

### Identity: Email Change Flow

Wallow includes a secure two-step email change flow. Users request a change via the API, receive a confirmation link at the new address, and click it to finalize.

#### Endpoints

**Initiate email change** -- authenticated users only:

```http
POST /v1/identity/auth/change-email
Authorization: Cookie (authenticated session)
Content-Type: application/json

{
  "newEmail": "newaddress@example.com"
}
```

Responses:

| Status | Body | Meaning |
|--------|------|---------|
| `200 OK` | `{ "succeeded": true }` | Confirmation email sent to the new address |
| `400 Bad Request` | `{ "succeeded": false, "error": "same_email" }` | New email matches the current email |
| `429 Too Many Requests` | `{ "succeeded": false, "error": "rate_limited" }` | Rate limit exceeded (max 3 per hour) |
| `401 Unauthorized` | `{ "succeeded": false, "error": "unauthorized" }` | Not authenticated |

**Confirm email change** -- unauthenticated, accessed via the link in the confirmation email:

```http
GET /v1/identity/auth/confirm-email-change
  ?token=<change-token>
  &userId=<user-id>
  &newEmail=<new-email>
```

Responses:

| Status | Body | Meaning |
|--------|------|---------|
| `200 OK` | `{ "succeeded": true }` | Email changed successfully |
| `400 Bad Request` | `{ "succeeded": false, "error": "token_expired" }` | Confirmation link expired (24-hour window) |
| `400 Bad Request` | `{ "succeeded": false, "error": "invalid_token" }` | Token invalid or already used |

#### Security Model

- **Token expiry**: Confirmation tokens are valid for **24 hours** from the time of the request. Expired tokens are rejected and the pending change is cleared automatically.
- **Rate limiting**: A maximum of **3 email change requests per hour** per user is enforced server-side via Redis. Requests beyond this limit return `429 Too Many Requests`.
- **Confirmation email goes to the new address**: A `UserEmailChangeRequestedEvent` is published, which the Notifications module handles to send a confirmation email to the new address.
- **Notification on completion**: When a change is confirmed, a `UserEmailChangedEvent` is published. The Notifications module handles this to send a security notice to the **old address**, alerting the account holder that their email was changed.
- **Username sync**: On confirmation, the user's username is updated to match the new email address.
- **Session continuity**: Existing sessions remain valid after an email change. If your fork requires forced re-authentication on email change, add a Wolverine handler for `UserEmailChangedEvent` that revokes the user's active sessions.

#### Integration with Email Verification Infrastructure

The email change flow uses the same ASP.NET Core Identity token infrastructure as initial email verification (`GenerateChangeEmailTokenAsync` / `ChangeEmailAsync`). Token generation and validation are handled entirely within the Identity module's `UserManager`.

The confirmation URL is constructed using the `AuthUrl` configuration key:

```json
{
  "AuthUrl": "https://auth.yourdomain.com"
}
```

The auth app (`apps/wallow-auth`) must serve the email-change confirmation route. This page reads the `token`, `userId`, and `newEmail` query parameters and calls the confirm endpoint on the API to finalize the change.

| Config Key | Shipped default | Description |
|------------|-----------------|-------------|
| `AuthUrl` | `""` | Base URL of the auth app (`apps/wallow-auth`). Used to build the confirmation link sent to the user. |

`appsettings.json` ships this **empty**, and there is a second copy under `ServiceUrls:AuthUrl` that the `ServiceUrlsOptions` binder reads (its own code default is `http://localhost:3002`). `appsettings.Development.json` fills both in with `http://localhost:3002`, so a local run works out of the box; a deployment that leaves them empty will mail a confirmation link with no origin. Set both for your fork.

---

### Connection Strings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=SET_VIA_ENV_OR_USER_SECRETS;SSL Mode=Disable",
    "Redis": "localhost:6379,password=,abortConnect=false"
  }
}
```

| Key | Description | Environment Variable |
|-----|-------------|---------------------|
| `DefaultConnection` | PostgreSQL connection string | `ConnectionStrings__DefaultConnection` |
| `Redis` | Redis/Valkey connection string for caching and SignalR backplane | `ConnectionStrings__Redis` |

### Why there is no CORS configuration

Wallow has no `Cors` configuration section, and the API calls neither `AddCors` nor `UseCors`. This is deliberate: the frontends use the **same-origin BFF pattern**. The browser only ever talks to its own origin, and each React app's server side proxies to the API and holds the token. No cross-origin browser request to the API is made, so there is nothing for CORS to permit.

If you add a genuinely cross-origin client to your fork, you are adding a new deployment shape rather than filling in an existing setting -- wire up ASP.NET Core CORS yourself and treat the allowed origins as fork-owned configuration.

Redirect URIs are a separate mechanism: OIDC clients register their own redirect URIs, which `OpenIddictRedirectUriValidator` validates per client. Those are not CORS origins.

### SMTP (Email)

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "DefaultFromAddress": "noreply@wallow.local",
    "DefaultFromName": "Wallow",
    "MaxRetries": 3,
    "TimeoutSeconds": 30
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Host` | `localhost` | SMTP server hostname |
| `Port` | `1025` | SMTP port (1025 for Mailpit, 587 for production) |
| `UseSsl` | `false` | Enable TLS/SSL |
| `Username` | `null` | SMTP authentication username (optional) |
| `Password` | `null` | SMTP authentication password (optional) |
| `DefaultFromAddress` | `noreply@wallow.local` | Default sender email address |
| `DefaultFromName` | `Wallow` | Default sender display name |
| `MaxRetries` | `3` | Number of retry attempts on failure |
| `TimeoutSeconds` | `30` | SMTP operation timeout |

**Local development**: Use Mailpit at `localhost:1025` (no auth, no SSL). View emails at `http://localhost:8025`.

### OpenTelemetry (Observability)

```json
{
  "OpenTelemetry": {
    "EnableLogging": false,
    "ServiceName": "Wallow",
    "OtlpEndpoint": "http://localhost:4318",
    "OtlpGrpcEndpoint": "http://localhost:4317",
    "TraceSamplingRatio": 1.0
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `EnableLogging` | `false` | Enable OpenTelemetry logging export |
| `ServiceName` | `Wallow` | Service name for traces and metrics |
| `OtlpEndpoint` | `http://localhost:4318` | OTLP HTTP endpoint |
| `OtlpGrpcEndpoint` | `http://localhost:4317` | OTLP gRPC endpoint (used for traces/metrics) |
| `TraceSamplingRatio` | `1.0` | Fraction of traces sampled — `1.0` records everything, which is the shipped default because local development wants complete traces |

**Note**: The application currently uses `OtlpGrpcEndpoint` for exporting traces and metrics.

### Storage Module

Wallow uses GarageHQ as the default S3-compatible object storage. The `S3StorageProvider` works with any S3-compatible backend (GarageHQ, AWS S3, Cloudflare R2, MinIO).

```json
{
  "Storage": {
    "Provider": "S3",
    "Local": {
      "BasePath": "/var/wallow/storage",
      "BaseUrl": "http://localhost:5001"
    },
    "S3": {
      "Endpoint": "http://localhost:3900",
      "AccessKey": "SET_VIA_Storage__S3__AccessKey",
      "SecretKey": "SET_VIA_Storage__S3__SecretKey",
      "BucketName": "wallow-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

That is the shipped `Storage` section in full. There is **no `ClamAv` key in `appsettings.json`** — the options class supplies the defaults below, and you add the section only when you want scanning on.

| Key | Default | Description |
|-----|---------|-------------|
| `Provider` | `S3` | Storage provider: `Local` or `S3` |
| `Local.BasePath` | `/var/wallow/storage` | Local filesystem path for file storage |
| `Local.BaseUrl` | `null` | Base URL for serving files (optional) |
| `S3.Endpoint` | `http://localhost:3900` | S3-compatible endpoint URL (GarageHQ default) |
| `S3.AccessKey` | - | S3 access key |
| `S3.SecretKey` | - | S3 secret key |
| `S3.BucketName` | `wallow-files` | S3 bucket name |
| `S3.UsePathStyle` | `true` | Use path-style URLs (required for GarageHQ and MinIO) |
| `S3.Region` | `us-east-1` | S3 region |
| `ClamAv.Enabled` | `false` | Enable ClamAV virus scanning on file uploads |
| `ClamAv.Host` | `localhost` | ClamAV daemon hostname |
| `ClamAv.Port` | `3310` | ClamAV daemon port |

**Local development**: GarageHQ runs on `http://localhost:3900` via Docker Compose. The init script auto-creates the access key and bucket. Admin API at `http://localhost:3903`.

#### ClamAV Virus Scanning (Optional)

ClamAV virus scanning is **disabled by default**. When disabled, file uploads skip scanning entirely (a no-op scanner returns clean for all files). To enable scanning:

1. Start the ClamAV container using the Docker Compose profile:

   ```bash
   cd docker && docker compose --profile clamav up -d
   ```

2. Enable scanning in your configuration:

   ```json
   {
     "Storage": {
       "ClamAv": {
         "Enabled": true,
         "Host": "localhost",
         "Port": 3310
       }
     }
   }
   ```

   Or via environment variables:

   ```bash
   Storage__ClamAv__Enabled=true
   Storage__ClamAv__Host=localhost
   Storage__ClamAv__Port=3310
   ```

When enabled, all file uploads are scanned synchronously before storage. Infected files are rejected with a validation error. A ClamAV health check is also registered at `/health` (tagged `clamav`).

---

## Quick Start

### 1. Create an Options Class

Create a class in your module's Infrastructure layer:

```csharp
// api/src/Modules/YourModule/Wallow.YourModule.Infrastructure/Configuration/YourModuleOptions.cs
namespace Wallow.YourModule.Infrastructure.Configuration;

public sealed class YourModuleOptions
{
    public const string SectionName = "YourModule";

    public string ApiKey { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

### 2. Register in Module Extensions

Bind the configuration section in your module's extension method:

```csharp
// api/src/Modules/YourModule/Wallow.YourModule.Infrastructure/Extensions/YourModuleExtensions.cs
public static IServiceCollection AddYourModuleInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Bind configuration section to options
    services.Configure<YourModuleOptions>(
        configuration.GetSection(YourModuleOptions.SectionName));

    // ... other registrations
    return services;
}
```

### 3. Add Configuration to appsettings.json

```json
{
  "YourModule": {
    "ApiKey": "your-api-key",
    "MaxRetries": 5,
    "Timeout": "00:00:45"
  }
}
```

### 4. Inject and Use Options

Inject `IOptions<YourModuleOptions>` into any service or controller and access `.Value`:

```csharp
public class YourService(IOptions<YourModuleOptions> options)
{
    private readonly YourModuleOptions _options = options.Value;
}
```

## Environment-Specific Configuration

.NET supports layered configuration files that override each other based on the environment.

### Configuration Loading Order

Configuration is loaded in this order (later sources override earlier ones):

1. `appsettings.json` - Base configuration (all environments)
2. `appsettings.{Environment}.json` - Environment-specific overrides
3. Environment variables
4. Command-line arguments
5. User secrets (Development only)

### Environment Names

| Environment | File | Usage |
|-------------|------|-------|
| Development | `appsettings.Development.json` | Local development |
| Staging | `appsettings.Staging.json` | Pre-production testing |
| Production | `appsettings.Production.json` | Live production |
| Testing | `appsettings.Testing.json` | Integration tests |

### Setting the Environment

```bash
# Via environment variable (recommended for servers)
export ASPNETCORE_ENVIRONMENT=Production

# Via command line
dotnet run --environment Production

# Via launchSettings.json (local development)
# Already configured in Properties/launchSettings.json
```

### Example: Environment-Specific Files

**appsettings.json** (base configuration):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=SET_VIA_ENV_OR_USER_SECRETS",
    "Redis": "localhost:6379,password=,abortConnect=false"
  },
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Endpoint": "http://localhost:3900",
      "AccessKey": "SET_VIA_Storage__S3__AccessKey",
      "SecretKey": "SET_VIA_Storage__S3__SecretKey",
      "BucketName": "wallow-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

**appsettings.Development.json** (local dev overrides):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "OpenTelemetry": {
    "EnableLogging": true
  }
}
```

**appsettings.Production.json** (production overrides):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.EntityFrameworkCore": "Error"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;Database=wallow;Username=OVERRIDE_VIA_ENV_VAR;Password=OVERRIDE_VIA_ENV_VAR"
  },
  "Smtp": {
    "Host": "OVERRIDE_VIA_ENV_VAR",
    "Port": 587,
    "UseSsl": true,
    "Username": "OVERRIDE_VIA_ENV_VAR",
    "Password": "OVERRIDE_VIA_ENV_VAR"
  },
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Endpoint": "http://garage:3900",
      "AccessKey": "OVERRIDE_VIA_ENV_VAR",
      "SecretKey": "OVERRIDE_VIA_ENV_VAR",
      "BucketName": "wallow-files"
    }
  }
}
```

## Environment Variables

Environment variables override all JSON configuration. Use double underscores (`__`) for nested keys:

```bash
# Connection strings
export ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=wallow;Username=user;Password=pass"
export ConnectionStrings__Redis="redis-server:6379,password=secret"

# SMTP
export Smtp__Host="smtp.example.com"
export Smtp__Port="587"
export Smtp__UseSsl="true"
export Smtp__Username="smtp-user"
export Smtp__Password="smtp-password"

# Storage
export Storage__Provider="S3"
export Storage__S3__Endpoint="https://s3.amazonaws.com"
export Storage__S3__AccessKey="AKIAIOSFODNN7EXAMPLE"
export Storage__S3__SecretKey="your-secret-key"
export Storage__S3__BucketName="my-production-bucket"

# OpenTelemetry
export OpenTelemetry__ServiceName="Wallow"
export OpenTelemetry__OtlpGrpcEndpoint="http://otel-collector:4317"
```

### Docker/Kubernetes

In containerized deployments, pass configuration via environment variables:

```yaml
# docker-compose.yml
services:
  api:
    image: wallow-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=wallow;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      - ConnectionStrings__Redis=valkey:6379
      - Storage__Provider=S3
      - Storage__S3__Endpoint=http://garage:3900
      - Storage__S3__AccessKey=${GARAGE_ACCESS_KEY}
      - Storage__S3__SecretKey=${GARAGE_SECRET_KEY}
      - Storage__S3__BucketName=${GARAGE_BUCKET}
```

```yaml
# Kubernetes ConfigMap/Secret
apiVersion: v1
kind: ConfigMap
metadata:
  name: wallow-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  Storage__Provider: "S3"
  OpenTelemetry__OtlpGrpcEndpoint: "http://otel-collector:4317"
---
apiVersion: v1
kind: Secret
metadata:
  name: wallow-secrets
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=wallow;Username=user;Password=secret"
  ConnectionStrings__Redis: "redis:6379,password=secret"
  Storage__S3__Endpoint: "http://garage:3900"
  Storage__S3__AccessKey: "your-access-key"
  Storage__S3__SecretKey: "your-secret-key"
  Storage__S3__BucketName: "wallow-files"
```

## Local Development Infrastructure

Start infrastructure services using Docker Compose:

```bash
pnpm backend:infra          # docker compose up -d, from the repo root
pnpm backend:infra:down     # stop the containers (volumes are kept)
```

This starts the following services. Every port is published to `127.0.0.1` only, so nothing here is reachable from another machine:

| Service | Port | Purpose | Default Credentials |
|---------|------|---------|---------------------|
| PostgreSQL | 5432 | Primary database | `POSTGRES_USER` / `POSTGRES_PASSWORD` from `docker/.env` |
| Valkey | 6379 | Cache and SignalR backplane | See `docker/.env` |
| GarageHQ | 3900, 3903 | S3-compatible object storage (S3 API: 3900, Admin: 3903) | See `docker/.env` |
| Mailpit | 1025, 8025 | Email testing (SMTP: 1025, UI: 8025) | N/A |
| Grafana Alloy | 4317, 4318 | OpenTelemetry collector (OTLP gRPC: 4317, OTLP HTTP: 4318) | N/A |
| Grafana LGTM | 3001 | Dashboards and the logs/metrics/traces backend | `admin` / See `docker/.env` |
| Docs | 5004 | The built DocFX site | N/A |
| ClamAV (optional) | 3310 | Antivirus file scanning (`--profile clamav`) | N/A |

Alloy is the collector the OTLP endpoints belong to; it forwards to the LGTM stack, which is why 4317/4318 and the Grafana UI are separate containers.

Docker environment variables are configured in `docker/.env` (copy from `docker/.env.example` and set your own values). The full key list is:

```env
COMPOSE_PROJECT_NAME=wallow
POSTGRES_USER=wallow
POSTGRES_PASSWORD=changeme
POSTGRES_DB=wallow
VALKEY_PASSWORD=changeme
VALKEY_MAXMEMORY=256mb
GARAGE_KEY_NAME=wallow-dev
GARAGE_ACCESS_KEY=changeme
GARAGE_SECRET_KEY=changeme
GARAGE_BUCKET=wallow-files
GARAGE_REGION=us-east-1
GF_ADMIN_PASSWORD=changeme
```

`pnpm lint:env` checks that every `${VAR}` the compose files interpolate is documented in the paired `.env.example`, so this list stays in step with `docker/docker-compose.yml`.

## User Secrets (Development Only)

For sensitive configuration during development, use User Secrets to keep credentials out of source control:

```bash
# Initialize user secrets (one-time)
cd api/src/Wallow.Api
dotnet user-secrets init

# Set secrets
dotnet user-secrets set "YourModule:ApiKey" "my-dev-api-key"
dotnet user-secrets set "Storage:S3:SecretKey" "my-s3-secret"

# List secrets
dotnet user-secrets list

# Clear all secrets
dotnet user-secrets clear
```

Secrets are stored in:
- **macOS/Linux**: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`

## Advanced Patterns

### Options Variants

- **`IOptions<T>`** -- Singleton, read once at startup. Use for configuration that does not change.
- **`IOptionsSnapshot<T>`** -- Scoped, re-reads on each request. Use for configuration that may change at runtime.
- **`IOptionsMonitor<T>`** -- Singleton with change notifications. Use in background services.

### Validation with Data Annotations

Register options with `ValidateDataAnnotations()` and `ValidateOnStart()` to fail fast on misconfiguration:

```csharp
services.AddOptions<YourModuleOptions>()
    .Bind(configuration.GetSection(YourModuleOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## Best Practices

| Do | Don't |
|----|-------|
| Use Options classes with `SectionName` constants | Use magic strings like `configuration["Module:Setting"]` |
| Inject `IOptions<T>` into services | Inject `IConfiguration` directly into services |
| Set sensible defaults in Options classes | Require all settings to be configured |
| Use environment variables for secrets in production | Commit secrets to source control |
| Use User Secrets for local development secrets | Store API keys in appsettings.json |
| Validate configuration with `ValidateOnStart()` | Let the app crash with cryptic errors |
| Keep Options classes in Infrastructure layer | Put configuration in Domain layer |

## File Locations

| Purpose | Location |
|---------|----------|
| Options classes | `api/src/Modules/{Module}/Wallow.{Module}.Infrastructure/Configuration/` |
| Module registration | `api/src/Modules/{Module}/Wallow.{Module}.Infrastructure/Extensions/` |
| Base configuration | `api/src/Wallow.Api/appsettings.json` |
| Environment overrides | `api/src/Wallow.Api/appsettings.{Environment}.json` |

## Troubleshooting

### Configuration Not Loading

1. Check the section name matches exactly (case-sensitive)
2. Verify the JSON structure matches the Options class hierarchy
3. Check environment variable naming: `Section__Property`

### Options Are Null

Ensure you're registering with `services.Configure<T>()` before the service is resolved:

```csharp
// This must happen in AddYourModule(), not later
services.Configure<YourModuleOptions>(
    configuration.GetSection(YourModuleOptions.SectionName));
```

### Environment Not Being Applied

```bash
# Check current environment
echo $ASPNETCORE_ENVIRONMENT

# Verify file exists
ls -la appsettings.Production.json
```
