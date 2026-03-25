# Dynamic Client Registration (DCR) Integration Guide

This guide explains how external applications register as OAuth2 clients with Wallow using OpenIddict's Dynamic Client Registration (RFC 7591).

## Overview

Instead of manually configuring clients, your app self-registers at startup via a single HTTP call. After registration, you use standard `client_credentials` flow to obtain tokens and call the Wallow API.

## Quick Start (Development)

### 1. Register Your Client

In development, no authentication is required (Trusted Hosts policy allows localhost).

```bash
curl -s -X POST http://localhost:5000/connect/dcr \
  -H 'Content-Type: application/json' \
  -d '{
    "client_id": "sa-my-app",
    "client_name": "My App",
    "grant_types": ["client_credentials"],
    "token_endpoint_auth_method": "client_secret_post"
  }'
```

**Response:**

```json
{
  "client_id": "sa-my-app",
  "client_secret": "generated-secret-value",
  "registration_access_token": "one-time-management-token",
  "grant_types": ["client_credentials"],
  "token_endpoint_auth_method": "client_secret_post"
}
```

Save `client_id`, `client_secret`, and `registration_access_token`. The registration access token is single-use and required for updating or deleting the client later.

### 2. Get an Access Token

```bash
curl -s -X POST http://localhost:5000/connect/token \
  -d 'grant_type=client_credentials' \
  -d 'client_id=sa-my-app' \
  -d 'client_secret=generated-secret-value'
```

**Response:**

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "expires_in": 300,
  "token_type": "Bearer"
}
```

### 3. Call the Wallow API

```bash
curl -s http://localhost:5000/api/v1/inquiries \
  -H 'Authorization: Bearer eyJhbGciOiJSUzI1NiIs...' \
  -H 'X-Tenant-Id: <tenant-guid>'
```

Service accounts are tenant-agnostic. For tenant-scoped endpoints, pass the tenant ID via the `X-Tenant-Id` header.

## The `sa-` Prefix Requirement

**Your `client_id` must start with `sa-`** (e.g., `sa-my-app`, `sa-personal-site`).

Wallow's middleware uses this prefix to determine how permissions are resolved:

- `sa-*` clients get **scope-based** permission expansion (service account flow)
- All other clients get **role-based** permission expansion (user flow)

If you register without the `sa-` prefix, your client will be treated as a regular user with no roles assigned, resulting in **403 Forbidden on every API request**. This is by design — the prefix is a fail-safe, not just a convention.

## The `aud: wallow-api` Claim

Every access token issued by OpenIddict includes `aud: wallow-api` via the default resource configuration. The Wallow API validates this claim on every request. You do not need to configure anything — all registered clients inherit this automatically.

If your token is rejected with an audience error, verify the OpenIddict resource configuration includes the `wallow-api` audience.

## Scopes and Permissions

DCR-registered clients start with **zero functional permissions**. The only default scope is `wallow-api-audience`, which handles JWT audience validation but grants no API access.

To grant your client access to specific APIs, an admin must assign scopes. Available scopes include:

| Scope | Grants |
|-------|--------|
| `inquiries.read` | Read inquiries |
| `inquiries.write` | Create/update inquiries |

Scopes are assigned via:
- **Wallow API:** `PUT /api/v1/identity/service-accounts/{id}/scopes`

Until scopes are assigned, all API calls return **403 Forbidden**.

## Production Registration

In production, anonymous registration is disabled. You must include an Initial Access Token:

```bash
curl -s -X POST https://api.yourdomain.com/connect/dcr \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer <initial-access-token>' \
  -d '{
    "client_id": "sa-my-app",
    "client_name": "My App",
    "grant_types": ["client_credentials"],
    "token_endpoint_auth_method": "client_secret_post"
  }'
```

Initial Access Tokens are created by admins via the Wallow API. They are single-use and time-limited.

## BFF Environment Variables

After registration, configure your BFF with these environment variables:

```env
WALLOW_CLIENT_ID=sa-my-app
WALLOW_CLIENT_SECRET=<from DCR response>
WALLOW_TOKEN_URL=http://localhost:5000/connect/token
```

For automatic registration on first startup:

```env
WALLOW_DCR_URL=http://localhost:5000/connect/dcr
WALLOW_REGISTRATION_TOKEN=<initial-access-token, required in prod>
```

## Full Example: Register, Authenticate, and Submit an Inquiry

```bash
# Step 1: Register the client
RESPONSE=$(curl -s -X POST http://localhost:5000/connect/dcr \
  -H 'Content-Type: application/json' \
  -d '{
    "client_id": "sa-my-app",
    "client_name": "My App",
    "grant_types": ["client_credentials"],
    "token_endpoint_auth_method": "client_secret_post"
  }')

CLIENT_ID=$(echo "$RESPONSE" | jq -r '.client_id')
CLIENT_SECRET=$(echo "$RESPONSE" | jq -r '.client_secret')

echo "Registered: $CLIENT_ID"

# Step 2: Get an access token
TOKEN=$(curl -s -X POST http://localhost:5000/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  | jq -r '.access_token')

echo "Token acquired"

# Step 3: Call the API (requires admin to assign inquiries.write scope first)
curl -s -X POST http://localhost:5000/api/v1/inquiries \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Tenant-Id: <tenant-guid>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Jane Doe",
    "email": "jane@example.com",
    "message": "Hello from my app"
  }'
```

## Credential Management

| Scenario | Action |
|----------|--------|
| Normal restart | Use cached `client_id` and `client_secret`. No re-registration needed. |
| Lost credentials | Re-register with a new Initial Access Token. Ask admin to clean up the orphaned client. |
| Secret rotation | Admin rotates via `POST /api/v1/identity/service-accounts/{id}/rotate-secret`. |

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| 403 on all API calls | Missing `sa-` prefix on `client_id` | Re-register with `sa-` prefix (e.g., `sa-my-app`) |
| 403 on specific endpoint | No scopes assigned | Ask admin to assign required scopes (e.g., `inquiries.write`) |
| 401 Unauthorized | Expired or invalid token | Request a new token via the token endpoint |
| Token missing `aud: wallow-api` | Resource configuration missing | Verify OpenIddict resource configuration includes `wallow-api` audience |
| Registration rejected (401) | Missing Initial Access Token (prod) | Get a token from admin via the Wallow API |
| Registration rejected (403) | Untrusted host | Register from localhost in dev, or use Initial Access Token in prod |
