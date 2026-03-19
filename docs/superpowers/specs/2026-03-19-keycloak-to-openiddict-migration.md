# Keycloak to OpenIddict Migration — Design Spec

**Date:** 2026-03-19
**Status:** Draft
**Author:** AI-assisted design
**Spec:** Migration from Keycloak 26 to ASP.NET Core Identity + OpenIddict

---

## 1. Goal

Replace Keycloak with ASP.NET Core Identity + OpenIddict as Foundry's identity and authorization server. Eliminate the external JVM process, own the entire user data model in PostgreSQL, and modernize the OAuth2 flow to Authorization Code + PKCE.

## 2. Motivation

- **Operational simplicity:** Remove Keycloak Docker container, its PostgreSQL database, Infinispan cache, and JVM runtime from the deployment topology.
- **Scaling:** Auth scales with the API — same pods, same database, same horizontal scaling strategy. No separate Keycloak HA cluster.
- **Data ownership:** User storage, organizations, and OAuth2 clients live in Foundry's PostgreSQL under the `identity` schema. One database, one migration toolchain.
- **Performance:** User CRUD and token operations become in-process EF Core calls instead of HTTP round-trips to an external service.
- **Standards compliance:** Move from deprecated ROPC grant to OAuth 2.1 Authorization Code + PKCE.

## 3. Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| OIDC server library | OpenIddict (MIT) | Free, EF Core native, full control over endpoints. Duende costs $1,500+/yr for one feature (built-in DCR) that's trivial to implement. |
| User storage | ASP.NET Core Identity + EF Core | Built-in password hashing (PBKDF2), lockout, MFA, email confirmation. No custom crypto. |
| Token flow | Authorization Code + PKCE | OAuth 2.1 compliant. ROPC dropped entirely. |
| SSO federation | OIDC only | SAML dropped. ASP.NET Core's `AddOpenIdConnect()` handles external IdP federation natively. |
| SCIM | Keep | Repoint from Keycloak admin API to `UserManager<FoundryUser>`. Same facade and audit log. |
| Organizations | New domain entity | `Organization` aggregate root in Identity module. 1:1 with `TenantId`. Replaces Keycloak Organizations API. |
| Migration strategy | Big-bang cutover | Pre-production codebase, no live data to migrate. Clean swap of all 7 Keycloak services at once. |
| Login UI | Razor Pages in Foundry.Api | 3 pages: Login, Logout, Consent. Self-contained, no separate frontend deployment. |
| Client management | Admin API only | `ClientsController` with CRUD endpoints. No admin UI — teams build their own dashboards. |

## 4. Architecture

### 4.1 Before (Current)

```
┌─────────┐     HTTP      ┌───────────┐     JDBC     ┌──────────────┐
│ Foundry │──────────────►│ Keycloak  │─────────────►│ keycloak_db  │
│   API   │               │  (JVM)    │              │ (PostgreSQL) │
│         │               └───────────┘              └──────────────┘
│         │─────────────────────────────────────────►┌──────────────┐
│         │              EF Core                     │ foundry_db   │
└─────────┘                                          │ (PostgreSQL) │
                                                     └──────────────┘
```

### 4.2 After (Target)

```
┌──────────────────────────────────┐
│          Foundry API             │
│  ┌────────────┐ ┌─────────────┐ │     EF Core    ┌──────────────┐
│  │ ASP.NET    │ │ OpenIddict  │ │───────────────►│ foundry_db   │
│  │ Core       │ │ Server      │ │                │ (PostgreSQL) │
│  │ Identity   │ │             │ │                │              │
│  └────────────┘ └─────────────┘ │                └──────────────┘
└──────────────────────────────────┘
```

### 4.3 Request Flow

**Browser-based (Auth Code + PKCE):**

```
Browser → BFF → GET /connect/authorize
                  → OpenIddict validates request
                  → Redirect to /account/login (Razor Page)
                  → User submits credentials
                  → SignInManager.PasswordSignInAsync()
                  → Redirect back to /connect/authorize
                  → OpenIddict issues auth code
                  → Redirect to BFF callback
             → POST /connect/token (code + PKCE verifier + client_secret)
                  → OpenIddict validates, issues JWT + refresh token
             → BFF stores tokens in encrypted HTTP-only cookie
             → BFF proxies API calls with Authorization: Bearer <jwt>
```

**Service account (Client Credentials):**

```
Service → POST /connect/token
            grant_type=client_credentials
            client_id=sa-tenant1-my-service
            client_secret=<secret>
            scope=showcases.read inquiries.read
          → OpenIddict validates client, issues JWT
          → JWT contains: sub (client_id), scopes, tenant claim
       → API call with Bearer token
          → PermissionExpansionMiddleware maps scopes → permissions (unchanged)
```

**OIDC Federation (enterprise SSO):**

```
Browser → Login page → "Sign in with [Company SSO]"
       → ASP.NET Core challenges external OIDC scheme
       → Redirect to external IdP (Okta, Azure AD)
       → User authenticates at external IdP
       → Callback to Foundry
       → Find-or-create FoundryUser, link external login
       → Issue Foundry's own JWT (external provider's token is never exposed)
```

## 5. Data Model

### 5.1 New Tables (identity schema)

**ASP.NET Core Identity tables:**

| Table | Purpose | Replaces |
|-------|---------|----------|
| `identity.asp_net_users` | User storage (email, password hash, MFA, lockout) | Keycloak user store |
| `identity.asp_net_roles` | Role definitions (admin, manager, user) | Keycloak realm roles |
| `identity.asp_net_user_roles` | User-role join table | Keycloak role mappings |
| `identity.asp_net_user_claims` | Custom claims per user | Keycloak user attributes |
| `identity.asp_net_user_logins` | External OIDC login links | Keycloak IdP links |
| `identity.asp_net_user_tokens` | MFA/password reset tokens | Keycloak credential store |
| `identity.asp_net_role_claims` | Claims attached to roles | Keycloak role attributes |

**OpenIddict tables:**

| Table | Purpose | Replaces |
|-------|---------|----------|
| `identity.openiddict_applications` | OAuth2 clients (BFF, SAs, app-* devs) | Keycloak client registrations |
| `identity.openiddict_authorizations` | Authorization grants (auth code sessions) | Keycloak sessions |
| `identity.openiddict_scopes` | Scope definitions (showcases.read, etc.) | Keycloak client scopes |
| `identity.openiddict_tokens` | Issued tokens (for revocation/introspection) | Keycloak token store |

**New domain entity:**

| Table | Purpose |
|-------|---------|
| `identity.organizations` | Organization aggregate root (name, domain, tenant_id) |
| `identity.organization_members` | User-org membership with role (Owner, Admin, Member) |

### 5.2 Existing Tables (unchanged)

| Table | Notes |
|-------|-------|
| `identity.service_account_metadata` | Still tracks SA usage, scopes, last_used_at |
| `identity.sso_configurations` | Stores OIDC federation config per tenant (SAML fields become unused) |
| `identity.scim_configurations` | SCIM endpoint config |
| `identity.scim_sync_logs` | SCIM operation audit trail |
| `identity.api_keys` | From Epic 3 (API key persistence) |

### 5.3 Removed

- `keycloak_db` PostgreSQL schema — dropped entirely
- `foundry-realm.json` — no longer needed
- `configure-dcr.sh` — no longer needed

### 5.4 FoundryUser Entity

```csharp
public sealed class FoundryUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
}
```

Extends ASP.NET Core Identity's base user. Password hashing (PBKDF2), lockout, MFA, email confirmation are handled by `UserManager<FoundryUser>`.

### 5.5 Organization Entity

```csharp
public sealed class Organization : AuditableEntity<OrganizationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string? Domain { get; private set; }
    private readonly List<OrganizationMember> _members = [];
    public IReadOnlyList<OrganizationMember> Members => _members.AsReadOnly();
}

public sealed class OrganizationMember
{
    public Guid UserId { get; init; }
    public OrganizationId OrganizationId { get; init; }
    public OrgMemberRole Role { get; private set; } // Owner, Admin, Member
    public DateTimeOffset JoinedAt { get; init; }
}
```

### 5.6 JWT Claims Format

After migration, JWTs issued by OpenIddict contain:

```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "given_name": "Jane",
  "family_name": "Doe",
  "org_id": "org-guid",
  "tenant_id": "tenant-guid",
  "role": ["admin"],
  "scope": "openid profile showcases.read",
  "aud": "foundry-api",
  "iss": "https://api.foundry.dev/"
}
```

**Change from Keycloak format:** The `organization` claim changes from Keycloak's nested JSON (`{"orgId": {"name": "orgName"}}`) to a flat `org_id` GUID claim. `TenantResolutionMiddleware` already supports simple GUID parsing as a primary path before falling back to Keycloak JSON — after migration, the Keycloak JSON fallback branch is removed as dead code.

## 6. Service Replacement Map

### 6.1 Replaced Services

| Current Class | New Class | Interface Change | Implementation |
|---------------|-----------|-----------------|----------------|
| `KeycloakAdminService` | `UserService` | `IKeycloakAdminService` → `IUserService` (rename, same methods) | `UserManager<FoundryUser>` + `RoleManager<FoundryRole>` |
| `KeycloakOrganizationService` | `OrganizationService` | `IKeycloakOrganizationService` → `IOrganizationService` (rename, same methods) | EF Core CRUD on `Organization` entity |
| `KeycloakTokenService` | **Removed** | `ITokenService` removed | OpenIddict handles `/connect/token` natively. `AuthController` is deleted entirely — its `POST /token`, `POST /refresh`, and `POST /logout` endpoints are replaced by OpenIddict's `/connect/token` and `/connect/logout`. |
| `KeycloakServiceAccountService` | `ServiceAccountService` | `IServiceAccountService` (unchanged) | `IOpenIddictApplicationManager` + existing metadata table |
| `KeycloakSsoService` | `OidcFederationService` | `ISsoService` (remove SAML methods) | Dynamic `AddOpenIdConnect()` per tenant |
| `KeycloakDeveloperAppService` | `DeveloperAppService` | `IDeveloperAppService` (unchanged) | `IOpenIddictApplicationManager` |
| `ScimUserService` | `ScimUserService` (repointed) | No interface change | `UserManager<FoundryUser>` instead of Keycloak admin API |
| `ScimGroupService` | `ScimGroupService` (repointed) | No interface change | `OrganizationService` instead of Keycloak admin API |

### 6.2 New Components

| Component | Purpose |
|-----------|---------|
| `AuthorizationController` | Handles `/connect/authorize` — renders consent, delegates to OpenIddict (~100 lines) |
| `TokenController` | Handles `/connect/token` — validates grants, adds custom claims, delegates to OpenIddict (~80 lines) |
| `LogoutController` | Handles `/connect/logout` — signs out, delegates to OpenIddict (~30 lines) |
| `ClientsController` | Admin CRUD API for OAuth2 client management |
| `Login.cshtml` | Razor page — email/password form, calls `SignInManager` |
| `Logout.cshtml` | Razor page — confirms logout |
| `Consent.cshtml` | Razor page — scope consent for third-party apps |
| `FoundryUser` | ASP.NET Core Identity user entity |
| `FoundryRole` | ASP.NET Core Identity role entity |
| `Organization` | New aggregate root |
| `OrganizationMember` | Membership join entity |
| `OrganizationId` | Strongly-typed ID |

### 6.3 Removed Components

| Component | Reason |
|-----------|--------|
| `KeycloakAdminService.cs` | Replaced by `UserService` |
| `KeycloakOrganizationService.cs` | Replaced by `OrganizationService` |
| `KeycloakTokenService.cs` | Replaced by OpenIddict built-in endpoints |
| `KeycloakServiceAccountService.cs` | Replaced by `ServiceAccountService` |
| `KeycloakSsoService.cs` | Replaced by `OidcFederationService` |
| `KeycloakDeveloperAppService.cs` | Replaced by `DeveloperAppService` |
| `KeycloakIdpService.cs` | Replaced by `OidcFederationService` |
| `KeycloakOptions.cs` | No longer needed |
| `AuthController.cs` | Replaced by OpenIddict controllers |
| `Keycloak.AuthServices.Authentication` NuGet | Replaced by OpenIddict + Identity |
| `Keycloak.AuthServices.Sdk` NuGet | No longer needed |
| `Testcontainers.Keycloak` NuGet | Replaced by in-process test auth |
| Docker Keycloak container | Eliminated |
| `foundry-realm.json` | No longer needed |
| `configure-dcr.sh` | No longer needed |
| `SsoClaimsSyncService.cs` | Claims live in local Identity tables now |

### 6.4 Unchanged Components

| Component | Why Unchanged |
|-----------|---------------|
| `PermissionExpansionMiddleware` | Reads JWT claims — doesn't care who issued them |
| `TenantResolutionMiddleware` | Minor update to parse `org_id` claim instead of Keycloak JSON (see Section 5.6) |
| `ServiceAccountTrackingMiddleware` | Reads `azp` claim — provider-agnostic |
| `ApiKeyAuthenticationMiddleware` | Reads `X-Api-Key` header — no JWT involvement |
| `RedisApiKeyService` | Completely independent of identity provider |
| `RolePermissionMapping` | Static mapping of roles → permissions — provider-agnostic |
| `ScimService` (facade) | Delegates to user/group services — facade is clean |
| All non-Identity modules | Never reference Keycloak directly |
| Domain events | Same events published, just from new service implementations |

## 7. OpenIddict Configuration

### 7.1 Server Setup

```csharp
services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<IdentityDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetLogoutEndpointUris("/connect/logout")
            .SetUserinfoEndpointUris("/connect/userinfo");

        options.AllowAuthorizationCodeFlow()
            .AllowClientCredentialsFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange();

        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();
        // Production: .AddEncryptionCertificate() .AddSigningCertificate()

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableLogoutEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });
```

### 7.2 ASP.NET Core Identity Setup

```csharp
services.AddIdentity<FoundryUser, FoundryRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false; // configurable per deployment
    })
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();
```

### 7.3 JWT Validation (replaces Keycloak.AuthServices)

```csharp
services.AddAuthentication(options =>
    {
        options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    });
```

OpenIddict validation replaces `AddKeycloakWebApiAuthentication()`. JWT validation uses the local server's signing keys — no OIDC discovery round-trip needed.

## 8. Client Management API

### 8.1 ClientsController Endpoints

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| GET | `/api/v1/identity/clients` | AdminAccess | List all OAuth2 clients |
| POST | `/api/v1/identity/clients` | AdminAccess | Create a new client |
| GET | `/api/v1/identity/clients/{id}` | AdminAccess | Get client details |
| PUT | `/api/v1/identity/clients/{id}` | AdminAccess | Update client config |
| DELETE | `/api/v1/identity/clients/{id}` | AdminAccess | Remove a client |
| POST | `/api/v1/identity/clients/{id}/rotate-secret` | AdminAccess | Rotate client secret |

### 8.2 Client Types

| Type | Example | Grant | Created By |
|------|---------|-------|------------|
| First-party confidential | `foundry-web-bff` | Authorization Code + PKCE | Admin API |
| Service account | `sa-tenant1-my-service` | Client Credentials | ServiceAccountsController |
| Developer app | `app-cool-viewer` | Client Credentials | AppsController (DCR proxy) |

All three types are stored in the same `openiddict_applications` table. The prefix convention (`sa-*`, `app-*`) is enforced by the respective controllers, not by OpenIddict itself.

## 9. OIDC Federation

### 9.1 Dynamic Scheme Registration

Enterprise customers configure OIDC federation via the existing SSO API. `OidcFederationService` dynamically registers ASP.NET Core authentication schemes per tenant:

```csharp
public async Task SaveOidcConfigurationAsync(SaveOidcConfigRequest request)
{
    // Store config in identity.sso_configurations (encrypted)
    // Register dynamic OIDC scheme via IOptionsMonitor<OpenIdConnectOptions>
    // Login page shows "Sign in with [Provider Name]" for this tenant's users
}
```

### 9.2 External Login Flow

1. Login page detects user's tenant (by email domain or explicit selection)
2. If tenant has OIDC federation configured, shows SSO button
3. ASP.NET Core challenges the tenant-specific OIDC scheme
4. User authenticates at external IdP
5. On callback: find-or-create `FoundryUser`, link via `asp_net_user_logins`
6. Foundry issues its own JWT — external provider's token is never exposed to the client

### 9.3 SAML Removal

All SAML-specific code is removed:
- `SaveSamlConfigurationAsync` removed from `ISsoService`
- `GetSamlServiceProviderMetadataAsync` removed
- `SsoProtocol.Saml` enum value deprecated
- Existing SAML configurations in `sso_configurations` table are left in place but non-functional

## 10. SCIM Repointing

### 10.1 ScimUserService Changes

| Operation | Before (Keycloak) | After (Identity) |
|-----------|-------------------|-------------------|
| Create user | POST `/admin/realms/{realm}/users` | `UserManager<FoundryUser>.CreateAsync()` |
| Update user | PUT `/admin/realms/{realm}/users/{id}` | `UserManager<FoundryUser>.UpdateAsync()` |
| Delete user | DELETE `/admin/realms/{realm}/users/{id}` | `UserManager<FoundryUser>.DeleteAsync()` |
| Search users | GET `/admin/realms/{realm}/users?search=` | EF Core query on `asp_net_users` |
| Assign to org | POST `/admin/realms/{realm}/organizations/{id}/members` | `OrganizationService.AddMemberAsync()` |

### 10.2 ScimGroupService Changes

| Operation | Before (Keycloak) | After (Identity) |
|-----------|-------------------|-------------------|
| Create group | POST `/admin/realms/{realm}/organizations` | `OrganizationService.CreateAsync()` |
| Update group | PUT `/admin/realms/{realm}/organizations/{id}` | `OrganizationService.UpdateAsync()` |
| Delete group | DELETE `/admin/realms/{realm}/organizations/{id}` | `OrganizationService.DeleteAsync()` |
| List groups | GET `/admin/realms/{realm}/organizations` | EF Core query on `organizations` |
| Manage members | POST/DELETE `.../members` | `OrganizationService.AddMemberAsync/RemoveMemberAsync()` |

### 10.3 Unchanged

- `ScimService` facade — same interface, delegates to repointed user/group services
- `ScimSyncLog` audit trail — same entity, same logging
- SCIM token validation — same mechanism (stored in `scim_configurations`)
- `ScimToKeycloakTranslator` — renamed to `ScimAttributeMapper` and updated to map SCIM attributes to ASP.NET Core Identity user properties instead of Keycloak user representations

## 11. Middleware Updates

### 11.1 TenantResolutionMiddleware

**Before:**
```csharp
// Parse Keycloak 26 organization claim: {"orgGuid": {"name": "orgName"}}
string? orgClaim = user.FindFirst("organization")?.Value;
JsonDocument doc = JsonDocument.Parse(orgClaim);
// Complex nested JSON parsing...
```

**After:**
```csharp
// Parse flat org_id claim from Foundry-issued JWT
string? orgId = user.FindFirst("org_id")?.Value;
if (Guid.TryParse(orgId, out Guid orgGuid))
    tenantContext.SetTenant(TenantId.Create(orgGuid));
```

### 11.2 All Other Middleware

No changes. `PermissionExpansionMiddleware`, `ServiceAccountTrackingMiddleware`, and `ApiKeyAuthenticationMiddleware` read standard JWT claims (`azp`, `scope`, `role`, `permission`) that OpenIddict issues in the same format.

## 12. Test Infrastructure

### 12.1 Removed

- `Testcontainers.Keycloak` NuGet package
- `KeycloakFixture` test fixture
- `foundry-realm.json` test realm import

### 12.2 Replacement

**Unit tests:** Continue using `TestAuthHandler` (already in use) — creates `ClaimsPrincipal` directly without any identity provider. No changes needed.

**Integration tests:** Replace `KeycloakFixture` with in-process OpenIddict. The test server hosts the full Identity pipeline — no external containers needed. Tests call `/connect/token` on the same test host.

```csharp
public sealed class IdentityFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Seed test users
        using IServiceScope scope = Services.CreateScope();
        UserManager<FoundryUser> userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<FoundryUser>>();

        FoundryUser testUser = new()
        {
            UserName = "test@foundry.dev",
            Email = "test@foundry.dev",
            FirstName = "Test",
            LastName = "User",
            IsActive = true
        };
        await userManager.CreateAsync(testUser, "Test123!");
        await userManager.AddToRoleAsync(testUser, "admin");

        // Seed test OAuth2 client
        IOpenIddictApplicationManager appManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictApplicationManager>();

        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            }
        });
    }

    public new Task DisposeAsync() => base.DisposeAsync().AsTask();
}
```

### 12.3 Test Auth Helper

```csharp
public static async Task<string> GetAccessTokenAsync(
    HttpClient client, string clientId = "test-client", string clientSecret = "test-secret")
{
    Dictionary<string, string> form = new()
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = clientId,
        ["client_secret"] = clientSecret,
        ["scope"] = "showcases.read"
    };

    HttpResponseMessage response = await client.PostAsync(
        "/connect/token", new FormUrlEncodedContent(form));
    JsonDocument doc = await JsonDocument.ParseAsync(
        await response.Content.ReadAsStreamAsync());
    return doc.RootElement.GetProperty("access_token").GetString()!;
}
```

### 12.4 Migration of Existing Integration Tests

Existing integration tests that use `KeycloakFixture.CreateServiceAccountClientAsync()` are updated to use `IdentityFixture` and seed clients via `IOpenIddictApplicationManager` instead. The test surface is the same — tests still get real JWTs and call real endpoints — but without Docker containers.

## 13. Docker Changes

### 13.1 Removed from docker-compose.yml

```yaml
# REMOVED
keycloak:
  image: quay.io/keycloak/keycloak:26.0
  # ... entire service block removed

# REMOVED from postgres init
keycloak_db schema creation
```

### 13.2 Updated .env

Remove all `KEYCLOAK_*` environment variables.

### 13.3 Health Checks

Remove Keycloak health check from API startup health checks. The identity system is now in-process — if the API is healthy, identity is healthy.

## 14. Configuration Changes

### 14.1 Removed (appsettings.json)

```json
// REMOVED
"Identity": {
  "Keycloak": {
    "Realm": "foundry",
    "AuthorityUrl": "http://localhost:8080/",
    "AdminClientId": "foundry-api",
    "AdminClientSecret": "..."
  }
}
```

### 14.2 Added (appsettings.json)

```json
"Identity": {
  "SigningKey": {
    "Type": "Development"
  },
  "Password": {
    "RequiredLength": 8,
    "RequireNonAlphanumeric": true
  },
  "Lockout": {
    "MaxFailedAttempts": 5,
    "DefaultLockoutMinutes": 15
  }
}
```

Production deployments provide RSA certificates via `SigningKey:Path` and `SigningKey:Password` instead of `SigningKey:Type: Development`.

## 15. NuGet Package Changes

### 15.1 Removed

| Package | Project |
|---------|---------|
| `Keycloak.AuthServices.Authentication` | Identity.Infrastructure |
| `Keycloak.AuthServices.Sdk` | Identity.Infrastructure |
| `Testcontainers.Keycloak` | Tests.Common, Identity.IntegrationTests |

### 15.2 Added

| Package | Project |
|---------|---------|
| `OpenIddict.AspNetCore` | Identity.Infrastructure |
| `OpenIddict.EntityFrameworkCore` | Identity.Infrastructure |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Identity.Infrastructure |

## 16. Interface Renames

The Application layer interfaces are renamed to remove the Keycloak prefix. Method signatures remain the same (with minor exceptions noted).

| Before | After | Signature Changes |
|--------|-------|-------------------|
| `IKeycloakAdminService` | `IUserService` | None |
| `IKeycloakOrganizationService` | `IOrganizationService` | None |
| `ITokenService` | **Removed** | OpenIddict handles natively |
| `IServiceAccountService` | `IServiceAccountService` | None |
| `ISsoService` | `ISsoService` | Remove `SaveSamlConfigurationAsync`, `GetSamlServiceProviderMetadataAsync` |
| `IDeveloperAppService` | `IDeveloperAppService` | None |
| `IScimService` | `IScimService` | None |

All controllers that reference these interfaces are updated with the new names. Since the method signatures are unchanged, controller logic stays the same.

### 16.1 AuthController Migration

`AuthController` is **deleted entirely**. Its three endpoints are replaced:

| AuthController Endpoint | Replacement |
|------------------------|-------------|
| `POST /api/v1/identity/auth/token` (email+password → JWT) | `POST /connect/token` via `TokenController` — OpenIddict handles grant validation; `TokenController` adds custom claims (`org_id`, `tenant_id`) |
| `POST /api/v1/identity/auth/refresh` (refresh token → new JWT) | `POST /connect/token` with `grant_type=refresh_token` — OpenIddict handles natively |
| `POST /api/v1/identity/auth/logout` (revoke refresh token) | `POST /connect/logout` via `LogoutController` |

Any client code calling these endpoints must update to use the standard OIDC endpoints. Since this is pre-production, no backward compatibility shim is needed.

### 16.2 UserDto Mapping

Controllers currently call `IKeycloakAdminService` methods that return `UserDto`. After migration, `IUserService` returns the same `UserDto` — the DTO is provider-agnostic. The new `UserService` implementation maps from `FoundryUser` (Identity entity) to `UserDto`:

```csharp
public async Task<UserDto?> GetUserByIdAsync(Guid userId)
{
    FoundryUser? user = await _userManager.FindByIdAsync(userId.ToString());
    if (user is null) return null;

    IList<string> roles = await _userManager.GetRolesAsync(user);
    return new UserDto(
        user.Id, user.Email!, user.FirstName, user.LastName,
        user.IsActive, roles.ToList());
}
```

All existing `UserDto` usages in controllers and other services remain unchanged.

### 16.3 SsoController SAML Endpoint Removal

The following endpoints are **deleted** from `SsoController`:
- `POST /api/v1/identity/sso/saml` — delete the action method
- `GET /api/v1/identity/sso/saml/metadata` — delete the action method

The `SsoProtocol.Saml` enum value is marked `[Obsolete]` but not removed to avoid breaking existing `sso_configurations` rows that reference it. Existing SAML configurations in the database remain but are non-functional.

### 16.4 Organization EF Core Migration

A single EF Core migration adds the Organization tables:

```bash
dotnet ef migrations add AddOrganizationsAndIdentityTables \
    --project src/Modules/Identity/Foundry.Identity.Infrastructure \
    --startup-project src/Foundry.Api \
    --context IdentityDbContext
```

This migration adds:
- ASP.NET Core Identity tables (`asp_net_users`, `asp_net_roles`, etc.)
- OpenIddict tables (`openiddict_applications`, `openiddict_tokens`, etc.)
- `organizations` table with columns: `id`, `tenant_id`, `name`, `domain`, `created_at`, `created_by`, `updated_at`, `updated_by`
- `organization_members` table with columns: `user_id`, `organization_id`, `role`, `joined_at`

All tables use the `identity` schema, consistent with existing Identity module tables.

### 16.5 ClientsController Implementation

`ClientsController` wraps `IOpenIddictApplicationManager` with business logic:

```csharp
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/clients")]
[HasPermission(PermissionType.AdminAccess)]
public sealed class ClientsController(
    IOpenIddictApplicationManager applicationManager) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateClientRequest request, CancellationToken ct)
    {
        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = request.ClientId,
            DisplayName = request.DisplayName,
            ClientType = request.IsConfidential
                ? OpenIddictConstants.ClientTypes.Confidential
                : OpenIddictConstants.ClientTypes.Public,
        };

        // Add requested grant types
        foreach (string grant in request.GrantTypes)
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.GrantType + grant);

        // Add requested scopes
        foreach (string scope in request.Scopes)
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);

        // Add redirect URIs
        foreach (Uri uri in request.RedirectUris)
            descriptor.RedirectUris.Add(uri);

        string? secret = request.IsConfidential ? GenerateClientSecret() : null;
        object app = await applicationManager.CreateAsync(descriptor, secret, ct);

        return Created($"/api/v1/identity/clients/{request.ClientId}",
            new ClientCreatedResponse(request.ClientId, secret));
    }
}
```

The `sa-*` and `app-*` prefix conventions are enforced by `ServiceAccountsController` and `AppsController` respectively — `ClientsController` is for admin-managed first-party clients and does not enforce naming conventions.

## 17. Cutover Procedure

Since this is a pre-production big-bang cutover, the procedure is:

1. **Create feature branch** from `expansion`
2. **Add NuGet packages** — OpenIddict, ASP.NET Core Identity
3. **Update `IdentityDbContext`** — add Identity and OpenIddict entity configurations
4. **Generate EF migration** — one migration for all new tables
5. **Implement new services** — `UserService`, `OrganizationService`, `ServiceAccountService`, `OidcFederationService`, `DeveloperAppService`
6. **Implement OpenIddict controllers** — `AuthorizationController`, `TokenController`, `LogoutController`
7. **Add Razor pages** — Login, Logout, Consent
8. **Implement `ClientsController`** — admin API for client management
9. **Repoint SCIM services** — `ScimUserService` and `ScimGroupService` use `UserManager` and `OrganizationService`
10. **Update DI registration** — swap all Keycloak registrations for new implementations in `IdentityInfrastructureExtensions.cs`
11. **Update `TenantResolutionMiddleware`** — remove Keycloak JSON fallback, keep GUID parsing
12. **Delete Keycloak code** — all `Keycloak*Service` classes, `KeycloakOptions`, `AuthController`, Keycloak NuGet packages
13. **Update Docker** — remove Keycloak container and its PostgreSQL database from `docker-compose.yml`
14. **Update test infrastructure** — replace `KeycloakFixture` with in-process `IdentityFixture`
15. **Run full test suite** — `dotnet test`
16. **Seed development data** — create default admin user, default OAuth2 client via `ClientsController` or seeder

## 18. Risk Assessment


| Risk | Severity | Mitigation |
|------|----------|------------|
| Data loss during migration | Low | Pre-production — no live user data to migrate. Fresh EF migration. |
| Missing Keycloak feature parity | Medium | Comprehensive service replacement map (Section 6) covers all current functionality. SAML intentionally dropped. |
| OpenIddict learning curve | Medium | Well-documented library with extensive samples. Auth/Token controllers are boilerplate. |
| Dynamic OIDC federation complexity | Medium | ASP.NET Core supports dynamic scheme registration. Start with static schemes, add dynamic per-tenant later if needed. |
| Test coverage regression | Low | Existing `TestAuthHandler` unit tests are unaffected. Integration tests simplified (no Docker dependency). |
| JWT format change breaks clients | Low | Pre-production. Document new claim format (`org_id` instead of Keycloak `organization` JSON). |

## 19. Out of Scope

- SAML federation — dropped, OIDC only
- ROPC grant type — dropped, Auth Code + PKCE only
- Keycloak Admin UI replacement — admin API only, no UI
- User data migration tooling — pre-production, no existing users to migrate
- Multi-region token validation — future concern
- Social login (Google, GitHub) — can be added later via `AddGoogle()` / `AddGitHub()`
