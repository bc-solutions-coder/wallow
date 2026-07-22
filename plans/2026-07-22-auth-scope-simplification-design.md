# Auth & Tenancy Scope Simplification — Design

**Date:** 2026-07-22
**Status:** Approved (Approach A — one golden path)
**Scope:** Identity module, TypeScript SDK/BFF, seed/config, integration docs

## Problem

Wallow's goal is a fork-first white-label platform: a developer forks the repo, gets a
.NET backend with auth solved, and connects web apps on any domain (wallow.dev,
bcordes.dev, a customer's site) through the BFF SDK using Wallow as the OIDC login
system and user administrator.

The multi-domain capability already works. The OpenIddict server (auth code + mandatory
PKCE, client credentials, refresh) lives in the API; the React `wallow-auth` app renders
login/consent; the BFF SDK keeps every browser call same-origin, so the API needs no
CORS; and `OpenIddictRedirectUriValidator` derives allowed origins from registered
clients' redirect URIs, so onboarding a new domain is data, not code.

The problem is scope creep around that working core:

1. **Five overlapping client-provisioning surfaces** — `SetupController`,
   `ClientsController`, `ClientRegistrationController` (RFC 7591 DCR +
   `InitialAccessToken` gating), `AppsController` (developer apps), and
   `ServiceAccountsController`. No two agree, and the docs describe two different ones.
2. **A large enterprise-IdP surface with zero frontend callers** — per-tenant SAML/OIDC
   SSO federation, SCIM provisioning, membership requests, organization-domain
   verification, dynamic client registration, initial access tokens.
3. **A dishonest tenant model** — no `Tenant` entity exists. A tenant *is* an
   `Organization`, but only because `PreRegisteredClientSyncService` mints the
   equivalence when syncing seed clients. `POST /organizations` mints a random id that
   never matches any caller's `tenant_id` claim, so self-created orgs are unreachable by
   their own controller (`OrganizationsController.cs:25,53` vs `Organization.cs:30`).
   Two membership models coexist unreconciled: `WallowUser.TenantId` (one tenant per
   user) and `Organization._members` (many, free-string role).
4. **The one path an integrator needs is broken.** The dashboard's self-service app
   registration (`AppsController`) rejects `openid profile email offline_access` — the
   exact scopes `docs/integrations/bff-pattern.md` tells users to request there — and
   returns a public client, while the SDK requires a confidential secret
   (`config.ts:105`). No self-service path can provision a working BFF login client.

## Goals

- One documented, working golden path: fork → brand → seed → register an app → deploy a
  BFF frontend on any domain/machine with 7 env vars → login works.
- A tenant model that is explicit and correct: creating a tenant is a real, working flow.
- A client-registration surface small enough for a fork maintainer to hold in their head.
- Fix the known auth bugs that break integrators.

## Non-goals

- Competing with FusionAuth/Keycloak on enterprise features (SAML SSO, SCIM) — parked.
- Pure-SPA / cross-origin browser access to the API (the BFF pattern stays mandatory;
  no CORS is added).
- Self-service *tenant* signup for anonymous users. Tenants are created by realm admins
  (or first-run setup); users self-serve into existing tenants via invitations and
  client-bound registration.

## Design

### 1. Organization IS the tenant — make it honest

Keep `Organization` as the tenant aggregate; do not introduce a separate `Tenant`
entity. Make the equivalence explicit and universal:

- `Organization.Create` becomes the single place a tenant id is minted:
  `org.Id == TenantId` by construction, documented on the aggregate.
- `POST /v1/identity/organizations` (realm-admin only) creates a fully addressable
  tenant: the returned org id works with every other `OrganizationsController` action.
  This fixes the orphaned-org bug.
- `PreRegisteredClientSyncService` keeps auto-creating orgs from seed-client
  `TenantName`s, but through the same creation path.
- Membership unifies on `OrganizationMember`. `WallowUser.TenantId` remains as the
  user's *home* tenant (set at registration) but membership checks read
  `Organization._members`. `OrganizationMember.Role` adopts the existing
  `OrgMemberRole` enum instead of a free string.

### 2. Client provisioning collapses to two surfaces

- **Admin: `ClientsController`** (`/v1/identity/clients`, realm-admin) — full CRUD,
  secret rotation, tenant binding. Absorbs service accounts: a client-credentials
  client is just a client (`sa-` prefix preserved for the operator-override path in
  `TenantResolutionMiddleware`). `ServiceAccountsController` is deleted; its
  rotate/revoke/update-scopes actions move onto `ClientsController`.
- **Self-service: `AppsController`** (`/v1/identity/apps`, dashboard) — reworked so a
  tenant admin can register a **confidential** app with:
  - OIDC login scopes (`openid profile email offline_access`) plus the existing
    `DeveloperAppScopes` for API access,
  - redirect + post-logout URIs (validated absolute HTTPS, localhost allowed in dev),
  - the client secret returned exactly once at creation, with rotation available.
  Registered redirect URIs feed `OpenIddictRedirectUriValidator` as today, so a new
  domain still requires no source change.

**Deleted to git history** (fork-first: forks inherit every line we keep):

- `ClientRegistrationController` (RFC 7591 DCR) and `InitialAccessTokensController`
  + the `InitialAccessToken` entity.
- `SetupController`'s client-provisioning step (first-run admin bootstrap stays;
  clients come from seed.json or the two surfaces above).
- `SsoController` + `SsoConfiguration` + `OidcFederationService` (per-tenant SAML/OIDC
  federation), `ScimController` + SCIM entities/auth handler,
  `MembershipRequestsController` + `MembershipRequest`,
  `OrganizationDomainsController` + `OrganizationDomain`.
- Social login (Google/Microsoft/GitHub/Apple) **stays** — it is config-gated,
  self-contained, and part of the login product.

### 3. Developer experience is the product

- The ~200 lines of per-app glue every BFF app hand-copies (`bff-server.ts`,
  `proxy-topology.ts`, the SDK singleton facade) move into the SDK (or the web-shell
  package) as a factory: an app supplies config, not plumbing.
- A `create-wallow-app` scaffold (or documented template in `examples/`) emits a
  minimal TanStack Start app wired to the five shared packages.
- Docs consolidate on the one golden path: `bff-pattern.md` describes the reworked
  self-service registration (confidential client, secret once); `dcr-integration.md`
  is deleted with DCR; a new deployment section covers real domains — issuer is the
  **API** origin, login UX is the **auth** origin, cookie-domain guidance
  (`Authentication:CookieDomain`), and a production `seed.json` example.

### 4. Bug fixes and config hygiene (in scope)

- **Wallow-ho2k (P1):** id_token missing `email`/`name`/roles for every client —
  `TokenController` sets claim destinations before `SetScopes()`. Fix per the filed
  analysis (mind the profile/email mis-gating noted there).
- **Wallow-41ot:** backslash open-redirect gap in `isSafeReturnUrl` /
  `ReturnUrlValidator.IsSafe`.
- wallow-auth h3 proxy forwards `/.well-known/**` so an issuer pointed at the auth
  origin resolves discovery.
- Port mismatch: `appsettings.Development.json` `WebUrl` (5003) vs the actual web app
  port (3000).
- `seed.json` gains commented production-shaped examples alongside the localhost
  entries.

## Reference deployment (the bcordes.dev case)

API at `api.wallow.dev` (OIDC issuer), auth app at `auth.wallow.dev` (`AuthUrl`),
cookie domain `.wallow.dev`. The external app registers a confidential
`bcordes-web-client` with redirect `https://bcordes.dev/bff/callback`, then runs
anywhere with the 7 BFF env vars (`OIDC_ISSUER`, `OIDC_CLIENT_ID`,
`OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`, `OIDC_POST_LOGOUT_REDIRECT_URI`,
`BFF_API_BASE_URL`, `COOKIE_PASSWORD`). Only outbound HTTPS from the BFF host to the
API is required; no CORS, no AppHost change.

## Risks

- **Deleting unwired surface touches migrations.** Dropping entities (SSO, SCIM,
  membership requests, org domains, initial access tokens) needs EF migrations per
  affected schema and a check that no seed/config references remain.
- **Membership unification changes claim emission.** `org_id`/`org_name` injection in
  `AuthorizationController` and `IClientTenantResolver` must keep working while
  membership reads move to `OrganizationMember`.
- **AppsController rework changes the OpenAPI surface.** The SDK snapshot
  (`packages/sdk/openapi/v1.json`) must be regenerated and the generated client + the
  dashboard register-app UI updated together, or `openapi-drift.yml` fails.

## Testing

- Unit/integration per module via `./scripts/run-tests.sh identity`.
- The decisive check is end-to-end: a seeded confidential client driving the full
  authorization-code flow through the containerised stack (`./scripts/e2e.sh`),
  asserting both tokens carry the profile claims (guards the Wallow-ho2k fix) and that
  a dashboard-registered app can complete BFF login with its returned secret.
