# Security Remediation Design Document

**Date:** 2026-03-03
**Status:** Draft
**Based on:** Security audit sweep + independent verification of all findings

---

## 1. Executive Summary

### Confirmed Vulnerabilities by Severity

| Severity | Count |
|----------|-------|
| Critical | 2 |
| High | 3 |
| Medium | 10 |
| Low | 12 |
| Informational | 4 |

### Overall Risk Posture

The Wallow platform has a **generally strong security architecture** with defense-in-depth: EF Core global query filters for tenant isolation, a permission-based authorization system, comprehensive security headers, proper exception handling, parameterized Dapper queries, and encrypted secrets at rest. However, two critical gaps and several high-severity cross-tenant isolation failures require immediate remediation.

### Top 5 Most Critical Items

1. **AUTH-001: No JWT Authentication Scheme Registered** -- The production code never calls `AddAuthentication().AddJwtBearer()`. JWT tokens are not validated (no signature, expiry, or issuer checks). This either means JWT auth is completely broken (all JWT requests return 401) or tokens are accepted without validation.
2. **C-1: Quota Admin Endpoints Missing Authorization** -- Any authenticated user can modify quotas for any tenant via `SetOverride` and `RemoveOverride` endpoints. This is a privilege escalation and cross-tenant write vulnerability.
3. **AUTH-005: User Operations Missing Tenant Ownership Check** -- `DeactivateUser`, `ActivateUser`, `AssignRole`, and `RemoveRole` endpoints have no tenant validation. A user with `UsersUpdate` permission in Tenant A can deactivate users in Tenant B.
4. **AUTH-006: Organization Endpoints Allow Cross-Tenant Access** -- All organization CRUD operations lack tenant scoping. Users can enumerate and modify members of any organization by GUID.
5. **HIGH-1 (Tenant): Redis Presence Service Has No Tenant Isolation** -- Presence data is stored in global Redis keys with no tenant prefix. Users in one tenant can see online status and page presence of users in other tenants.

---

## 2. Risk Matrix

Risk Score = Likelihood (1-5) x Impact (1-5)

| ID | Finding | Likelihood | Impact | Risk Score | Priority Tier |
|----|---------|-----------|--------|------------|---------------|
| AUTH-001 | No JWT authentication scheme registered | 5 | 5 | **25** | Immediate |
| C-1 | Quota admin endpoints missing authorization | 4 | 5 | **20** | Immediate |
| AUTH-005 | User operations missing tenant ownership | 4 | 5 | **20** | Immediate |
| AUTH-006 | Organization endpoints cross-tenant access | 4 | 5 | **20** | Immediate |
| HIGH-1-T | Presence service no tenant isolation | 4 | 4 | **16** | Immediate |
| AUTH-011 / M-6 | Hangfire dashboard role case mismatch | 3 | 4 | **12** | Short-term |
| M-1 / AUTH-004 | SCIM controller [AllowAnonymous] design | 2 | 5 | **10** | Short-term |
| AUTH-010 | No token revocation endpoint | 3 | 3 | **9** | Short-term |
| AUTH-009 | Auth rate limiting may be insufficient | 3 | 3 | **9** | Short-term |
| M-2 | SCIM endpoints not rate limited | 2 | 4 | **8** | Short-term |
| AUTH-002 / MEDIUM-2-T | Admin tenant override audit gaps | 2 | 4 | **8** | Short-term |
| H-4 / SEC-S09 | Health check details in staging | 2 | 3 | **6** | Medium-term |
| M-4 | No request body size limits | 2 | 3 | **6** | Medium-term |
| MED-4-DB | Report services standalone DB connections | 2 | 3 | **6** | Medium-term |
| AUTH-003 | Wolverine handlers no authorization | 2 | 3 | **6** | Medium-term |
| SEC-S01 / SEC-S11 | Redis password hardcoded in base config | 2 | 3 | **6** | Medium-term |
| SEC-S02 | Keycloak client secret in realm export | 2 | 2 | **4** | Medium-term |
| SEC-S10 | No startup config placeholder validation | 2 | 2 | **4** | Medium-term |
| L-1 / AUTH-008 | Account enumeration via error messages | 2 | 2 | **4** | Backlog |
| AUTH-012 | API key identity missing organization claim | 2 | 2 | **4** | Backlog |
| C-2 | MetersController missing permission (read-only metadata) | 2 | 1 | **2** | Backlog |
| H-3 | Announcements dismiss missing permission | 2 | 1 | **2** | Backlog |
| M-3 / AUTH-007 | Admin role auto-grants all permissions | 1 | 2 | **2** | Backlog |
| M-5 | Feature flags evaluate endpoint versioning | 1 | 1 | **1** | Backlog |
| AUTH-013 | SignalR hub limited group validation | 1 | 2 | **2** | Backlog |
| AUTH-014 | Service account permission reuse | 1 | 1 | **1** | Backlog |
| SEC-S03 | Design-time factory hardcoded creds | 1 | 1 | **1** | Backlog |
| LOW-2-DB | Dapper queries missing CancellationToken | 1 | 2 | **2** | Backlog |

---

## 3. Detailed Remediation Plans

### Phase 1: Immediate (Risk Score >= 15)

---

#### AUTH-001: No JWT Authentication Scheme Registered

- **Severity:** Critical | **Risk Score:** 25
- **Affected Files:**
  - `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`
  - `src/Wallow.Api/Program.cs`
- **Vulnerability Description:**
  The production code never registers a JWT Bearer authentication scheme. `app.UseAuthentication()` at `Program.cs:352` is a no-op without a registered scheme. The `PermissionAuthorizationPolicyProvider.GetFallbackPolicyAsync()` returns `RequireAuthenticatedUser()`, which provides a safety net by blocking unauthenticated requests, but no actual JWT validation occurs -- no signature verification, no token expiry checks, no issuer/audience validation. The practical effect is that **all JWT-based requests are rejected** (returning 401) because `IsAuthenticated` is never set to `true` by the authentication middleware.

  **Attack Scenario:** If this is misconfigured such that tokens are accepted without validation, any self-signed JWT would grant access. More likely, JWT auth is simply broken, preventing all non-API-key authentication from working.

- **Current Code:** No JWT registration exists in production code. The only registrations are in test code:
  ```csharp
  // tests/Wallow.Tests.Common/Factories/WallowApiFactory.cs:155
  services.AddAuthentication("Test").AddScheme<...>("Test", ...);
  ```

- **Recommended Fix:**
  Register Keycloak JWT Bearer authentication in the Identity module's infrastructure extensions. The `Keycloak.AuthServices.Authentication` package is already referenced.

- **Example Code:**
  ```csharp
  // In IdentityInfrastructureExtensions.cs - add to AddIdentityInfrastructure method
  public static IServiceCollection AddIdentityInfrastructure(
      this IServiceCollection services,
      IConfiguration configuration)
  {
      // Register Keycloak JWT Bearer authentication
      services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
          .AddKeycloakWebApiAuthentication(
              configuration,
              options =>
              {
                  options.RequireHttpsMetadata = !Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?.Equals("Development", StringComparison.OrdinalIgnoreCase) ?? true;
                  options.Audience = "wallow-api";
              },
              configSectionName: "Keycloak");

      // Existing authorization registration
      services.AddIdentityAuthorization();

      // ... rest of existing registrations
      return services;
  }
  ```

- **Testing Requirements:**
  - Verify JWT tokens from Keycloak are accepted and validated (signature, expiry, issuer, audience)
  - Verify expired tokens are rejected with 401
  - Verify tokens from wrong issuer are rejected
  - Verify API key authentication still works alongside JWT
  - Run existing integration test suite to confirm no regressions

- **Estimated Complexity:** Medium

---

#### C-1: Quota Admin Endpoints Missing Authorization Policy

- **Severity:** Critical | **Risk Score:** 20
- **Affected Files:**
  - `src/Modules/Billing/Wallow.Billing.Api/Controllers/QuotasController.cs:48-89`
- **Vulnerability Description:**
  The `SetOverride` and `RemoveOverride` endpoints accept an arbitrary `tenantId` GUID parameter and modify quotas for that tenant. The controller has `[Authorize]` at class level but **no `[HasPermission]` attribute** on these admin endpoints. Any authenticated user can set or remove quota overrides for any tenant, bypassing billing controls entirely.

  **Attack Scenario:** An authenticated user calls `PUT /api/v1/metering/quotas/admin/{victimTenantId}` with `Limit: 999999999` to remove quota limits for their own or any tenant, enabling unlimited resource consumption without billing enforcement.

- **Current Code:**
  ```csharp
  // QuotasController.cs:48-66 - No [HasPermission] attribute
  [HttpPut("admin/{tenantId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<IActionResult> SetOverride(
      Guid tenantId,
      [FromBody] SetQuotaOverrideRequest request,
      CancellationToken cancellationToken)
  {
      SetQuotaOverrideCommand command = new SetQuotaOverrideCommand(
          tenantId, request.MeterCode, request.Limit, request.Period, request.OnExceeded);
      Result result = await _bus.InvokeAsync<Result>(command, cancellationToken);
      return result.ToActionResult();
  }
  ```

- **Recommended Fix:**
  Add a `[HasPermission]` attribute with an admin-level billing permission. A new `BillingAdmin` permission type may be needed, or use the existing admin role check.

- **Example Code:**
  ```csharp
  /// <summary>
  /// Set a quota override for a tenant (admin only).
  /// </summary>
  [HttpPut("admin/{tenantId:guid}")]
  [HasPermission(PermissionType.BillingAdmin)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> SetOverride(
      Guid tenantId,
      [FromBody] SetQuotaOverrideRequest request,
      CancellationToken cancellationToken)
  {
      // ... existing implementation unchanged
  }

  /// <summary>
  /// Remove a quota override for a tenant (admin only).
  /// </summary>
  [HttpDelete("admin/{tenantId:guid}/{meterCode}")]
  [HasPermission(PermissionType.BillingAdmin)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RemoveOverride(
      Guid tenantId,
      string meterCode,
      CancellationToken cancellationToken)
  {
      // ... existing implementation unchanged
  }

  /// <summary>
  /// Get quota status for current tenant.
  /// </summary>
  [HttpGet]
  [HasPermission(PermissionType.BillingRead)]
  [ProducesResponseType(typeof(IReadOnlyList<QuotaStatusDto>), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
  {
      // ... existing implementation unchanged
  }
  ```

  Also add the new permission to `PermissionType`:
  ```csharp
  // In Shared.Kernel/Identity/Authorization/PermissionType.cs
  public const string BillingAdmin = "billing.admin";
  ```

  And register it in `RolePermissionMapping` for the admin role (already auto-granted via `PermissionType.All`).

- **Testing Requirements:**
  - Verify non-admin authenticated users receive 403 on `SetOverride` and `RemoveOverride`
  - Verify admin users can still call both endpoints
  - Verify `GetAll` works with `BillingRead` permission

- **Estimated Complexity:** Small

---

#### AUTH-005: Organization Access Control Missing on Multiple User Operations

- **Severity:** High | **Risk Score:** 20
- **Affected Files:**
  - `src/Modules/Identity/Wallow.Identity.Api/Controllers/UsersController.cs:115-157`
- **Vulnerability Description:**
  `GetUserById` correctly validates that the target user belongs to the current tenant's organization (lines 66-72), but `DeactivateUser`, `ActivateUser`, `AssignRole`, and `RemoveRole` skip this check entirely. They pass the raw GUID directly to the Keycloak admin service.

  **Attack Scenario:** A user with `UsersUpdate` permission in Tenant A discovers (or guesses) a user GUID from Tenant B and calls `POST /api/v1/identity/users/{tenantBUserId}/deactivate`, successfully deactivating a user in another tenant. Similarly, `AssignRole` could be used for cross-tenant privilege escalation.

- **Current Code (vulnerable):**
  ```csharp
  // UsersController.cs:115-121 - No tenant ownership check
  [HttpPost("{id:guid}/deactivate")]
  [HasPermission(PermissionType.UsersUpdate)]
  public async Task<ActionResult> DeactivateUser(Guid id, CancellationToken ct)
  {
      await _keycloakAdmin.DeactivateUserAsync(id, ct);
      return NoContent();
  }
  ```

- **Recommended Fix:**
  Extract the tenant ownership check from `GetUserById` into a private helper method and apply it to all user mutation endpoints.

- **Example Code:**
  ```csharp
  // Add private helper method to UsersController
  private async Task<bool> UserBelongsToTenantAsync(Guid userId, CancellationToken ct)
  {
      IReadOnlyList<OrganizationDto> userOrgs = await _keycloakOrg.GetUserOrganizationsAsync(userId, ct);
      return userOrgs.Any(o => o.Id == _tenantContext.TenantId.Value);
  }

  [HttpPost("{id:guid}/deactivate")]
  [HasPermission(PermissionType.UsersUpdate)]
  public async Task<ActionResult> DeactivateUser(Guid id, CancellationToken ct)
  {
      if (!await UserBelongsToTenantAsync(id, ct))
      {
          return NotFound();
      }

      await _keycloakAdmin.DeactivateUserAsync(id, ct);
      return NoContent();
  }

  [HttpPost("{id:guid}/activate")]
  [HasPermission(PermissionType.UsersUpdate)]
  public async Task<ActionResult> ActivateUser(Guid id, CancellationToken ct)
  {
      if (!await UserBelongsToTenantAsync(id, ct))
      {
          return NotFound();
      }

      await _keycloakAdmin.ActivateUserAsync(id, ct);
      return NoContent();
  }

  [HttpPost("{userId:guid}/roles")]
  [HasPermission(PermissionType.RolesUpdate)]
  public async Task<ActionResult> AssignRole(
      Guid userId,
      [FromBody] AssignRoleRequest request,
      CancellationToken ct)
  {
      if (!await UserBelongsToTenantAsync(userId, ct))
      {
          return NotFound();
      }

      await _keycloakAdmin.AssignRoleAsync(userId, request.RoleName, ct);
      return NoContent();
  }

  [HttpDelete("{userId:guid}/roles/{roleName}")]
  [HasPermission(PermissionType.RolesUpdate)]
  public async Task<ActionResult> RemoveRole(Guid userId, string roleName, CancellationToken ct)
  {
      if (!await UserBelongsToTenantAsync(userId, ct))
      {
          return NotFound();
      }

      await _keycloakAdmin.RemoveRoleAsync(userId, roleName, ct);
      return NoContent();
  }
  ```

- **Testing Requirements:**
  - Verify cross-tenant user operations return 404
  - Verify same-tenant user operations succeed
  - Verify `GetUsers` list endpoint returns only tenant-scoped users (may need additional scoping)

- **Estimated Complexity:** Small

---

#### AUTH-006: Organization Endpoints Allow Cross-Tenant Access

- **Severity:** High | **Risk Score:** 20
- **Affected Files:**
  - `src/Modules/Identity/Wallow.Identity.Api/Controllers/OrganizationsController.cs:33-97`
- **Vulnerability Description:**
  All organization controller endpoints (`GetById`, `GetMembers`, `AddMember`, `RemoveMember`, `GetAll`, `Create`) operate on arbitrary organization GUIDs without validating that the target organization belongs to the current tenant.

  **Attack Scenario:** A user with `OrganizationsManageMembers` permission in Tenant A calls `POST /api/v1/identity/organizations/{tenantBOrgId}/members` to add themselves (or an accomplice) to Tenant B's organization, gaining access to Tenant B's data.

- **Current Code (vulnerable):**
  ```csharp
  // OrganizationsController.cs:80-86 - No tenant validation
  [HttpPost("{id:guid}/members")]
  [HasPermission(PermissionType.OrganizationsManageMembers)]
  public async Task<ActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
  {
      await _orgService.AddMemberAsync(id, request.UserId, ct);
      return NoContent();
  }
  ```

- **Recommended Fix:**
  Inject `ITenantContext` and validate that the target organization ID matches the current tenant before all operations. For `GetAll`, filter to only return the current tenant's organization.

- **Example Code:**
  ```csharp
  public class OrganizationsController : ControllerBase
  {
      private readonly IOrganizationService _orgService;
      private readonly ITenantContext _tenantContext;

      public OrganizationsController(
          IOrganizationService orgService,
          ITenantContext tenantContext)
      {
          _orgService = orgService;
          _tenantContext = tenantContext;
      }

      private bool IsCurrentTenantOrg(Guid orgId) =>
          orgId == _tenantContext.TenantId.Value;

      [HttpGet("{id:guid}")]
      [HasPermission(PermissionType.OrganizationsRead)]
      public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken ct)
      {
          if (!IsCurrentTenantOrg(id))
          {
              return NotFound();
          }

          OrganizationDto? org = await _orgService.GetOrganizationByIdAsync(id, ct);
          return org is null ? NotFound() : Ok(org);
      }

      [HttpGet("{id:guid}/members")]
      [HasPermission(PermissionType.OrganizationsRead)]
      public async Task<ActionResult<IReadOnlyList<UserDto>>> GetMembers(Guid id, CancellationToken ct)
      {
          if (!IsCurrentTenantOrg(id))
          {
              return NotFound();
          }

          return Ok(await _orgService.GetMembersAsync(id, ct));
      }

      [HttpPost("{id:guid}/members")]
      [HasPermission(PermissionType.OrganizationsManageMembers)]
      public async Task<ActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
      {
          if (!IsCurrentTenantOrg(id))
          {
              return NotFound();
          }

          await _orgService.AddMemberAsync(id, request.UserId, ct);
          return NoContent();
      }

      [HttpDelete("{id:guid}/members/{userId:guid}")]
      [HasPermission(PermissionType.OrganizationsManageMembers)]
      public async Task<ActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
      {
          if (!IsCurrentTenantOrg(id))
          {
              return NotFound();
          }

          await _orgService.RemoveMemberAsync(id, userId, ct);
          return NoContent();
      }
  }
  ```

- **Testing Requirements:**
  - Verify cross-tenant organization access returns 404
  - Verify same-tenant operations succeed
  - Verify `GetAll` only returns current tenant's organization
  - Verify `AddMember` / `RemoveMember` cannot target other tenants

- **Estimated Complexity:** Small

---

#### HIGH-1 (Tenant): Presence Service Has No Tenant Isolation

- **Severity:** High | **Risk Score:** 16
- **Affected Files:**
  - `src/Wallow.Api/Services/RedisPresenceService.cs:10-158`
  - `src/Wallow.Api/Hubs/RealtimeHub.cs:67-85`
- **Vulnerability Description:**
  Redis presence keys are flat globals with no tenant prefix. `GetOnlineUsersAsync()` returns all users across all tenants. `GetUsersOnPageAsync()` returns cross-tenant page viewers. The `RealtimeHub.UpdatePageContext()` method broadcasts this data to SignalR clients, leaking cross-tenant presence information.

  **Attack Scenario:** User in Tenant A navigates to a page. The presence data broadcast via SignalR includes user IDs and page context from Tenant B users on the same page, revealing that Tenant B's employees are active and what pages they are viewing.

- **Current Code (vulnerable):**
  ```csharp
  // RedisPresenceService.cs:10-13 - Global keys with no tenant prefix
  private const string ConnectionToUserKey = "presence:conn2user";
  private const string UserConnectionsPrefix = "presence:user:";
  private const string ConnectionPagePrefix = "presence:connpage:";
  private const string PageViewersPrefix = "presence:page:";
  ```

- **Recommended Fix:**
  Add tenant ID to all Redis presence keys. The `IPresenceService` interface needs a tenant parameter, or the service should accept `ITenantContext`.

- **Example Code:**
  ```csharp
  internal sealed partial class RedisPresenceService(
      IConnectionMultiplexer redis,
      ILogger<RedisPresenceService> logger) : IPresenceService
  {
      private static readonly TimeSpan _connectionTtl = TimeSpan.FromMinutes(30);

      private IDatabase Db => redis.GetDatabase();

      // Tenant-scoped key builders
      private static string ConnectionToUserKey(Guid tenantId) => $"presence:{tenantId}:conn2user";
      private static string UserConnectionsKey(Guid tenantId, string userId) => $"presence:{tenantId}:user:{userId}";
      private static string ConnectionPageKey(string connectionId) => $"presence:connpage:{connectionId}";
      private static string PageViewersKey(Guid tenantId, string pageContext) => $"presence:{tenantId}:page:{pageContext}";

      public async Task TrackConnectionAsync(Guid tenantId, string userId, string connectionId, CancellationToken ct = default)
      {
          IDatabase db = Db;
          IBatch batch = db.CreateBatch();

          _ = batch.HashSetAsync(ConnectionToUserKey(tenantId), connectionId, userId);
          string userKey = UserConnectionsKey(tenantId, userId);
          _ = batch.SetAddAsync(userKey, connectionId);
          _ = batch.KeyExpireAsync(userKey, _connectionTtl);
          // Store tenantId alongside connection for cleanup
          _ = batch.StringSetAsync($"presence:conn:tenant:{connectionId}", tenantId.ToString(), _connectionTtl);

          batch.Execute();
          await Task.CompletedTask;
      }

      public async Task<IReadOnlyList<UserPresence>> GetOnlineUsersAsync(Guid tenantId, CancellationToken ct = default)
      {
          IDatabase db = Db;
          HashEntry[] allEntries = await db.HashGetAllAsync(ConnectionToUserKey(tenantId));
          // ... rest of implementation using tenant-scoped keys
      }

      public async Task<IReadOnlyList<UserPresence>> GetUsersOnPageAsync(Guid tenantId, string pageContext, CancellationToken ct = default)
      {
          IDatabase db = Db;
          RedisValue[] connectionIds = await db.SetMembersAsync(PageViewersKey(tenantId, pageContext));
          // ... rest of implementation using tenant-scoped keys
      }
  }
  ```

  The `RealtimeHub` already has `ITenantContext` injected and should pass `_tenantContext.TenantId.Value` to all presence service calls.

- **Testing Requirements:**
  - Verify users in Tenant A cannot see presence data from Tenant B
  - Verify presence tracking works correctly within a single tenant
  - Verify connection cleanup removes tenant-scoped keys
  - Load test to ensure tenant-prefixed keys do not cause Redis performance issues

- **Estimated Complexity:** Medium

---

### Phase 2: Short-term (Risk Score 8-14)

---

#### AUTH-011 / M-6: Hangfire Dashboard Role Case Mismatch

- **Severity:** Medium | **Risk Score:** 12
- **Affected Files:**
  - `src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs:22-23`
  - `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/RolePermissionMapping.cs:9`
- **Vulnerability Description:**
  `HangfireDashboardAuthFilter` checks `IsInRole("Admin")` with capital A, while `RolePermissionMapping` uses lowercase `"admin"`. `ClaimsPrincipal.IsInRole()` performs case-sensitive comparison by default. Keycloak typically maps roles as lowercase, so the check may never match, locking all admins out of the Hangfire dashboard in production. In development, the filter returns `true` unconditionally, meaning anyone with network access can view and manipulate background jobs.

- **Current Code:**
  ```csharp
  // HangfireDashboardAuthFilter.cs:14-24
  public bool Authorize(DashboardContext context)
  {
      if (_environment.IsDevelopment())
      {
          return true;
      }

      HttpContext httpContext = context.GetHttpContext();
      return httpContext.User.Identity?.IsAuthenticated == true
          && httpContext.User.IsInRole("Admin");  // Capital A -- likely never matches
  }
  ```

- **Example Code:**
  ```csharp
  public bool Authorize(DashboardContext context)
  {
      if (_environment.IsDevelopment())
      {
          return true;
      }

      HttpContext httpContext = context.GetHttpContext();
      return httpContext.User.Identity?.IsAuthenticated == true
          && httpContext.User.Claims
              .Where(c => c.Type == ClaimTypes.Role)
              .Any(c => string.Equals(c.Value, "admin", StringComparison.OrdinalIgnoreCase));
  }
  ```

- **Testing Requirements:**
  - Verify admin users with lowercase "admin" role can access Hangfire dashboard
  - Verify non-admin users cannot access the dashboard in production

- **Estimated Complexity:** Small

---

#### M-1 / AUTH-004: SCIM Controller Uses [AllowAnonymous] with Middleware-Based Auth

- **Severity:** Medium | **Risk Score:** 10
- **Affected Files:**
  - `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs:20`
  - `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ScimAuthenticationMiddleware.cs`
- **Vulnerability Description:**
  The SCIM controller is marked `[AllowAnonymous]`, bypassing the ASP.NET Core authorization pipeline. Authentication is handled solely by `ScimAuthenticationMiddleware`. While currently secure, this is fragile -- any middleware reordering silently removes SCIM authentication. Defense-in-depth dictates using the standard auth pipeline.

- **Recommended Fix:**
  Register a custom `"ScimBearer"` authentication scheme and replace `[AllowAnonymous]` with `[Authorize(AuthenticationSchemes = "ScimBearer")]`. Move the token validation logic from the middleware into an `AuthenticationHandler<T>`.

- **Example Code:**
  ```csharp
  // Register in DI
  services.AddAuthentication()
      .AddScheme<ScimBearerOptions, ScimBearerAuthenticationHandler>("ScimBearer", null);

  // On the controller
  [Authorize(AuthenticationSchemes = "ScimBearer")]
  public class ScimController : ControllerBase { ... }
  ```

- **Testing Requirements:**
  - Verify SCIM endpoints still accept valid SCIM bearer tokens
  - Verify invalid/missing tokens return 401
  - Verify SCIM discovery endpoints remain accessible (per SCIM spec)

- **Estimated Complexity:** Medium

---

#### AUTH-010: No Token Revocation Endpoint

- **Severity:** Medium | **Risk Score:** 9
- **Affected Files:**
  - `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthController.cs`
- **Vulnerability Description:**
  No logout or token revocation endpoint exists. Compromised refresh tokens remain valid until expiry. Refresh tokens typically have long lifetimes, leaving a large vulnerability window.

- **Recommended Fix:**
  Add a `/auth/logout` endpoint that calls Keycloak's token revocation endpoint.

- **Example Code:**
  ```csharp
  [HttpPost("logout")]
  [Authorize]
  public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
  {
      await _keycloakAuthService.RevokeTokenAsync(request.RefreshToken, ct);
      return NoContent();
  }

  public sealed record LogoutRequest(string RefreshToken);
  ```

- **Testing Requirements:**
  - Verify revoked refresh tokens cannot be used to obtain new access tokens
  - Verify the endpoint returns 204 on success

- **Estimated Complexity:** Small

---

#### AUTH-009: Rate Limiting on Auth Endpoints May Be Insufficient

- **Severity:** Medium | **Risk Score:** 9
- **Affected Files:**
  - `src/Wallow.Api/Extensions/RateLimitDefaults.cs:5-6`
- **Vulnerability Description:**
  Per-IP rate limit of 5 attempts per 5 minutes (60/hour) is reasonable for basic protection but insufficient against distributed brute force. No per-account rate limiting exists.

- **Recommended Fix:**
  Delegate brute force detection to Keycloak (which has built-in support) and consider adding per-account rate limiting as a secondary defense.

- **Example Code:**
  ```csharp
  // RateLimitDefaults.cs - tighten limits
  public const int AuthPermitLimit = 3;       // was 5
  public const int AuthWindowMinutes = 10;     // was 5
  ```

  Enable Keycloak brute force detection in realm settings (operational change, not code).

- **Testing Requirements:**
  - Verify rate limits are enforced per IP
  - Verify Keycloak brute force detection is enabled in realm config

- **Estimated Complexity:** Small

---

#### M-2: SCIM Endpoints Not Rate Limited

- **Severity:** Medium | **Risk Score:** 8
- **Affected Files:**
  - `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs`
- **Vulnerability Description:**
  SCIM endpoints create/delete Keycloak users but rely only on the global rate limiter (1000 req/hour). A compromised SCIM token could rapidly provision rogue accounts.

- **Recommended Fix:**
  Add a dedicated rate limiting policy for SCIM with a lower threshold.

- **Example Code:**
  ```csharp
  // Register SCIM rate limit policy in ServiceCollectionExtensions.cs
  options.AddFixedWindowLimiter("scim", limiter =>
  {
      limiter.PermitLimit = 100;
      limiter.Window = TimeSpan.FromHours(1);
  });

  // Apply to ScimController
  [EnableRateLimiting("scim")]
  public class ScimController : ControllerBase { ... }
  ```

- **Testing Requirements:**
  - Verify SCIM rate limit is enforced independently of global limit

- **Estimated Complexity:** Small

---

#### AUTH-002 / MEDIUM-2 (Tenant): Admin Tenant Override Audit Gaps

- **Severity:** Medium | **Risk Score:** 8
- **Affected Files:**
  - `src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs:41-51`
- **Vulnerability Description:**
  Admin tenant override via `X-Tenant-Id` header uses only the broad "admin" role, does not validate that the target tenant exists, and logs only via Serilog (no persistent audit table).

- **Recommended Fix:**
  1. Validate the target tenant GUID exists in the system
  2. Consider a more specific role requirement for cross-tenant access
  3. Log admin overrides to the audit table for persistent tracking

- **Example Code:**
  ```csharp
  if (!string.IsNullOrEmpty(headerTenantId) &&
      HasRealmAdminRole(context.User) &&
      Guid.TryParse(headerTenantId, out Guid overrideId))
  {
      // Validate tenant exists
      if (!await tenantValidator.TenantExistsAsync(overrideId))
      {
          context.Response.StatusCode = 400;
          await context.Response.WriteAsJsonAsync(new { error = "Invalid tenant ID" });
          return;
      }

      tenantSetter.SetTenant(TenantId.Create(overrideId));
      LogAdminTenantOverride(overrideId, resolvedTenantId, userId, requestPath);

      // Persist to audit table
      await auditService.LogAdminOverrideAsync(userId, resolvedTenantId, overrideId, requestPath);
  }
  ```

- **Testing Requirements:**
  - Verify invalid tenant GUIDs are rejected
  - Verify admin override events appear in audit table
  - Verify non-admin users cannot use X-Tenant-Id header

- **Estimated Complexity:** Medium

---

### Phase 3: Medium-term (Risk Score 4-7)

---

#### H-4 / SEC-S09: Health Check Details Exposed in Staging

- **Severity:** Medium | **Risk Score:** 6
- **Affected Files:**
  - `src/Wallow.Api/Program.cs:421-452`
- **Vulnerability Description:**
  Health check response uses `env.IsProduction()` to gate detail suppression. Staging and other non-production environments expose component names, durations, and exception messages to unauthenticated users.

- **Recommended Fix:**
  Change the guard to `!env.IsDevelopment()` to suppress details in all non-development environments.

- **Example Code:**
  ```csharp
  // Change from:
  if (env.IsProduction())
  // To:
  if (!env.IsDevelopment())
  ```

- **Testing Requirements:**
  - Verify staging health checks return only status
  - Verify development health checks still show full details

- **Estimated Complexity:** Small

---

#### M-4: No Request Body Size Limits on Most Endpoints

- **Severity:** Medium | **Risk Score:** 6
- **Affected Files:**
  - `src/Wallow.Api/Program.cs` (Kestrel configuration)
- **Vulnerability Description:**
  Only `StorageController.Upload` sets an explicit `[RequestSizeLimit]`. All other endpoints use Kestrel's 30MB default, enabling resource exhaustion via large JSON payloads.

- **Recommended Fix:**
  Set a global request body size limit in Kestrel configuration and override only for endpoints that need larger payloads.

- **Example Code:**
  ```csharp
  // In Program.cs Kestrel configuration
  builder.WebHost.ConfigureKestrel(options =>
  {
      options.Limits.MaxRequestBodySize = 1 * 1024 * 1024; // 1MB global default
  });
  ```

- **Testing Requirements:**
  - Verify payloads > 1MB are rejected on non-upload endpoints
  - Verify file uploads still work with the explicit `[RequestSizeLimit]`

- **Estimated Complexity:** Small

---

#### MED-4 (DB): Report Services Create Standalone DB Connections

- **Severity:** Medium | **Risk Score:** 6
- **Affected Files:**
  - `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/InvoiceReportService.cs:14-18,43`
  - `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/PaymentReportService.cs:14-18,43`
- **Vulnerability Description:**
  These services create their own `NpgsqlConnection` from a stored connection string instead of using `BillingDbContext.Database.GetDbConnection()`, causing connection pool fragmentation and bypassing EF Core interceptors.

- **Recommended Fix:**
  Inject `BillingDbContext` and use `GetDbConnection()`, matching the pattern in `InvoiceQueryService` and `RevenueReportService`.

- **Example Code:**
  ```csharp
  // Before (InvoiceReportService.cs)
  public InvoiceReportService(IConfiguration configuration, ITenantContext tenantContext)
  {
      _connectionString = configuration.GetConnectionString("DefaultConnection")!;
      _tenantContext = tenantContext;
  }

  // After
  public InvoiceReportService(BillingDbContext context, ITenantContext tenantContext)
  {
      _context = context;
      _tenantContext = tenantContext;
  }

  // In query methods, replace:
  await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
  // With:
  DbConnection connection = _context.Database.GetDbConnection();
  ```

- **Testing Requirements:**
  - Verify report queries produce identical results
  - Verify connection pooling metrics improve

- **Estimated Complexity:** Small

---

#### AUTH-003: Wolverine Message Handlers Have No Authorization Checks

- **Severity:** Medium | **Risk Score:** 6
- **Affected Files:**
  - All `*Handler.cs` files under `src/Modules/*/Application/`
- **Vulnerability Description:**
  Wolverine handlers perform no authorization checks. While controllers enforce permissions before invoking handlers, messages arriving via RabbitMQ (if compromised) or internal code paths bypass authorization.

- **Recommended Fix:**
  Add a Wolverine middleware that validates authorization for commands arriving via external transport. This is a defense-in-depth measure.

- **Example Code:**
  ```csharp
  // Wolverine middleware for external message authorization
  public class AuthorizationCheckMiddleware
  {
      public static void Before(Envelope envelope, ITenantContext tenantContext)
      {
          // Only enforce for messages from external transport
          if (envelope.Destination?.Scheme == "rabbitmq" && !tenantContext.IsResolved)
          {
              throw new UnauthorizedAccessException("External messages must have valid tenant context");
          }
      }
  }
  ```

- **Testing Requirements:**
  - Verify handlers still work when invoked from controllers
  - Verify unauthorized external messages are rejected

- **Estimated Complexity:** Medium

---

#### SEC-S01 / SEC-S11: Redis Password Hardcoded in Base Config

- **Severity:** Medium | **Risk Score:** 6
- **Affected Files:**
  - `src/Wallow.Api/appsettings.json:15`
  - `src/Wallow.Api/appsettings.Production.json`
  - `src/Wallow.Api/appsettings.Staging.json`
- **Vulnerability Description:**
  The base `appsettings.json` contains the actual Valkey password `WallowValkey123!`. Neither production nor staging configs override `ConnectionStrings:Redis`, creating a risk of deploying with the dev password.

- **Recommended Fix:**
  Replace the password in base config with a placeholder and add Redis override to prod/staging configs.

- **Example Code:**
  ```json
  // appsettings.json
  "Redis": "localhost:6379,password=SET_VIA_ConnectionStrings__Redis_OR_USER_SECRETS,abortConnect=false"

  // appsettings.Production.json and appsettings.Staging.json - add to ConnectionStrings:
  "Redis": "OVERRIDE_VIA_ENV_VAR"
  ```

- **Testing Requirements:**
  - Verify dev environment still works with `appsettings.Development.json` override
  - Verify production startup fails fast if Redis env var is not set

- **Estimated Complexity:** Small

---

#### SEC-S02: Keycloak Client Secret Hardcoded in Realm Export

- **Severity:** Medium | **Risk Score:** 4
- **Affected Files:**
  - `docker/keycloak/realm-export.json:229`
- **Vulnerability Description:**
  The development Keycloak realm export contains a hardcoded client secret `wallow-api-secret`. Visible to anyone with repo access.

- **Recommended Fix:**
  Add a clear comment in the file noting it is for development only. Consider using a unique-per-developer secret generation script.

- **Estimated Complexity:** Small

---

#### SEC-S10: No Startup Validation for Placeholder Config Values

- **Severity:** Low | **Risk Score:** 4
- **Affected Files:**
  - `src/Wallow.Api/Program.cs`
  - `src/Wallow.Api/appsettings.json:70,80,88`
- **Vulnerability Description:**
  No startup validation rejects placeholder values like `REPLACE_IN_PRODUCTION` or `OVERRIDE_VIA_ENV_VAR`. If environment variables are misconfigured, the app starts with invalid credentials and fails at runtime with confusing errors.

- **Recommended Fix:**
  Add a startup health check that validates configuration values are not sentinel placeholders in non-development environments.

- **Example Code:**
  ```csharp
  // Add to Program.cs startup, after building the app
  if (!app.Environment.IsDevelopment())
  {
      string[] sentinels = ["REPLACE_IN_PRODUCTION", "OVERRIDE_VIA_ENV_VAR", "SET_VIA_"];
      string[] keysToValidate = [
          "ConnectionStrings:DefaultConnection",
          "ConnectionStrings:Redis",
          "Keycloak:Confidential:Credentials:secret",
          "Keycloak:Admin:AdminClientSecret"
      ];

      foreach (string key in keysToValidate)
      {
          string? value = builder.Configuration[key];
          if (value is not null && sentinels.Any(s => value.Contains(s, StringComparison.OrdinalIgnoreCase)))
          {
              throw new InvalidOperationException(
                  $"Configuration key '{key}' contains a placeholder value. Set it via environment variables.");
          }
      }
  }
  ```

- **Testing Requirements:**
  - Verify app fails fast in staging/production with placeholder config
  - Verify development startup is unaffected

- **Estimated Complexity:** Small

---

### Phase 4: Backlog (Risk Score < 4)

These items are low-priority improvements. They should be tracked as backlog items and addressed as part of routine maintenance.

| ID | Finding | Fix Description | Complexity |
|----|---------|----------------|------------|
| L-1 / AUTH-008 | Account enumeration via error messages | Return generic "Invalid email or password" instead of forwarding Keycloak error descriptions | Small |
| AUTH-012 | API key identity missing organization claim | Add `organization` claim to API key `ClaimsPrincipal` for consistency | Small |
| C-2 | MetersController missing permission | Add `[HasPermission(PermissionType.BillingRead)]` to `GetAll` endpoint | Small |
| H-3 | Announcements dismiss missing permission | Add `[HasPermission(PermissionType.AnnouncementRead)]` to `DismissAnnouncement` | Small |
| AUTH-007 | Admin role auto-grants all permissions | Consider explicit permission listing instead of `PermissionType.All` | Medium |
| M-5 | Feature flags evaluate endpoint versioning | Move to versioned route for consistency | Small |
| AUTH-013 | SignalR hub limited group validation | Add allowlist of valid group name prefixes | Small |
| AUTH-014 | Service account permission reuse | Define separate `ServiceAccountsXxx` permissions | Small |
| SEC-S03 | Design-time factory hardcoded creds | Read from env vars with fallback | Small |
| LOW-2-DB | Dapper queries missing CancellationToken | Use `CommandDefinition` with `cancellationToken` consistently | Small |
| L-2 / L-3 | Server header / version info disclosure | Remove `Server` header; remove version from root endpoint | Small |

---

## 4. Architecture Changes

### Cross-Cutting Changes

1. **JWT Authentication Registration (AUTH-001):** Requires adding `AddKeycloakWebApiAuthentication()` to the Identity module's infrastructure extensions. This is a one-time registration that affects the entire authentication pipeline. Must be carefully tested to ensure it doesn't break API key authentication or SCIM middleware-based auth.

2. **Global Request Body Size Limit (M-4):** A Kestrel configuration change in `Program.cs` that affects all endpoints. Individual endpoints that need larger limits (file upload) must be explicitly annotated with `[RequestSizeLimit]`.

3. **Startup Configuration Validation (SEC-S10):** A new cross-cutting startup check in `Program.cs` that validates all sentinel config values are overridden in non-development environments.

### New Shared Infrastructure

1. **`BillingAdmin` Permission:** New permission constant in `PermissionType` class for quota administration. Auto-granted to admin role via `PermissionType.All`.

2. **Tenant Validation Service:** A lightweight service to validate tenant existence for the admin override flow. Could use Keycloak organization lookup or a local cache.

### Module Template/Convention Changes

1. **Tenant Ownership Pattern:** The `UserBelongsToTenantAsync()` pattern from the Users controller fix should be documented as a required pattern for any endpoint that operates on Keycloak resources by GUID. Consider creating a shared base controller or filter attribute.

2. **Redis Key Convention:** All Redis keys must include tenant ID prefix: `{feature}:{tenantId}:{rest}`. Document this in the developer guide.

---

## 5. Implementation Roadmap

### Phase 1: Critical Fixes (Immediate)

| Order | Fix | Dependencies | Estimated Effort |
|-------|-----|-------------|-----------------|
| 1 | AUTH-001: Register JWT authentication | None | 2-4 hours |
| 2 | C-1: Add permission to quota endpoints | May need new `BillingAdmin` permission | 1 hour |
| 3 | AUTH-005: Add tenant ownership to user operations | None | 2 hours |
| 4 | AUTH-006: Add tenant scoping to organization endpoints | None | 2 hours |
| 5 | HIGH-1-T: Add tenant isolation to presence service | Requires IPresenceService interface change | 4-6 hours |

**Dependencies:** AUTH-001 must be done first as it affects the entire authentication pipeline. Items 2-5 are independent of each other.

### Phase 2: High-Priority Fixes (Within 1 Sprint)

| Order | Fix | Dependencies | Estimated Effort |
|-------|-----|-------------|-----------------|
| 1 | AUTH-011/M-6: Fix Hangfire role case mismatch | None | 30 min |
| 2 | M-1/AUTH-004: SCIM authentication scheme | None | 3-4 hours |
| 3 | AUTH-010: Token revocation endpoint | AUTH-001 (JWT must work first) | 2 hours |
| 4 | AUTH-009: Tighten auth rate limits | None | 30 min |
| 5 | M-2: SCIM rate limiting | None | 30 min |
| 6 | AUTH-002: Admin tenant override improvements | Tenant validation service | 3-4 hours |

### Phase 3: Medium-Priority Hardening

| Order | Fix | Dependencies | Estimated Effort |
|-------|-----|-------------|-----------------|
| 1 | H-4/SEC-S09: Health check staging exposure | None | 15 min |
| 2 | M-4: Global request body size limit | None | 30 min |
| 3 | MED-4-DB: Report service connection refactor | None | 1 hour |
| 4 | AUTH-003: Wolverine handler authorization middleware | None | 3-4 hours |
| 5 | SEC-S01/SEC-S11: Redis connection string cleanup | None | 30 min |
| 6 | SEC-S10: Startup config validation | None | 1 hour |

### Phase 4: Low-Priority Improvements

Tracked as backlog items. Pick up during maintenance sprints.

---

## 6. Positive Security Findings

The following security controls are already well-implemented and should be maintained:

1. **Comprehensive Permission Model:** Nearly all endpoints use `[HasPermission(PermissionType.X)]` for granular RBAC. The `HasPermissionAttribute` extends `AuthorizeAttribute`, providing implicit authentication enforcement.

2. **EF Core Tenant Isolation:** `TenantAwareDbContext.ApplyTenantQueryFilters()` correctly uses expression trees bound to the `_tenantId` field. `TenantSaveChangesInterceptor` stamps TenantId on new entities and blocks modification on updates.

3. **Parameterized Dapper Queries:** All 5 Dapper services use parameterized queries via anonymous objects or `CommandDefinition`. Zero SQL injection risk.

4. **DDL Identifier Validation:** `CustomFieldIndexManager` validates identifiers against a strict regex before use in dynamic DDL, preventing SQL injection in schema operations.

5. **SSO Secrets Encrypted at Rest:** `IdentityDbContext` uses ASP.NET Core Data Protection with a dedicated purpose string for SSO configuration secrets.

6. **Security Headers:** `SecurityHeadersMiddleware` applies CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, and HSTS (production).

7. **CORS Configuration:** Explicit origins required in production (throws if not configured). Development uses a named policy with specific localhost origins.

8. **API Key Security:** Keys stored as SHA-256 hashes in Redis. Expiration enforced at both Redis TTL and application level. Scope-based permission mapping.

9. **HTML Sanitization:** Messaging and announcement content sanitized via `IHtmlSanitizationService` to prevent XSS.

10. **Global Exception Handler:** Prevents stack trace leakage in production using RFC 7807 Problem Details. Development-only gate for detailed error info.

11. **OpenAPI/Swagger/AsyncAPI/Elsa Admin:** All gated behind `IsDevelopment()`. Not exposed in production.

12. **Rate Limiting:** Auth endpoints at 5/5min, uploads with dedicated limits, global 1000/hour/IP safety net.

13. **Feature Flag Cache Keys Include TenantId:** `CachedFeatureFlagService.BuildCacheKey()` includes tenant ID, preventing cross-tenant cache poisoning.

14. **Metering Keys Include TenantId:** `ValkeyMeteringService` uses `meter:{tenantId}:{meterCode}:{period}` for per-tenant isolation.

15. **SignalR Tenant Group Validation:** `RealtimeHub.ValidateTenantGroup()` prevents cross-tenant group joins for `tenant:`-prefixed groups.

16. **Database Schema Isolation:** Each module uses its own PostgreSQL schema (`billing`, `communications`, `configuration`, `identity`, `storage`, `audit`, `hangfire`, `wolverine`).

17. **No Sensitive Data Logging:** No instances of passwords, tokens, secrets, or PII logged via `ILogger`.

18. **Conversation Participant Check:** `ConversationsController.GetMessages()` validates participant membership before returning messages.

19. **File Upload Validation:** Size limit (100MB), empty file check, and rate limiting on upload endpoints.

20. **Middleware Pipeline Order:** Authentication -> Tenant Resolution -> Permission Expansion -> Authorization is correctly ordered and documented.
