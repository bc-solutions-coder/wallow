**status: active**

# MIT Prior-Art Harvest — feature flags, billing, back office, scaffolding

Research artifact. Source material for the four epics filed alongside it. Read the relevant
section before claiming any child bead; the bead descriptions do not restate the detail here.

**Question this answers:** is there an MIT-licensed codebase worth forking as Wallow's base, and
if not, what is worth lifting out of one?

**Answer:** no fork. Wallow's spine — Wolverine modular monolith, TanStack Start SSR, a Node-side
BFF shipped as an npm SDK, fork-first merge drivers — has no equivalent anywhere on GitHub.
The nearest neighbour, `platformplatform/PlatformPlatform` (MIT), is a parallel-evolution twin
with product surface Wallow lacks and an architecture Wallow should not adopt. Harvest from it;
do not fork it.

---

## 1. License survey

| Repo | License | Verdict |
| --- | --- | --- |
| platformplatform/PlatformPlatform | MIT | Harvest source, keep attribution |
| fullstackhero/dotnet-starter-kit | MIT | Harvest agent-skill design |
| ardalis/modulith | MIT | Read for templating mechanism |
| kgrzybek/modular-monolith-with-ddd, evolutionary-architecture, AlphaYu/adnc, Practical.CleanArchitecture, oqtane, JasperFx/wolverine | MIT | Design reference |
| Finbuckle.MultiTenant | Apache-2.0 | Fine as a dependency |
| OrchardCore | BSD-3 | Fine |
| abpframework/abp | **LGPL-3.0** | Do not copy source into Wallow |
| DuendeSoftware/products | **NOASSERTION** (commercial, revenue-gated) | Not a fork base |
| saas-factory-labs/SaaS-Factory | **NOASSERTION** | Legally unusable |

Wallow is Apache-2.0. MIT source can be incorporated with the original copyright notice retained
on any file copied substantially. LGPL cannot.

---

## 2. What Wallow does not have today

Verified against the tree, not assumed:

- **No Plan / Subscription / Tier / Invoice** entity, table, module, or migration. No Stripe
  anywhere in `api/`, `apps/`, or `packages/`.
- **A `plan` claim is read but never written.** `ClaimsPrincipalExtensions.GetPlan()`
  (`api/src/Shared/Wallow.Shared.Kernel/Extensions/ClaimsPrincipalExtensions.cs:84-87`) reads a
  `"plan"` claim that nothing in `api/src` emits. `AnnouncementTargetingService.cs:86` targets
  `AnnouncementTarget.Plan` against it — so plan-targeted announcements match nobody, silently.
- **No feature flags.** `Microsoft.FeatureManagement` is used only for static
  `FeatureManagement.Modules.*` booleans in appsettings, evaluated at startup. Not per-tenant,
  not runtime-toggleable.
- **No admin UI.** Admin exists only as API: `AdminAnnouncementsController`,
  `AdminChangelogController`, `ClientsController` (`[HasPermission(PermissionType.AdminAccess)]`).
  Cross-tenant governance is the `is_global_admin` / `is_operator` claim pair, not the `admin`
  role (which is tenant-side).
- **No scaffolding.** Adding a module is 7 host touch points, by hand, every time.

**Stale docs to fix, not to plan against:** `api/src/Shared/README.md:53,65` and
`api/src/Modules/Notifications/README.md:74` describe `IInvoiceQueryService`,
`ISubscriptionQueryService`, `IRevenueReportService`, `InvoicePaidEvent` — none of which exist.

**Sequencing constraint:** `docs/plans/2026-07-31/1954-multi-org-membership-implementation.md`
is **active** and reshapes tenancy — `Membership` becomes a first-class aggregate, roles move to
`(user, organization)`, `WallowUser.TenantId` is dropped. Feature flags and billing both key off
tenant identity. Do not start either module's schema until that plan's Phase 2 gate passes.

---

## 3. Module anatomy — what a new module costs today

Smallest complete module is **ApiKeys**: 4 projects, 20 tracked source files.

Per-module file categories (paths from `api/src/Modules/ApiKeys/`): strongly-typed ID, entity,
repo interface, `DbContext` (`HasDefaultSchema`, extends `TenantAwareDbContext<T>`,
`ApplyTenantQueryFilters`), design-time factory, EF configuration, repository impl, infrastructure
DI extension, module-facade DI extension (`AddXModule` + `InitializeXModuleAsync`), migrations
(+ `.Designer.cs` + snapshot), controller, request/response contracts, `CLAUDE.md`, `README.md`.
Announcements adds the CQRS shape ApiKeys skips: `Commands/<Name>/{Command,Validator}.cs`,
`Queries/`, `DTOs/`, `Mappings/`, `Extensions/ApplicationExtensions.cs`, `Enums/`.

### The registration footgun — 7 host touch points

1. `api/Wallow.slnx` — new folder + 4 projects
2. `api/src/Wallow.Api/Wallow.Api.csproj:106-121` — `ProjectReference` to `.Api` **and**
   `.Infrastructure` (controller discovery is by referenced-assembly scan; there is no
   `AddApplicationPart` in `api/src`)
3. `api/src/Wallow.Api/WallowModules.cs` — three edits: `AddXModule`,
   `InitializeXModuleAsync`, `MigrateIfRegistered<XDbContext>`
4. `appsettings.json:80-88` `FeatureManagement.Modules.X` (+ `.Development.json:73`,
   `.Production.json:48`)
5. `api/src/Wallow.MigrationService/Program.cs` — `AddDbContext` + `DbContextMigrationRunner`
6. `api/tests/Modules/X/Wallow.X.Tests/` + slnx entry + shorthand in `scripts/run-tests.sh:22-36`
7. Hardcoded module lists in `api/tests/Wallow.Architecture.Tests/MigrationServiceTests.cs:189`
   and `MigrationRemovalTests.cs:16`

Wolverine is used two ways: request/response (`IMessageBus.InvokeAsync<Result<T>>` from a
controller) and integration events (declared in `Shared.Contracts/<Module>/Events/`, consumed by
static handlers in another module's `Application/EventHandlers/` — auto-discovered, no DI entry).

Architecture tests that already judge a new module with zero edits: `ModuleIsolationTests`,
`ModuleRegistrationTests`, `ModuleToggleTests`, `CleanArchitectureTests`, `CqrsConventionTests`,
`WolverineConventionTests`, `DenyByDefaultAuthorizationTests`, `MultiTenancyArchitectureTests`,
`ApiConventionTests`, `ApiVersioningTests`.

---

## 4. Feature flags — PlatformPlatform's design

Source: `application/shared-kernel/SharedKernel/FeatureFlags/` (~590 LOC) and
`application/account/Core/Features/FeatureFlags/`. PP documents it in
`.claude/rules/backend/feature-flags.md` and `.claude/rules/frontend/feature-flags.md`, which are
candid about its weak points and worth reading directly.

**Data model.** One `feature_flags` table; three row kinds discriminated by nullability.
`flag_key`, `tenant_id` (null), `user_id` (null), `enabled_at`/`disabled_at`
(`IsActive = enabled_at != null && (disabled_at == null || enabled_at > disabled_at)`),
`bucket_start`/`bucket_end`, `source` (`Manual`|`Plan`), `scope` (`System`|`Tenant`|`User`),
`orphaned_at`, `deleted_at`. Unique index `(flag_key, tenant_id, user_id) NULLS NOT DISTINCT`;
CHECKs `user_id IS NULL OR tenant_id IS NOT NULL` and both-or-neither bucket in 0..99.
Two columns bolted onto existing tables: `rollout_bucket` and `ab_inclusion_pin` on both
`tenants` and `users`, fed by two Postgres sequences.

System flags never get a DB row (config key + frontend env var). Plan is not a level — plan
gating materialises as a tenant row with `Source=Plan`.

**Precedence** (`FeatureFlagEvaluator.cs:41-101`): base row exists → base row `IsActive` →
parent dependency enabled (one level, two-pass sort) → per scope: manual/plan override wins
outright → else if not A/B-eligible, false → `AlwaysOn` pin → `NeverOn` pin → bucket range → false.

**Bucketing is not a hash of the entity ID.** Each tenant/user takes a Postgres sequence number
at creation; `RolloutBucketHasher.ComputeRolloutBucket` maps it through a **van der Corput**
low-discrepancy sequence (base 2) × 100 → bucket 0..99, persisted once. Per-flag offset is
**FNV-1a** over the flag key mod 100, with wrap-around inclusion. Consequence: ramping a
percentage never reshuffles existing members, and two flags at the same percentage cover
different populations. The migration reimplements van der Corput in plpgsql to backfill, then
drops it.

**Declaration → typed hook.** Flags are `public static readonly FeatureFlagDefinition` fields;
reflection builds a registry and `ValidateFlags()` runs in the static constructor. The *subtype*
encodes scope + admin level, so illegal combinations do not compile (`SystemFeatureFlag`,
`TenantAbTestFlag`, `PlanGatedTenantFlag`, `TenantOwnerConfigurableFlag`, `UserConfigurableFlag`,
`UserAbTestFlag`). Delivery is MSBuild codegen, not OpenAPI: API binary
`--emit-feature-flags-manifest` → JSON → `.mjs` generator → `registry.generated.ts` carrying a
`FeatureFlagKey` string-literal union. Deleting a flag in C# turns every stale callsite into a TS
compile error. **That union is the point of the whole pipeline.**

**Propagation.** Evaluated at JWT issue/refresh into a `feature_flags` claim; the gateway parses
it and sets `x-user-feature-flags` on every authenticated response. No per-request DB read.
Propagation floor is the 5-minute access-token TTL. Transient refresh failure suppresses the
header rather than emitting a stale set.

**Telemetry.** No per-evaluation event. Enabled flags with `TrackInTelemetry` become tags on
every telemetry event (`tenant.feature_flags.<key>` = `"enabled"`, tag absent when off —
deliberately avoiding OTel's reserved `feature_flag.*` namespace). Discrete events only for
mutations, each carrying an `Internal`/`Owner`/`Self` trigger axis.

### Porting verdict

**Copy as code:** `RolloutBucketHasher.cs` (85 lines, pure, the highest-value file in the
system), the enums, `FeatureFlagDefinition.cs` + `FeatureFlagsRegistry.cs` (only dependency is
`IConfiguration`), `FeatureFlagTelemetryProperties.cs`, the migration's plpgsql backfill and CHECK
constraints, the `FeatureFlag` aggregate state machine, `FeatureFlagEvaluator.cs` precedence.

**Copy as design:** the manifest→codegen pipeline (Wallow's shape is an OpenAPI endpoint plus a
small generator emitting the union — different plumbing, same contract); the startup reconciler
(converge DB to code at boot, fail the process rather than start inconsistent, advisory-lock
across replicas, throw if a hard-deleted key is reused).

**Rewrite:** module ownership and the schema boundary — PP puts flags in the Account slice with a
real FK to `users` and columns on `tenants`/`users`; Wallow's per-schema modules forbid a
cross-schema FK, so either the flags module owns a projection fed by an Identity integration
event, or Identity owns the bucket and publishes it. **This is the single largest design decision
and it has no PP precedent.** Also: browser delivery (Wallow's BFF holds the token; flags ride
`packages/auth`'s `currentUserQuery`), the `Symbol.for` global listener registry (exists only to
cross Module-Federation boundaries — Wallow has no federation, delete it), plan gating (no
subscriptions module), all 15 command/query slices, the back-office UI.

**Sharpest caveat:** PP's own rules doc admits its four back-office query mirrors re-implement
the evaluation math and diverge from the runtime evaluator — the mirrors skip parent-dependency
and return `IsEnabled=true` without checking `baseRow.IsActive`, so the back office can show a
flag as enabled that runtime excludes. **Project admin views from the one evaluator.**

**Size: LARGE as designed; MEDIUM if cut to System + Tenant + User with no plan tier, no A/B, no
pins.** The A/B machinery — buckets, sequences, pins, threshold columns, four audience views — is
more than half the total surface.

---

## 5. Billing — PlatformPlatform's design

Source: `application/account/Core/Features/{Subscriptions,Billing}/` and
`Core/Integrations/Stripe/`. ~9.7k lines of production C# across 52 files, comparable again in
tests. Stripe.net 50.3.0.

**Domain.** `Subscription` is one-per-tenant, `ITenantScopedEntity`, with a real FK to `Tenant`.
`payment_transactions`, `billing_info`, `payment_method`, `drift_discrepancies` are **jsonb blobs
on the subscription row**. Two real tables: `stripe_events` (INSERT-only raw payload archive with
`payload_hash`, `api_version`, Pending→Processed/Ignored/Failed) and `billing_events` (append-only
ledger, unique index on `stripe_event_id`, denormalized `committed_mrr`, no tenant FK because the
back office is cross-tenant by design). Prices are read live from Stripe (lookup keys, 1-minute
memory cache); invoices are flattened into the jsonb array. All internal recurring-revenue
amounts are ex-VAT by convention.

**Webhooks, two-phase.** `AcknowledgeStripeWebhookCommand` verifies the signature, SHA-256 hashes
the raw payload, inserts a `stripe_events` row. `ProcessPendingStripeEvents` takes a `FOR UPDATE`
row lock on the subscription to serialise concurrent webhooks per customer, syncs, emits ledger
rows. Idempotency is layered: duplicate id → no-op; same id, different hash → drift + telemetry,
existing row never overwritten; ledger insert idempotent on the unique index. 14 event types.

**The load-bearing idea:** the hot path never trusts the stored payload. It re-pulls Stripe's
`events.list` anchored on `last_synced_stripe_event_created_at`; the local archive is cold backup
for admin replay only (Stripe retains events 30 days). Failed enumeration does not advance the
anchor, so the next sync retries. An architecture test enforces that the hot path never reads the
payload column.

**Plan changes.** Upgrades prorate through Stripe, returning a `client_secret` when SCA is needed.
Downgrades use **Stripe subscription schedules**, not local scheduling. Commands never mutate
local state — the webhook does. Dunning is entirely delegated to Stripe Smart Retries; failed
payments explicitly do **not** revoke entitlements during the ~3-week retry window.

**Drift detection.** A pure function comparing plan / cancel-at-period-end / price / currency plus
coarse ledger checks; 10 discrepancy kinds. Runs inline at the end of every sync, plus a
detect-only startup pass.

**Frontend.** Stripe Checkout in `UiMode = "custom"` with `<PaymentElement>` inside the app's own
dialog; ~4.2k lines across 37 files. The UI **polls** `/subscriptions/current` because state only
lands via webhook.

### Porting verdict

**Copy as code:** `BillingDriftDetector.cs`, `MrrCalculator.cs`, `DashboardMrrCalculator`,
`StripeEventPayloadHasher.cs`, the `BillingEvent` / `StripeEvent` aggregate shapes + EF
configurations + the migration's table/index design.

**Copy as design:** the two-phase ack→process split; layered idempotency; rebuild from
`events.list` never from your own archive; append-only ledger; ex-VAT everywhere; commands call
Stripe and let the webhook move local state; delegate dunning entirely.

**Rewrite:** `StripeClient.cs` (1.9k lines — individual calls copyable, structure not), all
endpoints, all repositories, the drift worker (theirs is an artifact of Azure Container Apps
scaling to zero; Wallow wants a real scheduled job), the entire frontend.

**Wallow fit issues:** the `Subscription`→`Tenant` FK must become a Wolverine integration event
(`TenantCreated` → create Stripe customer, tenant id as a plain value). `GetDashboardKpis` joins
tenants, users, sessions and subscriptions in one handler — under Wallow's rules that needs a
read-side projection fed by integration events, or a dashboard split per module.
`SubscriptionPlanChanged` is the natural event for the flags module's plan tier.

**Do not copy:**
- **`payment_transactions` as a jsonb array.** Cannot index, paginate, join, or aggregate; their
  invoice list has to load subscriptions to render invoices, and their `AmountExcludingTaxClamped`
  drift kind exists because a CHECK on jsonb would otherwise 500 the webhook and trigger infinite
  Stripe retries. **Make invoices a real table.**
- That CHECK constraint, written as a raw `jsonb_path_exists` string in a migration.
- The 30-method `IStripeClient` god-interface — split it (catalog / customer / lifecycle / events).
- A `BillingEventType` enum hand-mirrored into a frontend constant (their own XML doc flags it).
- Denormalized `committed_mrr` on an append-only table — they already needed a
  `BillingEventDenormalizationStale` drift kind because a late-recovered event makes persisted
  rows wrong and the append-only invariant forbids fixing them.

**Size: LARGE** (~10k backend + 4k UI). **Cheapest useful slice:** the `billing_events` ledger +
`BillingDriftDetector` + `MrrCalculator` as a new module, invoices as a real table, a gateway
interface narrower than theirs. A few hundred lines, and the best-reasoned part of their design.

---

## 6. Back office — PlatformPlatform's design

A **second SPA served by the same ASP.NET Core process**, separated by hostname:
`.RequireHost(backOfficeHost)` + a distinct OpenAPI document, with the same image running as two
Container Apps. Auth reads Azure Easy Auth `X-MS-CLIENT-PRINCIPAL*` headers and base64-decodes
the principal — **no token validation in-process; it trusts the sidecar entirely.**

Dashboard KPIs are neither raw SQL nor a read model: repositories load whole unfiltered tables and
aggregate in memory with LINQ, and MRR is reconstructed by replaying the entire `billing_events`
log per request.

### Porting verdict

**Design only; rebuild all of it.** The auth model is actively wrong for Wallow (OIDC against
Wallow's own API) — do not port `BackOfficeIdentityHandler`. Wallow already has the right
primitives: the `is_global_admin` / `is_operator` claim pair plus
`PermissionType.AdminAccess`. Hostname-gated route groups, dual Kestrel listeners, the YARP
dev-static proxy and the dual-container trick are all Azure-shaped; a third TanStack Start app
behind the same-origin BFF is cheaper.

**Worth taking as shape:** tenant list/detail with a billing tab and health tiles, user detail
with sessions and flag overrides, an invoices table, a billing-events audit table with filters,
drift banners, KPI dashboard with period-over-period deltas.

**Do not copy:** in-memory KPI aggregation. Fine at 100 tenants, falls over well before 10k, and
every card redoes the work. SQL aggregates or an event-fed read model from day one. Also skip
their 8k-line `/components` showcase route — Wallow has Storybook in `packages/ui`.

A third app duplicates ~20 files from `apps/wallow-auth` (`package.json`, `vite.config.ts`,
`vitest.config.ts`, `vitest.setup.ts`, `vitest-styles.css`, `tsconfig.json`, `.oxlintrc.json`,
`.gitignore`, `.dockerignore`, `Dockerfile`, `playwright.config.ts`, `e2e/routes.spec.ts`,
`e2e/global-setup.ts`, `src/app/{start.ts,router.tsx,styles.css}`, `src/app/routes/__root.tsx`,
`src/app/routes/health.ts`, the BFF catch-alls, `ready-indicator.tsx`) plus, outside the app:
`api/src/Wallow.AppHost/Program.cs:5-6,70-110`, `docker/docker-compose.test.yml:195-260` + a
matching CI image tag, a free port, an OIDC client in `api/seed.json` +
`Identity__FirstPartyClients__N`, `scripts/e2e.sh`, and a sibling to
`api/tests/Wallow.Architecture.Tests/AppHost{Auth,Web}ResourceTests.cs`.

---

## 7. Scaffolding — fullstackhero vs. modulith

**fullstackhero's skills are prose, not generators.** Each is exactly one `SKILL.md`
(`add-module` 6.1 KB, `add-full-slice` 3.2 KB) — frontmatter, project tree, numbered steps with
inline C# using `{Name}` placeholders, `dotnet sln add` commands, a `⚠️ Register in ALL FOUR
places` section, a verify block, a `- [ ]` checklist. It explicitly says *copy an existing
module's two `.csproj` files and rename*. The repo is the template; the skill is the diff.
`add-full-slice` is a composer that delegates to `add-feature` and `add-react-page` and owns only
the thing neither does: a backend↔frontend **contract table** (route ↔ fetch path, DTO casing,
permission constant ↔ route guard). That seam is its entire value.

**Their rules/skills split:** rules are per-area invariants (read before working in an area,
1–7.5 KB, granular by concern, plus `rules/modules/<context>.md` per bounded context); skills are
per-task procedures. Skills cite rules by path; rules never cite skills. `workflows/` is a third
tier that sequences skills and adds a decision gate, explicitly *not restating the recipe*.
`CLAUDE.md` is **146 bytes** — three lines pointing at `AGENTS.md`, which is a 9.3 KB index with a
"Working on… / Read" table. Zero duplication by construction.

**ardalis/modulith** is a pure `dotnet new` template pack, no CLI project. `template.json` carries
`sourceName` substitution, choice symbols, `fileRename`/`replaces`/`forms` regex, and
`postActions` that add projects to the solution and wire three project references — all
`continueOnError: true` with manual fallbacks because SDK support is preview-gated. **DI needs no
wiring at all:** the Web host reflects over solution assemblies and invokes each module's
`IRegisterModuleServices`.

The comparison that matters: fullstackhero's `add-module` skill exists *because* its host has a
four-place registration footgun. **modulith deleted the footgun instead of documenting it.**

**Fork sync.** Neither has any. PlatformPlatform is the only one with real machinery, and it is
not merge drivers: `pull-platformplatform-changes` (10.9 KB) sets upstream, lists unported PRs,
writes `commits.md`/`learnings.md`/`port-plan.md`, and cherry-picks one PR at a time with
`-X theirs`, running the full gate after each and ticking a checklist — *"the goal is a downstream
that reflects intent, not literal preservation of every commit."* `rebrand` (15.6 KB) enforces
fork-config isolation **by convention, not merge driver**: all brand values in one config file,
logos at eight canonical paths whose filenames never change, *"source code is not touched."*

### Recommendation

**Agent skills, not `dotnet new`. Both is the wrong answer.** A template pack means a second copy
of the module shape in `working/content/` that drifts silently — modulith pays exactly this cost.
Token substitution cannot produce a correct Wolverine subscriber or a migration. Wallow's fork
model is *fork the repo*, so the repo is already the template.

1. **Delete the footgun before documenting it.** 7 touch points → assembly-attribute discovery,
   the way modulith does it. Deployment status says breaking `main` is free. A skill that
   documents avoidable ceremony is a permanent tax.
2. **Then four skills**, each ≤6 KB, copy-an-existing-module framing, naming ApiKeys as the
   reference: `add-module`, `add-feature`, `add-integration-event`, `create-migration`. Add
   `add-frontend-slice` after `add-feature` proves out — the OpenAPI-regen seam is a contract
   table exactly like fullstackhero's and is the highest-value thing a skill can carry.
3. **Restructure `CLAUDE.md` as an index.** It currently carries index *and* detail. Move detail
   into `.claude/rules/`; keep the map, golden rules, and a "Working on… / Read" table. Split
   `TESTING.md` — it currently does browser-mode gotchas, vitest projects, lint passes and comment
   policy in one file. Add `rules/modules/<name>.md` for per-module quirks.
4. **Fork sync: skip merge drivers, extend what exists.** Audit that no fork-varying value lives
   outside `branding.json` / `appsettings*` / `seed.json`. Write `pull-upstream-wallow` only once
   a real fork exists to test it against — building it speculatively yields a 10 KB skill nobody
   has run.

Wallow's architecture tests are already what makes scaffolding trustworthy; extend them rather
than trusting a skill's checklist.

---

## 8. Bead map

Filed 2026-07-31 — 4 epics, 26 children, 2 standalone bugs.

| Bead | Epic | Covers |
| --- | --- | --- |
| `Wallow-vmns` | Scaffolding & fork ergonomics | §3, §7 — footgun removal, skills, docs restructure, fork-config audit |
| `Wallow-qw03` | Feature flags module | §4 — MVP cut: System + Tenant + User, no plan tier, no A/B |
| `Wallow-vsi3` | Billing module | §5 — ledger-first slice, then Stripe gateway, webhooks, invoices table |
| `Wallow-9cx8` | Admin app & back office | §6 — auth decision, third app, KPI read model |
| `Wallow-0e8j` | (bug) | §2 — `GetPlan()` reads a claim nothing writes |
| `Wallow-75pg` | (bug) | §2 — two READMEs document billing services that do not exist |

Scaffolding is the only epic with no dependency on the multi-org membership plan. Start there —
`Wallow-vmns.1` (delete the 7-touch-point footgun) is the single highest-leverage bead in the set,
because every later module in every other epic pays its cost.

Deferred on filing: `Wallow-vmns.6` (upstream-pull skill, needs a real fork to test against) and
`Wallow-qw03.8` (A/B experiments). `Wallow-qw03.2` (flag primitives into `Shared.Kernel`) is the
one flags bead that clears the multi-org gate today — it is pure functions, no schema.
