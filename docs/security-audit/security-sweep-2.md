# Security Sweep 2 — Verified Findings & Remediation Design

**Date:** 2026-03-03
**Status:** Draft
**Scope:** Full codebase sweep of `src/`, `docker/`, CI/CD, and infrastructure configs
**Method:** 5 parallel scout agents + 2 independent verification agents
**Baseline:** Findings from Security Sweep 1 (`security-remediation-design.md`) excluded

---

## 1. Executive Summary

### New Verified Vulnerabilities by Severity

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 2 |
| Medium | 14 |
| Low | 18 |
| False Positive | 4 |

### Overall Assessment

The Wallow platform's core security architecture (EF Core tenant query filters, permission-based authorization, security headers) remains strong. This second sweep focused on **logic-level vulnerabilities** that architectural controls don't catch: missing authorization checks in specific flows, business logic flaws in billing, cross-tenant data leaks via SignalR groups, and file upload security bypass.

### Top 5 Most Critical New Findings

1. **CS-1: Presigned Upload Bypasses ClamAV Scanning** — Files uploaded via presigned URL completely bypass virus scanning, magic byte validation, and filename sanitization.
2. **NEW-IDENT-001: GetUsers Returns All Realm Users** — The user listing endpoint returns users across all tenants, unlike GetUserById which validates tenant ownership.
3. **B-4/B-5: Payment Processing Logic Flaws** — Partial payments mark invoices as fully paid; currency mismatch between payment and invoice is not validated.
4. **NEW-IDENT-004: SCIM Authentication Is Broken** — SCIM token validation queries EF Core with an empty tenant context, likely causing all SCIM requests to fail.
5. **NEW-API-2: SignalR Page Groups Leak Presence Cross-Tenant** — Page context groups are not tenant-scoped, so users from different tenants on the same page see each other's presence.

---

## 2. Risk Matrix

Risk Score = Likelihood (1-5) x Impact (1-5)

| ID | Finding | Likelihood | Impact | Risk Score | Priority |
|----|---------|-----------|--------|------------|----------|
| CS-1 | Presigned upload bypasses ClamAV | 4 | 5 | **20** | Immediate |
| NEW-IDENT-001 | GetUsers returns all realm users | 4 | 4 | **16** | Immediate |
| B-4 | Partial payment marks invoice as paid | 3 | 5 | **15** | Immediate |
| B-5 | Payment currency not validated against invoice | 3 | 5 | **15** | Immediate |
| NEW-IDENT-004 | SCIM token validation broken (empty tenant) | 4 | 4 | **16** | Immediate |
| NEW-API-2 | Page context groups leak presence cross-tenant | 3 | 3 | **9** | Short-term |
| NEW-API-3 | SendToAllAsync broadcasts across all tenants | 3 | 3 | **9** | Short-term |
| NEW-API-1 | SignalR missing auto-join to tenant group | 3 | 3 | **9** | Short-term |
| B-3 | Metering decimal-to-long truncation | 3 | 3 | **9** | Short-term |
| NEW-IDENT-002 | CreateUser doesn't add to tenant org | 3 | 3 | **9** | Short-term |
| NEW-IDENT-007 | API key scope-to-permission mapping incomplete | 2 | 4 | **8** | Short-term |
| NEW-API-8 | Plugin system no sandboxing | 2 | 4 | **8** | Short-term |
| CS-3 | MarkConversationRead missing participant check | 2 | 3 | **6** | Medium-term |
| CS-4 | SendMessage missing controller participant check | 2 | 3 | **6** | Medium-term |
| INFRA-09 | Production compose doesn't reset Postgres ports | 2 | 4 | **8** | Short-term |
| INFRA-05 | Service account has manage-realm role | 2 | 4 | **8** | Short-term |

---

## 3. Detailed Remediation Plans

### Phase 1: Immediate (Risk Score >= 15)

---

#### CS-1: Presigned Upload Bypasses ClamAV Scanning and Validation

- **Severity:** High | **Risk Score:** 20
- **Affected File:** `src/Modules/Storage/Wallow.Storage.Application/Queries/GetUploadPresignedUrl/GetUploadPresignedUrlHandler.cs:17-66`
- **Vulnerability:** The presigned upload flow creates a `StoredFile` DB record and returns a presigned S3 URL for direct upload. This completely bypasses:
  1. ClamAV virus scanning (done only in `UploadFileHandler`)
  2. Magic byte validation (done only in `UploadFileValidator`)
  3. `FileNameSanitizer` (raw filename stored)
- **Attack Scenario:** A user with `StorageWrite` permission uploads malware via presigned URL. The file is stored and downloadable without any security scanning.
- **Recommended Fix:** Implement a post-upload scanning pipeline:

  ```csharp
  // Option 1: S3 Event-Driven Scan
  // Configure S3 bucket notification on PutObject events
  // Lambda/background job scans the file after upload
  // Mark StoredFile.Status = "Quarantined" until scan passes

  // Option 2: Deferred Availability
  // In GetUploadPresignedUrlHandler, create StoredFile with Status = "PendingValidation"
  // Add a background job (Hangfire) that:
  //   1. Downloads the file from S3
  //   2. Runs ClamAV scan
  //   3. Validates magic bytes
  //   4. Updates status to "Available" or "Rejected"
  // Modify download endpoints to reject files with Status != "Available"

  // Option 3: Presigned URL Callback
  // Use S3 Object Lambda or CloudFront function to trigger scan on first access
  ```

- **Estimated Complexity:** Medium-High

---

#### NEW-IDENT-001: GetUsers Endpoint Lacks Tenant Filtering

- **Severity:** High | **Risk Score:** 16
- **Affected File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/UsersController.cs:43-51`
- **Vulnerability:** `GetUsers()` calls `_keycloakAdmin.GetUsersAsync(search, first, max, ct)` which queries ALL Keycloak realm users without tenant/organization filtering. Compare with `GetUserById()` which validates `UserBelongsToTenantAsync()`.
- **Attack Scenario:** Any user with `UsersRead` permission can list all users across all tenants, exposing usernames, emails, and user IDs.
- **Recommended Fix:**

  ```csharp
  [HttpGet]
  [HasPermission(PermissionType.UsersRead)]
  public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
      [FromQuery] string? search, [FromQuery] int first = 0,
      [FromQuery] int max = 50, CancellationToken ct = default)
  {
      // Use organization members endpoint instead of realm-wide user search
      IReadOnlyList<UserDto> orgMembers = await _keycloakOrg
          .GetMembersAsync(_tenantContext.TenantId.Value, ct);

      // Apply search filter client-side (or use Keycloak org members search if available)
      IEnumerable<UserDto> filtered = orgMembers;
      if (!string.IsNullOrWhiteSpace(search))
      {
          filtered = orgMembers.Where(u =>
              u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
              u.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
              u.LastName.Contains(search, StringComparison.OrdinalIgnoreCase));
      }

      List<UserDto> paged = filtered.Skip(first).Take(max).ToList();
      return Ok(new PagedResult<UserDto>(paged, orgMembers.Count));
  }
  ```

- **Estimated Complexity:** Small

---

#### B-4: ProcessPayment Marks Invoice as Paid Regardless of Amount

- **Severity:** Medium | **Risk Score:** 15
- **Affected File:** `src/Modules/Billing/Wallow.Billing.Application/Commands/ProcessPayment/ProcessPaymentHandler.cs:54-57`
- **Vulnerability:** After creating a payment, the handler marks the invoice as paid if status is `Issued` or `Overdue`, without checking that the payment amount covers the invoice total. A $1 payment on a $1000 invoice marks it as fully paid.
- **Recommended Fix:**

  ```csharp
  // After creating the payment, check if fully paid
  Money totalPaid = invoice.Payments
      .Where(p => p.Status == PaymentStatus.Completed)
      .Aggregate(Money.Zero(invoice.TotalAmount.Currency),
          (sum, p) => sum + p.Amount);

  if (totalPaid >= invoice.TotalAmount &&
      invoice.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue)
  {
      invoice.MarkAsPaid();
  }
  ```

- **Estimated Complexity:** Small

---

#### B-5: ProcessPayment Currency Mismatch Not Validated

- **Severity:** Medium | **Risk Score:** 15
- **Affected File:** `src/Modules/Billing/Wallow.Billing.Application/Commands/ProcessPayment/ProcessPaymentHandler.cs:42`
- **Vulnerability:** Payment currency comes from the command (user-supplied) but is never validated against `invoice.TotalAmount.Currency`. A user could pay in a different (cheaper) currency.
- **Recommended Fix:**

  ```csharp
  // Add currency validation before creating the payment
  if (!string.Equals(command.Currency, invoice.TotalAmount.Currency,
      StringComparison.OrdinalIgnoreCase))
  {
      return Result.Failure(BillingErrors.CurrencyMismatch);
  }
  ```

- **Estimated Complexity:** Small

---

#### NEW-IDENT-004: SCIM Token Validation Broken — Empty Tenant Context

- **Severity:** Medium | **Risk Score:** 16
- **Affected File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ScimAuthenticationMiddleware.cs:32-96`
- **Vulnerability:** Middleware order: TenantResolution -> ScimAuth. TenantResolution only sets tenant when `context.User.Identity?.IsAuthenticated == true`. SCIM requests use a custom Bearer token (not JWT), so the user is NOT authenticated at TenantResolution stage. TenantContext is empty when `ValidateTokenAsync` queries EF Core with tenant query filters, likely causing all SCIM tokens to fail validation.
- **Recommended Fix:**

  ```csharp
  // Option 1: SCIM middleware should run BEFORE TenantResolution
  // and set tenant context from the SCIM config's tenant

  // Option 2: ScimConfigurationRepository.GetAsync should bypass tenant filter
  // Similar to ServiceAccountRepository.GetByKeycloakClientIdAsync
  public async Task<ScimConfiguration?> GetAsync(CancellationToken ct)
  {
      return await _context.ScimConfigurations
          .IgnoreQueryFilters()  // SCIM token lookup must be cross-tenant
          .FirstOrDefaultAsync(ct);
  }
  // Then set tenant context from the matched config's TenantId

  // Option 3: Extract tenant from a header or token claim before validation
  ```

- **Estimated Complexity:** Medium

---

### Phase 2: Short-term (Risk Score 8-14)

---

#### NEW-API-2: SignalR Page Context Groups Not Tenant-Scoped

- **Severity:** Medium | **Risk Score:** 9
- **Affected File:** `src/Wallow.Api/Hubs/RealtimeHub.cs:67-85`
- **Vulnerability:** `UpdatePageContext` adds connections to group `$"page:{pageContext}"` without tenant prefix. Users from different tenants on the same page see each other's presence via `PageViewersUpdated` events.
- **Recommended Fix:**

  ```csharp
  // Change group name to include tenant prefix
  string tenantPageGroup = $"page:{tenantId}:{pageContext}";
  await Groups.AddToGroupAsync(Context.ConnectionId, tenantPageGroup);
  ```

- **Estimated Complexity:** Small

---

#### NEW-API-3: SendToAllAsync Broadcasts Across All Tenants

- **Severity:** Medium | **Risk Score:** 9
- **Affected File:** `src/Wallow.Api/Services/SignalRRealtimeDispatcher.cs:45-58`
- **Vulnerability:** `SendToAllAsync` uses `hubContext.Clients.All.SendAsync()`, broadcasting to every connected client across all tenants.
- **Recommended Fix:**

  ```csharp
  // Option 1: Remove the method entirely if not needed
  // Option 2: Require explicit tenant scoping
  public async Task SendToAllAsync(Guid tenantId, string method,
      object payload, CancellationToken ct = default)
  {
      string tenantGroup = $"tenant:{tenantId}";
      await hubContext.Clients.Group(tenantGroup)
          .SendAsync(method, payload, ct);
  }
  ```

- **Estimated Complexity:** Small

---

#### NEW-API-1: SignalR Hub Missing Auto-Join to Tenant Group on Connect

- **Severity:** Medium | **Risk Score:** 9
- **Affected File:** `src/Wallow.Api/Hubs/RealtimeHub.cs:16-31`
- **Vulnerability:** `OnConnectedAsync` broadcasts to `tenant:{tenantId}` group but never calls `Groups.AddToGroupAsync()`. Clients must manually call `JoinGroup`, creating a window of inconsistency and making tenant-scoped broadcasts non-functional.
- **Recommended Fix:**

  ```csharp
  public override async Task OnConnectedAsync()
  {
      Guid tenantId = _tenantContext.TenantId.Value;
      string userId = Context.UserIdentifier!;
      string tenantGroup = $"tenant:{tenantId}";

      // Auto-join tenant group
      await Groups.AddToGroupAsync(Context.ConnectionId, tenantGroup);

      await _presenceService.TrackConnectionAsync(tenantId, userId,
          Context.ConnectionId);

      // Now broadcast to the group the user just joined
      await Clients.Group(tenantGroup).SendAsync("UserOnline",
          new { UserId = userId });

      await base.OnConnectedAsync();
  }
  ```

- **Estimated Complexity:** Small

---

#### B-3: Metering Decimal-to-Long Truncation

- **Severity:** Medium | **Risk Score:** 9
- **Affected File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/ValkeyMeteringService.cs:64`
- **Vulnerability:** `db.StringIncrementAsync(key, (long)value)` truncates fractional values. A meter increment of 0.5 becomes 0.
- **Recommended Fix:**

  ```csharp
  // Use StringIncrementAsync with double overload for fractional support
  // Or multiply by a scaling factor (e.g., store milliunits)
  await db.StringIncrementAsync(key, (double)value, flags: CommandFlags.FireAndForget);

  // Update GetCurrentUsageAsync to match:
  // return (decimal)(await db.StringGetAsync(key)).AsDouble();
  ```

- **Estimated Complexity:** Small

---

#### NEW-IDENT-002: CreateUser Doesn't Add User to Tenant Organization

- **Severity:** Medium | **Risk Score:** 9
- **Affected File:** `src/Modules/Identity/Wallow.Identity.Api/Controllers/UsersController.cs:96-107`
- **Vulnerability:** `CreateUser` creates a Keycloak user but doesn't associate them with the current tenant's organization. The user is "dangling" in the realm.
- **Recommended Fix:**

  ```csharp
  [HttpPost]
  [HasPermission(PermissionType.UsersCreate)]
  public async Task<ActionResult<Guid>> CreateUser(
      CreateUserRequest request, CancellationToken ct)
  {
      Guid userId = await _keycloakAdmin.CreateUserAsync(
          request.Email, request.FirstName, request.LastName,
          request.TemporaryPassword, ct);

      // Add user to current tenant's organization
      await _keycloakOrg.AddMemberAsync(
          _tenantContext.TenantId.Value, userId, ct);

      return CreatedAtAction(nameof(GetUserById), new { id = userId }, userId);
  }
  ```

- **Estimated Complexity:** Small

---

#### NEW-IDENT-007: API Key Scope-to-Permission Mapping Incomplete

- **Severity:** Medium | **Risk Score:** 8
- **Affected File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs:111-136`
- **Vulnerability:** `MapScopeToPermission` only maps 12 scopes out of 40+ possible permissions. API keys created with unmapped scopes silently grant no permissions.
- **Recommended Fix:** Maintain a comprehensive mapping or use a naming convention that auto-maps.

  ```csharp
  // Convention-based mapping: scope "storage:read" -> PermissionType.StorageRead
  private static string? MapScopeToPermission(string scope)
  {
      // Convert scope format "module:action" to permission format "ModuleAction"
      string[] parts = scope.Split(':');
      if (parts.Length != 2) return null;

      string permissionName = string.Concat(
          CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[0]),
          CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[1]));

      // Validate against known permissions
      return typeof(PermissionType).GetField(permissionName)?.GetValue(null) as string;
  }
  ```

- **Estimated Complexity:** Medium

---

#### NEW-API-8: Plugin System Loads Assemblies Without Sandboxing

- **Severity:** Medium | **Risk Score:** 8
- **Affected File:** `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginLoader.cs:8-57`
- **Vulnerability:** Plugins run with full host process permissions. A malicious plugin can access all tenant data, infrastructure credentials, and services.
- **Recommended Fix:** Since .NET doesn't support AppDomain-based sandboxing post-.NET Core, implement defense-in-depth:

  1. Plugin allow-list: Only load plugins from a signed/approved list
  2. Plugin manifest validation: Require declared permissions and validate at load time
  3. DI scope restriction: Provide plugins with a restricted `IServiceProvider` that excludes sensitive services
  4. File integrity: Verify plugin DLL hashes against a manifest before loading

- **Estimated Complexity:** High

---

#### INFRA-09: Production Compose Doesn't Reset Postgres Ports

- **Severity:** Medium | **Risk Score:** 8
- **Affected File:** `docker/docker-compose.prod.yml`
- **Vulnerability:** Production override resets ports for RabbitMQ, Valkey, and Keycloak but NOT Postgres. Port 5432 from the base compose remains exposed.
- **Recommended Fix:**

  ```yaml
  # Add to docker-compose.prod.yml postgres section:
  postgres:
    ports: !reset []
  ```

- **Estimated Complexity:** Trivial

---

#### INFRA-05: Service Account Has manage-realm Role

- **Severity:** Medium | **Risk Score:** 8
- **Affected File:** `docker/keycloak/realm-export.json:305-308`
- **Vulnerability:** The `service-account-wallow-api` has `manage-realm` role, granting full realm administration. If API service account credentials are compromised, the attacker can modify the entire Keycloak realm.
- **Recommended Fix:** Scope down to only required roles: `manage-users`, `view-users`, `manage-clients` (if needed). Remove `manage-realm`.

- **Estimated Complexity:** Small

---

### Phase 3: Medium-term (Risk Score 4-7)

---

#### CS-3: MarkConversationRead Missing Participant Check

- **Severity:** Medium | **Risk Score:** 6
- **Affected File:** `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/MarkConversationRead/MarkConversationReadHandler.cs:12-30`
- **Vulnerability:** Any authenticated user within a tenant can call `MarkReadBy` on any conversation without being a participant. While the domain method is a no-op for non-participants, it confirms conversation existence (IDOR).
- **Recommended Fix:** Add participant validation before marking as read.
- **Estimated Complexity:** Small

---

#### CS-4: SendMessage Missing Controller-Level Participant Check

- **Severity:** Medium | **Risk Score:** 6
- **Affected File:** `src/Modules/Communications/Wallow.Communications.Api/Controllers/ConversationsController.cs:140-157`
- **Vulnerability:** `SendMessage` does not call `IsParticipantAsync()` before dispatching the command, unlike `GetMessages` which does. The domain entity does enforce this, but exceptions may leak internal details.
- **Recommended Fix:** Add `IsParticipantAsync` check in the controller, consistent with `GetMessages`.
- **Estimated Complexity:** Small

---

### Phase 4: Backlog (Low Severity)

| ID | Finding | Fix Description | Complexity |
|----|---------|----------------|------------|
| NEW-IDENT-003 | ServiceAccountTrackingMiddleware Task.Run scope risk | Use `IServiceScopeFactory` from constructor instead of `context.RequestServices` | Small |
| NEW-IDENT-005 | Email PII logged on auth failures | Hash or redact email in failure logs | Small |
| NEW-IDENT-006 | Keycloak error details forwarded to client | Return generic "Invalid credentials" message | Small |
| NEW-IDENT-008 | ServiceAccountRepository bypasses tenant filter | Restrict method visibility; add documentation | Small |
| NEW-IDENT-009 | RolesController exposes all realm roles | Filter to tenant-relevant roles or use predefined set | Small |
| B-1 | Billing GetById no ownership within tenant | Accepted risk — EF tenant filters protect cross-tenant; intra-tenant is by permission design | N/A |
| B-2 | GetByUserId allows querying any user's data | Same as B-1 — by design with RBAC | N/A |
| CS-2 | Storage path may allow traversal on local provider | Validate path segments; reject `..` in path | Small |
| CS-6 | Presigned URL no max expiry | Cap `expiryMinutes` at configurable max (e.g., 60) | Small |
| CS-7 | Magic byte validation limited to 5 content types | Add more content types or use a library like `MimeDetective` | Small |
| B-7 | CachedFeatureFlagService ConcurrentBag memory leak | Use `ConcurrentDictionary<string, HashSet<string>>` with dedup | Small |
| NEW-API-4 | Elsa workflow static dev signing key | Document the key is dev-only; ensure env guard is robust | Small |
| NEW-API-5 | AsyncAPI viewer loads external JS without SRI | Add SRI hashes to script/style tags | Small |
| NEW-API-7 | Audit interceptor swallows exceptions silently | Add logging to the catch block | Small |
| NEW-API-9 | TenantContext public setters | Make setters `internal` or use `init` | Small |
| INFRA-01 | Keycloak sslRequired "none" (dev-only) | Add comment noting dev-only; set "external" in prod template | Small |
| INFRA-02 | Keycloak verifyEmail false (dev-only) | Enable in production realm | Small |
| INFRA-03 | Open self-registration (dev-only) | Disable in production; use invitation flow | Small |
| INFRA-04 | ROPC grant enabled (dev-only) | Disable `directAccessGrantsEnabled` in production | Small |
| INFRA-06 | Keycloak SPA "+" webOrigins | Replace with explicit origins | Small |
| INFRA-07 | Dockerfile healthcheck uses curl | Use `wget` or .NET health check tool | Small |
| INFRA-08 | Docker compose binds to 0.0.0.0 (dev-only) | Bind to `127.0.0.1` | Small |
| INFRA-10 | No DB user separation for Keycloak | Create separate Postgres user for Keycloak | Small |
| INFRA-12 | ClamAV uses unpinned `latest` tag | Pin to specific version | Small |
| INFRA-13 | Valkey password in healthcheck command | Use `REDISCLI_AUTH` env var | Small |
| INFRA-14 | Prod Valkey disables appendonly | Acceptable if Valkey is cache-only | N/A |

---

## 4. Positive Security Findings

The following controls were verified as correctly implemented during this sweep:

1. **EF Core Tenant Query Filters:** All billing, communications, configuration, and storage DbContexts correctly apply `ApplyTenantQueryFilters()`. Cross-tenant data access via EF is prevented.
2. **TenantSaveChangesInterceptor:** Stamps `TenantId` on all new entities and blocks tenant modification on updates.
3. **Keycloak Brute Force Protection:** Enabled with `failureFactor: 5` in realm config.
4. **Short Access Token Lifespan:** 300 seconds (5 minutes), limiting exposure window.
5. **Dockerfile Security:** Multi-stage build, non-root user (`USER $APP_UID`), pinned base images with SHA digests.
6. **CI Security Scanning:** Trivy container scanning, CodeQL analysis, Dependabot for NuGet and GitHub Actions.
7. **Production TLS:** Keycloak uses TLS certificates in production compose.
8. **Environment Config Separation:** Production/staging configs use `OVERRIDE_VIA_ENV_VAR` placeholders; `.env` is gitignored.
9. **Parameterized Queries:** All Dapper queries use parameterized inputs. Zero SQL injection risk.
10. **Domain-Level Validation:** Conversation participant checks enforced in domain entities (defense-in-depth for CS-4).

---

## 5. Implementation Roadmap

### Phase 1: Immediate (1-2 days)

| Order | Fix | Dependencies | Effort |
|-------|-----|-------------|--------|
| 1 | CS-1: Presigned upload post-upload scanning | Hangfire (exists) | 4-6 hours |
| 2 | NEW-IDENT-001: Tenant-filter GetUsers | Keycloak org API | 1-2 hours |
| 3 | B-4: Validate payment covers invoice total | None | 1 hour |
| 4 | B-5: Validate payment currency matches invoice | None | 30 min |
| 5 | NEW-IDENT-004: Fix SCIM tenant context | Pipeline ordering | 2-3 hours |
| 6 | INFRA-09: Reset Postgres ports in prod compose | None | 5 min |

### Phase 2: Short-term (1 sprint)

| Order | Fix | Dependencies | Effort |
|-------|-----|-------------|--------|
| 1 | NEW-API-2: Tenant-scope SignalR page groups | None | 1 hour |
| 2 | NEW-API-3: Remove or scope SendToAllAsync | Check callers | 1-2 hours |
| 3 | NEW-API-1: Auto-join tenant group on connect | None | 30 min |
| 4 | B-3: Fix decimal metering truncation | None | 1 hour |
| 5 | NEW-IDENT-002: Add user to org on create | None | 30 min |
| 6 | NEW-IDENT-007: Complete scope-permission mapping | None | 2-3 hours |
| 7 | INFRA-05: Reduce service account roles | None | 30 min |

### Phase 3: Medium-term

| Order | Fix | Dependencies | Effort |
|-------|-----|-------------|--------|
| 1 | CS-3: Add participant check to MarkConversationRead | None | 30 min |
| 2 | CS-4: Add participant check to SendMessage controller | None | 30 min |
| 3 | NEW-API-8: Plugin sandboxing | Architecture decision | 8-16 hours |

### Phase 4: Backlog

All LOW findings — pick up during maintenance sprints.

---

## 6. Cross-Reference with Sweep 1

The following Sweep 1 findings were verified as still open during this sweep:

| Sweep 1 ID | Status | Notes |
|------------|--------|-------|
| AUTH-001 | **FIXED** | JWT auth registered in recent commit |
| C-1 | Open | Quota admin still missing permission guard |
| AUTH-005 | Open | User operations still missing tenant check |
| AUTH-006 | Open | Org endpoints still allow cross-tenant |
| HIGH-1-T | Open | Presence service still has no tenant isolation |

All other Sweep 1 findings remain in their original status. See `security-remediation-design.md` for details.

---

## 7. Methodology Notes

- **Scouts:** 5 specialized agents scanned different codebase areas in parallel
- **Verification:** 2 independent agents read actual source code at referenced lines to confirm/deny each finding
- **False Positive Rate:** 4 out of 41 raw findings (9.8%) were false positives, filtered by verification
- **Severity Adjustments:** 6 findings had severity adjusted after verification (mostly downgrades)
- **Excluded:** 30 known findings from Sweep 1 were excluded from this report
