# Reverse Proxy Deployment

Wallow supports deployment behind a reverse proxy. The stack is three services — the .NET API
plus two Node (TanStack Start) apps — and TLS terminates at the proxy while each service runs
plain HTTP internally. Two topologies are supported: path-based routing under a single domain, or
subdomain routing.

---

## Table of Contents

1. [Routing Overview](#1-routing-overview)
2. [Required Configuration Per Service](#2-required-configuration-per-service)
3. [TLS Termination](#3-tls-termination)
4. [Forwarded Headers](#4-forwarded-headers)
5. [Health Check Endpoints](#5-health-check-endpoints)
6. [Proxy Configuration Examples](#6-proxy-configuration-examples)
7. [Seeding the Production Client](#7-seeding-the-production-client)

---

## 1. Routing Overview

### Path-based routing (default)

Route incoming requests to each service based on path prefix. All services expose HTTP on port
`8080` inside the container network:

| Public path | Internal target | Prefix handling |
|-------------|-----------------|-----------------|
| `/api/*` | `wallow-api:8080` | Forward the full path; the API strips `/api` itself via `PathBase=/api`. **Do not** strip in the proxy. |
| `/auth/*` | `wallow-auth:8080` | The Node auth app has no path-base support and serves at root, so the proxy **must strip** the `/auth` prefix. |
| `/*` | `wallow-web:8080` | The Node web app serves at root (catch-all). |

**Routing precedence:** the `/api` and `/auth` prefixes must be evaluated before the catch-all
`/*` rule.

### Subdomain routing

Set `API_PATH_BASE=` (empty) in `.env.production` and route by host instead:

| Public host | Internal target | Notes |
|-------------|-----------------|-------|
| `api.example.com/*` | `wallow-api:8080` | API serves at subdomain root (`PathBase` empty). |
| `auth.example.com/*` | `wallow-auth:8080` | Node auth app serves at root. |
| `example.com/*` | `wallow-web:8080` | Node web app serves at root. |

With subdomains, align `API_PUBLIC_URL` / `AUTH_PUBLIC_URL` (and `COOKIE_DOMAIN`) to the
subdomains.

---

## 2. Required Configuration Per Service

Set these environment variables for each service when running behind a proxy.

### Wallow.Api (.NET)

```bash
# Strip /api prefix before ASP.NET Core route matching (leave empty for subdomain routing)
PathBase=/api

# The public-facing base URL including the path prefix; used to build redirect/link URLs
API_PUBLIC_URL=https://example.com/api

# OIDC issuer must match the browser-facing API URL
OpenIddict__Issuer=https://example.com/api

# CORS must allow the public origin of any browser client
Cors__AllowedOrigins__0=https://example.com

# Cookie domain for cross-origin auth cookies (see note below)
Authentication__CookieDomain=example.com
```

> **Cookie domain (`Authentication:CookieDomain`).** The API reads this key (double underscore in
> env-var form, `Authentication:CookieDomain` in `appsettings.json`) to scope its auth cookies. For
> **path-based routing** under one domain, set it to that bare host (`example.com`). For **subdomain
> routing** where the API, auth, and web apps live on sibling subdomains (`api.example.com`,
> `auth.example.com`, `example.com`), set it to a leading-dot parent domain (`.example.com`) so the
> cookie is shared across them — this is what the committed `appsettings.json` uses (`.wallow.dev`).
> Leave it empty for local development (`appsettings.Development.json` ships it blank).

### wallow-auth (Node — apps/wallow-auth)

The auth app is a pure same-origin reverse proxy: it holds no session and no cookie jar, and it
reads only three environment variables (`PORT`, `HOST`, `WALLOW_API_INTERNAL_URL`). It has **no**
path-base support and serves at root, so the proxy must strip the `/auth` prefix under path-based
routing.

```bash
PORT=8080
# Upstream the app reverse-proxies /v1/**, /connect/**, /.well-known/** to (container-to-container)
WALLOW_API_INTERNAL_URL=http://wallow-api:8080
```

### wallow-web (Node BFF — apps/wallow-web)

The web app is a BFF: its server holds the OIDC token set and proxies `/api/**` to the API. It
serves at root.

```bash
PORT=8080
# Browser-facing issuer (must match the API's OpenIddict issuer for redirects)
OIDC_ISSUER=https://example.com/api
# Container-reachable discovery URL (avoids a hairpin back through the proxy)
OIDC_METADATA_URL=http://wallow-api:8080/.well-known/openid-configuration
OIDC_CLIENT_ID=wallow-web-client
OIDC_CLIENT_SECRET=your-secret
OIDC_REDIRECT_URI=https://example.com/bff/callback
# Where the browser lands after logout (must be a registered post-logout redirect URI)
OIDC_POST_LOGOUT_REDIRECT_URI=https://example.com/
# Downstream API for the /api reverse proxy (container-to-container)
BFF_API_BASE_URL=http://wallow-api:8080
# Secret (32+ chars) that seals/unseals the session and transaction cookies
COOKIE_PASSWORD=a-32-plus-character-random-secret
```

These are the seven required BFF variables. `OIDC_CLIENT_SECRET` and `COOKIE_PASSWORD` are
confidential — set them from the container environment or a secrets manager, never in source
control. The full variable reference (including the optional `OIDC_SCOPES`, `COOKIE_SECURE`, and
`SESSION_TTL_SECONDS` knobs) lives in the
[TypeScript SDK guide](../integrations/typescript-sdk.md#environment-variables).

> **Local development:** no proxy configuration is needed. Leave `PathBase` empty on the API and
> run the apps directly. See the [Developer Guide](../getting-started/developer-guide.md) for
> local setup.

---

## 3. TLS Termination

The proxy accepts HTTPS from clients and forwards plain HTTP to each service internally. The
services do not need certificates.

Because the proxy terminates TLS, the API sees incoming requests as `http://` even though clients
connected over `https://`. Forwarded-headers handling (next section) restores the original scheme
so that redirect URIs, the OIDC issuer URL, and cookie `Secure` flags all work correctly.

---

## 4. Forwarded Headers

Enable ASP.NET Core's forwarded-headers handling on the API so it reconstructs the original
scheme and host from `X-Forwarded-Proto` / `X-Forwarded-Host`:

```bash
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
```

Without it, the API generates OIDC discovery documents and redirect URIs with `http://` instead
of `https://`, causing authentication failures. The auth app's h3 proxy forwards the real client
IP to the API on the requests it tunnels.

---

## 5. Health Check Endpoints

| Service | Internal URL |
|---------|-------------|
| Wallow.Api | `http://wallow-api:8080/health/ready` |
| wallow-auth | `http://wallow-auth:8080/health` |
| wallow-web | `http://wallow-web:8080/health` |

Configure your proxy or container orchestrator to poll these. A `200 OK` means the service is
ready to serve traffic.

---

## 6. Proxy Configuration Examples

The examples below show path-based routing. The key rules: forward `/api` unstripped (the API
handles its own `PathBase`), and **strip** `/auth` for the Node auth app.

### nginx

```nginx
server {
    listen 443 ssl;
    server_name example.com;

    ssl_certificate     /etc/letsencrypt/live/example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;

    # API — forward the full path (the app strips /api via PathBase)
    location /api {
        proxy_pass         http://wallow-api:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host  $host;
    }

    # Auth — strip the /auth prefix (trailing slash on proxy_pass) for the Node app
    location /auth/ {
        proxy_pass         http://wallow-auth:8080/;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host  $host;
    }

    # Web (catch-all) — must be last
    location / {
        proxy_pass         http://wallow-web:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host  $host;
    }
}

# Redirect HTTP to HTTPS
server {
    listen 80;
    server_name example.com;
    return 301 https://$host$request_uri;
}
```

### Caddy

```caddy
example.com {
    # API — forward the full path
    handle /api* {
        reverse_proxy wallow-api:8080 {
            header_up X-Forwarded-Proto {scheme}
            header_up X-Forwarded-Host  {host}
        }
    }

    # Auth — strip the /auth prefix for the Node app
    handle_path /auth* {
        reverse_proxy wallow-auth:8080 {
            header_up X-Forwarded-Proto {scheme}
            header_up X-Forwarded-Host  {host}
        }
    }

    # Web (catch-all)
    handle {
        reverse_proxy wallow-web:8080 {
            header_up X-Forwarded-Proto {scheme}
            header_up X-Forwarded-Host  {host}
        }
    }

    # Caddy handles TLS automatically via Let's Encrypt
}
```

> In Caddy, `handle_path` strips the matched prefix while `handle` preserves it — that is why
> `/auth` uses `handle_path` (strip) and `/api` uses `handle` (preserve).

---

## 7. Seeding the Production Client

The reference frontend (`apps/wallow-web`) authenticates as a **confidential** OIDC client. The
seeder (`Wallow.SeederService`, reading `api/seed.json`) provisions that client. In production, the
key rule is that **the issuer is the API origin** while **the login UX is served from the auth
origin** — the client's redirect and post-logout URIs point at your public web app, and its
`OIDC_ISSUER` (above) points at the API.

Add a confidential client to the `clients` array of `api/seed.json`. `api/seed.json` already ships a
commented `_productionExampleClients` entry (ignored by the seeder) you can copy from:

```json
{
  "clientId": "wallow-web-client",
  "displayName": "Wallow Web",
  "tenantName": "Wallow",
  "seedMembers": ["admin@example.com"],
  "secret": "replace-with-a-strong-generated-secret",
  "redirectUris": ["https://example.com/bff/callback"],
  "postLogoutRedirectUris": ["https://example.com/"],
  "scopes": [
    "openid",
    "email",
    "profile",
    "roles",
    "offline_access",
    "inquiries.read",
    "inquiries.write",
    "notifications.read",
    "notifications.write"
  ]
}
```

Rules that make this a valid production client:

- **`secret`** — present (the client is confidential). Use a strong, randomly generated value and
  wire the **same** value into the BFF's `OIDC_CLIENT_SECRET`. Never commit a real secret; keep the
  seed value a placeholder and inject the real one at deploy time.
- **`redirectUris` / `postLogoutRedirectUris`** — absolute HTTPS URLs on your public web origin.
  They must exactly match the BFF's `OIDC_REDIRECT_URI` and `OIDC_POST_LOGOUT_REDIRECT_URI`. Because
  the API validates registered URIs at seed time, adding a client for a new domain needs no source
  change.
- **`scopes`** — `openid`, `email`, `profile`, and `offline_access` for login, plus whichever API
  scopes the app calls.

`api/seed.json` is marked `merge=ours` in `.gitattributes`, so your fork's production seed survives
upstream merges. See the [Configuration guide](../getting-started/configuration.md) for the full
seed schema.

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Proxy strips `/api` | API routes return 404 | Forward `/api` unstripped; the app removes it via `PathBase=/api` |
| Proxy does **not** strip `/auth` | Auth app assets/routes 404 | Strip the `/auth` prefix (nginx trailing slash, Caddy `handle_path`) |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` missing on the API | OIDC redirects use `http://`; login fails | Set it on the API service |
| `OpenIddict__Issuer` / `OIDC_ISSUER` mismatch | `redirect_uri` or issuer errors during login | Point both at the public API URL |
| Redirect URIs not updated | OIDC login returns `redirect_uri mismatch` | Update the seeded client redirect URIs to the public `https://example.com/...` URLs |
