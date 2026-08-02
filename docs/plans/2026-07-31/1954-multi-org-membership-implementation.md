**status: completed**
**version: 2.0**

# Multi-Org Membership & Access Requests — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan
> task-by-task.

**Design doc:** `docs/plans/2026-07-31/1410-multi-org-membership-and-access-requests.md`.
Every `§n` reference points there. Read §1-5 before starting Phase 1; read the relevant section
before every phase. This plan does not restate the *why* — it only says what to build.

> **Design doc correction — read this before Phase 1.** §5.0 states there are **four**
> role-resolution sites. That number is wrong and this plan does not rely on it. The actual count
> of code making a role-derived authorization decision **or writing the role store** is at least
> ten, and the extras are not incidental: two of them (`OrganizationAccessPolicy`,
> `MfaExemptionChecker`) are security decisions whose dependencies Phase 1 deletes, and one class
> of them (the role *write* path) would make revocation silently lie. Phase 1 therefore opens with
> an inventory task and repairs every site **before** deleting anything. Amend §5.0 when Task 1.0
> produces the real list.

**Goal:** One identity per person with N tenant-scoped memberships, roles assigned per
`(user, organization)`, self-service enrollment governed by a per-org policy, an access-request
notification, and a working path for an external site to sign users in with Wallow.

**Architecture:** `Membership` becomes a first-class aggregate in the Identity module's domain,
replacing the `OrganizationMember` value object owned by `Organization`. Role *definitions* stay
global (`WallowRole`: `admin`/`manager`/`user`); role *assignment* moves to a new
`identity.membership_roles` table keyed `(MembershipId, RoleId)`. Every place that reads roles for
an authorization decision goes through one new `IMembershipRoleResolver`, and every place that
*writes* roles goes through the membership aggregate. Nothing else about the permission pipeline
changes: the resolver's output is role *names*, which `RolePermissionMapping` already expands.

**Tech Stack:** .NET 10, EF Core 10.0.10 (Npgsql, schema `identity`), ASP.NET Core Identity,
OpenIddict 7.6.0 (authorization code + PKCE, client credentials, refresh), Wolverine (in-memory
integration events), xUnit, TanStack Start + React on the frontend.

---

## Revision history

**2.0** — Rewritten after a four-lens review (grounding, security, architecture, executability) of
v1. Material changes:

| Area | v1 | v2 |
|---|---|---|
| Phase 1 shape | 13 tasks against §5.0's four sites | 19 tasks opening with an inventory; every role-*write* site and both orphaned security checks repaired **before** any deletion |
| `OrganizationAccessPolicy` | not mentioned | Task 1.7 — replaced with a permission-parameterized check across all twelve endpoints it gates |
| `MfaExemptionChecker` | cited only as an `IgnoreQueryFilters()` precedent | Task 1.8 — it is a live MFA bypass that Phase 4 arms |
| Role writes (`UserManagementService`, `UsersController`) | not mentioned | Task 1.9 — must ship with the read change or revocation lies |
| Scope-gate narrowing | Phase 5.5 | Task 1.11 — already broken for `wallow-web-client` today, and v1's version granted the scope it claimed to refuse |
| Token revocation | "use `IOpenIddictTokenManager`" | Task 2.6 — impossible as written on this configuration; now a stated decision |
| Authorize-flow tests | assumed available | Task 1.12 builds the harness; no test in the repo has ever driven `/connect/authorize` |
| `role_ids` | EF primitive-collection column | real `membership_roles` table (FK integrity + the Dapper read path) |
| `Membership : ITenantScoped` | implemented | dropped — it survives only because Identity omits the tenant interceptor |
| `ReviewUrl` on `AccessRequestedEvent` | on the contract, left as an open question | removed; Notifications composes it, as the invitation handler already does |
| Phase 3 ordering | six independent commits | defects A/B/C are one commit — splitting them opens a window that does not exist today |
| Invitation vs pending request | unscheduled (§12 still-open) | folded into Task 3.2 |
| Blast radius | one test file named for the `OrganizationMember` deletion | twelve, enumerated |

**1.0** — Initial plan.

---

## How to use this plan

**Detail level is deliberately uneven.** Phase 1 carries full step-by-step TDD tasks with inline
code, because it is the security work and getting it partially right is the specific failure mode
the design doc warns about. Phases 2-3 carry exact files, exact test names, and the non-obvious
code. Phases 4-6 carry task-level detail. Phase 7 is a pointer, not a plan.

**Phases are sequential and each ends green.** Do not start a phase before the previous one's gate
passes.

### The standing loop (every task)

1. Write the failing test.
2. Run it. **Confirm it fails, and fails for the stated reason.**
3. Write the minimal implementation.
4. Run the test. Confirm it passes.
5. **If the task introduced an injectable service, register it in BOTH composition roots** —
   `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`
   (see `:260`, `:376`, `:385` for the pattern) **and** `api/src/Wallow.SeederService/Program.cs:74-75`,
   which builds its own container. A missing registration is a *runtime* failure: `dotnet build`
   and every unit test stay green.
6. **If any endpoint or DTO shape moved, regenerate the OpenAPI snapshot** (see Commands).
   `.github/workflows/openapi-drift.yml:20-21` triggers on `api/**`, so *every* phase's PR runs it.
7. `dotnet format api/Wallow.slnx` and stage the formatting changes.
8. Commit with a conventional-commit message.

### Commands

```bash
./scripts/run-tests.sh identity        # Wallow.Identity.Tests
./scripts/run-tests.sh integration     # Wallow.Identity.IntegrationTests
./scripts/run-tests.sh kernel          # Wallow.Shared.Kernel.Tests
./scripts/run-tests.sh arch            # Wallow.Architecture.Tests
./scripts/run-tests.sh                 # everything
dotnet build api/Wallow.slnx
dotnet format api/Wallow.slnx          # before EVERY commit — warnings are errors
pnpm check                             # frontend gate (runs `pnpm build` itself, so no
                                       #   separate SDK build step is needed)
```

**`run-tests.sh identity` does NOT run container-backed tests.** `Wallow.Identity.Tests.csproj`
references `Microsoft.EntityFrameworkCore.InMemory` and carries no Testcontainers package, and
`scripts/run-tests.sh:48-50` appends `--filter "Category!=E2E&Category!=Integration"` to every
shorthand except `integration`. Consequences: **relational column mapping cannot be asserted
there** (the in-memory provider ignores it), and anything needing real Postgres behaviour belongs
in `Wallow.Identity.IntegrationTests`. (`api/CLAUDE.md:113` says "unit + Testcontainers Postgres" —
that line is stale; do not trust it.)

A compile failure in a test project produces **zero TRX files**, which
`scripts/run-tests.sh:141-145` reports as a missing-results warning rather than a test failure.
"No tests ran" means the project did not build.

OpenAPI regeneration (the workflow prints this verbatim at `openapi-drift.yml:78-79`):

```bash
WALLOW_OPENAPI_URL=http://localhost:5001/openapi/v1.json \
  pnpm --filter @bc-solutions-coder/sdk exec tsx scripts/generate.ts
git add packages/sdk/openapi packages/sdk/src/generated
```

EF migration:

```bash
dotnet ef migrations add <Name> \
    --project api/src/Modules/Identity/Wallow.Identity.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context IdentityDbContext
```

### Conventions that will fail the build if ignored

- **Explicit types, never `var`.**
- **Never call `logger.LogInformation(...)`** — `partial` class + `[LoggerMessage]` methods.
- **No `--` inside XML comments** anywhere.
- **JWT claims via `ClaimsPrincipalExtensions`**, never raw `FindFirst`.
- **Enum-to-string EF mapping is `.HasConversion<string>()`** — that is the live convention. The
  one `EnumToStringConverter` use in the tree is `OrganizationMemberConfiguration.cs:23`, which
  Task 1.16 deletes.
- Warnings-as-errors + StyleCop/Meziantou/Roslynator on every non-test project.

### Architecture tests this plan will trip

Run `./scripts/run-tests.sh arch` at every phase gate. Four will bite:

- **`CleanArchitectureTests`** asserts `Wallow.{Module}.Api` has no dependency on
  `Wallow.{Module}.Infrastructure`. `Wallow.Identity.Api.csproj:18` already project-references
  Infrastructure for DI, so this passes only by discipline — the moment a controller *names* an
  Infrastructure type, NetArchTest fails and `dotnet build` does not. **Every service injected
  into a controller needs an interface in `Wallow.Identity.Application/Interfaces`.**
- **`SeedClientIdConsistencyTests`** (`:12-38`) + **`PublicSeedClientRemovalTests`** pin
  `api/seed.json` clients **positionally** (`clients[1]` is canonical) and require
  `Identity:FirstPartyClients` entries to name clients `seed.json` actually seeds. Changing the
  client array means renumbering `Identity__FirstPartyClients__<index>` in
  `docker/docker-compose.test.yml`, `docker/docker-compose.production.yml` and
  `docker/.env.production.example` in the same commit.
- **`CrossTenantTestGateTests`** (`api/tests/Wallow.Architecture.Tests/CrossTenantTestGateTests.cs:48-57`)
  hardcodes the tenant-isolation test files and requires each to carry
  `[Trait("Category", "CrossTenant")]`. New cross-tenant tests must be appended to
  `_crossTenantTestFiles`.
- **`DenyByDefaultAuthorizationTests`** requires every controller action to declare authorization
  intent — an endpoint that "needs no permission" still needs `[Authorize]`.

`MultiTenancyArchitectureTests.cs:16-22` deliberately excludes Identity from `_tenantAwareModules`,
so nothing there forces `Membership` to be `ITenantScoped`. See Task 1.1 for why it must not be.

### Schema policy

Wallow has never been deployed. **Reshape and re-seed; do not migrate.** There are exactly three
migrations (`20260329204526_InitialCreate`, `20260722194304_ChangeOrgMemberRoleToEnum`,
`20260722203026_DropEnterpriseIdpSurface`) and `organization_members` is created by
**`InitialCreate`** (`:474`, `:484-486`, `:823-831`, `:1035`) — the *first*, not a trailing one.
Regenerate `InitialCreate`; trimming the tail leaves the table in the schema.

### Decisions awaiting your confirmation

Two choices are made below as defaults so execution is not blocked. Confirm or overturn each
**before** the task that depends on it.

1. **Task 2.6 — token revocation.** Default: enable `UseReferenceAccessTokens()` +
   `EnableTokenEntryValidation()`. Rationale: §4.3 claims a suspended membership "does not
   authenticate"; with the current self-contained JWTs that claim is false for the token lifetime.
   Cost: a DB lookup per request. **Alternative:** keep self-contained tokens, delete the claim,
   cut the access-token lifetime, and document the residual window in §4.3.
2. **Task 1.11 — scope-gate narrowing promoted into Phase 1** (v1 had it at 5.5.1). Rationale: the
   hard refusal already fires for `wallow-web-client` today, and Task 4.1 makes it worse. This
   reorders the design doc's §11 phasing.

---

## Phase 0: Verify the threat model

**No code changes.** Two hours here saves a phase of building against a threat that does not exist
or missing one that does.

### Task 0.1: Reproduce the cross-org escalation

Bring up the stack (`pnpm backend:infra`; `dotnet run --project api/src/Wallow.Api`;
`dotnet run --project api/src/Wallow.SeederService` — drop the DB first, since admin bootstrap is
skipped when any user already exists).

Create orgs A and B; create `alice@example.test`; give her the `admin` role; make her a member of
both; ensure an OIDC client is bound to B. Complete the code flow through B's client and decode the
token.

Expected if §1.1 holds: `org_id = B` **and** `role = admin`, and an API request resolved onto B
carries `permission` claims including `UsersDelete`, `RolesDelete`, `OrganizationsUpdate`.

Record the decoded token on the bead. If it does not reproduce, stop and correct the design doc.

### Task 0.2: Reproduce the scope-gate failure — note it is NOT only an external-RP problem

§14.2 frames the hard refusal as a `bcordes.dev` problem. It is not:
`RolePermissionMapping.cs:63-76` grants `["user"]` `InquiriesWrite` but **not** `InquiriesRead`,
while `wallow-web-client` in `api/seed.json` requests `inquiries.read`. The refusal therefore fires
for the reference frontend today.

Give a test user only the `user` role and a membership in `wallow-web-client`'s org. Start the code
flow requesting the client's full seeded scope list. Record whether the response is a `Forbid`
carrying `invalid_scope`.

If confirmed, Task 1.11 is a Phase 1 blocker, not a Phase 5.5 nicety.

---

## Phase 1: `Membership` + per-org roles — closes the escalation

Nineteen tasks. **The ordering is load-bearing: every consumer of the old model is repaired before
Task 1.16 deletes it.** Repairing a security check as a build fix, under pressure, with no test, is
how the escalation gets reintroduced through a different door.

### Task 1.0: Inventory every role-resolution and role-write site

**No production code.** The output is a checklist committed as a comment on the bead; the rest of
Phase 1 consumes it.

Search `api/src` for every site that (a) resolves roles for an authorization decision, or (b)
writes the role store:

```bash
rg -n 'GetRolesAsync|IsInRoleAsync|AddToRoleAsync|RemoveFromRoleAsync|GetUsersInRoleAsync' api/src
rg -n 'ClaimTypes\.Role|Claims\.Role|RoleClaimType|IsInRole\(' api/src
rg -n 'RolePermissionMapping|\[Authorize\(Roles|RequireRole' api/src
```

The sites already known — confirm each and add anything the greps turn up:

| Site | Kind | Repaired by |
|---|---|---|
| `AuthorizationController.cs:255` (`BuildClaimsIdentityAsync`) | resolve | 1.13 |
| `AuthorizationController.cs:315` (`ValidateRequestedScopesAsync`) | resolve | 1.11 |
| `TokenController.cs:120` (refresh grant) | resolve | 1.14 |
| `WallowUserClaimsPrincipalFactory.cs:24-27` | resolve | 1.15 |
| `OrganizationAccessPolicy.cs:26` | **decide** | 1.7 |
| `MfaExemptionChecker.cs:30-52` | **decide** | 1.8 |
| `HangfireDashboardAuthFilter` | resolve | 1.15 |
| `SetupStatusChecker.cs:19` (`GetUsersInRoleAsync("admin")`) | resolve | 1.6 |
| `UserManagementService.cs:218,221,250,257,280` | **write** | 1.9 |
| `AccountController.cs:760` (`AddToRoleAsync(user, "user")`) | **write** | 1.9 |
| `BootstrapAdminService.cs:52` | write | 1.9 |
| `TestSupportService.cs:35-38` | write | 1.16 |
| `OrganizationService.cs:41` (creator ⇒ Admin), `:114` | write | 1.5 |

**Commit:** nothing. Post the completed table to the bead and amend design §5.0.

### Task 1.1: The `Membership` domain entity

**Files:**
- Create: `api/src/Modules/Identity/Wallow.Identity.Domain/Enums/MembershipStatus.cs`
- Create: `api/src/Modules/Identity/Wallow.Identity.Domain/Identity/MembershipId.cs`
- Create: `api/src/Modules/Identity/Wallow.Identity.Domain/Entities/Membership.cs`
- Test: `api/tests/Modules/Identity/Wallow.Identity.Tests/Domain/MembershipTests.cs`

**Two design notes, both reversals from v1.**

*Surrogate key.* §4 describes the PK as `(UserId, OrganizationId)`. Use a surrogate `MembershipId`
with a **unique index** on `(UserId, OrganizationId)`. The reason is structural, not stylistic:
`api/src/Shared/Wallow.Shared.Kernel/Domain/Entity.cs:10-16` declares
`Entity<TId> where TId : struct, IStronglyTypedId<TId>` with a single `Id`. A composite PK is
impossible without abandoning the base class, losing identity equality and the domain-event bag.
The unique index preserves the guarantee §4 actually wants. Follow `OrganizationSettingsId` for the
ID shape.

*No `ITenantScoped`.* `Membership` must **not** implement it. `OrganizationId` *is* the scope, and
every read filters on it explicitly. A `TenantId` column would duplicate `organization_id` with no
independent writer or reader — the same "two rows that can disagree" failure this plan rejects
elsewhere. It is also actively dangerous: `IdentityDbContext.cs:173-194` applies a tenant filter to
every `ITenantScoped` type (which membership reads must bypass anyway), and
`TenantSaveChangesInterceptor.cs:42-49` **overwrites** `TenantId` on insert from the ambient
tenant. Identity is the only module that does not register that interceptor
(`IdentityInfrastructureExtensions.cs:226-254` — compare Storage, Inquiries, Announcements,
Notifications, ApiKeys, Branding, all of which do). So a ctor-derived `TenantId` would survive **by
luck**, and anyone "fixing the inconsistency" later would silently stamp every authorize-time
membership (Task 4.3) with the caller's *current* org instead of the target.

**Step 1: Write the failing tests**

```csharp
public class MembershipTests
{
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly OrganizationId _orgId = OrganizationId.New();
    private static readonly Guid _actorId = Guid.NewGuid();
    private static readonly FakeTimeProvider _time = new();

    [Fact]
    public void RequestAccess_creates_a_pending_membership_that_grants_nothing()
    {
        Membership membership = Membership.RequestAccess(_userId, _orgId, _time);

        Assert.Equal(MembershipStatus.Pending, membership.Status);
        Assert.Empty(membership.RoleIds);
        Assert.False(membership.IsOwner);
        Assert.Null(membership.JoinedAt);
        Assert.NotNull(membership.RequestedAt);
    }

    [Fact]
    public void Enroll_creates_an_active_membership_carrying_the_default_role()
    {
        Guid defaultRoleId = Guid.NewGuid();

        Membership membership = Membership.Enroll(_userId, _orgId, defaultRoleId, _time);

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal([defaultRoleId], membership.RoleIds);
        Assert.NotNull(membership.JoinedAt);
    }

    [Fact]
    public void Approve_activates_a_pending_membership_and_records_the_reviewer()
    {
        Guid defaultRoleId = Guid.NewGuid();
        Membership membership = Membership.RequestAccess(_userId, _orgId, _time);

        membership.Approve(defaultRoleId, _actorId, _time);

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(_actorId, membership.ReviewedBy);
        Assert.NotNull(membership.ReviewedAt);
        Assert.Equal([defaultRoleId], membership.RoleIds);
    }

    [Fact]
    public void Approve_rejects_a_membership_that_is_not_pending()
    {
        Membership membership = Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _time);

        BusinessRuleException ex = Assert.Throws<BusinessRuleException>(
            () => membership.Approve(Guid.NewGuid(), _actorId, _time));

        Assert.Equal("Identity.MembershipNotPending", ex.Code);
    }

    [Fact]
    public void Deny_records_the_review_and_leaves_no_roles()
    {
        Membership membership = Membership.RequestAccess(_userId, _orgId, _time);

        membership.Deny(_actorId, _time);

        Assert.Equal(MembershipStatus.Denied, membership.Status);
        Assert.Empty(membership.RoleIds);
        Assert.Equal(_actorId, membership.ReviewedBy);
    }

    [Fact]
    public void Suspend_keeps_the_roles_so_reinstating_restores_them()
    {
        Membership membership = Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _time);

        membership.Suspend(_actorId, _time);

        Assert.Equal(MembershipStatus.Suspended, membership.Status);
        Assert.Single(membership.RoleIds);
    }

    [Fact]
    public void AssignRole_is_idempotent()
    {
        Guid roleId = Guid.NewGuid();
        Membership membership = Membership.Enroll(_userId, _orgId, roleId, _time);

        membership.AssignRole(roleId, _actorId, _time);

        Assert.Single(membership.RoleIds);
    }

    [Fact]
    public void IsActive_is_true_only_for_Active()
    {
        Assert.True(Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _time).IsActive);
        Assert.False(Membership.RequestAccess(_userId, _orgId, _time).IsActive);
    }
}
```

**Step 2: Run and confirm failure** — `./scripts/run-tests.sh identity`; compile failure, no TRX.

**Step 3: Implement**

```csharp
// MembershipStatus.cs
namespace Wallow.Identity.Domain.Enums;

public enum MembershipStatus
{
    Pending,
    Active,
    Suspended,
    Denied
}
```

`MembershipId.cs` — copy `OrganizationSettingsId` exactly.

```csharp
// Membership.cs
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// A person's relationship with one organization. This is the entity that carries authorization:
/// roles hang off the membership, never off the user, so a role granted by one organization
/// confers nothing in another.
/// </summary>
/// <remarks>
/// Deliberately NOT ITenantScoped. OrganizationId is the scope and every read filters on it
/// explicitly; a TenantId column would duplicate it, and the tenant interceptor (which Identity
/// does not register) would overwrite it from the ambient tenant on insert, mis-stamping any
/// membership created while acting on behalf of a different organization.
/// </remarks>
public sealed class Membership : AggregateRoot<MembershipId>
{
    public Guid UserId { get; private set; }
    public OrganizationId OrganizationId { get; private set; }
    public MembershipStatus Status { get; private set; }

    /// <summary>
    /// Ownership only. Grants NO permission — it answers "who is the last person who cannot be
    /// removed" and seeds the access-request recipient fallback. Every authorization decision
    /// reads <see cref="RoleIds"/>. See design doc section 4.4.1.
    /// </summary>
    public bool IsOwner { get; private set; }

    public DateTimeOffset? RequestedAt { get; private set; }
    public DateTimeOffset? JoinedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }

    private readonly List<MembershipRole> _roles = [];
    public IReadOnlyList<Guid> RoleIds => _roles.Select(r => r.RoleId).ToList().AsReadOnly();

    public bool IsActive => Status == MembershipStatus.Active;

    private Membership() { } // EF Core

    private Membership(Guid userId, OrganizationId organizationId, TimeProvider timeProvider)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException("Identity.UserIdRequired", "User ID cannot be empty");
        }

        Id = MembershipId.New();
        UserId = userId;
        OrganizationId = organizationId;
        SetCreated(timeProvider.GetUtcNow(), userId);
    }

    /// <summary>Creates a Pending membership. A Pending membership authenticates nothing.</summary>
    public static Membership RequestAccess(
        Guid userId, OrganizationId organizationId, TimeProvider timeProvider)
    {
        return new Membership(userId, organizationId, timeProvider)
        {
            Status = MembershipStatus.Pending,
            RequestedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>Creates an Active membership directly — Open enrollment, or invitation accept.</summary>
    public static Membership Enroll(
        Guid userId, OrganizationId organizationId, Guid defaultRoleId, TimeProvider timeProvider)
    {
        Membership membership = new(userId, organizationId, timeProvider)
        {
            Status = MembershipStatus.Active,
            JoinedAt = timeProvider.GetUtcNow()
        };

        membership._roles.Add(new MembershipRole(membership.Id, defaultRoleId));
        return membership;
    }

    public void Approve(Guid defaultRoleId, Guid approvedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.MembershipNotPending", "Only a pending membership can be approved");
        }

        Status = MembershipStatus.Active;
        JoinedAt = timeProvider.GetUtcNow();
        ReviewedAt = timeProvider.GetUtcNow();
        ReviewedBy = approvedByUserId;
        AssignRole(defaultRoleId, approvedByUserId, timeProvider);
        SetUpdated(timeProvider.GetUtcNow(), approvedByUserId);
    }

    public void Deny(Guid deniedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.MembershipNotPending", "Only a pending membership can be denied");
        }

        Status = MembershipStatus.Denied;
        ReviewedAt = timeProvider.GetUtcNow();
        ReviewedBy = deniedByUserId;
        _roles.Clear();
        SetUpdated(timeProvider.GetUtcNow(), deniedByUserId);
    }

    public void Suspend(Guid suspendedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Active)
        {
            throw new BusinessRuleException(
                "Identity.MembershipNotActive", "Only an active membership can be suspended");
        }

        Status = MembershipStatus.Suspended;
        SetUpdated(timeProvider.GetUtcNow(), suspendedByUserId);
    }

    public void Reinstate(Guid reinstatedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Suspended)
        {
            throw new BusinessRuleException(
                "Identity.MembershipNotSuspended", "Only a suspended membership can be reinstated");
        }

        Status = MembershipStatus.Active;
        SetUpdated(timeProvider.GetUtcNow(), reinstatedByUserId);
    }

    public void AssignRole(Guid roleId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (_roles.Exists(r => r.RoleId == roleId))
        {
            return;
        }

        _roles.Add(new MembershipRole(Id, roleId));
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void RemoveRole(Guid roleId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (_roles.RemoveAll(r => r.RoleId == roleId) > 0)
        {
            SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
        }
    }

    public void MarkOwner(bool isOwner, Guid updatedByUserId, TimeProvider timeProvider)
    {
        IsOwner = isOwner;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
```

`MembershipRole` is a small owned entity beside it —
`sealed class MembershipRole(MembershipId membershipId, Guid roleId)` with both as properties.

`AuditableEntity.SetCreated`/`SetUpdated` are `(DateTimeOffset, Guid? = null)`;
`BusinessRuleException` is `(string code, string message)` and exposes `Code` via
`DomainException`.

**Step 4-5:** Run, confirm pass, `dotnet format`, commit —
`feat(identity): add the Membership aggregate carrying per-org authorization`

### Task 1.2: Persistence — `memberships` + `membership_roles`

**Files:**
- Create: `.../Wallow.Identity.Infrastructure/Persistence/Configurations/MembershipConfiguration.cs`
- Modify: `.../Wallow.Identity.Infrastructure/Persistence/IdentityDbContext.cs` (DbSet block, `:31-40`)
- Regenerate: `20260329204526_InitialCreate`

**Reversal from v1.** v1's task body mapped roles as an EF primitive-collection column (its own
Architecture summary said table). Use a real `identity.membership_roles` table keyed
`(MembershipId, RoleId)`. The primitive collection works on EF 10.0.10, but it costs three things
permanently: no FK to `AspNetRoles`, so deleting a role leaves dangling GUIDs the resolver silently
drops; no joinable shape for the Dapper reads `api/CLAUDE.md` mandates for complex queries, which
bites in **Phase 6's member list**, not Phase 7; and it makes per-org role definitions (§8) a
schema change rather than a row. The aggregate still owns the collection, so nothing above the
repository changes.

```csharp
public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => MembershipId.Create(value));

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(e => e.OrganizationId)
            .HasColumnName("organization_id")
            .HasConversion(id => id.Value, value => OrganizationId.Create(value))
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.IsOwner).HasColumnName("is_owner").IsRequired();
        builder.Property(e => e.RequestedAt).HasColumnName("requested_at");
        builder.Property(e => e.JoinedAt).HasColumnName("joined_at");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");

        // The uniqueness guarantee the design doc states as a composite primary key.
        builder.HasIndex(e => new { e.UserId, e.OrganizationId }).IsUnique();
        builder.HasIndex(e => e.OrganizationId);

        builder.OwnsMany<MembershipRole>("_roles", role =>
        {
            role.ToTable("membership_roles");
            role.WithOwner().HasForeignKey(r => r.MembershipId);
            role.HasKey(r => new { r.MembershipId, r.RoleId });
            role.Property(r => r.MembershipId).HasColumnName("membership_id");
            role.Property(r => r.RoleId).HasColumnName("role_id");
            role.HasIndex(r => r.RoleId);
        });
    }
}
```

Add `public DbSet<Membership> Memberships => Set<Membership>();`.

**Referential integrity on `role_id` is not optional.** `WallowRole` is an Identity type in the
same context, so prefer a real `HasOne<WallowRole>().WithMany()`. If that fights the Identity model
configuration, add the FK in raw SQL inside the migration and say so in a comment. Do not ship
without it.

**Regenerate `InitialCreate`**, drop the local identity schema, run `Wallow.MigrationService`, and
confirm both tables plus the unique index and the FK exist. Mapping assertions belong in
`IntegrationTests` — the in-memory provider will pass regardless.

**Commit:** `feat(identity): persist memberships and their per-org role assignments`

### Task 1.3: `IMembershipRepository`

**Files:**
- Create: `.../Wallow.Identity.Application/Interfaces/IMembershipRepository.cs`
- Create: `.../Wallow.Identity.Infrastructure/Repositories/MembershipRepository.cs`
- Test: `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/Memberships/MembershipRepositoryTests.cs`

```csharp
public interface IMembershipRepository
{
    Task<Membership?> GetAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Membership>> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Membership>> GetForOrganizationAsync(
        Guid organizationId, MembershipStatus? status = null, CancellationToken ct = default);
    void Add(Membership membership);
    void Remove(Membership membership);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**`IgnoreQueryFilters()` goes on `GetAsync` and `GetForUserAsync` ONLY** — a correction from v1,
which put it on every read. Those two run at authorize time where no tenant is resolved, exactly
like `OrganizationRepository.GetByUserIdAsync` (`:42-50`). `GetForOrganizationAsync` runs inside
authenticated, tenant-resolved handling; leaving the filter on keeps a backstop under Phase 5's
roster endpoints. Both filtered methods still `Where` on their own parameter — never relying on the
ambient filter, which is the trap §7.2 D describes for `GetPagedByTenantAsync`.

**Tests:** (a) `GetAsync` finds a membership while the ambient tenant is a *different* org — fails
without `IgnoreQueryFilters()`; (b) `GetForUserAsync` spans organizations.

**Commit:** `feat(identity): add the membership repository`

### Task 1.4: `IMembershipRoleResolver`

**Files:**
- Create: `.../Wallow.Identity.Application/Interfaces/IMembershipRoleResolver.cs`
- Create: `.../Wallow.Identity.Infrastructure/Services/MembershipRoleResolver.cs`
- Test: `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/Memberships/MembershipRoleResolverTests.cs`

The replacement for `userManager.GetRolesAsync(user)` in every authorization context. Interface in
Application, implementation in Infrastructure, interface injected into controllers — mirroring
`IOrganizationAccessPolicy` / `OrganizationAccessPolicy`. That is what keeps `CleanArchitectureTests`
green. It is **not** a domain service: it reads `AspNetRoles` and membership rows, which is I/O, and
Identity's Domain forbids persistence dependencies.

```csharp
public interface IMembershipRoleResolver
{
    /// <summary>
    /// The role names granted to this user BY this organization. Empty when there is no
    /// membership, or the membership is not Active. Feeds RolePermissionMapping unchanged.
    /// </summary>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(
        Guid userId, Guid organizationId, CancellationToken ct = default);
}
```

Reads the membership, returns empty unless `IsActive`, projects `RoleIds` to names via `WallowRole`
with `IgnoreQueryFilters()` — roles are seeded `TenantId = Guid.Empty` (`SeederWorker.cs:64`) and
the catalog is global per §4.1.

**Tests:** admin-in-A resolves `["user"]` in B (§1.1 in one assertion); empty for no membership;
empty for `Pending`; empty for `Suspended`.

**Commit:** `feat(identity): resolve role names per (user, organization)`

### Task 1.5: `OrganizationService` and `OrganizationRepository` delegate to memberships

**Files:**
- Modify: `.../Wallow.Identity.Infrastructure/Services/OrganizationService.cs` — `:41`, `:102`,
  `:114`, `:130`, `:144`, `:168`, `:180-193`, `:338`, `:343`, `:449`
- Modify: `.../Wallow.Identity.Infrastructure/Repositories/OrganizationRepository.cs` — **all four**
  `.Include(o => o.Members)` sites: `:20`, `:28`, `:46-47` (v1 named only `:42-50`)
- Modify: `.../Wallow.Identity.Application/Interfaces/IOrganizationService.cs:13-16`
- Test: `OrganizationServiceTests`, `OrganizationServiceGapTests`

`Organization.Members` stays alive in this task — only the service and repository move off it.
Deletion is Task 1.16.

**`AddMemberAsync` gains a role parameter.** v1 claimed the signature was unchanged; it cannot be.
`AddMemberAsync(Guid orgId, Guid userId, CancellationToken)` has nowhere to say which role, and a
per-org role model whose only add-path grants an implicit role is the escalation surface again. Add
`Guid roleId` (or a `defaultRole` fallback resolved from `OrganizationSettings`), and update all
four callers: `PreRegisteredClientSyncService.cs:243`, `UsersController.cs:102`,
`AccountController.cs:769`, `OrganizationsController.cs:124` — and its `AddMemberRequest` DTO, which
is an OpenAPI change, so regenerate.

`OrganizationService.cs:41` (`CreateOrganizationAsync` adds the creator as `OrgMemberRole.Admin`)
becomes: create an `Active` membership with the `admin` role **and** `IsOwner = true`.

`GetUserOrganizationsAsync` becomes "organizations where this user has an **Active** membership".

`OrganizationService.cs:180-193` projects `[member.Role.ToString()]` into `UserDto.Roles`; it now
projects the membership's role names. **OpenAPI-visible — regenerate the snapshot.**

**Commit:** `refactor(identity)!: read and write organization members through memberships`

### Task 1.6: Seeding — and the anonymous-bootstrap gate

**Files:**
- Modify: `.../Wallow.Identity.Infrastructure/Services/PreRegisteredClientSyncService.cs` —
  `:199-212` (org auto-create), `:219-243` (`EnsureSeedMembersAsync`)
- Modify: `api/seed.json`
- Modify: `api/src/Wallow.SeederService/SeedOptions.cs` (**not** `Options/SeedOptions.cs` — there is
  no `Options/` directory)
- Modify: `.../Wallow.Identity.Infrastructure/Services/SetupStatusChecker.cs:19`

**Ordered here deliberately** — immediately after 1.5, not at the end of the phase. Once 1.5
rewires `AddMemberAsync`, the seeder creates memberships with no role, so `admin@wallow.dev` signs
in with zero role claims. Nothing in `run-tests.sh` catches that; leaving it until later makes
every manual check in between misleading.

**v1 named the wrong files.** `SeederWorker` does not create organizations or memberships —
`api/seed.json` has only four top-level keys (`roles`, `apiScopes`, `admin`, `clients`) and
`BootstrapAdminAsync` (`:110-150`) creates the user and assigns the global `admin` role. Orgs and
members come from `PreRegisteredClientSyncService`. Add a per-client `seedMemberRoles` map beside
`seedMembers`.

**`SetupStatusChecker` re-arms anonymous admin bootstrap.** It gates the `[AllowAnonymous]` setup
endpoints on `userManager.GetUsersInRoleAsync("admin")` returning zero — a read of
`AspNetUserRoles`. If this task stops writing that table, a freshly seeded instance reports
setup-required and **any unauthenticated party can create an admin** via
`POST /v1/identity/setup/admin` (`BootstrapAdminService.CreateUserAsync` sets
`EmailConfirmed = true`, so no email challenge intervenes). Re-express the gate as "no Active
membership holds a role granting `AdminAccess`", and **state explicitly** whether the seeder still
writes `AspNetUserRoles`. Test: a seeded instance reports setup NOT required.

Also reconcile the clients per §10.4 — `bcordes-bff` and `sa-bcordes-bff` both on `Wallow`, and
`bcordes-bff` off `wallow-web-client`'s callback port. That disturbs `SeedClientIdConsistencyTests`
(positional) — run `./scripts/run-tests.sh arch` and renumber the
`Identity__FirstPartyClients__<index>` overrides in the three docker files in the same commit.

**Verify:** drop the DB, re-seed, sign in as `admin@wallow.dev`, confirm the admin role appears in
exactly the seeded organization and nowhere else.

**Commit:** `feat(identity): seed explicit per-organization memberships and roles`

### Task 1.7: Replace `OrganizationAccessPolicy` with a permission-parameterized check

**Files:**
- Modify: `.../Wallow.Identity.Application/Interfaces/IOrganizationAccessPolicy.cs` (including the
  doc comment at `:7-15`)
- Modify: `.../Wallow.Identity.Infrastructure/Services/OrganizationAccessPolicy.cs:26`
- Modify: `.../Wallow.Identity.Api/Controllers/OrganizationsController.cs` — `:28-37` (comment),
  `:46` (`CanAddressOrganizationAsync`), and every call site
- Test: `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/Organizations/OrganizationAccessPolicyTests.cs`

**This task did not exist in v1 and is the reason Phase 1 as written was unsafe.**
`OrganizationAccessPolicy` decides *all* cross-org reach via
`organization.Members.Any(m => m.UserId == userId && m.Role == OrgMemberRole.Admin)` — and Task 1.5
removes the roster while Task 1.16 deletes the enum. Left unnamed, whoever unblocks the build picks
the security semantics.

It gates **twelve** endpoints: `GET {id}`, `GET {id}/members`, `POST {id}/members`,
`DELETE {id}/members/{userId}`, `POST {id}/archive`, `POST {id}/reactivate`, `DELETE {id}`,
`GET/PUT {id}/branding`, `POST {id}/branding/logo`, `GET/PUT {id}/settings`.

**Do not replace it with a boolean.** One fixed predicate currently gates both `OrganizationsRead`
and `DELETE {id}` — conflating read reach with destroy reach is a latent bug, and this is the moment
to fix it:

```csharp
Task<bool> HasPermissionInOrganizationAsync(
    Guid organizationId, Guid userId, PermissionType required, CancellationToken ct = default);
```

Resolve the caller's roles in `organizationId` through `IMembershipRoleResolver` (which already
requires `Active`), expand through `RolePermissionMapping`, assert `required`.
`CanAddressOrganizationAsync` takes each endpoint's own `[HasPermission]` value as an argument.

This satisfies §12.1 — one permission, `OrganizationsManageMembers`, governs the member lifecycle —
without giving `IsOwner` permission semantics, which §4.4.1 forbids.

**Both tempting shortcuts are wrong.** `IsOwner` contradicts §4.4.1 and breaks the manager case.
"A membership exists" is an exploit: harmless-looking in Phase 1, where memberships are mintable
only by org creation, seeding and invitation — but Phase 4's `EnrollmentPolicy.Open` makes them
self-mintable, and `OrganizationsRead` stays on the `user` role even after Task 4.1. A self-enrolled
visitor would then read `GET {id}`, `GET {id}/members`, `GET {id}/branding` and `GET {id}/settings`:
full roster, every member email, and the org's MFA and enrollment settings.

**Rewrite the interface doc at `:7-15` and the controller comment at `:28-37`.** Both justify the
check's safety on the premise that `AddMember` always adds `OrgMemberRole.Member` so an Admin
relationship "cannot be granted by an attacker." Task 1.5 already destroyed that premise. A comment
asserting a constraint the code no longer has is worse than no comment.

**Tests:** a `user`-role member gets 403 from `DELETE {id}`; a `manager` gets 200 from
`POST {id}/members` and 403 from `DELETE {id}`; a member of A gets 403 on every endpoint for B.

**Commit:** `fix(identity)!: gate cross-organization access on per-org permissions`

### Task 1.8: `MfaExemptionChecker` — a live MFA bypass that Phase 4 arms

**Files:**
- Modify: `.../Wallow.Identity.Infrastructure/Services/MfaExemptionChecker.cs:30-52`
- Test: `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/Mfa/MfaExemptionCheckerTests.cs`

`IsExemptAsync` picks an org with `FirstOrDefaultAsync(m => m.UserId == user.Id)` — no `OrderBy`, no
org parameter, no status filter — then decides from **that** org's settings whether to skip the MFA
challenge (`AccountController.cs:111` and `:346`, both
`if (user.MfaEnabled && !await mfaExemptionChecker.IsExemptAsync(...))`).

Today "arbitrary". After Phase 4, **attacker-chosen**: a passwordless user with memberships in a
strict org A and a lax org B (`RequireMfa = false, AllowPasswordlessLogin = true`) authenticates
with a magic link and no second factor whenever the query yields B, then acquires a token for A.
Anyone who can create an org — or self-enroll into a lax one under `EnrollmentPolicy.Open` — mints
themselves a permanent exemption.

**There is no correct single-org answer at this call site**, because exemption is evaluated during
cookie login, before any client or org is known. Take the **strictest policy across every `Active`
membership**: exempt only if *every* such org has `RequireMfa == false && AllowPasswordlessLogin ==
true`. Keep `IgnoreQueryFilters()` — login has no tenant.

Second bypass in the same method: `MfaGraceDeadline` is user-level (`:23`), so a grace period
granted by a lax org exempts the user everywhere. Make it per-membership, or re-evaluate it against
the strictest org.

**Test:** a user in two orgs, one requiring MFA, is **not** exempt — regardless of row order.

**Commit:** `fix(identity): evaluate MFA exemption against every active membership`

### Task 1.9: The role *write* path

**Files:**
- Modify: `.../Wallow.Identity.Infrastructure/Services/UserManagementService.cs` — `:218`, `:221`,
  `:250`, `:257`, `:280`
- Modify: `.../Wallow.Identity.Api/Controllers/UsersController.cs` (both role routes)
- Modify: `.../Wallow.Identity.Api/Controllers/AccountController.cs:760`
- Modify: `.../Wallow.Identity.Infrastructure/Services/BootstrapAdminService.cs:52`
- Test: `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/Users/RoleAssignmentTests.cs`

**Absent from v1, and the omission would make revocation silently lie.** Once the resolver reads
`membership_roles`, `RemoveRoleAsync` (`:250`) deletes an `AspNetUserRoles` row nothing reads and
returns success; `GetUserRolesAsync` (`:280`) re-reads the same dead table, so the UI confirms the
role is gone. The user keeps every permission indefinitely. A grant is equally inert, which pressures
operators back toward a global path.

Rewrite `AssignRoleAsync` / `RemoveRoleAsync` / `GetUserRolesAsync` onto `(userId, organizationId)`
membership-role writes via the aggregate's `AssignRole`/`RemoveRole`. `UsersController`'s two role
routes take or derive an org.

**`AccountController.cs:760`** (`AddToRoleAsync(user, "user")` on self-registration) must **not**
become a membership write here. An `[AllowAnonymous]` endpoint that grants membership bypasses
`EnrollmentPolicy` entirely, and InviteOnly would mean nothing. Delete the role write; the membership
comes from Phase 4's enrollment path. Accept that a self-registered user authenticates nowhere until
Phase 4 — that is §1.2's dead end, which Phase 4 exists to close, and it is a strictly safer
intermediate state than the alternative.

**This must land in the same commit as, or before, Tasks 1.13/1.14.** The window where reads are
scoped and writes are not is the window where revocation lies.

**Test:** grant `admin` in org A; assert the user resolves `admin` in A and not in B; revoke; assert
the resolver no longer returns it.

**Commit:** `fix(identity)!: write role assignments to memberships, not the global role store`

### Task 1.10: Close all three permission-expansion paths

**Files:**
- Modify: `.../Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs` —
  `ExpandUserScopes` (`:89-106`), `ExpandServiceAccountScopes` (`:147-161`), `IsCrossTenantRequest`
  (`:134-137`)
- Test: `api/tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/PermissionExpansionMiddlewareTests.cs`

v1 guarded only `ExpandUserScopes`. That closes one of three paths:

1. `ExpandUserScopes` has no `IsCrossTenantRequest` guard while `ExpandUserRoles` (`:65-68`) does —
   so scope expansion is the bypass for role expansion. **Add the early return.**
2. `ExpandServiceAccountScopes` has **no guard at all** — any `sa-`/`app-` client or `api_key`
   principal gets unconditional scope expansion regardless of `org_id`. **Add the same guard.**
3. `IsCrossTenantRequest` returns **false** when the caller's own tenant id is empty, so a principal
   with no `org_id` expands everything. **Make it fail closed:** no tenant is not "same tenant", it
   is "no tenant", and must expand nothing.

**Tests:** one per path. The third asserts a principal with no `org_id` receives no `permission`
claim.

**Commit:** `fix(identity): stop permission expansion granting across or without a tenant`

### Task 1.11: The scope gate narrows instead of refusing — and actually narrows

**Files:**
- Modify: `.../Wallow.Identity.Api/Controllers/AuthorizationController.cs` —
  `ValidateRequestedScopesAsync` (`:301-338`), **`:225`** (the authorization descriptor), and
  **`:284`** (`identity.SetScopes(request.GetScopes())`)
- Test: `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/ScopeNarrowingTests.cs`

**Promoted from v1's Phase 5.5, and corrected.** Promoted because the hard refusal already fires for
`wallow-web-client` today (Task 0.2), and Task 4.1 makes it worse. Corrected because v1's version was
actively dangerous: it changed only the validator, while the scopes that reach the token are set at
`:284` from `request.GetScopes()` — the **requested** set. The narrowed set would have been computed
and discarded, `ExpandUserScopes` would map the full requested set through `ScopePermissionMapper`,
and a member requesting `users.manage` would receive `UsersDelete` where today they receive
`invalid_scope`. That is a silent grant replacing a hard refusal — the most dangerous single error in
v1.

So: **`ValidateRequestedScopesAsync` returns the narrowed set, not a bool**, and both `:225` and
`:284` consume it.

Keep the **first** gate refusing — scopes not registered for the client are a client
misconfiguration, and failing loudly is right. Log the narrowed set; a silently smaller grant with no
trace is its own debugging problem.

**Test — HTTP 200 is not the assertion.** Assert the refused scope is absent from the issued token's
`scope` claim **and** that no corresponding `permission` claim survives middleware expansion.

**Commit:** `fix(identity)!: grant the scopes a caller is entitled to instead of refusing the request`

### Task 1.12: The authorization-code-flow test harness

**Files:**
- Create: `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/AuthorizationCodeFlowHarness.cs`
- Test: one throwaway spec asserting the seeded admin completes the flow end to end

**Absent from v1, which assumed this existed.** It does not: `rg -l 'connect/authorize' api/tests`
returns **zero files**. `TokenAcquisitionTests.cs`'s only helper posts
`["grant_type"] = "client_credentials"` (`:72`), and `WallowApiFactory` wires `TestAuthHandler`,
which fabricates a principal and does **not** establish the ASP.NET Identity cookie
`AuthorizationController` reads. Tasks 1.11, 1.13, 1.14, 1.17, 2.4 and 2.5 all assert on a flow
nothing can currently drive.

The harness must: (a) sign a `WallowUser` in against the real cookie scheme; (b) GET
`/connect/authorize` with PKCE, following redirects manually; (c) POST the code to `/connect/token`;
(d) expose `RefreshAsync(refreshToken)`.

**Commit:** `test(identity): add an authorization-code flow harness`

### Task 1.13: `AuthorizationController` — reorder, per-org roles, and refuse the tenantless case

**Files:** `AuthorizationController.cs` — Test: `TokenAcquisitionTests`

Three changes; the second depends on the first.

**Reorder (§5.0).** `ValidateRequestedScopesAsync` is called at `:99`; `tenantInfo` is not resolved
until `:190`. Move tenant resolution (`:186-191`) and the membership gate (`:193-204`) **above** the
scope-gate call, so the gate sees the org and nobody reaches the consent screen (`:169-183`) before
being told they are not a member.

**Per-org roles at both sites.** `:255` in `BuildClaimsIdentityAsync` and `:315` in
`ValidateRequestedScopesAsync`:

```csharp
IReadOnlyList<string> roles =
    await membershipRoleResolver.GetRoleNamesAsync(Guid.Parse(userId), tenantInfo.TenantId);
```

Thread the resolved `ClientTenantInfo` in as a parameter; inject `IMembershipRoleResolver`.

**Refuse when `tenantInfo` is null.** v1 wrote `tenantInfo is not null ? … : []`, which issues a
role-free, **org-free** token — and the membership gate sits inside the same null check, so it is
skipped too. Combined with Task 1.10's third finding, that token expands every permission its scopes
map to against whatever tenant is resolved downstream. A client with no tenant binding must be
refused at authorize, not handed a tenantless token.

**Commit:** `fix(identity)!: issue org-scoped roles from the authorize endpoint`

### Task 1.14: `TokenController` — the refresh grant

**Files:** `TokenController.cs:120-124` — Test: `TokenAcquisitionTests`

`HandleAuthorizationCodeOrRefreshAsync` rebuilds the identity from scratch and re-injects global
roles. It already carries `org_id` forward at `:135-145` — read it *before* the role block.

The correct pattern is in the same method: `:126-132` deliberately re-reads global admin from the
claim store rather than trusting the incoming principal. Roles get the same treatment.

```csharp
string? orgId = principal.GetClaim("org_id");

if (orgId is not null && Guid.TryParse(orgId, out Guid organizationId))
{
    IReadOnlyList<string> roles =
        await membershipRoleResolver.GetRoleNamesAsync(user.Id, organizationId);

    foreach (string role in roles)
    {
        identity.AddClaim(Claims.Role, role);
    }
}
```

**Test:** acquire as admin-in-A through a B-bound client, refresh, assert no `admin` claim. This is
the case that silently reopens the escalation an hour after "fixing" authorize.

**Commit:** `fix(identity)!: resolve org-scoped roles on the refresh grant`

### Task 1.15: The cookie stops carrying roles — and the two things that read them

**Files:**
- Modify: `.../Wallow.Identity.Infrastructure/Services/WallowUserClaimsPrincipalFactory.cs`
  (`GenerateClaimsAsync`, `:20-30`)
- Modify: `api/src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs`
- Test: `WallowUserClaimsPrincipalFactoryTests`

`base.GenerateClaimsAsync(user)` stamps role claims from `AspNetUserRoles` into the auth cookie, and
those claims ride the exchange-ticket flow. Strip them:

```csharp
ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

// Roles are per-organization (design doc section 4). The cookie has no organization context,
// so it must not carry role claims — they are resolved at token issuance.
foreach (Claim roleClaim in identity.FindAll(identity.RoleClaimType).ToList())
{
    identity.RemoveClaim(roleClaim);
}
```

The `org_id` stamp at `:24-27` is Task 2.1's concern; leave it here.

**`HangfireDashboardAuthFilter` is a fifth role-derived decision** and reads exactly these claims
(`User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "admin")`). Post-strip it denies
everyone outside Development — fails closed, so not exploitable, but it also returns `true`
unconditionally in Development, so "it works locally" is misleading and the shortest fix for a broken
production dashboard is to re-add a role claim to the cookie, undoing this task. Convert it to a
permission check (`AdminAccess`) against the request principal, and gate the Development bypass on an
explicit configuration flag rather than the environment name.

**Commit:** `fix(identity)!: keep global role claims out of the auth cookie`

### Task 1.16: Delete `OrganizationMember` and `OrgMemberRole`

**Files — deletions:**
- `.../Wallow.Identity.Domain/Entities/OrganizationMember.cs`
- `.../Wallow.Identity.Domain/Enums/OrgMemberRole.cs`
- `.../Wallow.Identity.Infrastructure/Persistence/Configurations/OrganizationMemberConfiguration.cs`
- `api/tests/.../Domain/OrganizationMemberTests.cs`
- `api/tests/.../OrganizationMemberRoleMappingTests.cs` (pins the enum conversion)

**Files — modifications:** `Organization.cs:23-24` (`_members`/`Members`) and `:70-96` (`AddMember`
70-82, `RemoveMember` 84-96); `OrganizationConfiguration.cs:60` (`builder.HasMany(e => e.Members)`);
`IdentityDbContext.cs:36` (the `OrganizationMembers` DbSet); `TestSupportService.cs:35-38`;
`OrganizationMfaPolicyService.cs:36-39`.

**Test fallout — twelve files, not one.** v1 named only `OrganizationTests`. Update or delete:
`OrganizationTests` (28 facts), `RepositoryTests` (23), `ContractEventsTests` (17),
`OrganizationServiceGapTests` (16), `OrganizationServiceTests` (14), `OrganizationMemberTests` (10,
delete), `OrganizationMfaPolicyServiceTests` (6), `SimpleEmailTemplateServiceTests` (1 hit),
`OrganizationMemberRoleMappingTests` (2, delete), `MembershipReadModelTests` (2 — reads the
`OrganizationMembers` DbSet; rewrite, and **rename it**, since its name now collides with the new
aggregate), `OrganizationMemberAddedNotificationHandlerTests` (2),
`OrganizationMemberRemovedNotificationHandlerTests` (1).

**The verification gate, corrected.** v1 said `rg 'OrgMemberRole|OrganizationMember' … # must return
nothing`, which can never pass — `OrganizationMemberAddedEvent` survives (Task 6.1 uses it) and
matches the pattern. Use:

```bash
rg -n 'OrgMemberRole|OrganizationMember(?!Added|Removed)' api/src api/tests --pcre2
```

**Commit:** `refactor(identity)!: drop OrgMemberRole in favour of the shared role catalog`

### Task 1.17: The Phase 1 acceptance test

**Files:** Create
`api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/CrossOrgRoleIsolationTests.cs`,
and append it to `_crossTenantTestFiles` in
`api/tests/Wallow.Architecture.Tests/CrossTenantTestGateTests.cs:48-57`.

**All three assertions must hold.** Any one passing alone means only a subset of Task 1.0's inventory
landed.

```csharp
[Trait("Category", "CrossTenant")]
public class CrossOrgRoleIsolationTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    // Alice is admin in org A and a plain member of org B; the client under test is bound to B.
    // Drive the flow with AuthorizationCodeFlowHarness (Task 1.12).

    [Fact] public async Task Authorize_issues_no_admin_permission_in_the_other_organization() { }
    [Fact] public async Task Refresh_does_not_reintroduce_the_admin_role() { }
    [Fact] public async Task Requesting_a_privileged_scope_does_not_smuggle_the_permission_in() { }
}
```

`IdentityIntegrationTestBase` has one constructor, `(WallowApiFactory factory)`, so the
primary-constructor form above is required — v1's parameterless declaration would not compile, which
surfaces as *zero TRX files*, not a test failure. `[Collection]` and `[Trait("Category","Integration")]`
are already on the base (`:22-23`); do not repeat them. `[Trait("Category","CrossTenant")]` **is**
required by the arch gate.

**Commit:** `test(identity): pin cross-organization role isolation across every token path`

### Task 1.18: Docs

**Files:**
- `docs/architecture/authorization.md` — `:3`, `:25`, `:95` and the "Existing Roles" table
  (`:128-131`) all state the global-role model as fact. Rewrite around `(user, organization)` and
  `IMembershipRoleResolver`. Also fix `:48`, which already cites the wrong path for
  `RolePermissionMapping.cs` (it lives in `Shared/Wallow.Shared.Kernel/Identity/Authorization/`).
- `api/src/Modules/Identity/CLAUDE.md:16` — lists `OrganizationMember` as a domain entity.
- `README.md` if it describes role assignment.

**Commit:** `docs: describe per-organization role assignment`

### Task 1.19: Phase 1 gate

```bash
dotnet format api/Wallow.slnx
dotnet build api/Wallow.slnx
./scripts/run-tests.sh
./scripts/run-tests.sh arch
```

**Then a real smoke run** — `dotnet run --project api/src/Wallow.Api`, complete one authorize. Build
and unit tests cannot catch a missing DI registration, and this phase adds four services across two
composition roots.

Regenerate the OpenAPI snapshot (`AddMemberRequest` and `UserDto` both moved).

`pnpm check` is **not** needed here — Phase 1 touches zero TypeScript. It becomes necessary from
Phase 3 on.

Re-run Task 0.1's manual repro and confirm it no longer reproduces.

---

## Phase 2: One tenant identity per session

### Task 2.1: Collapse the three tenant-claim sources — and name the replacement

- `WallowUserClaimsPrincipalFactory.cs:24-27` — stop stamping `org_id` from `user.TenantId`.
- `TokenController.cs:184` — stamp `org_id`, not `tenant_id`, on client-credentials tokens.
  `GetTenantId()` reads only `org_id` and nothing consumes `tenant_id`, so service accounts today
  resolve to **no tenant at all**.
- `TokenController.cs:272` — drop the `tenant_id` arm.

**Name what cookie-authenticated requests resolve tenant from afterwards.** That claim exists
specifically so they resolve one; its own comment says so. Without a replacement,
`TenantResolutionMiddleware` finds nothing, `IdentityDbContext` is built with `TenantId = Guid.Empty`,
and while reads fail closed, **writes are stamped `Guid.Empty`** — which from Phase 3 on includes
`Membership` rows created by invitation acceptance. Either derive the tenant from the route or refuse
the request; never write silently to the empty tenant.

(Identity does not register `TenantSaveChangesInterceptor`, so the stamping happens in whichever
module the request lands in — that is a reason to be careful, not a reason to relax.)

**Tests:** a client-credentials token for `sa-bcordes-bff` resolves onto its tenant (cannot pass
today); a cookie-authenticated write either resolves a real tenant or is rejected.

**Commit:** `fix(identity)!: carry tenant identity on org_id alone`

### Task 2.2: Drop `WallowUser.TenantId` — domain, configuration, migration

Entity, EF configuration, regenerated `InitialCreate`. Split from the call sites because the blast
radius is larger than Task 1.16's: `AccountController.cs` (21 hits), `OrganizationService.cs` (11),
`IdentityDbContext.cs` (7).

**Commit:** `refactor(identity)!: drop the frozen home tenant from WallowUser`

### Task 2.3: Drop `WallowUser.TenantId` — call sites and tests

Test files v1 did not name: `UserManagementServiceTests` (14 hits), `OrganizationTests` (5),
`MfaControllerTests` (5), four `TenantResolutionMiddleware*` classes, `AuthAuditEventHandlersTests`
and `AccountControllerAuditTests` (4 each).

**Commit:** `refactor(identity)!: remove the last WallowUser.TenantId consumers`

### Task 2.4: The membership gate asserts `Active`

`AuthorizationController`'s gate (moved earlier by Task 1.13) asks only whether a row exists. §4.3
requires `Active`. Pending → the request-submitted screen (Phase 5; until then
`/error?reason=access_requested`). Suspended and Denied → their own reasons. Global admin **bypasses**
this gate (§5.3).

**Tests:** Pending, Suspended, Denied each assert no token is issued.

**Commit:** `feat(identity)!: refuse token issuance for a membership that is not active`

### Task 2.5: Refresh re-checks membership

`HandleAuthorizationCodeOrRefreshAsync` already re-checks that the user exists (`:95`) and re-reads
global admin (`:129`). Membership status for `org_id` is the third thing that must be re-read rather
than trusted. Not `Active` → `Forbid` with `invalid_grant`.

**Test:** acquire, suspend, refresh, assert `invalid_grant`.

**Commit:** `fix(identity): re-check membership status on the refresh grant`

### Task 2.6: Revocation on exit — **confirm the decision first**

**v1's version cannot work and its test cannot pass.** OpenIddict 7.6.0 here runs
`DisableAccessTokenEncryption()` with **no** `UseReferenceAccessTokens` and **no**
`EnableTokenEntryValidation` (`IdentityInfrastructureExtensions.cs`), so access tokens are
self-contained JWTs validated by signature alone — `IOpenIddictTokenManager` revocation marks rows the
validation pipeline never consults. A suspended user keeps full access for the token lifetime.

**Default (confirm before starting):** enable `UseReferenceAccessTokens()` +
`EnableTokenEntryValidation()`, making revocation real and testable, at the cost of a DB lookup per
request. **Alternative:** keep self-contained tokens, delete §4.3's "does not authenticate" claim, cut
the access-token lifetime, and document the residual window.

**Either way, terminate live realtime connections.** `SseEndpoint.cs:47` caches the role list into the
connection manager at connect time and `RealtimeHub.cs:147` derives staff status from it, so a
connection opened before suspension keeps receiving staff-targeted traffic past token expiry entirely.

**Test:** suspend, then assert the previously-issued access token is rejected on the next API call.
Under the default this passes; under the alternative it must be rewritten to assert the window
instead.

**Commit:** `feat(identity)!: revoke access when a membership leaves active`

### Task 2.7: Phase 2 gate

Full gates + arch + smoke + OpenAPI regeneration.

---

## Phase 3: Invitations actually work

§7.2 defects A-F. §11's nine acceptance criteria all apply. **Commit boundaries are load-bearing
here** — see Task 3.2.

### Task 3.1: Prerequisite — the tenant filter blocks every invitation lookup

**Files:** `InvitationRepository.GetByTokenAsync`, `InvitationService.CleanupExpiredAsync` — criteria
**5** and **6**

`Verify(token)` is `[AllowAnonymous]` so no tenant resolves; `Accept(token)` is `[Authorize]` so it
filters on the caller's *current* org — precisely the org they are not yet in; `CleanupExpiredAsync`
runs from a background job with no tenant. Add `IgnoreQueryFilters()`. The 32-byte random token **is**
the tenant selector — the same precedent as `ServiceAccountRepository`, `OrganizationRepository` and
`MfaExemptionChecker`.

**Commit:** `fix(identity): resolve invitations outside the ambient tenant`

### Task 3.2: Defects A, B and C — **one commit**

**Files:** `InvitationService.cs:62-69` (`AcceptInvitationAsync`), `Invitation.cs` (`Accept`),
`InvitationsController.cs` — criteria **1**, **2**, **3**

**Why these cannot be separate commits.** Today defect C is *masked* by the defect beside it: `Accept`
filters on the caller's `org_id`, so an attacker in org A physically cannot resolve an invitation to
org B. Task 3.1 removes that mask. If "acceptance creates a membership" (A) lands before "acceptance
is bound to the invited, verified email" (C), then any authenticated user holding a leaked or
forwarded invite token joins the target org with its `DefaultRoleId` — and in an InviteOnly org that
token is the entire perimeter. v1 split these into three commits with no ordering constraint.

- **A** — `AcceptInvitationAsync` flips the status and records who accepted, and never calls
  `AddMemberAsync`. Create the membership and flip the status in **one transaction**. Acceptance
  bypasses `EnrollmentPolicy` entirely (§7.3) — being invited *is* the authorization.
- **B** — `Accept` guards only `Status != Pending`, so an invitation past `ExpiresAt` whose sweep has
  not run still accepts. Check `ExpiresAt` against `TimeProvider` and mark it `Expired`.
- **C** — require the accepting user's email to match `Invitation.Email` **and** be verified. Compare
  on the *normalized* email (`NormalizedEmail`, upper-invariant); `Invitation.Email` is stored as
  typed, so a case difference would otherwise reject a legitimate acceptance.

**Also close §12's still-open item 1** — a pending access request superseded by an invitation.
Accepting an invitation must close any `Pending` membership for that `(user, org)` as approved. Left
unimplemented, the orphaned row both blocks future legitimate requests (§4.7 rate-limits on one
pending request per pair) and lets the 30-day denial cooldown be sidestepped by arranging an
invitation.

**Commit:** `fix(identity)!: bind invitation acceptance to the invited verified email and create the membership`

### Task 3.3: Criterion 4 — the registration path

Email matches but unverified → rejected. Without it, "register with someone else's invited address and
take their seat" reopens defect C. Overlaps Task 4.5; **this task owns the accept-time check, 4.5 owns
the `returnUrl` threading.**

**Commit:** `fix(identity): reject invitation acceptance from an unverified registration`

### Task 3.4: Defects D and E — parameters that silently do nothing

**Files:** `InvitationRepository.GetPagedByTenantAsync`, `InvitationService.CreateInvitationAsync`
(`:26`, `:38`) — criteria **7** and **8**

Both take a `tenantId` and ignore it, correct today only by the global query filter — which Task 3.1
just removed from neighbouring methods in the same files. A method named
`GetPagedByTenantAsync(tenantId)` that does not filter on its parameter is a loaded trap: one
"cleanup" turns the org-admin invitation list into every invited email address in the system.

Criterion 7 asserts the scoping with the filter **bypassed**, so it fails if the scoping rests on the
filter. Criterion 8: honour the parameter or delete it.

**Commit:** `fix(identity): scope invitation queries on their own parameters`

### Task 3.5: Defect F — duplicates and already-members

Five clicks mint five independently valid tokens for one seat, and `Revoke` acts on one invitation by
id, so revoking the visible invite leaves four live. Reuse or refresh the existing pending invitation;
reject when the email already holds an `Active` membership.

**Commit:** `fix(identity): keep one live invitation per email and organization`

### Task 3.6: Phase 3 gate

All nine §11 criteria green, plus full gates, `pnpm check`, and the wallow-auth Playwright suite —
`apps/wallow-auth/e2e/login.spec.ts` and `signup.spec.ts` are backend-dependent and the accept
contract changed.

---

## Phase 4: Same email joins a second organization

### Task 4.0: `AccessRequestedEvent` (moved forward from v1's Task 5.1)

**Files:** Create `api/src/Shared/Wallow.Shared.Contracts/Identity/Events/AccessRequestedEvent.cs`

Moved ahead of Phase 5 because Task 4.3 publishes it — v1 scheduled the publisher two phases before
the contract, so Phase 4 could not compile.

```csharp
public sealed record AccessRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string OrganizationName { get; init; }
    public required Guid RequesterUserId { get; init; }
    public required string RequesterEmail { get; init; }
    public required string RequesterName { get; init; }
    public required IReadOnlyList<string> RecipientEmails { get; init; }
}
```

`IntegrationEvent` supplies only `EventId` and `OccurredAt`, so `TenantId` is declared here as the
other Identity events do.

**No `RequestId`** — the pending membership *is* the request. **No `ReviewUrl`** (a v1 reversal):
`InvitationCreatedNotificationHandler.cs:10-22` carries the *token* on the contract and composes the
URL in Notifications from its own `ServiceUrls` configuration. Putting the URL on the contract makes
Identity responsible for how a Notifications template renders a link, and it is the only reason v1 had
an open question here. Carry the identifiers the link needs; let the handler compose.

`RecipientEmails` is a list from day one — that is what makes §8's role/group routing a change inside
Identity's resolver with no contract, handler or template change.

### Task 4.1: Narrow the `user` role

**Files:** `RolePermissionMapping.cs:63-76`; test
`api/tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/RolePermissionMappingTests.cs`
(**not** `api/tests/Shared/…`, which does not exist — the kernel test project is
`api/tests/Wallow.Shared.Kernel.Tests/` and holds no role-mapping tests)

Remove `OrganizationsUpdate` and `OrganizationsCreate` from `["user"]`.

**No existing test turns red.** `RolePermissionMappingTests:18-28` asserts only
`Contain(OrganizationsRead)` / `NotContain(UsersRead)` / `NotContain(AdminAccess)`; every other
`OrganizationsCreate`/`OrganizationsUpdate` reference in `api/tests` is a `ScopePermissionMapper` or
`HasPermissionAttribute` test. The guard is **net-new**, added beside
`GetPermissions_UserRole_ReturnsBasicPermissions`. (`GetPermissions_AdminRole_ReturnsAllPermissions`
asserts admin === `PermissionType.All`, so admin is unaffected.) Do not go hunting for fallout.

Requires Task 1.11 to have landed — otherwise every client requesting a scope mapping to either
permission hard-fails for plain members, and the shortest fix under pressure is to put the permissions
back, reinstating the §4.6 escalation as a one-line seed change.

Self-service org creation, if it should exist, is a deliberate grant — file a bead, do not reintroduce
it here.

### Task 4.2: `EnrollmentPolicy` on `OrganizationSettings` — **same commit as 4.1**

`EnrollmentPolicy { Open, RequestApproval, InviteOnly }`; add `EnrollmentPolicy`,
`AccessRequestEmail`, `DefaultRoleId` to `OrganizationSettings` and extend `Create`/`Update`.
`InviteOnly` is the default for a new organization — today's de facto behaviour, made explicit, and
the safe failure mode.

**Gate the three new fields on `OrganizationsManageMembers`**, not `OrganizationsUpdate` (§4.6).
`UpdateSettingsAsync` now spans two permission levels, so **split it into two endpoints**
(`OrganizationsController.cs:304-318` is one `[HasPermission(OrganizationsUpdate)]` route today).
Per-field authorization inside one endpoint means either a partial write — the worst outcome — or a
403 for a request the caller was partly entitled to make. Split `IOrganizationService` to match; do
not leave one method with nullable "don't touch" parameters. OpenAPI change — regenerate.

**Commit 4.1 and 4.2 together:** `feat(identity)!: add per-organization enrollment policy`

### Task 4.3: Enrollment as an Application service, not a controller branch

**Files:** Create a `.../Wallow.Identity.Application/…/EnrollUserInOrganization` use case; modify
`AuthorizationController`'s membership gate and `AccountController`

| Policy | Behaviour |
|---|---|
| `Open` | Create `Active` membership with `DefaultRoleId`, continue |
| `RequestApproval` | Create `Pending` membership, publish `AccessRequestedEvent`, redirect to `/error?reason=access_requested` until Task 5.3 ships the real screen |
| `InviteOnly` | Reject — the existing `not_a_member` path |

**Do not write this in the controller.** v1 put a three-way policy branch, membership construction,
recipient resolution, event publication and redirect selection into `AuthorizationController` — and
Task 4.4 needs the same logic from `AccountController`, guaranteeing it is written twice, including
the email-verification precondition, which is a security rule that must not diverge. Return a
discriminated outcome (`Enrolled` / `PendingApproval` / `Rejected`) and let each controller map it to
a redirect. This also gives Task 5.1's resolver a home that does not create an Api→Infrastructure
dependency.

**Email verification is required before a second membership under any policy** (§4.2). New members
always receive `DefaultRoleId`, never an inherited role.

### Task 4.4: Fix `Register`'s `email_taken` dead end

§1.2: `CreateAsync` fails on `RequireUniqueEmail`, `AccountController.cs:753` maps it to `email_taken`
and returns at `:756` — thirteen lines above the `AddMemberAsync` call at `:769`. When the email
exists and the target org's policy allows it, enroll the existing identity through Task 4.3's service
(subject to verification) rather than reporting a collision.

### Task 4.5: Invite-through-registration

The token must survive registration → email verification → sign-in. `Register` already threads a
validated `returnUrl` through the verification link (`:776-780`, via `IRedirectUriValidator` /
`OpenIddictRedirectUriValidator`); the invitation token rides the same path. **The invitation is not
accepted at registration time**, only after verification — otherwise defect C reopens.

### Task 4.6: Phase 4 gate

Full gates + arch + smoke + OpenAPI regeneration, plus two acceptance criteria:

- A user who self-enrolls into an `Open` org gets **403** from `GET {id}/members` for a *different*
  org (the Task 1.7 guard, under the conditions that arm it).
- A plain `user` member completes an authorize against `wallow-web-client` with its full seeded scope
  list and receives a token (the Task 1.11 guard).

Under `RequestApproval` the manual join path terminates at an error page until Phase 5 — expected.

---

## Phase 5: Access requests

### Task 5.1: Recipient resolution

**Files:** Create **`.../Wallow.Identity.Application/Interfaces/IAccessRequestRecipientResolver.cs`**
and `.../Wallow.Identity.Infrastructure/Services/AccessRequestRecipientResolver.cs`

The interface is not optional — v1 specified only the concrete class, which `CleanArchitectureTests`
fails the moment a controller names it (and `dotnet build` does not, because
`Wallow.Identity.Api.csproj:18` already project-references Infrastructure).

Resolution order: `OrganizationSettings.AccessRequestEmail` → the emails of the org's owners
(`Membership.IsOwner`) → **log and skip the send, never fail the request.** The membership row is the
durable record; the email is a convenience.

### Task 5.2: The Notifications handler and template

**Files:** Create
`.../Wallow.Notifications.Application/EventHandlers/AccessRequestedNotificationHandler.cs`; modify
`SimpleEmailTemplateService` (`:28` switch, `WrapInLayout` as at `:228`)

A **static** class with `public static async Task Handle(AccessRequestedEvent message, ...)`,
dependencies as method parameters, Wolverine auto-discovers it, no DI registration — verified against
`InvitationCreatedNotificationHandler.cs:10-22`. One `SendEmailCommand` per recipient. The handler
composes the review URL from `ServiceUrls` configuration, exactly as the invitation handler composes
its invitation URL.

**Template key is `"accessrequest"`, not `"access-request"`.** `SimpleEmailTemplateService.cs:28`
dispatches on `templateName.ToLowerInvariant()` and every existing arm is lowercase with no hyphen
(`"organizationmemberadded"`, `"emailverification"`, `"magiclink"`, `"otpcode"`, `"invitation"` at
`:228`); `ToLowerInvariant()` does not normalise a hyphen, so a hyphenated key silently falls through
to the default arm.

### Task 5.3: The request-submitted screen

**Files:** Create `apps/wallow-auth/src/features/access-request/` (barrel + `components/` + co-located
specs) and `apps/wallow-auth/src/app/routes/access-request.tsx`; modify
`apps/wallow-auth/src/features/error/components/ErrorPage.tsx`

Replaces the `not_a_member` dead end. Note the error page has **two** actions today, not one —
`ErrorFooter` (`:97-107`) renders the auth-gated `SignOutLink` (`:84-94`) *and* an always-present
back-home link — so do not write an assertion premised on a single action.

Three things v1 omitted:

- Add `/access-request` to `apps/wallow-auth/e2e/routes.spec.ts:11-26`, which enumerates routes as a
  literal array — otherwise the reachability gate silently under-covers.
- Run `pnpm --filter @bc-solutions-coder/wallow-auth build` and commit `routeTree.gen.ts`. Codegen is
  a side effect of `vite build`; `route-tree-drift.yml` fails any PR touching
  `apps/wallow-*/src/app/routes/**` whose committed tree drifted.
- Run `pnpm lint:tests`, not just `pnpm lint`, for the new specs.

Component specs land on the browser project automatically; a spec asserting the route's `beforeLoad`
redirect must be named `access-request.ssr.test.tsx`. Heading is
`<Text as="h2" variant="subheading" color="onCard">` with no `weight` (`wallow/text-heading-variant`).

### Task 5.4: Phase 5 gate

Full gates + `pnpm check` + Mailpit (`http://localhost:8025`) to eyeball the rendered email.

---

## Phase 5.5: Sign in with Wallow from another site

Smaller than in v1 — the scope-gate fix moved to Task 1.11.

- **5.5.1** Seed `OpenIddictScopes` rows so the consent screen stops rendering null descriptions
  (§13/§14.3). A footnote while every client is first-party; a blocker the moment one is not.
- **5.5.2** `ClientsController.Create` (`:143-158`) attaches `scp:*` permissions — without them a
  client created through the admin API fails `ScopeSubsetValidator` on its first authorize, and
  registering an RP through the admin API is exactly the external-site path.
- **5.5.3** `org_id`/`org_name` on `UserinfoController` (`:32-70`) behind a scope; add `getOrgId` to
  `packages/sdk/src/claims.ts` (genuinely absent — the module exports `getRoles`, `hasRole`,
  `isAdmin`, `isOperator`, `isGlobalAdmin`). Note in that file that role names are org-scoped after
  Phase 1, since its header says the helpers mirror `ClaimsPrincipalExtensions`. Regenerate the SDK.
- **5.5.4** Global logout — **a decision to record, not just code** (§12 still-open item 3).
  `LogoutController` (`:42`, `:58`) calls `SignOutAsync(IdentityConstants.ApplicationScheme)`
  unconditionally on both paths, so signing out of one RP ends the session at all of them with no
  notification. Keep it — that is what an SSO platform should do — but **register front-channel logout
  URIs** so other RPs clear local sessions instead of failing on the next silent renew. Document in
  `docs/integrations/bff-pattern.md`, which today says nothing about roles or `org_id`, so this is a
  new section rather than an edit.
- **5.5.5** Wire the external site end to end. The client belongs to the organization **whose data it
  reads** — `Wallow`, not a new `bcordes` org (§14.1, and the §4.5 data-boundary rule).
  `postLogoutRedirectUris` are **already seeded** for both `wallow-web-client` and `bcordes-bff`
  (`seed.json:184-229`), so that part is a no-op; what does need fixing is the shared `redirectUris`
  collision (both currently `http://localhost:3000/bff/callback`) and `sa-bcordes-bff`'s
  `tenantName: "Dev"`. Set the org's `EnrollmentPolicy` — §4.6 must have landed before any org goes
  `Open`. The site uses `createWallowBffServer` (`packages/sdk/src/server/bff-server.ts:136`), not
  browser-to-API calls, which is why unimplemented `Cors__AllowedOrigins` does not block this.
- **5.5.6** Gate: full gates + a real end-to-end sign-in from the external origin.

---

## Phase 6: Close the loop

- **6.1** Approve / deny / suspend / reinstate endpoints, all gated on `OrganizationsManageMembers`
  (§4.4 — one permission, no `MembershipsApprove`). Approve publishes the existing
  `OrganizationMemberAddedEvent` so the current welcome email keeps working.
- **6.2** Leave-organization. Needs no *permission* — it is the caller's own membership — but
  `DenyByDefaultAuthorizationTests` still requires `[Authorize]`. Revokes their tokens for that org
  (Task 2.6).
- **6.3** Last-owner guard. **Name the mechanism, not just the rule.** This is a cross-aggregate
  invariant: two concurrent demote-owner requests each see two owners and both succeed, leaving zero.
  Aggregate boundaries do not help, because the two owners are two aggregates. Use a serialized read
  in the same transaction (`SELECT … FOR UPDATE` over the org's owner rows) or a count constraint.
  Written as "add the last-owner rule", this will be implemented as an unguarded `CountAsync`. Nothing
  enforces this today — it is new work, not a restoration.
- **6.4** Denial cooldown: 30 days before the same `(user, org)` may request again, clearable by an
  approver at any time.
- **6.5** `GET /v1/identity/me/organizations` — the caller's `Active` memberships. The honest version
  of a switcher (§5.4).
- **6.6** Audit events for every §4.7 transition. "Who let this person in" is the first question
  anyone asks.
- **6.7** Dashboard screens in `apps/wallow-web` — pending requests, outstanding invitations
  (`GET /invitations` already returns them paged; §7.1 needs a screen, not a backend), and member role
  management. **Three screens, each with a route, a query, a form and specs — this is a phase, not a
  task.** Give it its own plan.
- **6.8** Regenerate `packages/sdk/openapi/v1.json` and the SDK client; full gates.

---

## Phase 7: Later

Per-org custom role definitions, groups, and role/group request routing (§8, §9). **Do not plan this
now.** §8 is deferred because building a resolver against a table that does not yet hold per-org roles
is writing against a fiction, and §9 notes that building groups alongside memberships means debugging
two indirections at once — the one carrying the security guarantee is roles.

---

## Known gaps this plan does not close

Stated so they are decisions rather than oversights:

- **`ClientBrandingRepository.GetByClientIdAsync` lacks `IgnoreQueryFilters()`** (§13) — the same
  defect class as Task 3.1's prerequisite, and cited as its precedent. It fails closed, so it is a bug
  rather than a hole, but fixing the pattern in one of two places makes the remaining one read as
  intentional. File a bead.
- **`AspNetUserRoles` is not dropped.** After Phase 1 nothing reads it for authorization, but the
  table and ASP.NET Identity's writes to it remain. Task 1.6 must state explicitly whether the seeder
  still populates it — `SetupStatusChecker` depends on the answer. Dropping it is a follow-up.

---

## Session completion

1. File issues for remaining work.
2. Run quality gates (`./scripts/run-tests.sh`, `./scripts/run-tests.sh arch`, `pnpm check`).
3. Close finished issues, update in-progress ones.
4. `git pull --rebase && bd dolt push && git push`
5. Verify `git status` shows "up to date with origin".

Archive to `~/Documents/wallow-plans-archive/` when the status line reads `completed`.
