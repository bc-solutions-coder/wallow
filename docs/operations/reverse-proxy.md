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

| Public path | Internal target    | Prefix handling                                                                                                            |
| ----------- | ------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| `/api/*`    | `wallow-api:8080`  | Forward the full path; the API strips `/api` itself via `PathBase=/api`. **Do not** strip in the proxy.                    |
| `/auth/*`   | `wallow-auth:8080` | Forward the full path; the auth app is **built** with `AUTH_BASE_PATH=/auth` and serves under it. **Do not** strip either. |
| `/*`        | `wallow-web:8080`  | The Node web app serves at root (catch-all).                                                                               |

**The proxy strips nothing.** Both prefixed services rebase themselves — the API at runtime via
`PathBase`, the auth app at build time via `AUTH_BASE_PATH` — so a proxy that removes a prefix
breaks the service behind it.

**Routing precedence:** the `/api` and `/auth` prefixes must be evaluated before the catch-all
`/*` rule, and prefix matches must respect segment boundaries so that `/apidocs` and
`/authentication` fall through to the web app rather than matching `/api` or `/auth`.

> **Reference implementation.** `docker/caddy/Caddyfile.example` is a working, validated
> version of this topology, wired into `docker/docker-compose.production.yml` as the `caddy`
> service. Copy it (`cp caddy/Caddyfile.example caddy/Caddyfile`) and point
> `CADDYFILE_HOST_PATH` at your copy rather than starting from scratch.

### Subdomain routing

Set **both** `API_PATH_BASE=` and `AUTH_BASE_PATH=` (empty) in `.env.production` and route by
host instead:

| Public host          | Internal target    | Notes                                            |
| -------------------- | ------------------ | ------------------------------------------------ |
| `api.example.com/*`  | `wallow-api:8080`  | API serves at subdomain root (`PathBase` empty). |
| `auth.example.com/*` | `wallow-auth:8080` | Node auth app serves at root.                    |
| `example.com/*`      | `wallow-web:8080`  | Node web app serves at root.                     |

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
reads only three environment variables (`PORT`, `HOST`, `WALLOW_API_INTERNAL_URL`).

```bash
PORT=8080
# Upstream the app reverse-proxies /v1/**, /connect/**, /.well-known/** to (container-to-container)
WALLOW_API_INTERNAL_URL=http://wallow-api:8080
```

Under path-based routing the app serves everything — SSR HTML, client assets, and its `/v1`,
`/connect`, and `/.well-known` passthrough routes — under the `/auth` prefix, so the proxy
forwards the full path unchanged.

> **`AUTH_BASE_PATH` is a build input, not a runtime variable.** `vite build` bakes it into
> every emitted asset URL, so the prefix is fixed when the image is built and setting it in the
> container environment does nothing. Build with `--build-arg AUTH_BASE_PATH=/auth` (the
> production compose file does this for you, which is why path-based deployments must
> `up --build` rather than `pull`). The published `ghcr.io/bc-solutions-coder/wallow-auth`
> image is built at root, so it suits subdomain routing as-is; served under `/auth` it returns
> HTML pointing at `/assets/*` and every asset 404s.

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
of `https://`, causing authentication failures. The auth app's passthrough server routes append
the real client IP to `X-Forwarded-For` on the requests they tunnel, so an outer ingress's
leftmost entry survives the hop.

---

## 5. Health Check Endpoints

| Service     | Internal URL                          |
| ----------- | ------------------------------------- |
| Wallow.Api  | `http://wallow-api:8080/health/ready` |
| wallow-auth | `http://wallow-auth:8080/health`      |
| wallow-web  | `http://wallow-web:8080/health`       |

Configure your proxy or container orchestrator to poll these. A `200 OK` means the service is
ready to serve traffic.

---

## 6. Proxy Configuration Examples

The examples below show path-based routing. The key rule: **forward the full path and strip
nothing** — the API rebases itself via `PathBase`, and the auth app is built with
`AUTH_BASE_PATH=/auth`. `docker/caddy/Caddyfile.example` is the maintained, validated version of
the Caddy config below.

### nginx

```nginx
server {
    listen 443 ssl;
    server_name example.com;

    ssl_certificate     /etc/letsencrypt/live/example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;

    # API — forward the full path (the app strips /api via PathBase).
    # The (/|$) regex keeps the match on a segment boundary, so /apidocs falls
    # through to the web app instead of being routed here.
    location ~ ^/api(/|$) {
        proxy_pass         http://wallow-api:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host  $host;
    }

    # Auth — also forwarded unstripped; the app is built with AUTH_BASE_PATH=/auth
    # and serves its HTML, assets, and passthrough routes under that prefix.
    location ~ ^/auth(/|$) {
        proxy_pass         http://wallow-auth:8080;
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
	encode zstd gzip

	# API — forward the full path (PathBase=/api strips it inside the app)
	@api path /api /api/*
	handle @api {
		reverse_proxy wallow-api:8080
	}

	# Auth — also forwarded unstripped; the image is built with AUTH_BASE_PATH=/auth
	@auth path /auth /auth/*
	handle @auth {
		reverse_proxy wallow-auth:8080
	}

	# Web (catch-all)
	handle {
		reverse_proxy wallow-web:8080
	}

	# Caddy handles TLS automatically via Let's Encrypt
}
```

> **Use `handle`, never `handle_path`.** In Caddy, `handle_path` strips the matched prefix while
> `handle` preserves it, and neither prefix may be stripped here. The `path /api /api/*` matcher
> (rather than `/api*`) is what keeps the match on a segment boundary, so `/apidocs` and
> `/authentication` reach the web app.

> **No `header_up` needed.** Caddy's `reverse_proxy` sets `X-Forwarded-For`,
> `X-Forwarded-Proto`, and `X-Forwarded-Host` on every upstream request by default; adding
> explicit directives is redundant and `caddy validate` warns about it. That default is also the
> contract any replacement proxy must reproduce — the nginx example above sets the same three
> headers by hand. Note that Caddy at the edge sets `X-Forwarded-Proto` from the connection it
> actually served and discards what the client sent, so putting it **behind** another TLS
> terminator makes it forward `http` and breaks every OIDC redirect; in that topology declare
> the outer proxy trusted with `servers { trusted_proxies static <ranges> }`.

In nginx, the equivalent rule is `proxy_pass` **without** a URI part (no trailing slash, no
path): that forwards the original request URI untouched. Adding a trailing slash —
`proxy_pass http://wallow-auth:8080/;` — is what strips the prefix, and it must not be used
here.

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

| Mistake                                                  | Symptom                                      | Fix                                                                                 |
| -------------------------------------------------------- | -------------------------------------------- | ----------------------------------------------------------------------------------- |
| Proxy strips `/api`                                      | API routes return 404                        | Forward `/api` unstripped; the app removes it via `PathBase=/api`                   |
| Proxy strips `/auth`                                     | Auth app routes 404                          | Forward `/auth` unstripped (no trailing slash on nginx `proxy_pass`; Caddy `handle`, not `handle_path`) |
| Auth image built without `AUTH_BASE_PATH=/auth`          | Auth page loads but every asset 404s under `/auth` | Rebuild with `--build-arg AUTH_BASE_PATH=/auth` (`up --build`, not `pull`)     |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` missing on the API | OIDC redirects use `http://`; login fails    | Set it on the API service                                                           |
| `OpenIddict__Issuer` / `OIDC_ISSUER` mismatch            | `redirect_uri` or issuer errors during login | Point both at the public API URL                                                    |
| Redirect URIs not updated                                | OIDC login returns `redirect_uri mismatch`   | Update the seeded client redirect URIs to the public `https://example.com/...` URLs |
