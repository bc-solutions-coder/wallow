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

### 1. Create a Service Account

Send a POST request to create a service account for your tenant:

```http
POST /v1/identity/clients/service-accounts
Authorization: Bearer <user-token>
Content-Type: application/json

{
  "name": "Production Backend",
  "description": "Main production server integration",
  "scopes": ["announcements.read", "announcements.manage", "notifications.read"]
}
```

Response (201 Created):

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "clientId": "sa-tenant12-production-backend",
  "clientSecret": "xK9mN2pL8qR5sT7vW3yZ1aB4cD6eF8gH0iJ2kL5mN7oP9qR1sT3uV5wX7yZ9",
  "tokenEndpoint": "https://api.yourplatform.com/connect/token",
  "scopes": ["announcements.read", "announcements.manage", "notifications.read"],
  "warning": "Save this secret now. It will not be shown again."
}
```

The client secret is shown only at creation time. Store it in environment variables or a secret manager.

### 2. Request an Access Token

```bash
curl -X POST https://api.yourplatform.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=your-client-id" \
  -d "client_secret=your-client-secret" \
  -d "grant_type=client_credentials"
```

### 3. Call the API

Use the access token in the `Authorization` header:

```bash
curl -X GET https://api.yourplatform.com/v1/announcements \
  -H "Authorization: Bearer <access-token>"
```

Cache tokens and refresh before expiry. Each token request counts toward rate limits.

---

## API Reference

Service accounts are managed through the **clients** controller, so every route is nested under
`/v1/identity/clients/service-accounts`. All management endpoints require user authentication (not
service account authentication) and the `AdminAccess` permission.

| Method | Route |
|--------|-------|
| `GET` | `/v1/identity/clients/service-accounts` |
| `POST` | `/v1/identity/clients/service-accounts` |
| `GET` | `/v1/identity/clients/service-accounts/{id}` |
| `PUT` | `/v1/identity/clients/service-accounts/{id}/scopes` |
| `POST` | `/v1/identity/clients/service-accounts/{id}/rotate-secret` |
| `DELETE` | `/v1/identity/clients/service-accounts/{id}` |
| `GET` | `/v1/identity/scopes` |

`{id}` is the service account metadata GUID (the `id` returned at creation), not the `clientId`.

### List Service Accounts

```http
GET /v1/identity/clients/service-accounts
Authorization: Bearer <user-token>
```

Response (200 OK) — an array of `ServiceAccountDto`:

```json
[
  {
    "id": { "value": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" },
    "clientId": "sa-tenant12-production-backend",
    "name": "Production Backend",
    "description": "Main production server integration",
    "status": 0,
    "scopes": ["announcements.read", "announcements.manage", "notifications.read"],
    "createdAt": "2026-02-06T15:30:00+00:00",
    "lastUsedAt": null
  }
]
```

`id` is a strongly-typed identifier and serializes as an object with a `value` property. `status` is
numeric: `0` = Active, `1` = Revoked. `description` and `lastUsedAt` may be `null`.

### Get Service Account

```http
GET /v1/identity/clients/service-accounts/{id}
Authorization: Bearer <user-token>
```

Returns a single `ServiceAccountDto` in the shape above, or 404 Not Found.

### Update Scopes

```http
PUT /v1/identity/clients/service-accounts/{id}/scopes
Authorization: Bearer <user-token>
Content-Type: application/json

{
  "scopes": ["announcements.read", "announcements.manage", "notifications.read", "inquiries.read"]
}
```

The list replaces the existing scopes wholesale. Returns 204 No Content on success, 404 Not Found if
the service account does not exist.

### Rotate Secret

```http
POST /v1/identity/clients/service-accounts/{id}/rotate-secret
Authorization: Bearer <user-token>
```

Response (200 OK):

```json
{
  "newClientSecret": "aB1cD2eF3gH4iJ5kL6mN7oP8qR9sT0uV1wX2yZ3aB4cD5eF6gH7iJ8kL9",
  "rotatedAt": "2026-02-06T16:00:00Z",
  "warning": "Save this secret now. It will not be shown again."
}
```

The old secret is invalidated immediately.

### Delete Service Account

```http
DELETE /v1/identity/clients/service-accounts/{id}
Authorization: Bearer <user-token>
```

Revokes and deletes the account. Returns 204 No Content, or 404 Not Found.

### List Available Scopes

```http
GET /v1/identity/scopes?category=Identity
Authorization: Bearer <user-token>
```

Lives on the scopes controller rather than the clients controller, and requires the `ScopeRead`
permission. The `category` query parameter is optional; omit it to list every scope. Response
(200 OK):

```json
[
  {
    "id": { "value": "3f1a9c22-5d4e-4a1b-9f0c-71b8e2d6a904" },
    "code": "users.read",
    "displayName": "Read Users",
    "category": "Identity",
    "description": "Access to read user profiles and data",
    "isDefault": true
  }
]
```

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
| `serviceaccounts.read` | Read service account data | Yes |
| `serviceaccounts.write` | Create and update service accounts | No |
| `serviceaccounts.manage` | Full service account management | No |

### Storage

| Scope | Description | Default |
|-------|-------------|---------|
| `storage.read` | Read files and storage data | Yes |
| `storage.write` | Upload and modify files | No |

### Communications

| Scope | Description | Default |
|-------|-------------|---------|
| `announcements.read` | Read announcements | Yes |
| `announcements.manage` | Manage announcements | No |
| `changelog.manage` | Manage changelog entries | No |
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
- **Rotate secrets regularly.** Rotate every 90 days, or immediately after a security incident or personnel change.
- **Handle errors gracefully.** Implement retry logic for 401 (token expired) and 429 (rate limited) responses with exponential backoff.
- **Never use service accounts from client-side code.** Client secrets cannot be kept secure in browsers or mobile apps. Use OIDC for interactive applications.
- **Never log secrets or tokens.** Log only non-sensitive metadata like token expiry times.

---

## Error Responses

| Status | Error | Meaning |
|--------|-------|---------|
| 401 | `invalid_token` | Token expired. Refresh and retry. |
| 401 | `invalid_client` | Wrong `client_id` or `client_secret`, or account was revoked. |
| 403 | `insufficient_scope` | Service account lacks required scopes. Update via the API. |
| 429 | `rate_limit_exceeded` | Too many requests. Respect the `Retry-After` header. |
