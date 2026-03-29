# Client Registration Integration Guide

This guide explains how external applications register as OAuth2 clients with Wallow and obtain API access tokens.

## Overview

External applications register via the client registration endpoint, then use the standard `client_credentials` flow to obtain tokens and call the Wallow API.

## Quick Start (Development)

### 1. Register Your Client

In development, no authentication is required.

```bash
curl -s -X POST http://localhost:5001/api/v1/identity/register \
  -H 'Content-Type: application/json' \
  -d '{
    "clientId": "sa-my-app",
    "clientName": "My App",
    "grantTypes": ["client_credentials"],
    "scopes": []
  }'
```

**Response:**

```json
{
  "clientId": "sa-my-app",
  "clientSecret": "generated-secret-value"
}
```

Save `clientId` and `clientSecret`.

### 2. Get an Access Token

```bash
curl -s -X POST http://localhost:5001/connect/token \
  -d 'grant_type=client_credentials' \
  -d 'client_id=sa-my-app' \
  -d 'client_secret=generated-secret-value'
```

### 3. Call the Wallow API

```bash
curl -s http://localhost:5001/api/v1/inquiries \
  -H 'Authorization: Bearer <access-token>' \
  -H 'X-Tenant-Id: <tenant-guid>'
```

Service accounts are tenant-agnostic. For tenant-scoped endpoints, pass the tenant ID via the `X-Tenant-Id` header.

## Client ID Prefix Requirements

Client IDs must follow a prefix convention that determines how permissions are resolved by `PermissionExpansionMiddleware`:

- **`sa-`** prefix (e.g., `sa-my-app`): Required for `client_credentials` grant type. Gets scope-based permission expansion.
- **`app-`** prefix (e.g., `app-my-app`): Required for `authorization_code` grant type. Also gets scope-based permission expansion.

Registering without the correct prefix returns a 400 error. Mixing `client_credentials` and `authorization_code` grant types in a single registration is not allowed.

## Scopes and Permissions

Newly registered clients can request scopes during registration if those scopes exist in the system. If registered without scopes, the client has no API access.

To assign scopes after registration, an admin uses:

- `PUT /api/v1/identity/service-accounts/{id}/scopes`

Until scopes are assigned, API calls return **403 Forbidden**.

The full list of registered scopes is defined in `IdentityInfrastructureExtensions.cs` and includes scopes such as `inquiries.read`, `inquiries.write`, `billing.read`, `storage.read`, `messaging.access`, and others.

## Production Registration

In production, anonymous registration is disabled. You must include an Initial Access Token:

```bash
curl -s -X POST https://api.yourdomain.com/api/v1/identity/register \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer <initial-access-token>' \
  -d '{
    "clientId": "sa-my-app",
    "clientName": "My App",
    "grantTypes": ["client_credentials"],
    "scopes": []
  }'
```

Initial Access Tokens are created by admins via the `InitialAccessTokensController`. They are time-limited and validated by hash.

## Re-registration Behavior

If you register with a `clientId` that already exists, the endpoint rotates the client secret and returns the new secret (HTTP 200 instead of 201). This allows credential recovery without admin intervention.

## Credential Management

| Scenario | Action |
|----------|--------|
| Normal restart | Use cached `clientId` and `clientSecret`. No re-registration needed. |
| Lost credentials | Re-register with the same `clientId` to rotate the secret. |
| Secret rotation | Admin rotates via `POST /api/v1/identity/service-accounts/{id}/rotate-secret`. |

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| 400 on registration | Missing or wrong `client_id` prefix | Use `sa-` for client_credentials, `app-` for authorization_code |
| 403 on all API calls | No scopes assigned | Ask admin to assign required scopes |
| 401 Unauthorized | Expired or invalid token | Request a new token via `/connect/token` |
| Registration rejected (401) | Missing Initial Access Token (prod) | Get a token from admin |
