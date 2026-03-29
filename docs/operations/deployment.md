# Wallow Production Deployment Guide

Wallow deploys as a set of Docker containers managed by a single `docker-compose.yml`. The canonical deployment configuration lives in `deploy/dockhand/` and is designed for servers running [Dockhand](https://dockhand.dev) with [Pangolin](https://pangolin.dev) handling TLS and routing.

For step-by-step setup instructions (secrets generation, `.env` configuration, Pangolin routes, verification, and ongoing operations), see the **[Dockhand deployment README](https://github.com/bc-solutions-coder/wallow/blob/main/deploy/dockhand/README.md)**.

This page provides an architecture overview, explains what happens on first boot, and covers topics that apply regardless of how you run the containers.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Deployment Files](#2-deployment-files)
3. [First Boot Sequence](#3-first-boot-sequence)
4. [Environment Variables](#4-environment-variables)
5. [OIDC Client Registration](#5-oidc-client-registration)
6. [Connecting External Clients](#6-connecting-external-clients)
7. [CI/CD Pipeline](#7-cicd-pipeline)
8. [Scaling](#8-scaling)

---

## 1. Architecture Overview

```
                        ┌──────────────────────────────────────────────┐
                        │  Your Server                                  │
                        │                                               │
  Users ──► HTTPS ──►   │  ┌─────────────────────────────────────┐      │
                        │  │  Pangolin (TLS + routing)            │      │
                        │  │  api.example.com  → :8080            │      │
                        │  │  auth.example.com → :8081            │      │
                        │  │  app.example.com  → :8082            │      │
                        │  └─────────────────────────────────────┘      │
                        │         │            │            │            │
                        │  ┌──────┴──┐  ┌──────┴──┐  ┌─────┴───┐       │
                        │  │ Wallow  │  │ Wallow  │  │ Wallow  │       │
                        │  │ API     │  │ Auth    │  │ Web     │       │
                        │  │ :8080   │  │ :8081   │  │ :8082   │       │
                        │  └────┬────┘  └─────────┘  └─────────┘       │
                        │       │                                       │
                        │  ┌────┴────┐  ┌─────────┐  ┌──────────┐      │
                        │  │Postgres │  │ Valkey  │  │ GarageHQ │      │
                        │  │ :5432   │  │ :6379   │  │ (S3)     │      │
                        │  │+ replica│  │ (cache) │  └──────────┘      │
                        │  └─────────┘  └─────────┘                     │
                        └──────────────────────────────────────────────┘
```

**Services:**

| Service | Purpose | Required |
|---------|---------|----------|
| **Wallow.Api** | REST API, OIDC provider, SignalR hub, background jobs | Yes |
| **Wallow.Auth** | Blazor Server login/register/password reset UI | Yes |
| **Wallow.Web** | Blazor Server dashboard (OIDC client of API) | Yes |
| **PostgreSQL** | Primary database (one schema per module) | Yes |
| **PostgreSQL Replica** | Read replica for query scaling | Recommended |
| **Valkey** | Cache + SignalR backplane (Redis-compatible) | Yes |
| **GarageHQ** | S3-compatible object storage | Yes |
| **Grafana** | Observability dashboards (optional profile) | No |

**Image registry:** `ghcr.io/bc-solutions-coder/wallow`

---

## 2. Deployment Files

All deployment configuration lives in `deploy/dockhand/`:

```
deploy/dockhand/
├── docker-compose.yml   # Full production stack
├── .env.example         # Template — copy to .env and fill in secrets
└── README.md            # Step-by-step deployment instructions
```

The compose file references shared configs from `docker/` (replica init script, GarageHQ config, Alloy collector config) via relative paths.

---

## 3. First Boot Sequence

The compose file orchestrates startup order via `depends_on` health checks:

| Step | What Happens | Triggered By |
|------|-------------|--------------|
| 1. Infrastructure | Postgres, Postgres replica, Valkey, and GarageHQ start and become healthy | `docker compose up -d` |
| 2. Database migrations | `wallow-migrations` init container applies EF Core bundles for all 10 schemas, then exits | Every deployment (idempotent) |
| 3. API starts | Wallow API starts after migrations succeed | `depends_on` |
| 4. Default roles | Creates `admin`, `manager`, `user` roles | Always (idempotent) |
| 5. Admin account | Creates the initial admin user with email confirmed and `admin` role | `AdminBootstrap__*` env vars |
| 6. OIDC clients | Creates/updates OAuth2 client applications in OpenIddict | `PreRegisteredClients__*` env vars |
| 7. Organizations | Auto-creates an organization for each client's `TenantName` | Client has `TenantName` set |
| 8. Org members | Adds users listed in `SeedMembers` to the organization | Client has `SeedMembers` set |
| 9. Auth + Web start | Wallow Auth and Web start after the API health check passes | `depends_on` |

First boot takes a couple minutes while migrations run and the replica syncs.

**What happens if `AdminBootstrap__*` vars are NOT set?**

The API enters **setup mode**. A `SetupMiddleware` intercepts ALL requests (except `/health`, `/api/v1/identity/setup`, `/.well-known`, `/connect`) and returns `503 Service Unavailable` with a message directing you to the setup endpoint. The API is effectively locked until an admin is created.

In setup mode, you must create the admin manually:

```bash
# Check setup status
curl https://api.yourdomain.com/api/v1/identity/setup/status
# → {"setupRequired": true}

# Create admin account
curl -X POST https://api.yourdomain.com/api/v1/identity/setup/admin \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "YourStrongPassword123!",
    "firstName": "Admin",
    "lastName": "User"
  }'

# Mark setup complete
curl -X POST https://api.yourdomain.com/api/v1/identity/setup/complete
```

**Recommendation:** Always set `AdminBootstrap__*` in your `.env` so the API is fully operational on first boot without manual intervention. The bootstrap is idempotent.

---

## 4. Environment Variables

All configuration is done via the `.env` file. Copy the template and replace every `CHANGE_ME` value:

```bash
cp deploy/dockhand/.env.example deploy/dockhand/.env
```

See `deploy/dockhand/.env.example` for the full list of variables with descriptions. Key categories:

| Category | Examples |
|----------|----------|
| **Database** | `POSTGRES_PASSWORD` |
| **Cache** | `VALKEY_PASSWORD` |
| **Security** | `Identity__SigningKey` |
| **URLs** | `ServiceUrls__ApiUrl`, `ServiceUrls__AuthUrl`, `ServiceUrls__WebUrl` |
| **CORS** | `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1` |
| **SMTP** | `Smtp__Host`, `Smtp__Password`, etc. |
| **Admin** | `AdminBootstrap__Email`, `AdminBootstrap__Password` |
| **OIDC** | `OIDC_CLIENT_SECRET`, `PreRegisteredClients__Clients__*` |
| **Storage** | `GARAGE_ACCESS_KEY`, `GARAGE_SECRET_KEY` |
| **Optional** | External auth providers, OpenTelemetry, feature flags |

The [Dockhand README](https://github.com/bc-solutions-coder/wallow/blob/main/deploy/dockhand/README.md) walks through secrets generation and configuration step by step.

---

## 5. OIDC Client Registration

OIDC clients are registered automatically via `PreRegisteredClients__Clients__N__*` environment variables. On every startup, the `PreRegisteredClientSyncService`:

1. **Creates or updates** each client defined in the env vars
2. **Creates organizations** from each client's `TenantName` (if the org doesn't exist)
3. **Adds seed members** to the organization
4. **Removes** clients that were previously synced from config but are no longer present

This is fully idempotent.

### Client Types

| Type | Has Secret? | Grant Type | Use Case |
|------|-------------|------------|----------|
| **Confidential** (e.g., Wallow.Web) | Yes | Authorization Code + PKCE | Server-side apps with a secure backend |
| **Public** (e.g., mobile app) | No | Authorization Code + PKCE | SPAs, mobile apps, CLI tools |
| **Service Account** (prefix `sa-`) | Yes | Client Credentials | Backend-to-backend, M2M |

### Available Scopes

| Category | Scopes |
|----------|--------|
| Standard | `openid`, `email`, `profile`, `roles`, `offline_access` |
| Billing | `billing.read`, `billing.manage`, `invoices.read`, `invoices.write`, `payments.read`, `payments.write`, `subscriptions.read`, `subscriptions.write` |
| Identity | `users.read`, `users.write`, `users.manage`, `roles.read`, `roles.write`, `roles.manage`, `organizations.read`, `organizations.write`, `organizations.manage` |
| Storage | `storage.read`, `storage.write` |
| Communications | `messaging.access`, `announcements.read`, `announcements.manage`, `changelog.manage`, `notifications.read`, `notifications.write` |
| Inquiries | `inquiries.read`, `inquiries.write` |
| Identity (API Keys) | `apikeys.read`, `apikeys.write`, `apikeys.manage` |
| Identity (SSO/SCIM) | `sso.read`, `sso.manage`, `scim.manage` |
| Identity (Service Accounts) | `serviceaccounts.read`, `serviceaccounts.write`, `serviceaccounts.manage` |
| Configuration | `configuration.read`, `configuration.manage` |
| Platform | `webhooks.manage` |

---

## 6. Connecting External Clients

### Token Proxy (Quick Testing)

```bash
curl -X POST https://api.yourdomain.com/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email": "admin@yourdomain.com", "password": "your-admin-password"}'
```

### API Keys (Backend Services)

API keys don't expire and don't require token refresh — ideal for M2M integrations.

```bash
# Create an API key (requires JWT auth first)
curl -X POST https://api.yourdomain.com/api/auth/keys \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "My Backend", "scopes": ["billing.read"]}'

# Use the API key
curl -H "X-Api-Key: sk_live_..." \
  https://api.yourdomain.com/api/billing/invoices
```

### OAuth2 Authorization Code Flow (SPAs / Mobile)

Register your app as a pre-registered client, then use standard OIDC libraries.

**Discovery endpoint:** `https://api.yourdomain.com/.well-known/openid-configuration`

```typescript
import { UserManager } from 'oidc-client-ts';

const mgr = new UserManager({
  authority: 'https://api.yourdomain.com',
  client_id: 'my-spa-client',
  redirect_uri: 'https://myapp.com/callback',
  post_logout_redirect_uri: 'https://myapp.com/',
  scope: 'openid email profile roles offline_access',
  response_type: 'code',
});

await mgr.signinRedirect();
const user = await mgr.signinRedirectCallback();
```

### Client Credentials Flow (Service Accounts)

```bash
curl -X POST https://api.yourdomain.com/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=sa-my-backend" \
  -d "client_secret=<your-secret>" \
  -d "scope=billing.read billing.manage"
```

### SignalR Real-Time Updates

```typescript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://api.yourdomain.com/hubs/realtime', {
    accessTokenFactory: () => getAccessToken()
  })
  .withAutomaticReconnect()
  .build();

connection.on('ReceiveNotification', (notification) => {
  console.log('New notification:', notification);
});

await connection.start();
```

---

## 7. CI/CD Pipeline

1. **Push to `main`** -- CI (`ci.yml`) builds, tests, and pushes images with `:latest` and `:sha` tags to GHCR. release-please creates/updates a Release PR with changelog.
2. **Merge the Release PR** -- release-please creates a git tag (`v1.2.3`) and GitHub Release.
3. **Tag push triggers `publish.yml`** -- retags the `:latest` images with semver tags (`1.2.3`, `1.2`) and scans with Trivy.

To deploy, Dockhand pulls the new images and restarts the stack automatically. Or deploy manually:

```bash
cd deploy/dockhand
docker compose pull && docker compose up -d
```

To pin a specific version, set `APP_TAG` in `.env`:

```ini
APP_TAG=1.2.3
```

---

## 8. Scaling

- **Database:** Use managed PostgreSQL (AWS RDS, Hetzner Cloud, Supabase)
- **Cache:** Use managed Redis/Valkey
- **App instances:** Run multiple API containers behind a load balancer (SignalR uses Valkey backplane for multi-instance support)
- **Storage:** Use managed S3 (AWS, Cloudflare R2) instead of GarageHQ

### Account Creation Methods

| Method | When It Runs | What Gets Created |
|--------|-------------|-------------------|
| **AdminBootstrap** | First startup (if configured) | Admin user with confirmed email + admin role |
| **PreRegisteredClients + SeedMembers** | Every startup (sync) | OIDC clients, organizations, org memberships |
| **Self-Registration** | Anytime (always enabled) | Users register themselves via `/api/v1/identity/auth/register` |
| **External OAuth** (Google, Microsoft, GitHub, Apple) | Anytime (if providers configured) | Users auto-created on first external login |
| **SSO Auto-Provisioning** | Anytime (per-org SSO config) | Users auto-created from SAML/OIDC IdP |
| **SCIM Provisioning** | Anytime (per-org SCIM config) | Users created/updated/deactivated by external directory sync |
| **Invitations** | Anytime | Org admins invite users by email; user account created on acceptance |
