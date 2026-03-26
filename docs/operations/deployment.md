# Wallow Production Deployment Guide

Complete step-by-step guide to deploy Wallow on a fresh Linux server (bare metal, VPS, or Portainer). By the end, you will have the API running with the admin user created, OIDC clients registered, and the Web dashboard connected.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Prerequisites](#2-prerequisites)
3. [Option A: Bare Linux Server Setup](#3-option-a-bare-linux-server-setup)
4. [Option B: Portainer Setup](#4-option-b-portainer-setup)
5. [Configure Environment Variables](#5-configure-environment-variables)
6. [Start Infrastructure](#6-start-infrastructure)
7. [Deploy the Application](#7-deploy-the-application)
8. [Configure Reverse Proxy (HTTPS)](#8-configure-reverse-proxy-https)
9. [Register OIDC Clients](#9-register-oidc-clients)
10. [Connect Wallow.Web (Dashboard)](#10-connect-wallowweb-dashboard)
11. [Connect Wallow.Auth (Login UI)](#11-connect-wallowauth-login-ui)
12. [Connect External Clients](#12-connect-external-clients)
13. [Verify Everything Works](#13-verify-everything-works)
14. [Ongoing Operations](#14-ongoing-operations)
15. [Troubleshooting](#15-troubleshooting)

---

## 1. Architecture Overview

```
                        ┌──────────────────────────────────────────────┐
                        │  Your Server                                  │
                        │                                               │
  Users ──► HTTPS ──►   │  ┌─────────────────────────────────────┐      │
                        │  │  Caddy (reverse proxy, auto-TLS)    │      │
                        │  │  api.example.com  → :8080           │      │
                        │  │  auth.example.com → :8090           │      │
                        │  │  app.example.com  → :8070           │      │
                        │  └─────────────────────────────────────┘      │
                        │         │            │            │            │
                        │  ┌──────┴──┐  ┌──────┴──┐  ┌─────┴───┐       │
                        │  │ Wallow  │  │ Wallow  │  │ Wallow  │       │
                        │  │ API     │  │ Auth    │  │ Web     │       │
                        │  │ :8080   │  │ :8090   │  │ :8070   │       │
                        │  └────┬────┘  └─────────┘  └─────────┘       │
                        │       │                                       │
                        │  ┌────┴────┐  ┌─────────┐                     │
                        │  │Postgres │  │ Valkey  │                     │
                        │  │ :5432   │  │ :6379   │                     │
                        │  │+ replica│  │ (cache) │                     │
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
| **Caddy** | Reverse proxy with automatic HTTPS | Recommended |

**Image registry:** `ghcr.io/bc-solutions-coder/wallow`

---

## 2. Prerequisites

- A Linux server (Ubuntu 22.04+, Debian 12+, or any Docker-capable distro)
- Docker Engine 24+ and Docker Compose v2
- A domain name with DNS pointed to your server (3 subdomains: `api.`, `auth.`, `app.`)
- Ports 80, 443 open for HTTPS (if using Caddy)
- A GitHub Personal Access Token (PAT) with `read:packages` scope to pull from GHCR

---

## 3. Option A: Bare Linux Server Setup

### 3.1 Automated Bootstrap (Recommended)

The repository includes a bootstrap script that handles everything:

```bash
# SSH into your server as root
ssh root@YOUR_SERVER_IP

# Download and run the bootstrap script
# (or scp the deploy/ directory from the repo)
scp -r deploy/ root@YOUR_SERVER_IP:/tmp/wallow-deploy/
ssh root@YOUR_SERVER_IP "sudo bash /tmp/wallow-deploy/bootstrap.sh --ssh-key 'ssh-ed25519 AAAA...your-public-key'"
```

The bootstrap script:
- Installs Docker Engine + Compose v2
- Creates a `deploy` user with Docker access
- Creates `/opt/wallow/{dev,staging,prod}` directories
- Copies compose files and generates `.env` files with random passwords
- Configures UFW firewall (ports 22, 8080-8082)
- Starts Postgres + Valkey infrastructure containers

### 3.2 Manual Setup

If you prefer manual control:

```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
systemctl enable docker && systemctl start docker

# Create deploy user
useradd -m -s /bin/bash deploy
usermod -aG docker deploy

# Create directory structure
mkdir -p /opt/wallow/prod
chown -R deploy:deploy /opt/wallow
```

Copy the deployment files to the server:

```bash
# From your local machine (in the repo root)
scp deploy/docker-compose.base.yml deploy@YOUR_SERVER_IP:/opt/wallow/prod/
scp deploy/docker-compose.prod.yml deploy@YOUR_SERVER_IP:/opt/wallow/prod/
scp deploy/init-db.sql deploy@YOUR_SERVER_IP:/opt/wallow/prod/
scp deploy/init-replica.sh deploy@YOUR_SERVER_IP:/opt/wallow/prod/
scp deploy/.env.production.example deploy@YOUR_SERVER_IP:/opt/wallow/prod/.env
scp deploy/deploy.sh deploy@YOUR_SERVER_IP:/opt/wallow/prod/
```

### 3.3 Authenticate with GitHub Container Registry

```bash
# On the server, as the deploy user
# Create a PAT at: GitHub → Settings → Developer settings → Personal access tokens
# Required scope: read:packages

echo "YOUR_GITHUB_PAT" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

---

## 4. Option B: Portainer Setup

### 4.1 Create a Stack

1. Log into Portainer and navigate to **Stacks → Add Stack**
2. Name it `wallow-prod`
3. Choose **Repository** or **Upload** method:

**Repository method:**
- Repository URL: `https://github.com/bc-solutions-coder/wallow`
- Compose path: `deploy/docker-compose.base.yml`
- Additional compose files: `deploy/docker-compose.prod.yml`

**Upload method:**
- Upload `deploy/docker-compose.base.yml` and `deploy/docker-compose.prod.yml`
- You will also need `deploy/init-db.sql` and `deploy/init-replica.sh` accessible to the containers

### 4.2 Configure Environment Variables

In Portainer's **Environment variables** section for the stack, add all the variables from [Section 5](#5-configure-environment-variables) below.

### 4.3 Configure Registry Access

1. Go to **Registries → Add registry**
2. Select **Custom registry**
3. Registry URL: `ghcr.io`
4. Username: your GitHub username
5. Password: your GitHub PAT (with `read:packages` scope)

### 4.4 Deploy

Click **Deploy the stack**. The infrastructure containers (Postgres, Valkey) will start first due to health check dependencies.

---

## 5. Configure Environment Variables

Edit the `.env` file on your server. Every `CHANGE_ME` value **must** be replaced before first startup.

### 5.1 Generate Secrets

```bash
# Generate strong random passwords (run once per value you need)
openssl rand -base64 32

# Generate the JWT signing key (48 bytes minimum)
openssl rand -base64 48
```

### 5.2 Required Variables

```ini
# =============================================================================
# ENVIRONMENT
# =============================================================================
COMPOSE_PROJECT_NAME=wallow-prod
ASPNETCORE_ENVIRONMENT=Production

# =============================================================================
# APPLICATION IMAGE
# =============================================================================
APP_IMAGE=ghcr.io/bc-solutions-coder/wallow
APP_TAG=latest

# =============================================================================
# POSTGRESQL (REQUIRED)
# =============================================================================
POSTGRES_USER=wallow
POSTGRES_PASSWORD=<generated-password>
POSTGRES_DB=wallow

# =============================================================================
# VALKEY / REDIS (REQUIRED)
# =============================================================================
VALKEY_PASSWORD=<generated-password>

# =============================================================================
# JWT SIGNING KEY (REQUIRED — API will not start without this)
# =============================================================================
# Generate with: openssl rand -base64 48
Identity__SigningKey=<generated-base64-key>

# =============================================================================
# SERVICE URLS (REQUIRED — must match your reverse proxy / domain setup)
# =============================================================================
# These are the PUBLIC URLs your users will see in their browser.
ServiceUrls__ApiUrl=https://api.yourdomain.com
ServiceUrls__AuthUrl=https://auth.yourdomain.com
ServiceUrls__WebUrl=https://app.yourdomain.com

# Cookie domain — leading dot enables subdomain sharing
Authentication__CookieDomain=.yourdomain.com

# =============================================================================
# CORS (REQUIRED — list every frontend origin)
# =============================================================================
Cors__AllowedOrigins__0=https://app.yourdomain.com
Cors__AllowedOrigins__1=https://auth.yourdomain.com

# =============================================================================
# SMTP (REQUIRED for registration, password reset, notifications)
# =============================================================================
# Use any SMTP provider: Amazon SES, Postmark, Mailgun, SendGrid, etc.
Smtp__Host=smtp.example.com
Smtp__Port=587
Smtp__UseSsl=true
Smtp__Username=<smtp-username>
Smtp__Password=<smtp-password>
Smtp__DefaultFromAddress=noreply@yourdomain.com
Smtp__DefaultFromName=Wallow

# =============================================================================
# ADMIN BOOTSTRAP (first run only — creates the initial admin user)
# =============================================================================
# After the first successful startup, these are ignored.
AdminBootstrap__Email=admin@yourdomain.com
AdminBootstrap__Password=<strong-admin-password>
AdminBootstrap__FirstName=Admin
AdminBootstrap__LastName=User
```

### 5.3 OIDC Client Registration (Pre-Registered Clients)

These environment variables tell the API to automatically create OIDC clients and their associated organizations on startup. This is how you connect Wallow.Web and any other client apps.

```ini
# =============================================================================
# WALLOW.WEB CLIENT (the dashboard — REQUIRED)
# =============================================================================
PreRegisteredClients__Clients__0__ClientId=wallow-web-client
PreRegisteredClients__Clients__0__DisplayName=Wallow Web
PreRegisteredClients__Clients__0__Secret=<generated-secret>
PreRegisteredClients__Clients__0__TenantName=Default
PreRegisteredClients__Clients__0__RedirectUris__0=https://app.yourdomain.com/signin-oidc
PreRegisteredClients__Clients__0__PostLogoutRedirectUris__0=https://app.yourdomain.com/signout-callback-oidc
PreRegisteredClients__Clients__0__Scopes__0=openid
PreRegisteredClients__Clients__0__Scopes__1=email
PreRegisteredClients__Clients__0__Scopes__2=profile
PreRegisteredClients__Clients__0__Scopes__3=roles
PreRegisteredClients__Clients__0__Scopes__4=offline_access
PreRegisteredClients__Clients__0__SeedMembers__0=admin@yourdomain.com
```

To add more clients (e.g., a mobile app, a service account), increment the index:

```ini
# =============================================================================
# EXAMPLE: SERVICE ACCOUNT FOR A BACKEND INTEGRATION
# =============================================================================
PreRegisteredClients__Clients__1__ClientId=sa-my-backend
PreRegisteredClients__Clients__1__DisplayName=My Backend Service
PreRegisteredClients__Clients__1__Secret=<generated-secret>
PreRegisteredClients__Clients__1__TenantName=Default
PreRegisteredClients__Clients__1__Scopes__0=billing.read
PreRegisteredClients__Clients__1__Scopes__1=billing.manage

# =============================================================================
# EXAMPLE: SPA / MOBILE APP (public client — no secret)
# =============================================================================
PreRegisteredClients__Clients__2__ClientId=my-mobile-app
PreRegisteredClients__Clients__2__DisplayName=My Mobile App
PreRegisteredClients__Clients__2__TenantName=Default
PreRegisteredClients__Clients__2__RedirectUris__0=myapp://callback
PreRegisteredClients__Clients__2__PostLogoutRedirectUris__0=myapp://logout
PreRegisteredClients__Clients__2__Scopes__0=openid
PreRegisteredClients__Clients__2__Scopes__1=email
PreRegisteredClients__Clients__2__Scopes__2=profile
PreRegisteredClients__Clients__2__Scopes__3=roles
PreRegisteredClients__Clients__2__Scopes__4=offline_access
```

### 5.4 Optional Variables

```ini
# =============================================================================
# FILE STORAGE (defaults to local filesystem)
# =============================================================================
Storage__Provider=Local
Storage__Local__BasePath=/var/wallow/storage
Storage__Local__BaseUrl=https://api.yourdomain.com

# Uncomment for S3-compatible storage (AWS, Cloudflare R2, MinIO, GarageHQ):
# Storage__Provider=S3
# Storage__S3__Endpoint=https://s3.amazonaws.com
# Storage__S3__AccessKey=<aws-access-key>
# Storage__S3__SecretKey=<aws-secret-key>
# Storage__S3__BucketName=wallow-files
# Storage__S3__Region=us-east-1
# Storage__S3__UsePathStyle=false

# =============================================================================
# EXTERNAL AUTH PROVIDERS (optional — uncomment to enable SSO)
# =============================================================================
# Authentication__Google__ClientId=
# Authentication__Google__ClientSecret=

# Authentication__Microsoft__ClientId=
# Authentication__Microsoft__ClientSecret=

# Authentication__GitHub__ClientId=
# Authentication__GitHub__ClientSecret=

# =============================================================================
# OBSERVABILITY (optional — OpenTelemetry)
# =============================================================================
# OpenTelemetry__EnableLogging=true
# OpenTelemetry__ServiceName=Wallow
# OpenTelemetry__OtlpEndpoint=http://alloy:4318

# =============================================================================
# MODULE FEATURE FLAGS (all enabled by default except ApiKeys)
# =============================================================================
# FeatureManagement__Modules.Billing=true
# FeatureManagement__Modules.Identity=true
# FeatureManagement__Modules.Storage=true
# FeatureManagement__Modules.Notifications=true
# FeatureManagement__Modules.Messaging=true
# FeatureManagement__Modules.Announcements=true
# FeatureManagement__Modules.Inquiries=true
# FeatureManagement__Modules.ApiKeys=false
```

---

## 6. Start Infrastructure

```bash
ssh deploy@YOUR_SERVER_IP
cd /opt/wallow/prod

# Start Postgres + Valkey first
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d postgres valkey

# Wait for them to become healthy
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml ps
```

You should see both containers with `(healthy)` status. The PostgreSQL init script (`init-db.sql`) automatically creates the module schemas on first run.

If using the read replica:

```bash
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d postgres-replica

# The replica uses init-replica.sh to stream a base backup from the primary.
# Check logs if it doesn't become healthy:
docker logs wallow-prod-postgres-replica
```

---

## 7. Deploy the Application

### 7.1 Pull and Start

```bash
cd /opt/wallow/prod

# Pull the latest image
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml pull app

# Start the application
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d app
```

### 7.2 Watch Startup Logs

```bash
docker logs -f wallow-prod-app
```

### 7.3 First Run — Automatic Account & Client Creation

On first startup, the API runs a multi-step initialization sequence that automatically creates everything you need:

| Step | What Happens | Triggered By |
|------|-------------|--------------|
| 1. Database migration | EF Core creates all module schemas and tables | Always (first run) |
| 2. Default roles | Creates `admin`, `manager`, `user` roles | Always (idempotent) |
| 3. Admin account | Creates the initial admin user with email confirmed and `admin` role assigned | `AdminBootstrap__*` env vars |
| 4. OIDC clients | Creates/updates OAuth2 client applications in OpenIddict | `PreRegisteredClients__*` env vars |
| 5. Organizations | Auto-creates an organization for each client's `TenantName` | Client has `TenantName` set |
| 6. Org members | Adds users listed in `SeedMembers` to the organization | Client has `SeedMembers` set |

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

# Optionally register an OIDC client during setup
curl -X POST https://api.yourdomain.com/api/v1/identity/setup/clients \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "wallow-web-client",
    "redirectUris": ["https://app.yourdomain.com/signin-oidc"]
  }'

# Mark setup complete
curl -X POST https://api.yourdomain.com/api/v1/identity/setup/complete
```

**Recommendation for production:** Always set `AdminBootstrap__*` in your `.env` so the API is fully operational on first boot without manual intervention. The bootstrap is idempotent — if the admin already exists, it's skipped.

### 7.4 Account Auto-Creation Summary

| Method | When It Runs | What Gets Created |
|--------|-------------|-------------------|
| **AdminBootstrap** | First startup (if configured) | Admin user with confirmed email + admin role |
| **PreRegisteredClients + SeedMembers** | Every startup (sync) | OIDC clients, organizations, org memberships |
| **Self-Registration** | Anytime (always enabled) | Users register themselves via `/api/v1/identity/auth/register` |
| **External OAuth** (Google, Microsoft, GitHub, Apple) | Anytime (if configured) | Users auto-created on first external login |
| **SSO Auto-Provisioning** | Anytime (per-org SSO config) | Users auto-created from SAML/OIDC IdP (`AutoProvisionUsers=true` by default) |
| **SCIM Provisioning** | Anytime (per-org SCIM config) | Users created/updated/deactivated by external directory sync |
| **Invitations** | Anytime | Org admins invite users by email; user account created on acceptance |

Self-registration is always available — there is no global toggle to disable it. Forks can implement the `IRegistrationValidator` extension point to add domain restrictions or approval workflows.

### 7.5 Health Check

```bash
# From the server
curl http://localhost:8080/health/ready
```

Expected: `Healthy`

### 7.6 Using the Deploy Script

For subsequent deployments, use the included script:

```bash
# Deploy a specific version
bash /opt/wallow/scripts/deploy.sh prod v1.2.3

# Deploy latest
bash /opt/wallow/scripts/deploy.sh prod latest
```

The script pulls the new image, restarts only the app container (no infrastructure downtime), runs health checks, and automatically rolls back if the health check fails.

### 7.7 CI/CD Automated Deployment

The repository's CI/CD pipeline automates the full flow:

1. Push to `main` → release-please creates/updates a Release PR with changelog
2. Merge the Release PR → creates a git tag (`v1.2.3`)
3. Tag push triggers `publish.yml` → builds Docker image, scans with Trivy, pushes to GHCR with tags (`1.2.3`, `1.2`, `latest`)

To deploy the published image to your server, SSH in and run:

```bash
bash /opt/wallow/scripts/deploy.sh prod v1.2.3
```

Or set up a webhook/GitHub Action to SSH and deploy automatically.

---

## 8. Configure Reverse Proxy (HTTPS)

### 8.1 Install Caddy

```bash
# As root on the server
apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
apt update && apt install caddy
```

### 8.2 Configure Caddyfile

```bash
nano /etc/caddy/Caddyfile
```

```caddyfile
# Wallow API (OIDC provider + REST API)
api.yourdomain.com {
    reverse_proxy localhost:8080
}

# Wallow Auth (login/register UI)
auth.yourdomain.com {
    reverse_proxy localhost:8090
}

# Wallow Web (dashboard)
app.yourdomain.com {
    reverse_proxy localhost:8070
}
```

```bash
systemctl restart caddy
```

Caddy automatically provisions Let's Encrypt TLS certificates. Ensure ports 80 and 443 are open and DNS A records point to your server IP.

### 8.3 Alternative: Nginx or Traefik

If you prefer Nginx or Traefik, the key is to proxy these three endpoints:

| Domain | Backend |
|--------|---------|
| `api.yourdomain.com` | `http://localhost:8080` |
| `auth.yourdomain.com` | `http://localhost:8090` |
| `app.yourdomain.com` | `http://localhost:8070` |

Ensure WebSocket support is enabled for the API (SignalR hub at `/hubs/realtime`).

---

## 9. Register OIDC Clients

OIDC clients are registered automatically via the `PreRegisteredClients` environment variables (see [Section 5.3](#53-oidc-client-registration-pre-registered-clients)).

### How It Works

On every startup, the `PreRegisteredClientSyncService` runs and:

1. **Creates or updates** each client defined in `PreRegisteredClients__Clients__N__*`
2. **Creates organizations** from each client's `TenantName` (if the org doesn't exist)
3. **Adds seed members** to the organization
4. **Removes** clients that were previously synced from config but are no longer present (cleanup)

This is fully idempotent — safe to restart without duplicating data.

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
| Billing | `billing.read`, `billing.manage`, `invoices.read`, `invoices.manage`, `payments.read`, `payments.manage`, `subscriptions.read`, `subscriptions.manage` |
| Identity | `users.read`, `users.write`, `users.manage`, `roles.read`, `roles.manage`, `organizations.read`, `organizations.write`, `organizations.manage` |
| Storage | `storage.read`, `storage.write` |
| Communications | `messaging.read`, `messaging.write`, `announcements.read`, `announcements.manage`, `notifications.read`, `notifications.manage` |
| Platform | `apikeys.manage`, `sso.read`, `sso.manage`, `scim.manage`, `configuration.read`, `configuration.manage`, `serviceaccounts.read`, `serviceaccounts.manage`, `webhooks.read`, `webhooks.manage` |

---

## 10. Connect Wallow.Web (Dashboard)

Wallow.Web is a Blazor Server app that authenticates users via OpenID Connect against the Wallow API.

### 10.1 Build and Deploy

The Web app uses the same Dockerfile with different build args:

```bash
# Build the Web image
docker build \
  --build-arg BUILD_PROJECT=src/Wallow.Web/Wallow.Web.csproj \
  --build-arg ENTRYPOINT_DLL=Wallow.Web.dll \
  -t ghcr.io/bc-solutions-coder/wallow-web:latest .
```

### 10.2 Run the Container

Add to your compose or run directly:

```bash
docker run -d \
  --name wallow-prod-web \
  --network wallow \
  -p 8070:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ServiceUrls__ApiUrl=https://api.yourdomain.com \
  -e ServiceUrls__AuthUrl=https://auth.yourdomain.com \
  -e ServiceUrls__WebUrl=https://app.yourdomain.com \
  -e Oidc__Authority=https://api.yourdomain.com \
  -e Oidc__ClientId=wallow-web-client \
  -e Oidc__ClientSecret=<same-secret-from-PreRegisteredClients> \
  ghcr.io/bc-solutions-coder/wallow-web:latest
```

### 10.3 Verify

Visit `https://app.yourdomain.com`. You should be redirected to the API's OIDC authorization endpoint, which renders the login form. After logging in, you're redirected back to the dashboard.

---

## 11. Connect Wallow.Auth (Login UI)

Wallow.Auth is a Blazor Server app that provides the login, registration, and password reset UI. It communicates with the API via HTTP (not OIDC).

### 11.1 Build and Deploy

```bash
docker build \
  --build-arg BUILD_PROJECT=src/Wallow.Auth/Wallow.Auth.csproj \
  --build-arg ENTRYPOINT_DLL=Wallow.Auth.dll \
  -t ghcr.io/bc-solutions-coder/wallow-auth:latest .
```

### 11.2 Run the Container

```bash
docker run -d \
  --name wallow-prod-auth \
  --network wallow \
  -p 8090:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ApiBaseUrl=https://api.yourdomain.com \
  -e ServiceUrls__ApiUrl=https://api.yourdomain.com \
  -e ServiceUrls__AuthUrl=https://auth.yourdomain.com \
  -e ServiceUrls__WebUrl=https://app.yourdomain.com \
  ghcr.io/bc-solutions-coder/wallow-auth:latest
```

### 11.3 Verify

Visit `https://auth.yourdomain.com`. You should see the branded login page.

---

## 12. Connect External Clients

### 12.1 Getting a Token (Simple Token Proxy)

For quick testing or simple integrations, use the token proxy endpoint:

```bash
curl -X POST https://api.yourdomain.com/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email": "admin@yourdomain.com", "password": "your-admin-password"}'
```

Response:

```json
{
  "access_token": "eyJhbGciOi...",
  "refresh_token": "eyJhbGciOi...",
  "token_type": "Bearer",
  "expires_in": 900
}
```

### 12.2 Using API Keys (Backend Services)

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

### 12.3 OAuth2 Authorization Code Flow (SPAs / Mobile)

Register your app as a pre-registered client (see [Section 5.3](#53-oidc-client-registration-pre-registered-clients)), then use standard OIDC libraries:

**Discovery endpoint:** `https://api.yourdomain.com/.well-known/openid-configuration`

```typescript
// Example: SPA using oidc-client-ts
import { UserManager } from 'oidc-client-ts';

const mgr = new UserManager({
  authority: 'https://api.yourdomain.com',
  client_id: 'my-spa-client',
  redirect_uri: 'https://myapp.com/callback',
  post_logout_redirect_uri: 'https://myapp.com/',
  scope: 'openid email profile roles offline_access',
  response_type: 'code',
});

// Login
await mgr.signinRedirect();

// After redirect back
const user = await mgr.signinRedirectCallback();
console.log(user.access_token);
```

### 12.4 Client Credentials Flow (Service Accounts)

For backend-to-backend communication without user interaction:

```bash
curl -X POST https://api.yourdomain.com/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=sa-my-backend" \
  -d "client_secret=<your-secret>" \
  -d "scope=billing.read billing.manage"
```

### 12.5 Making Authenticated API Requests

```bash
# With JWT token
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  https://api.yourdomain.com/api/billing/invoices

# With API key
curl -H "X-Api-Key: sk_live_..." \
  https://api.yourdomain.com/api/billing/invoices
```

### 12.6 SignalR Real-Time Updates

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

## 13. Verify Everything Works

### 13.1 Health Checks

```bash
# Full health (all dependencies)
curl https://api.yourdomain.com/health

# Readiness (Postgres + Valkey + Hangfire)
curl https://api.yourdomain.com/health/ready

# Liveness (app is responsive)
curl https://api.yourdomain.com/health/live
```

### 13.2 API Info

```bash
curl https://api.yourdomain.com/
```

Expected:

```json
{
  "name": "Wallow API",
  "version": "1.0.0",
  "environment": "Production",
  "documentation": "/scalar/v1",
  "health": "/health",
  "backgroundJobs": "/hangfire"
}
```

### 13.3 API Documentation

Visit `https://api.yourdomain.com/scalar/v1` for interactive API docs (Scalar).

### 13.4 Test the Login Flow

1. Visit `https://app.yourdomain.com`
2. You should be redirected to the OIDC login page
3. Log in with your admin credentials (`AdminBootstrap__Email` / `AdminBootstrap__Password`)
4. You should land on the dashboard

### 13.5 Check Background Jobs

Visit `https://api.yourdomain.com/hangfire` (requires admin auth) to see:
- System heartbeat (every 5 minutes)
- Email retry (every 5 minutes)
- Token pruning (every 4 hours)
- Invitation pruning (hourly)

---

## 14. Ongoing Operations

### 14.1 Viewing Logs

```bash
# Application logs
docker logs wallow-prod-app -f --tail 100

# All services
cd /opt/wallow/prod
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml logs -f
```

### 14.2 Deploying Updates

```bash
# Using the deploy script (recommended — includes health checks + rollback)
bash /opt/wallow/scripts/deploy.sh prod v1.3.0

# Manual
cd /opt/wallow/prod
# Edit .env: APP_TAG=v1.3.0
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml pull app
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d --no-deps app
```

### 14.3 Rollback

The deploy script automatically rolls back on health check failure. For manual rollback:

```bash
cd /opt/wallow/prod
# Edit .env: APP_TAG=v1.2.0 (the previous working version)
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml pull app
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d --no-deps app
```

### 14.4 Database Backups

```bash
# Backup
docker exec wallow-prod-postgres pg_dump -U wallow wallow > backup_$(date +%Y%m%d).sql

# Restore
docker exec -i wallow-prod-postgres psql -U wallow wallow < backup_20260326.sql
```

### 14.5 Adding a New OIDC Client

1. Add `PreRegisteredClients__Clients__N__*` variables to your `.env` file
2. Restart the app container:

```bash
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml restart app
```

The `PreRegisteredClientSyncService` will create the new client on startup.

### 14.6 Scaling

- **Database:** Use managed PostgreSQL (AWS RDS, Hetzner Cloud, Supabase)
- **Cache:** Use managed Redis/Valkey
- **App instances:** Run multiple API containers behind a load balancer (SignalR uses Valkey backplane for multi-instance support)
- **Storage:** Use managed S3 (AWS, Cloudflare R2) instead of local filesystem

---

## 15. Troubleshooting

### App Won't Start

```bash
# Check logs for startup errors
docker logs wallow-prod-app --tail 200

# Common causes:
# - Missing Identity__SigningKey
# - Wrong database password
# - Database not reachable (Postgres not started)
```

### Health Check Fails

```bash
# Check what's unhealthy
curl -s http://localhost:8080/health | python3 -m json.tool

# Check individual services
docker exec wallow-prod-postgres pg_isready -U wallow
docker exec wallow-prod-valkey valkey-cli -a YOUR_VALKEY_PASSWORD ping
```

### OIDC Login Redirect Fails

- Verify `ServiceUrls__ApiUrl` matches your public domain exactly
- Verify `PreRegisteredClients__Clients__0__RedirectUris__0` matches the callback URL exactly
- Check CORS origins include all frontend URLs
- Ensure `Authentication__CookieDomain` is set to `.yourdomain.com`

### Docker Pull Fails

```bash
# Re-authenticate with GHCR
echo "YOUR_GITHUB_PAT" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin

# Verify the image exists
docker pull ghcr.io/bc-solutions-coder/wallow:latest
```

### Database Connection Issues

```bash
# Test from inside the Docker network
docker exec wallow-prod-app curl -sf http://localhost:8080/health/ready

# Check Postgres logs
docker logs wallow-prod-postgres --tail 50
```

### Reset Everything (Destroys All Data)

```bash
cd /opt/wallow/prod
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml down -v
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d
```

---

## Quick Reference

### Ports

| Environment | API Port | Auth Port | Web Port |
|-------------|----------|-----------|----------|
| Production | 8080 | 8090 | 8070 |
| Staging | 8082 | — | — |
| Development | 8081 | — | — |

### Key URLs

| Endpoint | URL |
|----------|-----|
| API | `https://api.yourdomain.com` |
| Auth UI | `https://auth.yourdomain.com` |
| Web Dashboard | `https://app.yourdomain.com` |
| API Docs | `https://api.yourdomain.com/scalar/v1` |
| Health Check | `https://api.yourdomain.com/health` |
| Background Jobs | `https://api.yourdomain.com/hangfire` |
| OIDC Discovery | `https://api.yourdomain.com/.well-known/openid-configuration` |

### Deploy Commands

```bash
# Deploy specific version
bash /opt/wallow/scripts/deploy.sh prod v1.0.0

# View app logs
docker logs wallow-prod-app -f

# Restart app
cd /opt/wallow/prod
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml restart app

# Check status
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml ps
```
