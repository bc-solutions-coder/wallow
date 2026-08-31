# Service Accounts

Service accounts provide programmatic access to the Wallow API for server-to-server integrations. They use the OAuth2 client credentials flow, unlike user authentication which requires browser-based OIDC login.

Use service accounts for automated systems, background jobs, external integrations, and custom tooling that needs to read or write data.

| Aspect | User Token | Service Account |
|--------|-----------|-----------------|
| Authentication | Browser login (OIDC) | Client credentials (OAuth2) |
| Lifespan | Session-based (hours) | Short-lived (5-15 min) |
| Scope | User permissions | Explicit scopes |
| Use case | Interactive applications | Server-to-server |
| Credential type | Password + 2FA | Client ID + Secret |

---

## Getting Started

### 1. Register a service account

A service account is one of an organization's **clients**, registered on the same route as a
developer application with `kind: "service-account"`. The caller needs `OrganizationClientsManage`
in that organization — an organization admin has it through their role. The wallow-web
organization page offers the same registration as a two-step form (Basics → Scopes) and shows the
env block below when it completes.

```http
POST /v1/identity/organizations/{orgId}/clients
Authorization: Bearer <user-token>
Content-Type: application/json

{
  "kind": "service-account",
  "name": "Nightly sync",
  "redirectUris": [],
  "postLogoutRedirectUris": [],
  "scopes": ["organizations.read", "inquiries.read"]
}
```

Response (201 Created):

```json
{
  "client": {
    "clientId": "sa-acme-nightly-sync",
    "name": "Nightly sync",
    "kind": "service-account",
    "status": "active",
    "redirectUris": [],
    "postLogoutRedirectUris": [],
    "scopes": ["organizations.read", "inquiries.read"],
    "createdByUserId": "6f1c…",
    "createdAt": "2026-08-30T00:00:00Z"
  },
  "clientSecret": "xK9mN2pL8qR5sT7vW3yZ1aB4cD6eF8gH0iJ2kL5mN7oP9qR1sT3uV5wX7yZ9",
  "issuer": "https://auth.example.com/auth",
  "apiBaseUrl": "https://api.example.com"
}
```

The secret is shown once. The client id is derived as `sa-<organization>-<name>`, and the URI
fields are ignored for a service account: it can only use the `client_credentials` grant, so it
has no redirect. Scopes follow one rule for both client kinds: any scope in the catalog may be
granted except a platform-only one, which is never grantable on the org-scoped surface.

Registration binds the client to the organization, so every token it obtains carries that
organization's `org_id` and is tenant-scoped exactly like an interactive user's.

### 2. Configure the SDK

The response maps onto the env block `createServiceClient()` reads
(see [TypeScript SDK](../integrations/typescript-sdk.md#service-accounts-createserviceclient)):

```bash
OIDC_ISSUER=https://auth.example.com/auth
OIDC_SERVICE_CLIENT_ID=sa-acme-nightly-sync
OIDC_SERVICE_CLIENT_SECRET=xK9mN2pL8qR5sT7vW3yZ1aB4cD6eF8gH0iJ2kL5mN7oP9qR1sT3uV5wX7yZ9
OIDC_SERVICE_SCOPES=organizations.read inquiries.read
BFF_API_BASE_URL=https://api.example.com
```

### 3. Request an Access Token

Without the SDK, exchange the credentials at the token endpoint directly:

```bash
curl -X POST https://api.example.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=sa-acme-nightly-sync" \
  -d "client_secret=<client-secret>" \
  -d "grant_type=client_credentials" \
  -d "scope=organizations.read inquiries.read"
```

The access token's `sub` and `azp` are the client id, `org_id` is the organization the client was
registered for, and `scope` is the granted subset of what was requested.

### 4. Call the API

Use the access token in the `Authorization` header:

```bash
curl -X GET https://api.example.com/v1/identity/organizations \
  -H "Authorization: Bearer <access-token>"
```

Cache tokens and refresh before expiry. Each token request counts toward rate limits.

---

## API Reference

Service accounts and developer applications share the organization-scoped clients surface, one
permission (`OrganizationClientsManage`) and one record. `{clientId}` is the `clientId` returned at
registration, e.g. `sa-acme-nightly-sync`.

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/v1/identity/organizations/{orgId}/clients` | List the organization's clients; `kind` tells them apart |
| `POST` | `/v1/identity/organizations/{orgId}/clients` | Register an application or a service account |
| `GET` | `/v1/identity/organizations/{orgId}/clients/{clientId}` | Get one client |
| `PATCH` | `/v1/identity/organizations/{orgId}/clients/{clientId}` | Update scopes (and, for an application, URIs and refresh-token lifetime) |
| `DELETE` | `/v1/identity/organizations/{orgId}/clients/{clientId}` | Delete the client; its tokens stop validating |
| `POST` | `/v1/identity/organizations/{orgId}/clients/{clientId}/rotate-secret` | Issue a new secret (shown once); body `{ "revokeActiveTokens": true }` also ends every token the client holds |
| `GET` | `/v1/identity/scopes` | List the scope catalog (`ScopeRead`); `platformOnly` scopes cannot be granted here |

`PATCH` takes the same `redirectUris`, `postLogoutRedirectUris`, `backchannelLogoutUri` and
`scopes` fields as registration, minus `kind` and `name`, which are immutable, plus an optional
`refreshTokenLifetime` (seconds, 60–31,536,000; `null` or absent keeps the current value, and a
change applies to new logins only). For a service account only `scopes` matters — it uses the
`client_credentials` grant and holds no refresh tokens, so it has no lifetime either.

### Pre-registered service accounts

The seeder (`api/seed.json`) can define a service account for a deployment: a client whose id
starts with `sa-` gets the `client_credentials` grant, and its `tenantName` names the organization
the tokens are bound to. The dev/e2e seed defines `sa-wallow-nightly-sync` in the `Wallow`
organization for exactly this.

---

## Available Scopes

### Identity

| Scope | Description | Default |
|-------|-------------|---------|
| `users.read` | Read user profiles and data | Yes |
| `users.write` | Create and update users | No |
| `users.manage` | Full user management | No |
| `roles.read` | Read roles and role assignments | Yes |
| `roles.write` | Create and update roles | No |
| `roles.manage` | Full role management | No |
| `organizations.read` | Read organization data | Yes |
| `organizations.write` | Create and update organizations | No |
| `organizations.manage` | Full organization management | No |
| `apikeys.read` | Read API key metadata | Yes |
| `apikeys.write` | Create and update API keys | No |
| `apikeys.manage` | Full API key management | No |

### Storage

| Scope | Description | Default |
|-------|-------------|---------|
| `storage.read` | Read files and storage data | Yes |
| `storage.write` | Upload and modify files | No |

### Announcements

| Scope | Description | Default |
|-------|-------------|---------|
| `announcements.read` | Read announcements | Yes |
| `announcements.manage` | Manage announcements | No |
| `changelog.manage` | Manage changelog entries | No |

### Notifications

| Scope | Description | Default |
|-------|-------------|---------|
| `notifications.read` | Read notifications | No |
| `notifications.write` | Send notifications | No |

### Configuration

| Scope | Description | Default |
|-------|-------------|---------|
| `configuration.read` | Read configuration data | Yes |
| `configuration.manage` | Manage configuration | No |

### Inquiries

| Scope | Description | Default |
|-------|-------------|---------|
| `inquiries.read` | Read inquiries | No |
| `inquiries.write` | Create and update inquiries | No |

### Platform

| Scope | Description | Default |
|-------|-------------|---------|
| `webhooks.manage` | Manage webhook subscriptions | No |

---

## Best Practices

- **Store credentials securely.** Use environment variables or a secret manager (AWS Secrets Manager, Azure Key Vault, HashiCorp Vault). Never commit secrets to source control.
- **Cache access tokens.** Tokens are valid for 5-15 minutes. Refresh 30-60 seconds before expiry to avoid mid-request failures.
- **Use minimum necessary scopes.** Request only the scopes your integration needs. This limits exposure if credentials are compromised.
- **Rotate credentials regularly.** Every 90 days, or immediately after a security incident or personnel change. An organization manager rotates a service account's secret from the organization page (or `POST /v1/identity/organizations/{orgId}/clients/{clientId}/rotate-secret`); the old secret stops working the moment the new one is issued, so update the consumer before rotating or expect a short outage. Tick **also revoke active tokens** after a suspected leak — it ends every access token the account already holds, not just the secret.
- **Handle errors gracefully.** Implement retry logic for 401 (token expired) and 429 (rate limited) responses with exponential backoff.
- **Never use service accounts from client-side code.** Client secrets cannot be kept secure in browsers or mobile apps. Use OIDC for interactive applications.
- **Never log secrets or tokens.** Log only non-sensitive metadata like token expiry times.

---

## Error Responses

| Status | Error | Meaning |
|--------|-------|---------|
| 401 | `invalid_token` | Token expired. Refresh and retry. |
| 401 | `invalid_client` | Wrong `client_id` or `client_secret`, or the client was deleted or suspended. |
| 403 | `insufficient_scope` | Service account lacks required scopes. Grant them with `PATCH`. |
| 429 | `rate_limit_exceeded` | Too many requests. Respect the `Retry-After` header. |

---

## Related Documentation

- [Authorization](../architecture/authorization.md) — the scope and permission model these scopes plug into
- [Authentication](../architecture/authentication.md) — the interactive OIDC path this credential replaces
- [BFF Pattern](../integrations/bff-pattern.md) — what an interactive frontend uses instead of a service account
- [TypeScript SDK](../integrations/typescript-sdk.md#service-accounts-createserviceclient) — `createServiceClient()` and the `OIDC_SERVICE_*` env block
- `api/src/Modules/ApiKeys/README.md` — the other machine credential (`X-Api-Key`, not OAuth2)
