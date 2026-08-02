**status: superseded**

> **Superseded by what actually shipped.** Part 1 below recommends option 1 (issue a `plan`
> claim). The decision taken was **option 3 — remove `GetPlan()` and `AnnouncementTarget.Plan`
> entirely**, because Wallow has no billing and nothing can issue a plan. `AnnouncementTarget.Plan`
> is tombstoned as `// 2 was Plan (removed)`. The scope also grew to include **Wallow-d9en**
> (phantom module references): the `Communications` contract namespace was renamed to
> `Notifications`, the `Communications` API-scope category was split into `Announcements` and
> `Notifications`, and phantom `invoices.*`/`payments.*`/`subscriptions.*`/`messaging.access`
> OIDC scope registrations were dropped. Part 2 (phantom README types) shipped as written.
> Keep this doc only for the analysis; do not follow Part 1's recommendation.

# Implementation doc — Wallow-0e8j (plan claim) and Wallow-75pg (phantom README types)

Two independent bugs from the `Wallow-qi90` backend bug queue. They share no files and can land
in either order, or as two commits on one branch. Both are small.

- **Wallow-0e8j** — `GetPlan()` reads a `plan` claim that no token-issuance path writes, so
  plan-targeted announcements match zero users, silently. **Fix: issue the claim.**
- **Wallow-75pg** — two READMEs name 14 types that do not exist. **Fix: delete them** (plus four
  more drift sites found while verifying).

---

# Part 1 — Wallow-0e8j: make the `plan` claim real

## What is actually broken

The read side is complete and correct; the write side does not exist.

| Layer | File | State |
|---|---|---|
| Claim reader | `api/src/Shared/Wallow.Shared.Kernel/Extensions/ClaimsPrincipalExtensions.cs:86` | `GetPlan()` reads `"plan"` |
| Consumer | `api/src/Modules/Announcements/Wallow.Announcements.Api/Controllers/AnnouncementsController.cs:84` | passes it into `GetActiveAnnouncementsQuery` |
| Matcher | `.../Application/Announcements/Services/AnnouncementTargetingService.cs:103-111` | `MatchesPlan` returns `false` when `PlanName` is null/empty |
| **Issuer** | — | **nothing** |

Verified: `grep -rn 'GetPlan\|"plan"' api/src --include='*.cs'` returns exactly four lines — the
doc comment, the read, and the one call site. No `identity.AddClaim("plan", …)` anywhere.

So `MatchesPlan` takes its `string.IsNullOrEmpty(userContext.PlanName)` early return on every
request, every time. `AnnouncementTarget.Plan` is a dead branch of the switch. No exception, no
log — which is why nobody noticed.

`AnnouncementTargetingServiceTests` already covers plan matching (`_TargetPlan_MatchesByPlanName`,
`_IsCaseInsensitive`, `_ExcludesDifferentPlan`) and passes, because those tests construct
`UserContext` directly and never go through a `ClaimsPrincipal`. The gap is precisely the seam the
unit tests skip.

## Decision: option 1, and where the constants live

The bead offers three ways out; `Wallow-vsi3.6` (plan catalog) has already recorded that it wants
option 1 — **issue the claim now with a hardcoded default**, so billing later swaps a constant for
a lookup instead of re-adding a removed feature. There is no reason here to overrule that: Wallow
has no plan concept at all today (`Organization` has `Name`, `Slug`, `IsActive`, `ArchivedAt`,
`ArchivedBy` — no tier, no subscription), so a constant is honest about the present state.

**Invariant to adopt and state in code: the `plan` claim accompanies `org_id`.** A plan is a fact
about the organization, so every principal that carries a tenant carries a plan, and no principal
that carries no tenant carries one. That single rule decides all four issuance sites below with no
case-by-case argument.

Because `ApiKeys.Infrastructure` is one of those sites and cannot reference `Identity.Application`,
the default value has to live in the shared kernel alongside the claim type.

## Changes

### 1. New — `api/src/Shared/Wallow.Shared.Kernel/Identity/PlanClaim.cs`

```csharp
namespace Wallow.Shared.Kernel.Identity;

/// <summary>
/// The subscription plan a principal's organization is on. The plan travels as a token claim
/// rather than a lookup so a resource server can gate on it without a call back to Identity.
/// </summary>
/// <remarks>
/// <see cref="Default"/> is the whole plan catalog until billing lands (Wallow-vsi3.6), which
/// replaces this constant with the organization's real plan at issuance time. It is deliberately
/// a constant and not configuration: a fork with one plan should not have to configure one.
/// </remarks>
public static class PlanClaim
{
    /// <summary>The claim type carrying the organization's subscription plan.</summary>
    public const string ClaimType = "plan";

    /// <summary>The plan every organization is on until billing assigns real ones.</summary>
    public const string Default = "free";
}
```

### 2. `ClaimsPrincipalExtensions.cs` — read through the constant

```csharp
    /// <summary>
    /// Resolves the organization's subscription plan from the <c>plan</c> claim. Absent only on
    /// a principal that carries no organization — see <see cref="PlanClaim"/>.
    /// </summary>
    public static string? GetPlan(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(PlanClaim.ClaimType)?.Value;
```

Needs `using Wallow.Shared.Kernel.Identity;`.

### 3. `Identity.Api/Controllers/AuthorizationController.cs` — authorization-code issuance

In `BuildClaimsIdentityAsync`, immediately after the `org_id`/`org_name` block (currently lines
319-323, which is unconditionally inside a tenant-bound flow):

```csharp
        identity.AddClaim(PlanClaim.ClaimType, PlanClaim.Default);
```

No change to `GetDestinations` (line 452): `plan` falls to the `_ =>
[Destinations.AccessToken]` default, which is what the resource server reads. It does **not**
belong in the id token — the browser has no use for it and `org_id`/`org_name` are the only
non-standard claims that go there.

### 4. `Identity.Api/Controllers/TokenController.cs` — refresh and client credentials

**Refresh** (`HandleCodeOrRefresh…`, around lines 121-166). Set it inside the existing
`organizationId is not null` region, next to where roles are re-resolved:

```csharp
        if (organizationId is not null)
        {
            identity.SetClaim(PlanClaim.ClaimType, PlanClaim.Default);
            // …existing role resolution…
        }
```

Set it fresh; **do not carry it forward from the incoming principal**, for the same reason the file
already gives for roles and global-admin: a refresh token must not keep a plan alive after the
organization's plan changes. Today the value is a constant so it cannot differ — write it fresh
anyway, so the shape is already correct when billing supplies a real value.

**Client credentials** (`HandleClientCredentialsAsync`, lines 203-221), inside the branch that
sets `org_id` from the client's `TenantId` property:

```csharp
                identity.SetClaim("org_id", tenantId);
                identity.SetClaim(PlanClaim.ClaimType, PlanClaim.Default);
```

A service account is not going to read an announcement, but it acts for a tenant, and the
invariant is worth more than the one saved line.

### 5. `ApiKeys.Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs:62-68`

Add to the claim list, next to `org_id`:

```csharp
            new(PlanClaim.ClaimType, PlanClaim.Default)
```

### Deliberately not changed

- **`UserinfoClaims.Project`** — userinfo answers "who is this user"; the plan is a property of
  the organization, and `org_id` is already there for a client that needs to ask. Adding it is a
  contract change with no consumer.
- **`WallowUserClaimsPrincipalFactory`** — the auth cookie deliberately carries no `org_id`
  (a person belongs to many organizations), so by the invariant it carries no plan either.

## Optional hardening (bead's option 2, folded in)

Once the claim is always issued, "silently matches nobody" becomes near-unreachable, so this is
optional and can be skipped without failing the acceptance criteria. If you want the belt as well
as the braces, the cheap version is validation rather than logging: reject
`Target == AnnouncementTarget.Plan` with an empty `TargetValue` at the admin create/update
endpoint, so a plan-targeted announcement that can never match cannot be created. Do **not** add a
logger to `AnnouncementTargetingService` — its matchers are static and pure, and that is worth
keeping.

## Tests

The bead requires "a plan-targeted announcement against a user who should match". The chain is
issuance → claim → controller → query → matcher, and the matcher end is already covered. Close the
other two links:

**A. `api/tests/Wallow.Shared.Kernel.Tests/Extensions/ClaimsPrincipalExtensionsTests.cs`** (new
file; the folder currently holds only `ServiceCollectionExtensionsTests.cs`)

- `GetPlan` returns the value of a `plan` claim.
- `GetPlan` returns null when the claim is absent.
- The wire name is literally `"plan"` — assert against the string, not `PlanClaim.ClaimType`, so
  the test fails if someone renames the claim and "fixes" the constant to match.

**B. `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/PlanClaimTests.cs`** (new;
or extend `TokenAcquisitionTests`). This is the test that would have caught the bug. Use the
existing `AuthorizationCodeFlowHarness` — `ReadClaimValues(token, "plan")` reads the unencrypted
JWT payload directly, and `CreateUserAsync`/`CreateOrganizationAsync`/`RegisterClientAsync` set up
the tenant-bound client.

- Authorization-code access token carries `plan` = `free`.
- A refreshed access token still carries it.
- A client-credentials token for a tenant-bound client carries it.

**C. `api/tests/Modules/Announcements/Wallow.Announcements.Tests/Api/Controllers/AnnouncementsControllerTests.cs`**

The existing constructor builds a principal with only `ClaimTypes.NameIdentifier`. Add a test that
puts `new Claim("plan", "free")` on the principal and asserts the captured
`GetActiveAnnouncementsQuery.PlanName` is `"free"` — the existing
`GetAnnouncements_PassesCorrectUserIdToQuery` test shows the `_bus.Received` capture pattern. Add
the negative too: no claim → `PlanName` is null.

Run: `./scripts/run-tests.sh kernel`, `./scripts/run-tests.sh announcements`,
`./scripts/run-tests.sh integration`, then `./scripts/run-tests.sh`.

## Done when

`plan` is present on every access token that carries `org_id`; a plan-targeted announcement
reaches a matching user; tests A/B/C are green; `dotnet format api/Wallow.slnx` is clean.

Then: `bd note Wallow-0e8j` recording that option 1 was taken and where the constant lives, and
`bd note Wallow-vsi3.6` pointing at `PlanClaim.Default` as the constant it replaces.

---

# Part 2 — Wallow-75pg: delete the phantom types

## Verified inventory

All 14 confirmed at **zero** hits across `api/src/**/*.cs`:

`InvoiceId`, `InvoiceCreatedEvent`, `InvoicePaidEvent`, `InvoiceOverdueEvent`,
`PaymentReceivedEvent`, `QuotaThresholdReachedEvent`, `UsageFlushedEvent`, `IInvoiceQueryService`,
`ISubscriptionQueryService`, `IRevenueReportService`, `IMeteringQueryService`,
`IUsageReportService`, `PasswordResetEvent`, `MessageSentEvent`.

Every other type named in either file **does** resolve — `EmailSentEvent`
(`Shared.Contracts/Delivery/Events/`), `NotificationCreatedEvent`, `IUserQueryService`,
`IRealtimeDispatcher`, `ISseDispatcher`, `IPresenceService`, `RealtimeEnvelope`, all the Identity /
Announcements / Inquiries events, and every Notifications entity, value object, provider and
service in that README.

## Four more drift sites found while verifying

The bead says "a README wrong once is usually wrong twice". It is:

1. **`api/src/Shared/README.md:94`** lists `Wallow.Shared.Infrastructure.Workflows` as a dependency
   of `Wallow.Shared.Infrastructure`. That project does not exist — `api/src/Shared/` holds Api,
   Contracts, Infrastructure, Infrastructure.BackgroundJobs, Infrastructure.Core,
   Infrastructure.Plugins, Kernel. **Delete the line.**
2. **`api/src/Modules/Notifications/README.md:60`** lists `BillingInvoice` as a `NotificationType`
   member. The enum has it commented out as removed (`// 4 was BillingInvoice (removed)`).
   **Delete it from the table.**
3. **`.../Notifications/README.md:61`** lists `Webhook` as a `ChannelType` member. The enum is
   `Email, Sms, InApp, Push`. **Delete it.**
4. **`.../Notifications/README.md:73`** — the Identity row is also *incomplete*, not just wrong. The
   module has handlers for four more real events: `InvitationCreatedEvent`,
   `MagicLinkRequestedEvent`, `OtpCodeRequestedEvent`, and `AccessRequestedEvent` (all defined in
   `Shared.Contracts/Identity/Events/`). Adding them is optional under the acceptance criteria —
   which only requires that everything named be real — but the row is being edited anyway.

## Edits

### `api/src/Shared/README.md`

| Line | Action |
|---|---|
| 26 | Replace the `InvoiceId` example. Reads `(e.g., \`InvoiceId\`, \`TenantId\`)` → `(e.g., \`UserId\`, \`TenantId\`)`. Both exist in `Wallow.Shared.Kernel.Identity`. |
| 53 | Delete the whole `**Billing**: …` line. |
| 59 | Delete the whole `**Metering**: …` line. |
| 65 | Delete the `IInvoiceQueryService, ISubscriptionQueryService, IRevenueReportService (Billing)` bullet. |
| 66 | Delete the `IMeteringQueryService, IUsageReportService (Metering)` bullet. |
| 94 | Delete `Wallow.Shared.Infrastructure.Workflows` from the dependency list. |

Line 55 (`**Delivery**: EmailSentEvent`) — **leave it.** The type is real. Whether the *Delivery*
namespace should keep that name is `Wallow-d9en`'s call, and 75pg's criterion is that named types
resolve.

After the deletions the integration-event catalog is Identity, Delivery, Notifications, and the
query-service list is `IUserQueryService (Identity)` alone — which is the true state.

### `api/src/Modules/Notifications/README.md`

| Line | Action |
|---|---|
| 60 | Remove `BillingInvoice` from the `NotificationType` values. |
| 61 | Remove `Webhook` from the `ChannelType` values. |
| 73 | `PasswordResetEvent` → `PasswordResetRequestedEvent`. Optionally append `InvitationCreatedEvent`, `MagicLinkRequestedEvent`, `OtpCodeRequestedEvent`, `AccessRequestedEvent`. |
| 74 | Delete the whole `Billing` row. |
| 77 | Delete the whole `Messaging` row. |

Line 73 is the one the bead singles out, and it is right to: `PasswordResetEvent` sits in a row
where every other entry is real, one character short of the actual
`PasswordResetRequestedEvent`. A reader spot-checking that row is the reader most likely to be
misled.

## Coordinating with Wallow-d9en

`Wallow-d9en` (phantom Billing/Communications/Delivery **modules**) edits the same two files on
adjacent lines. The beads are deliberately not merged — phantom types vs phantom modules — but
the bead text says to sweep them together. Two options, both fine:

- **Land 75pg first** (recommended — it is a pure delete and needs no judgement calls), then d9en
  rebases onto the shrunken files. Note that d9en's criterion "the Billing event and query-interface
  entries are gone from `api/src/Shared/README.md`" is *satisfied by* 75pg's lines 53/65 deletions;
  d9en still owns `Wallow.Api/README.md`, the ten `Consumers: Communications` doc comments,
  `ApiScopes.cs:31`, the Delivery entry, and the `Communications.Email` namespace decision.
- Or do both in one branch as two commits. Do not squash them.

## Verification

The acceptance criterion is mechanical — every type named must resolve. Run this after editing:

```bash
cd /Users/traveler/Repos/Wallow
for f in api/src/Shared/README.md api/src/Modules/Notifications/README.md; do
  grep -o '`[A-Z][A-Za-z0-9]*\(<[A-Za-z]*>\)\?`' "$f" \
    | tr -d '`' | sed 's/<.*//' | sort -u \
    | while read -r t; do
        n=$(grep -rl "\b$t\b" api/src --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
        [ "$n" = "0" ] && echo "PHANTOM $f: $t"
      done
done
```

Expect no output. It will also flag prose words that happen to be backticked and capitalised —
eyeball anything it reports rather than deleting on sight.

No code changes, so no test run is strictly required; `./scripts/run-tests.sh` is still worth one
pass before pushing. Commit as `docs:` — scope it `docs(shared)` or just `docs` — which does not
trigger a release.

## Done when

Both files are clean, the loop above is silent, and `bd close Wallow-75pg` records the four extra
drift sites so the audit trail shows the scope was wider than filed (again).

---

## Suggested commits

```
fix(identity): issue the plan claim alongside org_id
docs: delete phantom types from the Shared and Notifications READMEs
```

Both beads are children of `Wallow-qi90`. Neither blocks the other.
