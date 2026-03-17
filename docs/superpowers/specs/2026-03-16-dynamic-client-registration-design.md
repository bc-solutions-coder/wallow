# Dynamic Client Registration via Keycloak DCR

**Date:** 2026-03-16
**Status:** Draft
**Module:** Identity

## Problem

External applications (BFFs, mobile apps) that integrate with Foundry need OAuth2 client credentials to call the API. Currently, clients are manually configured in Keycloak's realm export. This doesn't scale as new apps are built.

## Decision

Use Keycloak's built-in OpenID Connect Dynamic Client Registration (RFC 7591) so apps can self-register as OAuth2 clients at startup. Foundry's existing middleware and service account infrastructure handle the rest.

## Mental Model

- **Apps/Products** register as Keycloak clients via DCR. Their BFFs use client_credentials flow for server-to-server calls (e.g., inquiry submission).
- **Users** authenticate through those apps via standard OIDC (authorization code flow with PKCE). Users belong to tenants/organizations. Tenant context comes from the user's JWT `organization` claim.
- **Service accounts are tenant-agnostic.** They act on behalf of the app, not a tenant. Tenant-scoped operations require user authentication.

## Architecture

```
App Startup (one-time):
┌──────────┐  POST /realms/foundry/clients-registrations/openid-connect  ┌──────────┐
│  App BFF │────────────────────────────────────────────────────────────►│ Keycloak │
│          │  Authorization: Bearer <initial-access-token> (prod only)   │          │
│          │◄────────────────────────────────────────────────────────────│          │
│          │  { client_id, client_secret, registration_access_token }    └──────────┘
└──────────┘

Runtime (every request):
┌──────────┐  POST /realms/foundry/protocol/openid-connect/token         ┌──────────┐
│  App BFF │────────────────────────────────────────────────────────────►│ Keycloak │
│          │  grant_type=client_credentials                              │          │
│          │◄────────────────────────────────────────────────────────────│          │
│          │  { access_token: "jwt..." }                                 └──────────┘
│          │
│          │  POST /api/v1/inquiries                                     ┌──────────┐
│          │────────────────────────────────────────────────────────────►│ Foundry  │
│          │  Authorization: Bearer <jwt>                                │ API      │
│          │◄────────────────────────────────────────────────────────────│          │
└──────────┘                                                             └──────────┘
```

## Keycloak Configuration

### Audience Mapper as Realm Default Client Scope

The Foundry API validates that JWTs contain `aud: foundry-api` (configured in `IdentityInfrastructureExtensions.cs` line 33). DCR-registered clients must get this audience claim automatically.

**Solution:** Create a realm-level default client scope named `foundry-api-audience` that contains the audience protocol mapper. All clients (including DCR-registered) inherit realm default client scopes automatically.

Add to `realm-export.json` `clientScopes` array:
```json
{
  "name": "foundry-api-audience",
  "description": "Adds foundry-api audience claim to access tokens",
  "protocol": "openid-connect",
  "attributes": {
    "include.in.token.scope": "false",
    "display.on.consent.screen": "false"
  },
  "protocolMappers": [
    {
      "name": "foundry-api-audience",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-audience-mapper",
      "consentRequired": false,
      "config": {
        "included.client.audience": "foundry-api",
        "id.token.claim": "false",
        "access.token.claim": "true"
      }
    }
  ]
}
```

Add `"foundry-api-audience"` to the realm's top-level `defaultDefaultClientScopes` array:
```json
{
  "realm": "foundry",
  "defaultDefaultClientScopes": ["foundry-api-audience"],
  ...
}
```

**IMPORTANT — Atomic change:** The per-client `foundry-api-audience` protocol mappers on `foundry-api`, `sa-foundry-api`, and `foundry-spa` must be removed **in the same commit** as adding the realm default scope. Both changes go into the same realm-export.json update. After realm re-import, all clients (existing and new) inherit the audience mapper from the realm default scope.

### Client Registration Policies (Post-Import Setup)

Keycloak's DCR policies are **not configurable via realm-export.json**. They must be configured via the Keycloak Admin REST API (component model) after realm import.

**Development setup** — Add a shell script `docker/keycloak/configure-dcr.sh` that runs after Keycloak starts:

```bash
#!/bin/bash
# Wait for Keycloak to be ready, then get admin token
ADMIN_TOKEN=$(curl -s -X POST "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
  -d "grant_type=client_credentials&client_id=admin-cli&client_secret=$ADMIN_SECRET" \
  | jq -r '.access_token')

# Get the realm's internal ID
REALM_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/foundry" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.id')

# Add trusted hosts policy for anonymous DCR from localhost
# Uses the Keycloak component model (PUT /admin/realms/{realm}/components)
curl -X POST "$KEYCLOAK_URL/admin/realms/foundry/components" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Trusted Hosts\",
    \"providerId\": \"trusted-hosts\",
    \"providerType\": \"org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy\",
    \"parentId\": \"$REALM_ID\",
    \"config\": {
      \"trusted-hosts\": [\"localhost\", \"127.0.0.1\"],
      \"host-sending-registration-request-must-match\": [\"true\"]
    }
  }"

# Max clients policy (prevents abuse in dev)
curl -X POST "$KEYCLOAK_URL/admin/realms/foundry/components" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Max Clients\",
    \"providerId\": \"max-clients\",
    \"providerType\": \"org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy\",
    \"parentId\": \"$REALM_ID\",
    \"config\": {
      \"max-clients\": [\"100\"]
    }
  }"
```

> **Note:** The exact API paths and payload shapes should be verified against the installed Keycloak 26 version. The component model API may have minor variations between versions.

**Production:** Remove the trusted hosts policy. Require Initial Access Tokens for all registrations.

### DCR-Registered Client Properties

Apps request these properties during registration:
- `grant_types: ["client_credentials"]`
- `token_endpoint_auth_method: "client_secret_basic"`

Keycloak creates a confidential client with service account enabled.

### Scope Assignment

DCR-registered clients inherit only the realm default client scope:
- `foundry-api-audience` (audience mapper, ensures `aud: foundry-api`)

**Scopes like `inquiries.write`, `showcases.read` are NOT realm defaults.** They are optional client scopes that must be assigned per-client by an admin after registration, via:
- Keycloak Admin Console, or
- Foundry's existing `PUT /api/v1/identity/service-accounts/{id}/scopes` endpoint

This ensures DCR-registered clients start with minimal access. Elevated scopes require admin intervention.

**Exception: pre-configured clients** like `sa-foundry-api` (the personal-site BFF) have scopes assigned directly in the realm export via `defaultClientScopes`.

## Client Naming Convention

### The `sa-` Prefix

Foundry's `PermissionExpansionMiddleware` identifies service accounts by checking if the JWT's `azp` claim starts with `sa-`. This determines whether scope-based (service account) or role-based (user) permission expansion is used.

### Enforcement Strategy

Keycloak DCR (RFC 7591) has **no built-in client_id prefix enforcement**. Instead:

1. **Documentation:** App developers are instructed to use `sa-` prefix (e.g., `sa-my-app`).
2. **Fail-safe:** If an app registers without the `sa-` prefix, `PermissionExpansionMiddleware` treats it as a regular user. Since service accounts have no realm roles, role-based expansion yields zero permissions. The app gets 403 on every request — a clear signal to fix the client_id.
3. **Admin cleanup:** Admins can detect misconfigured clients via the Foundry service accounts API (they won't appear in lazy sync since they're not `sa-*`).

This approach is safe: a missing prefix results in LESS access (zero permissions), never more.

## Foundry Backend Changes

### Existing Middleware (no changes needed)

The quick fix already applied covers:
- **PermissionExpansionMiddleware:** Detects `azp` starting with `sa-` and uses scope-based permission expansion.
- **TenantResolutionMiddleware:** Allows `sa-*` clients to use the `X-Tenant-Id` header.
- **ServiceAccountTrackingMiddleware:** Tracks `LastUsedAt` for `sa-*` clients.

### New: Lazy Metadata Sync (implementation required)

`ServiceAccountTrackingMiddleware` currently only updates `LastUsedAt` for known service accounts. It needs a new code path: when it encounters an unknown `sa-*` client (registered via DCR), it should auto-create a `ServiceAccountMetadata` record.

**Data mapping:**
- `KeycloakClientId`: from `azp` claim
- `Name`: from `azp` claim (e.g., "sa-personal-site")
- `Status`: `Active`
- `Scopes`: extracted from `scope` claim (space-separated)
- `TenantId`: well-known platform sentinel `TenantId.Platform` (see below)
- `CreatedByUserId`: `Guid.Empty` (system-created)

**TenantId handling:** `ServiceAccountMetadata` implements `ITenantScoped` which requires a `TenantId`. For DCR-registered (tenant-agnostic) accounts, use a well-known non-zero sentinel GUID:

```csharp
// In TenantId struct
public static TenantId Platform => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
```

This sentinel must be non-zero to avoid collision with `Guid.Empty` (which represents "unset/no tenant resolved"). The EF Core global tenant query filter in `TenantAwareDbContext` must be updated to include `TenantId.Platform` records when the caller is a platform admin. The existing `IServiceAccountUnfilteredRepository` (used by `ServiceAccountTrackingMiddleware`) already bypasses the tenant filter, so lazy sync will work without filter changes.

**Error handling:** Lazy sync runs fire-and-forget (catch and log errors). A failed sync must NOT block the API request. The middleware already has the permissions it needs from the JWT — metadata sync is for admin visibility only.

**Middleware ordering** (existing, no changes needed):
```
UseAuthentication
UseMiddleware<TenantResolutionMiddleware>
UseMiddleware<PermissionExpansionMiddleware>
UseAuthorization
UseMiddleware<ServiceAccountTrackingMiddleware>  ← lazy sync happens here
```

### Realm Export Changes

Changes to `docker/keycloak/realm-export.json`:
1. `inquiries.read`, `inquiries.write`, `showcases.read` client scopes (done in quick fix)
2. `foundry-api-audience` as a realm default client scope with the audience mapper
3. `defaultDefaultClientScopes: ["foundry-api-audience"]` at realm root level
4. Remove per-client `foundry-api-audience` protocol mappers from `foundry-api`, `sa-foundry-api`, `foundry-spa` (they inherit from realm default)
5. `sa-foundry-api` client for the existing personal-site BFF (done in quick fix)

New file: `docker/keycloak/configure-dcr.sh` for post-import DCR policy setup.

## App Integration Pattern

### Development

```bash
# No auth required — Trusted Hosts policy allows localhost
curl -X POST http://localhost:8080/realms/foundry/clients-registrations/openid-connect \
  -H 'Content-Type: application/json' \
  -d '{
    "client_id": "sa-my-app",
    "client_name": "My App",
    "grant_types": ["client_credentials"],
    "token_endpoint_auth_method": "client_secret_basic"
  }'
# Response: { "client_id": "sa-my-app", "client_secret": "...", "registration_access_token": "..." }
```

### Production

Same request but with `Authorization: Bearer <initial-access-token>` header. Initial Access Tokens are created by admin via Keycloak Admin Console (`Clients > Initial Access Token > Create`).

**Recommended token settings:**
- Expiry: 24 hours (per-deployment)
- Max clients: 1 (one token per app registration)

### App Startup Logic

```
if credentials file exists:
    load client_id and client_secret from file
else:
    call DCR endpoint (with token if FOUNDRY_REGISTRATION_TOKEN env var is set)
    store client_id, client_secret, registration_access_token to file
use client_id + client_secret for client_credentials flow
```

### Frontend BFF Environment Variables

```env
# After DCR registration:
FOUNDRY_CLIENT_ID=sa-personal-site
FOUNDRY_CLIENT_SECRET=<from DCR response>
FOUNDRY_TOKEN_URL=http://localhost:8080/realms/foundry/protocol/openid-connect/token

# Optional: for automatic registration on first startup
FOUNDRY_KEYCLOAK_DCR_URL=http://localhost:8080/realms/foundry/clients-registrations/openid-connect
FOUNDRY_REGISTRATION_TOKEN=<initial-access-token, required in prod>
```

## Re-Registration and Credential Recovery

- **Normal restarts:** App uses cached credentials. No re-registration needed.
- **Lost credentials:** App re-registers with a new Initial Access Token. Admin cleans up the orphaned client in Keycloak. Note: `registration_access_token` rotates on each use per RFC 7591 — it cannot be reused if lost.
- **Secret rotation:** Admin rotates via Foundry's existing `POST /api/v1/identity/service-accounts/{id}/rotate-secret` or via the registration_access_token (single-use, returns new token).
- **Orphaned client detection:** `ServiceAccountMetadata` records with `LastUsedAt` older than 30 days can be flagged for admin review. No automated cleanup — admin decides.

## Security Considerations

- **Dev mode:** Anonymous registration is only available from trusted hosts (localhost). Configured via Keycloak Admin API post-import, not in realm-export.json.
- **Prod mode:** Initial Access Tokens are single-use (configurable max client count) and time-limited.
- **`sa-` prefix convention:** Not enforced by Keycloak but fail-safe — a missing prefix results in zero permissions (role-based expansion with no roles), never elevated access.
- **Scope limitation:** DCR-registered clients start with zero functional scopes (only `foundry-api-audience` for JWT validation). Admin must explicitly grant `inquiries.write`, `showcases.read`, etc. per-client.
- **JWT validation:** All tokens are validated against Keycloak's public keys. The `aud` claim must include `foundry-api` (enforced by audience mapper in realm default scope).
- **Full Scope Allowed:** Keycloak disables "Full Scope Allowed" for DCR-registered clients by default. This means they only get explicitly assigned scopes.

## Scope-to-Permission Mapping

The `MapScopeToPermission()` method in `PermissionExpansionMiddleware` is the source of truth for which OAuth2 scopes map to which `PermissionType` values. When adding new scopes:
1. Define the scope in `ApiScopes.cs`
2. Add the Keycloak client scope in realm-export.json
3. Add the mapping in `MapScopeToPermission()`

All three must be updated together. An unmapped scope in a JWT is silently ignored by the middleware.

## Out of Scope

- Tenant/organization auto-creation during registration (tenancy is user-level, not app-level)
- UI for managing DCR clients (use Keycloak Admin Console)
- Automated credential rotation
- Custom Keycloak SPI for prefix enforcement (fail-safe approach is sufficient)
- Automated orphaned client cleanup (admin-driven for now)

## Implementation Steps

1. **Realm export: audience mapper as realm default scope** — Create `foundry-api-audience` client scope with audience mapper, add `defaultDefaultClientScopes` at realm root level, remove per-client audience mappers from all three clients (atomic change)
2. **DCR setup script** — Create `docker/keycloak/configure-dcr.sh` using the component model API to configure trusted hosts policy; verify API paths against Keycloak 26
3. **Docker compose integration** — Run `configure-dcr.sh` after Keycloak starts (health check + script)
4. **TenantId.Platform sentinel** — Add `TenantId.Platform` constant (`00000000-0000-0000-0000-000000000001`); update EF Core global query filter if needed for platform admin queries
5. **Lazy metadata sync** — Add creation logic to `ServiceAccountTrackingMiddleware` for unknown `sa-*` clients; fire-and-forget with error logging
6. **Scope assignment for sa-foundry-api** — Assign `inquiries.write`, `inquiries.read`, `showcases.read` to the pre-configured `sa-foundry-api` client as `defaultClientScopes` (already done in quick fix)
7. **Test: DCR registration** — Register a client from localhost without auth, verify client creation
8. **Test: client_credentials flow** — Get token with DCR-registered client, verify `aud` and `scope` claims
9. **Test: inquiry submission** — Submit inquiry via DCR-registered service account (after admin assigns `inquiries.write` scope)
10. **Test: wrong prefix** — Register without `sa-` prefix, verify 403 on API calls
11. **Documentation** — App integration guide for frontend developers

## References

- [Keycloak Client Registration Service](https://www.keycloak.org/securing-apps/client-registration)
- [RFC 7591 — OAuth 2.0 Dynamic Client Registration](https://datatracker.ietf.org/doc/html/rfc7591)
- [Keycloak Client Scopes](https://medium.com/@torinks/keycloak-client-scopes-bc3ba10b2dbb)
- [Keycloak Admin REST API](https://www.keycloak.org/docs-api/latest/rest-api/index.html)
