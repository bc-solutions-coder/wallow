**status: completed**

# Documentation accuracy audit — 2026-08-04

## Remediation record

The audit below was read-only: no audited file was edited while it was produced. Remediation
followed in the same session, tracked by bead **Wallow-gg42**.

- The findings were grouped into **8 fix batches** and applied on 2026-08-04 across roughly
  **92 files**, all in a single working tree.
- Each batch was re-checked afterwards by a per-batch verification pass; the residual issues that
  pass surfaced were fixed before the work was considered done.
- The **§6 non-documentation issues** — defects the audit found in code, config, or tooling rather
  than in prose — were not fixed here. They were filed as beads instead; run `bd search` to find
  them.

Everything after this section is the audit report exactly as it was produced.

---

# Repo Documentation Accuracy Audit — 2026-08-04

**Scope:** every `.md` in the repo, split into six areas (A root governance · B `api/` · C frontend
apps + packages · D docfx getting-started/development · E docfx architecture/api · F docfx
integrations/operations), each read by three finder lenses and then adjudicated by one adversarial
verifier. **This report is built from the six `verification/` files only** — verdicts, corrected
citations and settled counts are the verifier's, not the finders'. Where a finder and a verifier
disagree on a number, the verifier's number is used and the finder's is called out as a trap.

**Audit only — no audited file was edited.**

---

## 1. Executive summary

1. 196 lens-level findings resolve to **176 distinct confirmed defects**: 9 critical, 38 high, 98
   medium, 31 low. 6 findings were refuted outright and 14 need rescoping before anyone acts.
2. The single largest defect is a **route-prefix myth**: 20+ documents teach `/api/…` as the API
   base path. `PathBase` is `""` and `ApiVersionRewriteMiddleware` prepends `/v1`, so every
   documented URL 404s. All 125 paths in `openapi/v1.json` start with `/v1`.
3. The second is **startup auto-migration**, taught in six documents. All seven
   `Initialize{Module}ModuleAsync` are `return Task.FromResult(app);`; `Wallow.MigrationService` is
   the only migrator. Two of those documents ship destructive `docker compose down -v` recovery
   procedures that leave an empty database.
4. Three documents ship **copy-pasteable code that is wrong in a security-relevant way**: a
   hand-rolled tenant `DbContext` that bypasses `TenantAwareDbContext<T>`, a `COOKIE_PASSWORDS`
   rotation example that causes the mass session invalidation rotation exists to prevent, and a
   Wolverine audit-handler recipe missing `[WolverineHandler]` that silently records nothing.
5. `module-creation.md`'s ten-step walkthrough **does not compile** — three csproj snippets omit
   WolverineFx, StackExchange.Redis and `Wallow.Shared.Api`.
6. `audit-events.md` has no operational value as written: every SQL example queries lowercase
   columns against a PascalCase (quoted-identifier) table.
7. Counts drift everywhere they are duplicated: components 47/56 vs **60**, oxlint configs five vs
   **six**, test shorthands 12 vs **14**, CI jobs 7 vs **9**, permissions "~35" vs **39**.
8. Recurring root cause: the same fact is maintained in two to six places with no generator and no
   test. Every high-severity count defect is a copy that drifted from a copy.
9. `docs/toc.yml` is **complete in both directions (37/37)** and must not be "fixed"; several
   finders claimed otherwise.
10. Work is partitioned into **8 fix batches with disjoint file ownership**, plus 9 non-doc issues
    to file as beads.

---

## 2. Scorecard

Effort: **S** = one line or one number · **M** = one section · **L** = whole page or multi-file sweep.
Verdict is the verifier's, not the finder's.

### Critical

| ID | Finding | Sev | Effort | Verdict |
|---|---|---|---|---|
| W-C1 | `/api/…` taught as the API base path across 20+ docs; real surface is `/v1/…` | Critical | L | CONFIRMED |
| W-C2 | Startup auto-migration taught in 6 docs; no module migrates outside `Testing` | Critical | L | CONFIRMED |
| W-C3 | Every SQL example in `audit-events.md` fails against the real table | Critical | M | CONFIRMED |
| W-C4 | `COOKIE_PASSWORDS` rotation example invalidates every existing session | Critical | S | CONFIRMED |
| W-C5 | Two guides walk through a hand-rolled tenant `DbContext`, bypassing the base class | Critical | L | CONFIRMED |
| W-C6 | `module-creation.md`'s walkthrough does not compile (3 csproj snippets short) | Critical | M | CONFIRMED |
| W-C7 | `wallow-web/README.md` inverts the SDK's typed-operation error contract | Critical | M | CONFIRMED |
| W-C8 | `run-tests.sh identity` documented as running the Identity integration tests | Critical | S | CONFIRMED |
| W-C9 | `COOKIE_PASSWORDS` absent from both BFF env-var tables | Critical | M | CONFIRMED |

### High

| ID | Finding | Sev | Effort | Verdict |
|---|---|---|---|---|
| W-H1 | `Wallow.Identity.Api/README.md` endpoint table comprehensively stale | High | L | CONFIRMED |
| W-H2 | `ClientsController` guarded by `AdminAccess`, docs claim `ServiceAccounts*` | High | S | CONFIRMED |
| W-H3 | `Wallow.Shared.Infrastructure.Workflows` documented; does not exist | High | S | CONFIRMED |
| W-H4 | `Wallow.Identity.Tests/README.md` documents SCIM/ and Sso/ dirs that do not exist | High | M | CONFIRMED |
| W-H5 | Identity README documents ApiKeys endpoints with wrong route AND wrong permission | High | S | CONFIRMED |
| W-H6 | EF-migration snippets omit the `api/` path prefix in 8 blocks | High | M | CONFIRMED |
| W-H7 | "Adding a New Permission" recipe wrong on all three steps | High | M | CONFIRMED |
| W-H8 | Port 5000 in 6 places; the API listens on 5001 | High | S | CONFIRMED |
| W-H9 | `CONTRIBUTING.md` module list names two phantom modules, omits two real ones | High | S | CONFIRMED |
| W-H10 | `.beads/README.md` is stock boilerplate teaching a `bd sync` command that does not exist | High | L | CONFIRMED |
| W-H11 | `enterprise-architect.md` points at a `.claude/docs/` directory that does not exist | High | S | CONFIRMED |
| W-H12 | `csharp-developer.md` documents `run-tests.sh auth`, which is not a valid shorthand | High | S | CONFIRMED |
| W-H13 | `code-reviewer.md` contradicts itself on FluentAssertions vs AwesomeAssertions | High | S | CONFIRMED |
| W-H14 | Prerequisites in 3 entry-point docs omit Node/pnpm, then run pnpm commands | High | M | CONFIRMED |
| W-H15 | oxlint config census wrong in two docs ("five nested"; truth is 8 files, 6 enabling) | High | S | CONFIRMED |
| W-H16 | `code-reviewer.md` promises unscoped review, contains zero frontend content | High | M | CONFIRMED |
| W-H17 | `wallow-web/README.md` describes a `setSsrRequestContextResolver` bridge that is gone | High | S | CONFIRMED |
| W-H18 | `wallow-web/README.md` calls `getV1IdentityUsersMe()`, which does not exist | High | S | CONFIRMED |
| W-H19 | `wallow-web/README.md` says csrf wires an interceptor onto a shared client | High | S | CONFIRMED |
| W-H20 | `packages/env/CLAUDE.md:95` describes a deleted charter spec in the present tense | High | S | CONFIRMED |
| W-H21 | `packages/logger/CLAUDE.md` asserts the same deleted charter spec twice | High | M | CONFIRMED |
| W-H22 | `packages/testing/CLAUDE.md` states its consumer set and browser-project census wrong | High | M | CONFIRMED |
| W-H23 | Two guides teach that a module's Api layer may reference its own Infrastructure | High | M | CONFIRMED |
| W-H24 | Dapper documented as in use at 6 sites; zero usages in `api/src` | High | L | CONFIRMED |
| W-H25 | Component count given as 47 and 56; the catalog has 60 | High | S | CONFIRMED |
| W-H26 | `component-library.md` cites a deleted `DashboardNav.tsx` twice | High | S | CONFIRMED |
| W-H27 | `module-creation.md:51` `dotnet sln add` has no solution path and fails | High | S | CONFIRMED |
| W-H28 | `authentication.md` says no ticket is issued for passwordless; two call sites issue one | High | M | CONFIRMED |
| W-H29 | MFA lockout table wrong from the 5th lockout on (overstates by 6×) | High | S | CONFIRMED |
| W-H30 | `background-jobs.md` wrong twice on the Hangfire dashboard guard | High | M | CONFIRMED |
| W-H31 | `audit-events.md` handler recipe produces a handler Wolverine never discovers | High | M | CONFIRMED |
| W-H32 | `observability.md` states P and ¬P about `AddSource`/`AddMeter` | High | M | CONFIRMED |
| W-H33 | Cookbook's "outside the workspace" install of `@bc-solutions-coder/styles` 404s | High | S | CONFIRMED |
| W-H34 | `typescript-sdk.md` gives the wrong `COOKIE_NAME` default and omits `COOKIE_HOST_PREFIX` | High | S | CONFIRMED |
| W-H35 | `typescript-sdk.md` documents `logout()` as a browser navigation; it is a POST | High | S | CONFIRMED |
| W-H36 | `CookieSessionStore` called production-viable in one doc, dev-only in the other | High | S | CONFIRMED |
| W-H37 | `reverse-proxy.md` says wallow-auth reads three env vars; it reads seven | High | S | CONFIRMED |
| W-H38 | `troubleshooting.md` prescribes a Valkey connection string missing the dev password | High | S | CONFIRMED |

### Medium and Low

98 medium and 31 low findings are listed in full in §3.3 and §3.4. All are CONFIRMED. Effort is
**S** for roughly three quarters of them (a number, a path, a permission name); the rest are **M**.

---

## 3. Confirmed findings

Severity-ordered. Deduped across lenses **and** across areas — a defect appearing in two areas
appears once here, with every location listed. Critical and high findings carry the full
What / Where / Evidence / Why it matters / Fix. Medium and low findings are condensed to
Where + Evidence + Fix; nothing is dropped, only the prose around it.

### 3.1 Critical

#### W-C1 — `/api/…` is taught as the API base path; the real surface is `/v1/…`

- **What:** More than twenty documents present `/api/...` as the canonical request path. There is
  no `/api` prefix at default settings, so every documented URL 404s.
- **Where:**
  - `api/src/Modules/Storage/README.md:85`
  - `api/src/Modules/Announcements/README.md:98-128`, `Announcements/CLAUDE.md:48`
  - `api/src/Modules/Notifications/README.md:81-114`
  - `api/src/Modules/Inquiries/README.md:100`
  - `api/src/Modules/Branding/README.md:49` (also missing its leading slash)
  - `api/src/Modules/ApiKeys/README.md:46` (uses an `api/v{version}` template), `ApiKeys/CLAUDE.md:20`
  - `api/src/Modules/Identity/…/README.md:124, 137, 149, 177, 185`
  - `docs/architecture/module-creation.md:360`
  - `docs/architecture/authorization.md:66, 180`
  - `docs/architecture/authentication.md:20, 24` (omits `/v1/` rather than adding `/api`)
  - `docs/integrations/external-auth.md` (the three external-auth endpoints, and `:154`)
- **Evidence:** `appsettings.json:2` sets `PathBase: ""` (consumed at `Program.cs:400-404`).
  `ApiVersionRewriteMiddleware.InvokeAsync:35` is `context.Request.Path = "/v1" + path` and never
  touches `/api`. All 125 paths in `packages/sdk/openapi/v1.json` begin with `/v1`; none begins with
  `/api`. Enumerating every `[Route(...)]` under `api/src/Modules` gives **31** attributes, none
  with an `api/` prefix — 27 are `v{version:apiVersion}/…` and the 4 exceptions are OpenIddict
  protocol endpoints. `authorization.md:66`'s `[Route("api/inquiries/submissions")]` is doubly
  wrong: `grep -rn submissions api/src --include='*.cs'` returns zero hits and the real controller
  is `InquiriesController.cs:28` `[Route("v{version:apiVersion}/inquiries")]`.
  `ExternalProviders.tsx:58` is `const EXTERNAL_LOGIN_PATH = "/v1/identity/auth/external-login"`.
- **Why it matters:** `/api/users` is rewritten to `/v1/api/users` and 404s. This is the first thing
  a fork tries and the most-copied snippet class in the repo.
- **Fix:** Replace `/api/…` with `/v1/…` everywhere above. `docs/architecture/file-storage.md:115-117`
  already states the rule correctly — copy its wording. **Two traps:** (a) B-BP-09's proposed
  `/api/v1/<module-path>` standard is WRONG and would contradict `openapi/v1.json`; (b) `/api` is
  not nowhere — it is the wallow-web BFF mount and the ingress path-based mount for the API, so
  `external-auth.md` needs one sentence distinguishing the two mounts rather than a blanket
  find-and-replace. `module-creation.md` never mentions API versioning at all; add it.
  `authorization.md:272` and `:289` are already correct (`/v1/identity/mfa/admin/…`) — leave them.

#### W-C2 — Startup auto-migration is taught in six documents and does not exist

- **What:** Six documents state or demonstrate that EF Core migrations run when the API starts. They
  do not. Two of them ship a destructive recovery procedure built on the false premise.
- **Where:** `docs/getting-started/developer-guide.md:703` and its Note at `:705`;
  `developer-guide.md:441-459` (a copy-pasteable `InitializeTicketsModuleAsync` containing
  `await db.Database.MigrateAsync();`); `docs/getting-started/fork-guide.md:710`;
  `docs/getting-started/onboarding.md:184-185`; `docs/architecture/module-creation.md:344-350` and
  `:422`; `docs/operations/troubleshooting.md:125` and `:344-350`; `api/CLAUDE.md:94`.
- **Evidence:** All seven `Initialize{Module}ModuleAsync` methods are literally
  `return Task.FromResult(app);` — Announcements `:68-71`, ApiKeys `:17-20`, Branding `:17-20`,
  Identity `:22-25`, Inquiries `:19-22`, Notifications `:175-178`, Storage `:19-22`.
  `api/src/Wallow.Api/WallowModules.cs:96-101` gates `RunTestMigrationsAsync` on
  `app.Environment.IsEnvironment("Testing")`, with a comment naming `MigrationService` as the
  production/Aspire path. `grep MigrateAsync` outside `Migrations/` finds call sites only in
  `Wallow.MigrationService/*` and inside `RunTestMigrationsAsync` (`WallowModules.cs:157-159,179`).
  The real migrator is `Wallow.MigrationService/Program.cs` (nine `AddDbContext` registrations,
  `CoreMigrationRunners` + `FeatureMigrationRunners`).
- **Why it matters:** `onboarding.md:184-185` and `troubleshooting.md:344-350` both tell the reader
  to run `docker compose down -v` and then `dotnet run --project api/src/Wallow.Api`. That drops the
  volumes and never re-migrates, leaving an empty database and no error. Worse,
  `onboarding.md:19-36` promotes bare `dotnet run` to **step 4 of the primary path**, demoting
  `pnpm backend` to an aside at `:38-43` — the promoted path is the broken one.
- **Fix:** Delete the auto-migration claims; point at `Wallow.MigrationService` and `pnpm backend`.
  `docs/development/database-migrations.md:286` already states the correct behaviour — copy it.
  Fix `api/CLAUDE.md:94` ("Modules auto-migrate only in Development/Testing") — the Development half
  is wrong. Remove the `MigrateAsync` call from `developer-guide.md:441-459`. **Genuine missing step
  to add:** a new module that is not added to `RunTestMigrationsAsync` gets no schema under test.

#### W-C3 — Every SQL example in `audit-events.md` fails against the real table

- **What:** All five query blocks and the `CREATE INDEX` statement reference lowercase column names
  that do not exist.
- **Where:** `docs/operations/audit-events.md:47-103`.
- **Evidence:** `20260801104059_InitialCreate.cs` creates `Id`, `EventType`, `UserId`, `ActorId`,
  `TenantId`, `IpAddress`, `UserAgent`, `OccurredAt` — PascalCase, therefore quoted identifiers in
  Postgres. `AuthAuditDbContext.OnModelCreating` configures only `HasDefaultSchema`, `ToTable`,
  `HasKey` and the `now()` default. Grepping `api/src` and `api/Directory.Packages.props` for
  `UseSnakeCase`, `SnakeCaseNamingConvention` and `EFCore.NamingConventions` returns **no matches**,
  so nothing rewrites the identifiers.
- **Why it matters:** The page is an operator runbook. Every command on it errors.
- **Fix:** Rewrite all six blocks with quoted PascalCase identifiers.

#### W-C4 — The `COOKIE_PASSWORDS` rotation example causes the outage rotation exists to prevent

- **What:** The documented key map uses arbitrary key ids (`k2`, `k1`). Any id other than `default`
  fails to unseal every cookie sealed by an earlier build.
- **Where:** `docs/operations/deployment.md:322`; `docker/.env.production.example:157`;
  `docker/docker-compose.production.yml:586`.
- **Evidence:** `packages/sdk/src/server/config.ts:33-44` states it outright: iron-webcrypto seals a
  bare-string password with an EMPTY id and normalizes that empty id back to the literal `"default"`
  when unsealing against a key map, so "a map keyed by anything else fails every cookie sealed by an
  earlier build with `Cannot find password: default` — i.e. the mass session invalidation this whole
  feature exists to avoid." `config.ts:372-373` confirms the single-password path wraps under
  `DEFAULT_COOKIE_KEY_ID`.
- **Why it matters:** Following the documented rotation procedure logs out every user — the exact
  failure the feature prevents.
- **Fix:** `docs/integrations/bff-pattern.md:486` and its step-1 procedure at `:490` are CORRECT —
  copy them. Fix all three locations or the divergence just moves; the two `docker/` files are
  shipped config, not docs, and are the ones a fork actually copies.

#### W-C5 — Two guides walk the reader through a hand-rolled tenant `DbContext`

- **What:** Copy-paste walkthroughs build a plain `DbContext` with an injected `ITenantContext` and
  a per-entity `.HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`, registered with
  `services.AddDbContext<T>`. A third guide teaches the same thing in prose.
- **Where:** `docs/getting-started/fork-guide.md:501-533`;
  `docs/getting-started/developer-guide.md:365-412`;
  `docs/architecture/module-creation.md:289-296` and its Pre-PR item at `:523`.
- **Evidence:** All six non-Identity module DbContexts extend `TenantAwareDbContext<T>` (Branding
  `:7`, Inquiries `:7`, ApiKeys `:7`, Announcements `:8`, Storage `:8`, Notifications `:12`);
  `IdentityDbContext` implements `ITenantAwareContext` over the ASP.NET Identity base.
  `HasQueryFilter` appears in exactly two files in `api/src`, both base classes
  (`TenantAwareDbContext.cs:47`, `IdentityDbContext.cs:184`) — no module hand-writes one. The real
  registration is `AddPooledDbContextFactory` + `AddTenantAwareScopedContext<T>()`
  (`AnnouncementsModuleExtensions.cs`), which `database-migrations.md:68-88` already documents.
- **Why it matters:** Tenant isolation is a security boundary, and these are the two pages a fork
  reads first. A hand-rolled filter that misses an entity is a cross-tenant data leak.
- **Fix:** Replace both walkthroughs with `TenantAwareDbContext<T>` +
  `AddTenantAwareScopedContext<T>()`. Mitigation worth preserving: `module-creation.md`'s paragraph
  already says "See `InquiriesDbContext` for a reference implementation", so that one is
  misdirection rather than omission. The dropped `NoTracking` default (`api/CLAUDE.md:92-94`) should
  be restored to `module-creation.md` at the same time.

#### W-C6 — `module-creation.md`'s ten-step walkthrough does not compile

- **What:** Three csproj snippets omit references that later steps require.
- **Where:** `docs/architecture/module-creation.md` — Application `:104-113`, Infrastructure
  `:118-132`, Api `:137-144`; failures surface at `:207-217`, `:252-266` and Step 6.
- **Evidence:** The Application snippet has FluentValidation twice but omits **WolverineFx**; the
  Infrastructure snippet omits **WolverineFx** and **StackExchange.Redis**; the Api snippet omits
  **`Wallow.Shared.Api`**. Two hard breaks: (1) Step 4's handler sample at `:207-217` injects
  `IMessageBus` and the domain-event bridge at `:252-266` takes `IMessageBus bus`, while
  `Wallow.Shared.Contracts.csproj` is a 5-line bare SDK project with no `PackageReference` and no
  `ItemGroup` at all — so Wolverine cannot arrive transitively; (2) `ToActionResult` is defined only
  at `Shared/Wallow.Shared.Api/Extensions/ResultExtensions.cs:13,23`, and all seven module Api
  projects reference `Wallow.Shared.Api`. `module-creation.md:493` lists `Shared.Api/` in its Shared
  Infrastructure table, so the guide names the project without ever wiring it.
- **Why it matters:** This is *the* guide for the repo's headline extension point. A new module
  author following it verbatim gets three build failures with no hint of the cause.
- **Fix:** Add the three missing references. **E-BP-20 rated this "Low" — that is wrong; Step 6
  does not compile.**

#### W-C7 — `wallow-web/README.md` inverts the SDK's typed-operation error contract

- **What:** The README says generated operations "resolve to `{ data, error, response }` and never
  throw on a non-2xx", and its sample branches on `error !== undefined`. The opposite is true.
- **Where:** `apps/wallow-web/README.md:172-183`.
- **Evidence:** `packages/sdk/src/generated/client.gen.ts:18` builds the client with
  `throwOnError: true`; `sdk.gen.ts:1123` is `<ThrowOnError extends boolean = true>` returning
  `RequestResult<…, ThrowOnError, 'data'>`, and every operation passes `responseStyle: 'data'`.
- **Why it matters:** Code written from this sample silently swallows every API error — the `error`
  branch is unreachable and the throw is unhandled.
- **Fix:** `packages/sdk/README.md:458-460` already states the correct contract ("reject with a
  `WallowError` rather than resolving an `{ data, error }` envelope") — copy from there.

#### W-C8 — `run-tests.sh identity` is documented as running the Identity integration tests

- **What:** The README tells the reader a command runs integration tests. It runs neither the
  integration project nor integration-category tests.
- **Where:** `api/src/Modules/Identity/Wallow.Identity.Api/README.md:45-46`.
- **Evidence:** `resolve_filter()` maps `identity` to `Wallow.Identity.Tests` only, and
  `scripts/run-tests.sh:49-51` appends `--filter "Category!=E2E&Category!=Integration"` for every
  filter except the literal `integration`.
- **Why it matters:** A developer believes integration coverage ran when it did not. Highest
  consequence-per-character finding in area B.
- **Fix:** Correct the command to `./scripts/run-tests.sh integration`, or state plainly that
  `identity` runs unit tests only.

#### W-C9 — `COOKIE_PASSWORDS` is absent from both BFF env-var tables

- **What:** Both maintained copies of the BFF environment table omit the keyed rotation variable, so
  a fork concludes `COOKIE_PASSWORD` (singular) is the only sealing secret and that rotating it logs
  everyone out. The two tables have diverged besides.
- **Where:** `apps/wallow-web/README.md:190-205`; `packages/sdk/README.md:58-71`.
- **Evidence:** `packages/sdk/src/server/config.ts` names `COOKIE_PASSWORDS` at lines 76, 129, 140,
  151, 167, 179, 184, 190, 202, 258, 308 and 310, including key-ID validation and a
  `CookiePasswordSet` parse. `docker/docker-compose.production.yml:588` maps
  `COOKIE_PASSWORDS: ${BFF_COOKIE_PASSWORDS:-}` with a worked rotation example in the comment at
  `:586`. Divergence: `REDIS_URL` and `PORT` appear in the app README table and are absent from the
  SDK README table.
- **Why it matters:** The variable that makes zero-downtime secret rotation possible is invisible in
  both places a reader would look.
- **Fix:** Document `COOKIE_PASSWORDS` (with the `default` key-id constraint from W-C4) in one table
  and have the other link to it rather than restate it.

### 3.2 High

#### W-H1 — `Wallow.Identity.Api/README.md` endpoint table is comprehensively stale
**Where:** `…/Wallow.Identity.Api/README.md:124, 137, 149, 177, 185`.
**Evidence:** Routes verified from every `[Route]` attribute — `UsersController.cs:19`
`v{version:apiVersion}/identity/users` vs the doc's `/api/users`; `OrganizationsController.cs:17`;
`RolesController.cs:16`; `ScopesController.cs:17`; `ApiKeysController.cs:22` (ApiKeys module)
`v{version:apiVersion}/identity/auth/keys`. **No `ServiceAccountsController` exists** — the six
actions are on `ClientsController.cs` at 328, 339, 368, 380, 398, 417. `OrganizationsController`
carries **24** `[Http*]` attributes; the README documents 6; the 18 undocumented ones are at lines
151-485.
**Why it matters:** The module's only endpoint reference is wrong on routes, wrong on class names,
and 75 % incomplete.
**Fix:** Regenerate the table from the controllers. **Trap:** the scout claimed the Clients row was
"the one row still correct" — it is not (doc `/api/v1/identity/clients`, route `/v1/identity/clients`).
The `/connect/*` table at `:115-122` **is** genuinely correct — leave it. `/me` at
`Identity/README.md:130` is also correct (`UsersController.cs:73` is `[HttpGet("me")]`) — do not move it.

#### W-H2 — Service-account actions are guarded by `AdminAccess`, not the purpose-built permissions
**Where:** `ClientsController.cs:24` (class-level `[HasPermission(PermissionType.AdminAccess)]`);
docs describe `ServiceAccountsRead/Write/Manage`.
**Evidence:** `PermissionType.cs:73-75` declares all three purpose-built constants; the controller
uses none of them.
**Why it matters:** The documented least-privilege model is not the enforced one — a reader granting
`ServiceAccountsRead` gets a 403.
**Fix:** Document the actual guard, and file a bead to decide whether the code should narrow instead.

#### W-H3 — `Wallow.Shared.Infrastructure.Workflows` is documented but does not exist
**Where:** `api/src/Shared/README.md:7` and `:27`.
**Evidence:** `find api -iname '*Workflow*'` (excluding bin/obj) returns nothing; the csproj declares
exactly five ProjectReferences — Kernel, Contracts, Core, BackgroundJobs, Plugins. Line 28 is
`…Plugins`, which IS real.
**Why it matters:** A reader plans work against a shared project that was never built.
**Fix:** Delete both lines. `api/CLAUDE.md:64` and `Shared/README.md:88-90` are already correct.

#### W-H4 — `Wallow.Identity.Tests/README.md` documents two test directories that do not exist
**Where:** `Wallow.Identity.Tests/README.md:20` (`### SCIM Tests (Scim/)`) and `:24` (`### SSO Tests (Sso/)`).
**Evidence:** `git ls-files` gives the real directories: Apps, Fakes, Invitations, Memberships, Mfa,
OAuth2, Organizations, Resilience, ServiceAccounts, Settings, **Users**.
**Why it matters:** The reader is told the product has SCIM and SSO test coverage it does not have.
**Fix:** Replace with the real list — and **include `Users/`**, which the reviewer's replacement list
omitted. `README:37`'s "WireMock" sentence is fine; the "WireMock to simulate IdP Admin API"
phrasing lives inside the SCIM/SSO sections being deleted, so do not carry it over.

#### W-H5 — Identity README documents ApiKeys endpoints with wrong route AND wrong permission
**Where:** `api/src/Modules/Identity/README.md:177-183`.
**Evidence:** The doc shows `/api/auth/keys` with permission "Authenticated" on all three rows.
`ApiKeysController.cs` carries `[HasPermission(PermissionType.ApiKeyManage)]` at lines 46, 168 and
201, and `ApiKeys/README.md:44` states it correctly.
**Why it matters:** Wrong on the security-relevant axis — a reader concludes any authenticated user
can mint API keys.
**Fix:** Copy `ApiKeys/README.md:44`.

#### W-H6 — EF-migration snippets omit the `api/` path prefix in eight blocks
**Where:** `Announcements/README.md:151-152`, `ApiKeys/README.md:88-89`, `Branding/README.md:87-88`,
`Identity/README.md:219-220`, `Inquiries/README.md:141-142`, `Notifications/README.md:144-145`,
`Storage/README.md:183-184`, and `api/src/Wallow.Api/README.md:87` (`dotnet run --project src/Wallow.Api`).
**Evidence:** `src/` exists only under `api/`. The canonical form is at `api/CLAUDE.md:32-35`.
**Why it matters:** Eight copy-paste commands that fail from the repo root.
**Fix:** Prefix with `api/`. **Use this file list — the reviewer's version misses Notifications.**
Nit: the Announcements Testing command is line 144, not 143.

#### W-H7 — "Adding a New Permission" is wrong on all three steps
**Where:** the recipe in `api/src/Modules/Identity/README.md`.
**Evidence:** `PermissionType` is a `public static class` of `public const string` (39 constants) at
`Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs:7` — not an enum, and not in
`Wallow.Identity.Domain`. `MapScopeToPermission` is on `ScopePermissionMapper`
(`Kernel/…/ScopePermissionMapper.cs:5`), not on `PermissionExpansionMiddleware`, which only calls it
at 106 and 176. `RolePermissionLookup.cs` is a four-line passthrough
(`return RolePermissionMapping.GetPermissions(roles).ToArray();`), with `RolePermissionMapping.cs`
in the Kernel.
**Why it matters:** Every step sends the reader to the wrong assembly and the wrong construct.
**Fix:** Rewrite against the Kernel types.

#### W-H8 — Port 5000 appears six times; the API listens on 5001
**Where:** `api/src/Wallow.Api/README.md:90, 91, 93, 94, 95` (`/scalar`, `/hangfire`,
`ws://…/hubs/realtime`, `/events`) and `api/src/Modules/Storage/README.md:140`
(`"Storage": {"Local": {"BaseUrl": "http://localhost:5000"}}`).
**Evidence:** `launchSettings.json`, `appsettings.json:65` and the OpenAPI `servers` block all say
5001; `appsettings.Testing.json:14` agrees. **The Storage occurrence was found by the verifier, not
by any finder.**
**Why it matters:** Six dead URLs, one of which is a config value a fork will copy.
**Fix:** Sweep until `grep -rn "localhost:5000" api/` is clean.

#### W-H9 — `CONTRIBUTING.md` names two phantom modules and omits two real ones
**Where:** `CONTRIBUTING.md:57`; example scope at `:88`.
**Evidence:** The doc lists Identity, Billing, Storage, Notifications, Messaging, Announcements,
Inquiries. `ls api/src/Modules` gives Announcements, ApiKeys, Branding, Identity, Inquiries,
Notifications, Storage. No Billing, no Messaging; ApiKeys and Branding missing. `:88`'s example is
`feat(billing): add invoice PDF export`.
**Why it matters:** The commit-scope guidance points at modules that do not exist, and the correct
list is already in five other places (root `CLAUDE.md:118`, `README.md:90-97`,
`code-reviewer.md:33`, `csharp-developer.md:13`, `enterprise-architect.md:37`).
**Fix:** Replace the list and the example scope.

#### W-H10 — `.beads/README.md` is stock boilerplate that teaches a command that does not exist
**Where:** `.beads/README.md` (81 lines) — notably `:30` and `:39`.
**Evidence:** `:30` teaches `bd sync`; `bd version` is 1.1.0 and `bd sync --help` reports an unknown
command. `:39` claims "Auto-syncs with your commits", which is false. It teaches
`bd update <id> --status done` where root `CLAUDE.md` teaches `bd close`, never mentions `bd ready`
or `bd dolt`, and `grep -ci dolt` returns 0 — while `.beads/` actually holds `dolt/`,
`dolt-server.lock`, `.beads-credential-key`, `backup/` and `config.yaml`.
**Why it matters:** Beads is the issue tracker; its README teaches a workflow that silently never
pushes. `docfx.json`'s `src: "docs"` never reaches `.beads/`, so this is repo hygiene rather than a
docs-site bug — but it is the file an agent reads first.
**Fix:** Rewrite against `bd ready` / `bd close` / `bd dolt push`, or delete it and link to
`docs/getting-started/developer-guide.md`.

#### W-H11 — `enterprise-architect.md` points at a directory that does not exist
**Where:** `.claude/agents/enterprise-architect.md:66`.
**Evidence:** It cites `.claude/docs/module-creation.md`; there is no `.claude/docs/`. The real path
is `docs/architecture/module-creation.md`.
**Why it matters:** The architecture agent's primary reference is unreachable.
**Fix:** Repoint. **Citation warning:** every `.claude/agents/*.md` line number in the Area A
REVIEWER findings file is fabricated — use the corrected map in §5.

#### W-H12 — `csharp-developer.md` documents a `run-tests.sh` shorthand that is not valid
**Where:** `.claude/agents/csharp-developer.md:148-149` (`./scripts/run-tests.sh auth`).
**Evidence:** `resolve_filter()` (`scripts/run-tests.sh:20-37`) has no `auth` case; the argument
falls through to the literal-path branch and fails. Real cases: identity, storage, notifications,
announcements, inquiries, branding, apikeys, api, arch|architecture, seeder, migrations, shared,
kernel, integration. `seeder` and `migrations` are real and undocumented everywhere.
**Why it matters:** A documented command that fails, plus two real shorthands nobody knows about.
**Fix:** Replace `auth` with `identity`; add `seeder` and `migrations`.

#### W-H13 — `code-reviewer.md` contradicts itself on the assertion library
**Where:** `.claude/agents/code-reviewer.md:45` and `:90`.
**Evidence:** `:45` says AwesomeAssertions (not FluentAssertions — licensing); `:90` says "Tests use
NSubstitute + FluentAssertions". `csharp-developer.md:109` and `README.md:141` both say
AwesomeAssertions.
**Why it matters:** A self-contradiction inside one 135-line file, on a choice that was made for
licensing reasons.
**Fix:** Correct `:90`.

#### W-H14 — Prerequisites omit Node and pnpm, then the next step runs pnpm
**Where:** `README.md:42-43` (then `:59` runs `pnpm backend`, `:66-67` run `pnpm --filter`);
`CONTRIBUTING.md:9-11` (its Local Setup at `:15-26` has no `pnpm install` at all);
`docs/getting-started/fork-guide.md:25-31` (then `:49` has the reader edit
`packages/styles/branding.json`, a pnpm-workspace package).
**Evidence:** All three list only .NET 10 SDK / Docker / Git. `.npmrc:1` is `engine-strict=true`, so
a wrong Node version is a hard install failure rather than a warning.
**Why it matters:** Three of the repo's entry-point documents make first-run failure the default.
**Fix:** Add Node 24 (`.nvmrc`) and pnpm 10.20.0 to all three, and add `pnpm install` to
`CONTRIBUTING.md`'s Local Setup.

#### W-H15 — The oxlint config census is wrong in two documents
**Where:** root `CLAUDE.md:55`; `.oxlintrc.json:5`. (Second half: `packages/lint/CLAUDE.md` never
documents the fork-smoke config.)
**Evidence:** Both say "five nested configs (both apps, `ui`, `forms`, `navigation`)". Truth: **8**
`.oxlintrc*.json` files = 1 root + 7 nested; **6** of the 7 enable `wallow/*` rules per tree (the
sixth is `apps/minimal-app`); `scripts/fork-smoke/.oxlintrc.json` adds only `no-restricted-imports`
and is correctly excluded from that count. Wrong on both halves — three apps, not two.
**Why it matters:** The lint-config topology is the one thing root `CLAUDE.md` explicitly delegates,
and its own summary of the delegation is wrong.
**Fix:** `packages/lint/CLAUDE.md`'s "six" is correct — copy it. **Trap: A-BP-11's "seven" is
wrong; use six.** Separately, the fork-smoke config is documented only in
`scripts/fork-smoke/README.md`, never in `packages/lint/CLAUDE.md`, which root `CLAUDE.md`
designates as the owner of oxlint-config knowledge.

#### W-H16 — `code-reviewer.md` promises unscoped review and contains zero frontend content
**Where:** `.claude/agents/code-reviewer.md:3` (frontmatter) vs `:9`.
**Evidence:** `:3` promises review "against the plan and Wallow's standards"; `:9` narrows to the
".NET 10 modular monolith", and grepping all 135 lines for pnpm, oxlint, vitest, typescript, jsdom
or `packages/` returns **zero** hits.
**Why it matters:** Half the repo is TypeScript. A frontend PR routed to this agent gets reviewed
against nothing.
**Fix:** Either narrow the frontmatter to backend review or add the frontend standards.

#### W-H17 — `wallow-web/README.md` describes a bridge that no longer exists
**Where:** `apps/wallow-web/README.md:68`.
**Evidence:** It describes `src/app/start.ts` as installing "the D13b `setSsrRequestContextResolver`
bridge (temporary…)". The file is a plain `createMiddleware().server(...)` minting
`createWallowSdk()` + `resolveForkLinks()` and returning `next({context:{sdk, forkLinks}})`.
**Why it matters:** The README describes an architecture that was removed.
**Fix:** Describe the middleware that is actually there. **Trap: do not repeat the reviewer's "grep
returns nothing" claim** — the symbol still appears in root `.oxlintrc.json:66,191,263` as a banned
import, in a 2026-07-27 plan, and at `packages/sdk/README.md:344`. It is absent from SDK source and
app source, which is the actual point.

#### W-H18 — `wallow-web/README.md` calls an SDK export that does not exist
**Where:** `apps/wallow-web/README.md:178` (`getV1IdentityUsersMe()`).
**Evidence:** No such export exists under `packages/sdk/src/generated/`. The real operation is
`usersGetCurrentUser` (`sdk.gen.ts:1123`).
**Fix:** Rename in the sample.

#### W-H19 — `wallow-web/README.md` contradicts itself on how the SDK client is minted
**Where:** `apps/wallow-web/README.md:154` vs `:169-171`.
**Evidence:** `:154` says the SDK's `csrf.ts` "wires a request interceptor onto the shared client";
`:169-171` correctly says the facade is minted per request by `src/app/start.ts` and read off router
context. `start.ts`'s own comment states the hazard: "a module-global client would be shared by
every concurrent render in a server process, so configuring it per request would let one user's
forwarded cookie leak into another user's render."
**Why it matters:** The wrong half describes a pattern the code deliberately avoids for a
cross-user data-leak reason.
**Fix:** Delete `:154`'s claim; keep `:169-171`.

#### W-H20 — `packages/env/CLAUDE.md` describes a deleted spec as live
**Where:** `packages/env/CLAUDE.md:95` (the closing step of the "Adding a module" checklist).
**Evidence:** `charter.test.ts` exists nowhere in the repo. `:39-44` correctly says the spec "used
to assert this" and is gone; `:95` then says in the present tense "The charter spec diffs all four
against `src/*.ts` and fails until they agree."
**Why it matters:** A procedure whose final gate does not exist, in the same file that records the
deletion 50 lines earlier.
**Fix:** Reuse the phrasing `packages/env/CLAUDE.md:44` and `packages/utils/CLAUDE.md:59` already
carry: load-bearing on review, not enforced by a spec.

#### W-H21 — `packages/logger/CLAUDE.md` asserts the same deleted spec twice
**Where:** `packages/logger/CLAUDE.md` — `:11-13` records the deletion; ~`:24` then asserts "The
charter spec asserts `index.ts` never mentions `createLogIngestHandler`, `createRateLimiter` or
`toOtlpLogsPayload`"; ~`:129` lists "the charter" among the node project's covered behaviours.
**Evidence:** No such spec exists.
**Why it matters:** The unheld constraint is a browser/server boundary — in the doc's own words,
"the browser bundle must not carry the guards, the limiter or the OTLP encoder."
**Fix:** Same as W-H20. Consider a `wallow/*` lint rule instead (see §6 beads).

#### W-H22 — `packages/testing/CLAUDE.md` states its consumer set two wrong ways and hides a real gap
**Where:** `packages/testing/CLAUDE.md:3-4` and `:156-158`.
**Evidence:** `:3-4` names four consumers; **six** declare the dependency (all three apps plus ui,
forms, navigation). `:156-158` claims "All five browser projects are wired". **Definitive: 8 browser
projects exist and 3 are unwired — `apps/minimal-app`, `packages/logger`, and `packages/testing`
itself** — because `createVitestProjects()`'s `browserSetupFiles` defaults to `[]`
(`vitest-projects.ts:128,160`), making the guard strictly opt-in. **Real `createVitestProjects`
consumers = 7**: `packages/logger/vitest.config.ts` names the symbol only in a comment explaining
why it does *not* use the preset, and declares no dependency on `@bc-solutions-coder/testing`.
**Why it matters:** The doc's own argument is that an opt-in guard cannot catch the file that forgot
— and three files forgot.
**Fix:** Correct both counts. **Traps: do not copy reviewer 10's "six browser projects", C-BP-07's
"eight consumers", or its two-item unwired set.** The wiring gap itself is a bead, not a docs fix.

#### W-H23 — Two guides teach that a module's Api layer may reference its own Infrastructure
**Where:** `docs/getting-started/fork-guide.md:405-406`; `docs/architecture/assessment.md:77-79`
(the Api block under "Api: Composes everything").
**Evidence:** `CleanArchitectureTests.cs:95-107` `ApiLayer_ShouldNotDependOn_InfrastructureLayer` is
a `[Theory]` over `TestConstants.AllModules`, discovered from `Wallow.*.Domain.dll` with nothing
excluded. Enumerating the csprojs: **6 of 7 module Api projects do not reference their own
Infrastructure; Identity is a genuine exception** (`Wallow.Identity.Api.csproj:18` carries the
ProjectReference and the suite still passes because NetArchTest reads IL type references, not
project references). `assessment.md:77-79` also contradicts `module-creation.md:135`, `:146` and `:502`.
**Why it matters:** The guide sanctions the one dependency the architecture suite exists to forbid.
**Fix:** Best remedy is to delete the block from `assessment.md` and link to `module-creation.md`;
correct `fork-guide.md`. **Do not write an absolute "no Api project references any Infrastructure"
rule** — `Wallow.Announcements.Api.csproj:14` references `Wallow.Shared.Infrastructure`, and ApiKeys
also takes `Shared.Contracts` + `Shared.Kernel`. Record Identity as a documented exception.

#### W-H24 — Dapper is documented as in use at six sites and is used nowhere
**Where:** `docs/development/database-development.md:3`, `:9-12`, `:227-239`, `:261`, `:382-387`;
`docs/index.md:20`.
**Evidence:** `grep -rn "Dapper\|QueryAsync<\|QueryFirstOrDefaultAsync" api/src --include="*.cs"`
returns zero. `find api/src -iname "*Report*"` returns zero. Dapper survives only as a
PackageReference in five module Infrastructure csprojs. `IReadDbContext.cs` does exist, at
`Shared/Wallow.Shared.Kernel/Persistence/IReadDbContext.cs`.
**Why it matters:** `:382-387` actively recommends "Dapper + Materialized Views" as a best practice
for a pattern the codebase has never used.
**Fix:** Remove Dapper from all six sites or relabel it as an available-but-unused option. File a
bead to drop the five unused PackageReferences.

#### W-H25 — The component count is given as 47 and as 56; the catalog has 60
**Where:** `docs/development/frontend-setup.md:160` ("The 47-component Base UI + CVA catalog") and
`:405` ("a catalog of 56 components").
**Evidence:** `ls -d packages/ui/src/components/*/ | wc -l` → **60**.
`docs/development/component-library.md:16` and `packages/ui/CLAUDE.md:3` both say 60 and are right.
**Why it matters:** `frontend-setup.md:405` is an inlined summary of the component-library guide, so
the duplication *is* the defect.
**Fix:** Correct both numbers and replace `:405`'s summary with a link.

#### W-H26 — `component-library.md` cites a deleted component twice
**Where:** `docs/development/component-library.md:81` and `:151` (`DashboardNav.tsx`).
**Evidence:** `apps/wallow-web/src/shared/components/` holds DashboardLayout.tsx,
dashboard-destinations.ts, PublicLayout.tsx, ready-indicator.tsx, SignOut.tsx and their specs.
**Fix:** Repoint to `packages/navigation/src/app-nav.tsx`. Three stale mentions also survive in
`packages/ui` source comments — see §6.

#### W-H27 — `module-creation.md:51`'s `dotnet sln add` has no solution path and fails
**Where:** `docs/architecture/module-creation.md:51`.
**Evidence:** The command is `dotnet sln add api/src/Modules/{Module}/**/*.csproj`. There is no
`.sln`/`.slnx` at the repo root; the only solution is `api/Wallow.slnx`. The surrounding
`dotnet new -o api/src/Modules/…` lines place the reader at the repo root unambiguously.
**Why it matters:** It is the first command a module author runs, and it fails silently at the end
of a copy-pasteable block. **E-BP-16 rated this Low; it is High.**
**Fix:** `dotnet sln api/Wallow.slnx add …`. Keep the `**` globbing caveat (zsh expands globstar by
default; bash needs `shopt -s globstar`).

#### W-H28 — `authentication.md` says no ticket is issued for passwordless; two call sites issue one
**Where:** `docs/architecture/authentication.md:114-126`.
**Evidence:** `grep -n CreateSignInTicket AccountController.cs` gives call sites at 135 (MFA grace),
153 (password login), 248 (MFA verify), **901** and **931**, plus the definition at 1058. Line 901
is inside `VerifyMagicLink` (893-905) and 931 inside `VerifyOtp` (923-935); both return
`Ok(new { succeeded = true, email = result.Email, signInTicket })`. The frontend wire docs agree
with the code (`magic-link-result.ts`, `otp-result.ts`).
**Why it matters:** The page is the authoritative description of the ticket flow, and it excludes
two of the flows that use it.
**Fix:** Correct the claim. The external-OAuth bullet in the same list is **not** contradicted by
any call site — keep it.

#### W-H29 — The MFA lockout table is wrong from the 5th lockout on
**Where:** `docs/architecture/authorization.md:236-242`.
**Evidence:** `WallowUser.RecordMfaFailure` (`WallowUser.cs:159-176`) computes
`TimeSpan.FromMinutes(15 * Math.Pow(2, MfaLockoutCount))`, capped at 24 h. Real series from count 0:
15 m, 30 m, 1 h, 2 h, **4 h, 8 h, 16 h, 24 h (capped from 32 h)**. The doc says "5th+ | capped at 24
hours" — the cap first binds on the **8th**, so the 5th is overstated 6×.
**Why it matters:** Support answers a locked-out user with a number that is six times too large.
**Fix:** Replace the table rows. Surrounding prose is correct: `ResetMfaAttempts()` (`:179-183`)
clears `MfaFailedAttempts` and `MfaLockoutEnd` but deliberately leaves `MfaLockoutCount`.

#### W-H30 — `background-jobs.md` is wrong twice about the Hangfire dashboard guard
**Where:** `docs/architecture/background-jobs.md:23` and the table at `:27`.
**Evidence:** The doc says access "requires an authenticated user with the `admin` role";
`HangfireDashboardAuthFilter.Authorize` checks
`httpContext.User.GetPermissions().Contains(PermissionType.AdminAccess, StringComparer.OrdinalIgnoreCase)`
— a **permission**, not a role claim. The doc also frames dev access as environment-driven ("In
development, access is open"); it is the config flag `Hangfire:AllowAnonymousDashboard`
(`HangfireExtensions.cs:40-45`), whose own comment says it is "deliberately a configuration flag
rather than an environment check: … no environment name can turn it on by accident." The observed
behaviour matches only because `appsettings.Development.json:4` sets it `true`.
**Why it matters:** A fork that trusts the environment framing can ship an open dashboard by setting
`ASPNETCORE_ENVIRONMENT` — or lock itself out by granting the role and not the permission.
**Fix:** Fix the mechanism, not just the role/permission wording. **This also means
`background-jobs.md` is NOT a zero-findings file, contrary to one finder's claim.** Its five cron
expressions and job file paths ARE correct — leave them.

#### W-H31 — The audit-handler recipe produces a handler Wolverine never discovers
**Where:** `docs/operations/audit-events.md` (~`:120-130`), with the false reassurance at `:130`.
**Evidence:** The example is `public static class MyModuleAuditHandlers` with no attribute, followed
by "Wolverine auto-discovers handlers in all `Wallow.*` assemblies, so no explicit registration is
needed". The real `AuthAuditEventHandlers.cs` carries `[WolverineHandler]` above a doc comment
stating exactly why: "Wolverine's conventional discovery matches a type name ending in 'Handler' or
'Consumer' and reads instance methods; a static class named '...Handlers' satisfies neither, so
without it every method here is silently unreachable and nothing is ever audited."
**Why it matters:** The recipe reproduces the precise shape the real file documents as broken, and
the failure is silent — no audit trail, no error.
**Fix:** Add `[WolverineHandler]` to the example and carry over the explanation.

#### W-H32 — `observability.md` states P and ¬P about instrument registration
**Where:** `docs/operations/observability.md:680` and `:793`, with misleading follow-on instructions
at `:656` and `:673`; the correct explanation is at `:304-349` (precise passages `:146`, `:155`,
`:320`, `:329`).
**Evidence:** `:680` says "`ConfigureOpenTelemetry` ships with no `AddMeter` calls" and `:793` says
"none is registered with `AddSource`". `api/src/Wallow.ServiceDefaults/Extensions.cs:74,78,87,96`
registers `.AddSource(namespacePrefix, moduleNamespaces)` and
`.AddMeter(namespacePrefix, moduleNamespaces)`.
**Why it matters:** A reader following `:656`/`:673` adds duplicate registrations.
**Fix:** Delete the two stale negatives and the instructions that depend on them.

#### W-H33 — The cookbook's "outside the workspace" install cannot succeed
**Where:** `docs/integrations/integration-cookbook.md` (install step).
**Evidence:** `npm install @bc-solutions-coder/styles` 404s. `packages/styles/package.json` is
`"version": "0.0.0"` and nothing publishes it — `grep -rn styles .github/workflows/` finds only
docs-theme and CI references, and `sdk-publish.yml` is scoped `working-directory: packages/sdk` on
`sdk-v*` tags. (The package is *not* `"private": true`, unlike the other ten, so it is publishable
in principle — but nothing publishes it.)
**Why it matters:** The cookbook's entire stated audience is out-of-workspace consumers, and step
one fails.
**Fix:** Either publish `styles` or tell the reader to vendor the CSS. Related: `packages/config` is
`"private": true` (`package.json:4`), so the cookbook's hand-rolled Vite config is the *correct*
form for that audience — see the rescope note for F-R-26 in §5.

#### W-H34 — `typescript-sdk.md` gives the wrong cookie-name default and omits a variable
**Where:** `docs/integrations/typescript-sdk.md:288` and its env table.
**Evidence:** `packages/sdk/src/server/config.ts:220-242` derives `__Host-wallow_bff` whenever
`COOKIE_SECURE` is on (the default) and `COOKIE_HOST_PREFIX` is not the literal `false`. The doc
says "Defaults to `wallow_bff`" and has no `COOKIE_HOST_PREFIX` row.
`docker-compose.production.yml:603` passes the variable explicitly, and `deployment.md:337-338`
already states the derived name correctly — the two pages disagree.
**Fix:** Copy `deployment.md:337-338`. Same defect as the substantive half of F-BP-04.

#### W-H35 — `typescript-sdk.md` documents `logout()` as a browser navigation
**Where:** `docs/integrations/typescript-sdk.md:432` and `:444`.
**Evidence:** `packages/sdk/src/auth.ts:92` is
`export function logout(options?: LogoutOptions): Promise<void>`, delegating to `endSession()`,
which issues `fetch("/bff/logout", { method: "POST", credentials: "include", redirect: "manual", headers })`
with `x-csrf-token` and throws `Logout failed: the BFF answered ${response.status}`. The page's own
`:218` says a bare `GET /bff/logout` answers `405 + Allow: POST`.
**Why it matters:** Self-contradictory, and the wrong half is the one in the API reference list.
**Fix:** Correct `:432` and `:444` to match `:218`.

#### W-H36 — `CookieSessionStore` is dev-only in one doc and production-viable in the other
**Where:** `docs/integrations/bff-pattern.md:551, 556, 557` vs `docs/integrations/typescript-sdk.md:309`.
**Evidence:** bff-pattern says "Development only", "a **development default only**", and
"Production deployments must construct a `ValkeySessionStore` explicitly". typescript-sdk says
"Simple apps and local development — nothing extra to run".
**Why it matters:** Divergence on a security-relevant default; a fork reading the SDK guide ships
cookie-only sessions.
**Fix:** Make typescript-sdk match bff-pattern.

#### W-H37 — `reverse-proxy.md` says wallow-auth reads three environment variables; it reads seven
**Where:** `docs/operations/reverse-proxy.md:97-98`.
**Evidence:** A full enumeration of `process.env` reads under `apps/wallow-auth/src` gives
`AUTH_BASE_PATH`, `WALLOW_API_INTERNAL_URL`, `PORT`, `OTEL_EXPORTER_OTLP_ENDPOINT`
(`log-ingest.server.ts:34`) and `E2E_BASE_URL`, plus `resolveForkLinks(process.env)` at
`app/start.ts:53`, which reads `WALLOW_REPOSITORY_URL` and `WALLOW_DOCS_URL`
(`packages/styles/src/branding.ts:323-324`). `docker-compose.production.yml:517, 523-524` passes all
three of the "extra" ones.
**Why it matters:** The failure mode is real and silent — `log-ingest.server.ts:34`'s own comment
says "Unset — the default outside the compose stacks — logs to stdout", so browser log batches are
answered `204` and written to container stdout with nothing shipped.
**Note:** the Area F scout marked this "verified true"; the reviewer filed it as a defect. **The
reviewer is right.** The routing table, the `AUTH_BASE_PATH`-as-build-arg callout and the
health-check URLs on the same page all check out.
**Fix:** Replace the list; drop the word "only".

#### W-H38 — `troubleshooting.md` prescribes a Valkey connection string that omits the dev password
**Where:** `docs/operations/troubleshooting.md:154-161`.
**Evidence:** The page offers `"Redis": "localhost:6379"` as the *fix* for a
`RedisConnectionException`. `api/src/Wallow.Api/appsettings.Development.json:9` is
`"Redis": "localhost:6379,password=WallowValkey123!,abortConnect=false"`.
**Why it matters:** The prescribed remedy breaks a working connection.
**Fix:** Use the real development connection string.

### 3.3 Medium

Condensed format: **Where** — Evidence — *Fix*.

**Root governance and `.claude/`**

- **W-M1** `.claude/agents/docfx-specialist.md:17-19` — cites a `docfx/toc.yml` that does not exist (only `docs/toc.yml` does) and omits `build.content[0].exclude` = `["plans/**","claude/**","CLAUDE.md","audits/**"]`. *Correct the path; add the exclude list.*
- **W-M2** `SECURITY.md:5-8`, `docs/getting-started/fork-guide.md:738`, `docs/operations/versioning.md:88` and `:74-78` — all three treat the platform as pre-1.0. `SECURITY.md` claims 0.2.x supported / <0.2 unsupported; fork-guide pins `"minWallowVersion": "0.2.0"`; versioning says "The project starts at `0.x.y`" and its worked example runs 0.1.0 → 0.2.0. `.release-please-manifest.json` is `{".": "4.0.0", "packages/sdk": "0.2.0"}` and the CHANGELOG's top entry is 4.0.0 (2026-07-26). **0.2.0 is `packages/sdk`'s separate release-please component, not the platform.** *Update all four sites to 4.x and say which component 0.2.0 belongs to.*
- **W-M3** root `CLAUDE.md:93` vs `:108-109` — `:93` says `pnpm dev # turbo run dev (both apps, deps built first)`; `:108-109` says "dev declares no dependency". `turbo.jsonc:47-50` is `{cache:false, persistent:true}` with no `dependsOn`. Note "both apps" is literally right — `package.json`'s dev script filters to exactly wallow-web and wallow-auth. *Drop "deps built first".*
- **W-M4** `.claude/rules/TESTING.md` — says "Three specs are deliberately outside it"; `.oxlintrc.json`'s final override lists **seven** files under `wallow/no-source-tests: off`. It also names `packages/testing/src/browser-styles-wiring` as a spec — it is a helper module (`packages/testing/package.json:51-53`), not a `*.test.*`, and needs no exemption. Only ONE listed entry is genuinely unexplained: `packages/query/src/index.test.ts` (imports `existsSync` at `:29`; a third "runtime/compile-time identity" class). *Fix the count, the non-spec naming, and add the third class. **Not** "four orphan entries" — the sdk and styles entries are already blessed by TESTING.md's own "an artifact is not source" sentence.*
- **W-M5** `README.md:145` (+ badges `:14-15`, restatement `:28`) — "6,078 tests across 45 assemblies". Real counts: `api/tests` `.csproj` = 17; all `api` `.csproj` = 57; JS workspace members with a test script = 15. None is 45. No workflow regenerates them (`grep -rln README .github/workflows/` → nothing). *Only "45 assemblies" is disproven — the test/coverage percentages are unverified, not wrong. Either regenerate these figures in CI or remove them.*
- **W-M6** `.claude/agents/docfx-specialist.md:23-30` — the `integrations/` row names a DCR page that no longer exists (CHANGELOG 4.0.0 records the removal of ClientRegistration/InitialAccessTokens) and omits `integration-cookbook.md`; the same table omits `architecture/{assessment,authorization,background-jobs}`, `development/{component-library,forms,logging,frontend-state,database-migrations}` and `operations/{audit-events,reverse-proxy,request-correlation}`. Real counts: architecture 9, development 10, operations 7, integrations 5. *Regenerate the table.*
- **W-M7** `README.md:99-101` and `:104-106` — the `Shared/` tree shows `Contracts/ Kernel/ Infrastructure/`; real `api/src/Shared` holds seven projects (Wallow.Shared.Api, .Contracts, .Infrastructure, .Infrastructure.BackgroundJobs, .Infrastructure.Core, .Infrastructure.Plugins, .Kernel), and the tree uses unprefixed names that do not exist. The `apps/` sub-tree lists only wallow-auth and wallow-web, omitting minimal-app. *Regenerate both trees.*
- **W-M8** `README.md` and `CONTRIBUTING.md` — `apps/minimal-app` appears in neither (grep → 0), though it has its own `.oxlintrc.json`, a knip workspace entry, a root-config override and a line in root `CLAUDE.md:60`. *Add it.*
- **W-M9** `.claude/agents/docfx-specialist.md:32-35` — the heading "## Rules (from `docs/CLAUDE.md`)" attributes to `docs/CLAUDE.md` a `docs/plans/` rule that lives in root `CLAUDE.md` (docs/CLAUDE.md's Rules are exactly three bullets, none about plans), and drops the path shape, the status line and the commit requirement. Two further bullets under that heading come from docs/CLAUDE.md's "Adding a New Guide" section, not its Rules. *Re-attribute and restore the dropped requirements.*
- **W-M10** `.claude/agents/enterprise-architect.md:34` vs `code-reviewer.md` / `csharp-developer.md` — enterprise-architect says the Api layer holds "Endpoints"; the other two say Controllers, and the sample derives from `ControllerBase`. Reality: **27** files under `api/src` reference `ControllerBase`; `grep -rl MapGroup api/src --include=*.cs` → **0** (three `MapGet|MapPost` hits, health/infra only). The repo is controller-based. *Standardise on Controllers. ("Fat Controllers/Endpoints" is at `enterprise-architect.md:103`, not :104.)*
- **W-M11** `.claude/agents/csharp-developer.md` — teaches a Billing/Invoice domain that does not exist: 26 `Invoice` occurrences (InvoicesController, CreateInvoiceCommand, `Invoice : AggregateRoot`, IInvoiceRepository, LogInvoiceCreated). Same phantom domain as `CONTRIBUTING.md:88`. *Re-base the examples on a real module.*
- **W-M12** `.claude/agents/csharp-developer.md:174` — bare `dotnet build   # Build solution`; there is no root solution file. `api/CLAUDE.md:21` is `dotnet build api/Wallow.slnx`. Adjacent lines in the same block do use repo-relative paths. *Add the solution path.*
- **W-M13** root `CLAUDE.md:86-101` — documents 15 of `package.json`'s 20 scripts, omitting `lint:fix`, `lint:tests:fix`, `secrets:prod` and `prepare`; `:88` writes ":down to stop" where the real script is `backend:infra:down`. *Complete the inventory and fix the script name.*
- **W-M14** `README.md:190-199` — the Documentation table has 8 rows and zero frontend guides, while root `CLAUDE.md`'s Documentation section surfaces frontend-setup, component-library, forms, logging, frontend-state, bff-pattern and typescript-sdk. *Add the frontend rows.*
- **W-M15** `CONTRIBUTING.md:20-23` — step 3 starts the backend with `dotnet run --project api/src/Wallow.Api` rather than the canonical `pnpm backend` (Aspire) that `README.md:59` and `api/CLAUDE.md` both lead with. CONTRIBUTING never mentions beads or `.claude/rules` (grep → 0) and routes everything through GitHub issues (`:33-51`). *Switch to `pnpm backend`; add a beads pointer.*
- **W-M16** Commit-type tables disagree in three places: root `CLAUDE.md` names eight non-releasing types (`chore refactor docs test ci style perf build`); `CONTRIBUTING.md:74-83` omits `style` and `build`; `docs/operations/versioning.md:19-28` omits `perf`, `style` and `build`. **Nothing arbitrates:** there is no `commit-msg` hook (`.husky/` holds `_`, post-checkout, post-merge, pre-commit, pre-push, prepare-commit-msg) and no commitlint config. *Make root `CLAUDE.md` the source and have the other two link to it. Trap: A-BP-03's supporting claim ".husky has prepare-commit-msg only" is refuted — restate it as "no commit-msg hook".*

**Backend (`api/`)**

- **W-M17** `api/src/Modules/ApiKeys/README.md:75` — places `ScopePermissionMapper` in Contracts; it is in the Kernel. `ApiScopes.cs` genuinely *is* in Contracts (`Contracts/Identity/ApiScopes.cs`). `ApiKeys/CLAUDE.md:24` states it correctly. *Move only the mapper.*
- **W-M18** `Inquiries/README.md:108` — marks `PATCH /{id}/status` "Authenticated"; `InquiriesController.cs:140-141` requires `[HasPermission(PermissionType.InquiriesWrite)]`. *Correct the permission.*
- **W-M19** `Inquiries/README.md:5` — says visitors submit inquiries; `InquiriesController.cs:29` is class-level `[Authorize]` with zero `[AllowAnonymous]`, and POST at `:36-37` requires `InquiriesWrite`. `README:100` self-contradicts ("All endpoints require authentication"). The contrast still holds: `ChangelogController.cs:18` really does carry `[AllowAnonymous]`. *Correct `:5`.*
- **W-M20** `api/src/Wallow.Api/README.md:78-82` — teaches a non-canonical infra command; `package.json:7` is `"backend:infra": "cd docker && docker compose up -d"`, and the README never mentions the Aspire host though `api/CLAUDE.md:11-12` makes it the documented entrypoint. **Citation fix: the Aspire source is `api/src/Wallow.AppHost/Program.cs`, NOT `AppHost.cs`** (the `:9-44` span is right for Program.cs). *Point at `pnpm backend`.*
- **W-M21** `Wallow.Identity.Api/README.md:70-76` — lists 5 domain entities; `Wallow.Identity.Domain/Entities/` holds 11. *Complete the list.*
- **W-M22** `api/CLAUDE.md:27-28`, `docs/development/testing.md:35-36`, `docs/getting-started/developer-guide.md:63` — all three list 12 `run-tests.sh` shorthands; there are **14**. `resolve_filter()` lines 29-31 also accept `architecture`, `seeder` (→ `Wallow.SeederService.Tests`) and `migrations` (→ `Wallow.MigrationService.Tests`). Separately `api/CLAUDE.md:145-156` omits the `Benchmarks`, `Wallow.AppHost.Tests`, `Wallow.MigrationService.Tests` and `Wallow.SeederService.Tests` projects. *Regenerate all three lists from `resolve_filter()`.*
- **W-M23** `api/CLAUDE.md:11` — says the AppHost runs "Postgres + Redis"; `Wallow.AppHost/Program.cs` provisions Postgres (9), Valkey (16), Garage (22), Mailpit (34), ClamAV (39), MigrationService (44), SeederService (49), Wallow.Api (54), wallow-auth (96) and wallow-web (118). Line 57 references Garage's endpoint rather than declaring it. *Complete the list.*
- **W-M24** `Shared/Wallow.Shared.Contracts/README.md` — the interface list omits eight that exist: `IUserService`, `IUserQueryService`, `IScopeSubsetValidator`, `ISetupStatusProvider`, `IEmailService`, `IRealtimeDispatcher`, `IRealtimeAccessRevoker`, `IPresenceService`. *Complete it.*
- **W-M25** `Identity/CLAUDE.md:17` and `:16` — lists 17 controllers; 18 exist (`MeController.cs:17`, `v{version:apiVersion}/identity/me`, is missing). `:16` omits entities `MembershipRole`, `OrganizationBranding`, `OrganizationSettings`. *Add `Me` and the three entities. Trap: **do not** "move" `Identity/README.md:130` — `UsersController.cs:73` really is `[HttpGet("me")]`.*
- **W-M26** `api/src/Wallow.Api/README.md:28` — names four background jobs; `Program.cs` registers five, at 609, 618, 624, 629 and **634** — `SessionPruningJob` is omitted. *Add it.*
- **W-M27** `Branding/README.md:69` vs `:61` — `:69` says the module publishes integration events; `:61` in the same file says it publishes none, and `Branding/CLAUDE.md:36` agrees with `:61`. *Delete `:69`.*
- **W-M28** `Branding/README.md:10` — names GarageHQ as Branding's storage backend; `Storage/README.md:137` shows `"Provider": "Local"` as the default. *Correct or qualify.*
- **W-M29** `Wallow.Tests.Common/README.md:58-59` — tells the reader to run `./scripts/run-tests.sh shared`, which `run-tests.sh:32` maps to `Wallow.Shared.Infrastructure.Tests` — a different assembly. `Wallow.Tests.Common` has no shorthand and no tests of its own. *Say so.*
- **W-M30** The Identity integration-event catalogue is maintained five ways and diverges: `Identity/README.md:97-108` lists exactly 5 under "Integration Events Published" **with no hedge**; `Shared/README.md:51` the same 5 plus "and others"; `Contracts/README.md:15` lists 11 plus "and others"; `Notifications/README.md:73` lists 13 including `AccessRequestedEvent`; `Identity/CLAUDE.md:44-49` is prose with an ellipsis. Real: ~29 events (30 files; `MembershipTransition.cs` is a supporting type). *Keep one canonical list and link to it.*
- **W-M31** `api/CLAUDE.md:151-152` — names only `apps/wallow-auth/e2e/`; three suites exist (`apps/wallow-auth/e2e`, `apps/wallow-web/e2e`, `apps/wallow-web/e2e-cross-app`). *Add the two. Trap: `api/CLAUDE.md:29` does **not** have the same gap — it reads "(E2E is per-app Playwright now — see .claude/rules/E2E.md)" and names no suite. Fix 151-152 only.*
- **W-M32** Valkey/Redis naming is mixed across the backend and the architecture docs. Case-insensitive counts — `ApiKeys/CLAUDE.md` 3 Valkey / 12 Redis; `ApiKeys/README.md` 5/5; `Wallow.Api/README.md` 2/6; `Tests.Common/README.md` 3/3; `api/CLAUDE.md` Redis only; Inquiries and `Identity/CLAUDE.md` Valkey only; `docs/architecture/caching.md` 24/11; `realtime.md` 1/17; `authentication.md` 0/9; `authorization.md` 0/2; `module-creation.md:485` uses the `Valkey/Redis` slash form. `docker/docker-compose.yml:47` runs `image: valkey/valkey:8-alpine` under a comment that already states the rule: "CACHE & BACKPLANE (Valkey — Redis-compatible)". *Config keys and .NET APIs are genuinely `Redis`, so adopt the product-vs-API split: Valkey for the service, Redis for keys and client types.*
- **W-M33** Four `Wallow.Shared.*` projects have no README (only Kernel, Contracts and Infrastructure do), and four documents give four diverged descriptions of `Wallow.Shared.Infrastructure.Core`: `ApiKeys/README.md:76`, `Branding/README.md:70`, `Inquiries/README.md:129`, `Notifications/README.md:132`. *Write one description and link to it.*
- **W-M34** `Announcements/README.md:94` — says the event "includes target criteria and resolved user IDs"; `Announcements/CLAUDE.md:34` disagrees, and `AnnouncementTargetingService.ResolveTargetUsersAsync:53-62` is a TODO returning an empty list. The record does carry `TargetUserIds`, so this is schema-vs-behaviour. *Document the TODO.*
- **W-M35** `api/src/Wallow.Api/README.md:82` — the infra service list omits `clamav` (documented at `Storage/README.md:13`) and `docs` (host 5004), and calls the observability service "Grafana" where it is `grafana-lgtm` on host 3001. **Count correction: `docker/docker-compose.yml` defines EIGHT services** — postgres, mailpit, valkey, alloy, grafana-lgtm, garage, clamav, docs — not nine. *Correct the list and the name.*
- **W-M36** `Storage/README.md:69` and `docs/architecture/messaging.md:7-11`, `:17` — Storage's README lists `UploadFileCommand` as a module command and messaging.md describes `Shared.Contracts` as holding only integration events and read-only query interfaces, classifying "Commands and queries" as "within one module". `Shared/Wallow.Shared.Contracts/Storage/Commands/UploadFileCommand.cs` exists and is the only Command file in that assembly (only the handler and validator live in `Wallow.Storage.Application/Commands/UploadFile/`). `api/CLAUDE.md:62-63` gives a third, also-incomplete list. *Correct all three against the tree.*

**Frontend apps and packages**

- **W-M37** `packages/testing/CLAUDE.md:13-28` — enumerates 14 of the package's 16 export keys, omitting `./console-guard` and `./network-escape` entirely (`grep -c` → 0 across the file). The eight guard consumers are `apps/wallow-web/vitest.setup.ts`, `apps/wallow-auth/vitest.setup.ts`, `packages/ui/vitest.setup.ts`, `packages/ui/.storybook/preview.tsx`, `packages/ui/src/components/label/label.test.tsx`, `packages/forms/vitest.setup.ts`, `packages/forms/src/form/app-form.test.tsx`, `packages/navigation/vitest.setup.ts`. ***The fixer must open the two source files — the scout guessed at their contents.***
- **W-M38** `apps/CLAUDE.md:3-7` and `apps/minimal-app/README.md:17-24`, `:25-29` — apps/CLAUDE.md names four optional packages (forms, auth, navigation, logger) and says minimal-app "omits all four"; it omits five, `utils` too. minimal-app's real deps are env, query, sdk, styles, testing, ui (plus config in devDeps). The minimal-app README lists five and omits `env`, though `src/start.ts:1-2` imports `resolveInternalOrigin` from `@bc-solutions-coder/env/internal-origin` and `resolveRequestOrigin` from `…/request-origin`; the copy-outside-the-monorepo caveat omits `env` too. *One fix across both files.*
- **W-M39** `apps/wallow-web/e2e/CLAUDE.md:53-70` — describes `e2e-cross-app/` as a three-origin journey suite but documents only `login-journey.spec.ts`; `external-origin-login.spec.ts` also exists and is never mentioned (`grep -n "external-origin\|bff-example"` → no matches). Its "Two supported stacks" command block reads as covering the whole directory. *Document the second spec.*
- **W-M40** `packages/navigation/CLAUDE.md:5` — says `DashboardLayout.tsx` is 36 lines; `wc -l` → **46**. *Correct or drop the number.*
- **W-M41** `packages/navigation/CLAUDE.md:47` — lists `/error-banner` among the ui subpaths navigation imports; `grep -rn "error-banner\|ErrorBanner" packages/navigation/src` → no matches. Real imports: `/navigation-menu`, `/button` (twice — `Button` and `buttonRecipe`), `/theme-toggle`. *Correct the list.*
- **W-M42** `packages/config/CLAUDE.md` — says "all seven `packages/*` library builds"; **eleven** use `defineLibraryConfig` (auth, env, forms, logger, navigation, query, sdk, styles, testing, ui, utils). Only `config` and `lint` are absent, because neither has a `build` script. *Correct the count and say why two are excluded.*
- **W-M43** `packages/ui/CLAUDE.md:17` — says `src/core/` holds "`cn.ts` (tailwind-merge wrapper) plus the package's own scaffold guards"; `ls` gives exactly `browser-deps.test.ts`, `cn.test.ts`, `cn.ts`, `storybook-setup.test.ts`. The same file at ~`:132` records that `package-scaffold.test.ts` and `dist-structure.test.ts` are gone. *Correct `:17`.*
- **W-M44** `packages/forms/CLAUDE.md:94` and `docs/development/forms.md:333` — both list "the on-disk scaffold guard" among the node project's coverage; `packages/forms/src/core/` has no `package-scaffold.test.ts` (contents: browser-deps, contexts, errors, form-hook, server-error, test-id and their specs), and `packages/forms/CLAUDE.md:27-30` records the deletion by name four sections earlier. *Remove from both.*
- **W-M45** `scripts/fork-smoke/README.md` — justifies its "config must stay comment-free JSON" constraint with a sweep that was deleted (`packages/query/src/` is now four files; `grep -rn "JSON.parse\|oxlintrc\|fork-smoke" packages/query/src/` → no matches; `packages/query/CLAUDE.md:59-64` records the deletion by name and "not to be recreated"). The constraint is very likely **obsolete**, not merely mis-justified: `packages/lint/CLAUDE.md:70-77` says oxlint parses config as JSONC, and the surviving reader `packages/sdk/src/oxlint-guardrails.test.ts:76-82` strips line comments before `JSON.parse`. *Re-derive whether the constraint is still needed before rewording it.*
- **W-M46** `packages/ui/CLAUDE.md:5-6` — names two consumers; five declare `@bc-solutions-coder/ui` (minimal-app, wallow-web, wallow-auth, packages/forms, packages/navigation). Two of the three omitted are *packages*, which is the load-bearing part. *Correct the list.*
- **W-M47** `packages/lint/CLAUDE.md:256-257` — says `no-source-tests` applies "under any of the six configured trees"; root `.oxlintrc.json:20` sets `"wallow/no-source-tests": "error"` at top level, outside any override, and the same file states this correctly at `:118`. Error is in the safe direction. *Correct `:256-257`.*
- **W-M48** `packages/lint/CLAUDE.md:173-175` — places `SignOut.tsx` at `src/features/bff-demo/components/`; **that directory does not exist at all**. The only SignOut is `apps/wallow-web/src/shared/components/SignOut.tsx`, which `packages/navigation/CLAUDE.md:36-39` and `apps/wallow-web/README.md:76` both locate correctly. *The paragraph's scoped-override argument rests on this path, so re-derive where the raw `<button>`s actually live rather than repathing one line.*
- **W-M49** `packages/sdk/README.md:347` — links `../../apps/wallow-web/src/start.ts`; the file is `apps/wallow-web/src/app/start.ts`. Dead link. *Repoint.*
- **W-M50** `apps/CLAUDE.md:97` and `packages/navigation/CLAUDE.md:133` — both describe the `*.test.*` override wrongly. All six rule-enabling nested configs turn off exactly the same four rules (`no-hand-rolled-mutation`, `no-sidebar-inversion`, `no-tinted-text`, `text-heading-variant`). apps/CLAUDE.md's "The first three" accounts for three of four (`no-hand-rolled-mutation` appears in that file only at `:124`, in an unrelated paragraph). navigation's "The four class-string rules" has the right count but the wrong label — `no-hand-rolled-mutation` inspects a `mutationFn` property, not a class string — and its closing "exactly as in the two apps" is stale, since three apps carry the block. *Correct both.*
- **W-M51** `ReadyIndicator` ownership is told three incompatible ways. Real layering: catalog component `packages/ui/src/components/ready-indicator/ready-indicator.tsx` plus three app wrappers (`apps/wallow-web/src/shared/components/`, `apps/wallow-auth/src/shared/components/`, and `apps/minimal-app/src/components/` — note the different path). Both e2e guides (`apps/wallow-auth/e2e/CLAUDE.md:36`, `apps/wallow-web/e2e/CLAUDE.md:40`) attribute the marker to "the marker `src/shared/components/ready-indicator.tsx` stamps"; `packages/ui/CLAUDE.md:216-218` attributes it to the catalog and says "across both apps" — three apps consume ui. *State the catalog-plus-wrapper layering once.*
- **W-M52** `apps/CLAUDE.md:32` vs `:36` — `:32` states host-layout rules as universals, naming `createApiPassthrough` "for wallow-auth/minimal-app" under `src/app/routes/**` and giving `src/app/routeTree.gen.ts`. minimal-app's tree is flat (`src/routes/`, `src/routeTree.gen.ts`, `src/start.ts`, `src/router.tsx`, `src/lib/`) with no `app/` directory — and `:36`, four lines later, says minimal-app "is deliberately not" zoned. *Scope `:32` to the two zoned apps.*
- **W-M53** root `CLAUDE.md`'s Local Development table — omits minimal-app's port **3010**, which `apps/CLAUDE.md:16`, `apps/minimal-app/README.md:49` and `:61`, and `apps/minimal-app/vite.config.ts` all carry. *Add the row.*
- **W-M54** `apps/wallow-auth/e2e/CLAUDE.md:29-40` and `apps/wallow-web/e2e/CLAUDE.md:32-44` — "Selectors" and "Readiness" are **byte-identical** between the two files, including `login-email`/`login-submit` examples, which are wallow-auth's screen; wallow-web's `e2e/` has no login screen (it covers `/bff-demo` reachability). The "Playwright, not Vitest browser mode" block and the "Drive the backend manually" commands are near-verbatim, and the "Seeder gotcha" sits mid-document in one and near the end in the other. *Land the shared content in a hub (`.claude/rules/E2E.md`) first, then trim both.*

**docs/ — getting-started and development**

- **W-M55** `docs/development/testing.md:151` and `docs/development/frontend-setup.md:485-486` — testing.md says `pnpm -r test`; root `package.json`'s `"test"` is `turbo run test`. frontend-setup says `pnpm --parallel --filter`; the real `dev` script is `turbo run dev --filter @bc-solutions-coder/wallow-web --filter @bc-solutions-coder/wallow-auth`. *Correct both.*
- **W-M56** `docs/getting-started/onboarding.md:152` — asks "Why doesn't Identity use CQRS?" Identity's Application layer has `Commands/` (BootstrapAdmin, CreateServiceAccount, RevokeServiceAccount, RotateServiceAccountSecret, UpdateServiceAccountScopes) and `Queries/` (GetApiScopes, GetServiceAccount, GetServiceAccounts, IsSetupRequired). The real no-CQRS modules are Branding (`Application/` = DTOs + Interfaces) and ApiKeys (`Interfaces/` only), which is what `docs/development/api-development.md:112` says. *Re-target the FAQ entry.*
- **W-M57** `docs/getting-started/onboarding.md:129` — says "Watch for Testcontainers spinning up Postgres and Valkey" during a run that excludes them; `run-tests.sh` appends `--filter Category!=E2E&Category!=Integration` unless the argument is literally `integration`. *Correct.*
- **W-M58** The CI table has 7 rows; `ci.yml` has **9** jobs — build `:17`, unit-tests `:65`, integration-tests `:109`, cross-tenant-tests `:194`, docker-images-app `:264`, docker-images-infra `:372`, e2e-tests `:398`, fork-smoke `:490`, merge-coverage `:523`. `cross-tenant-tests` and `fork-smoke` are absent. The frontend gate is not in `ci.yml` at all — it is `js.yml`'s single `build` job (`pnpm lint`, `lint:tests`, `lint:manifests`, `lint:deps`, `lint:env`, `format:check`, `turbo run build typecheck test`, `check:exports`). *Add both jobs and the `js.yml` gate.*
- **W-M59** The `api/tests/` inventory is short in both its tree and the `:52-53` sentence. Real contents: Benchmarks, Modules, Wallow.Api.Tests, Wallow.AppHost.Tests, Wallow.Architecture.Tests, Wallow.MigrationService.Tests, Wallow.SeederService.Tests, Wallow.Shared.Infrastructure.Tests, Wallow.Shared.Kernel.Tests, Wallow.Tests.Common. *Regenerate.*
- **W-M60** `docs/development/frontend-setup.md:645-650`, `:665-666` and `docs/getting-started/configuration.md:48` — the `--warning` theme tokens are missing from the docs and used as the example of a token that does not exist yet. `branding.json`'s `theme.light`/`theme.dark` each carry 27 keys **including `warning` and `warningForeground`**. frontend-setup's CSS block ends at `--success, --success-foreground`, and `:665-666` uses "a `warning` or `success` color" as its worked example of *adding a new token* — both already ship. configuration.md's enumerated list ends at `success, successForeground, radius`. Also `styles.css:66-72` has **seven** two-level fallbacks (sidebar, sidebar-foreground, sidebar-accent, success, success-foreground, warning, warning-foreground) against the doc's "last five". *Add the tokens; pick a genuinely absent token for the worked example; fix "five" → "seven".*
- **W-M61** The ClamAV configuration example contradicts its own table and prose twice, and the key does not exist: `appsettings.json`'s `Storage` section contains only `Provider`, `Local`, `S3` — **no `ClamAv` key**. The doc example at `:337-341` shows `"Enabled": true, "Host": "clamav"`; the table at `:357` says `false` / `localhost`; the prose at `:365` says "disabled by default". *Reconcile against shipped config.*
- **W-M62** The OpenTelemetry example shows `EnableLogging: true` and never mentions `TraceSamplingRatio`; `appsettings.json` ships `EnableLogging: false` and `TraceSamplingRatio: 1`. *Correct.*
- **W-M63** `docker/.env.example` has **12** keys; the doc block lists 10, omitting `VALKEY_MAXMEMORY` and `GARAGE_REGION`. *Add both.*
- **W-M64** `docs/development/frontend-state.md` — never names the repo's only Zustand store. Grepping for `zustand|useNavStore|navigation` returns five hits, all generic prose (`:1` title, `:21`, `:24`, `:118`, and `:172`, where "navigation" is used in a routing sentence). The store is `packages/navigation/src/nav-store.ts`, the only `from "zustand"` importer in the workspace. *Name it.*
- **W-M65** The shared-package dependency table is wrong and its ordinals do not survive. Real `@bc-` runtime deps: wallow-auth = auth, env, forms, logger, query, sdk, styles, testing, ui, utils; wallow-web = the same plus navigation; minimal-app = env, query, sdk, styles, testing, ui (**six**). The table omits logger, env and utils. Compounded: the table has 7 rows (`:157-165`), `:167` says "the first six", `:170` says "Only the first five are core", `:176` heads "Depend on the five core packages" — and minimal-app takes six, so the arithmetic is wrong as well as unreadable. *Rebuild the table and drop the ordinal references.*
- **W-M66** `docs/CLAUDE.md`'s Structure block omits exactly two pages: `operations/request-correlation.md` (operations has 7 files) and `integrations/integration-cookbook.md` (integrations has 5). *Add both. (Its architecture/ and development/ lines are COMPLETE at 9 and 10 — do not touch them.)*
- **W-M67** `docs/CLAUDE.md:31` — says "Docs site content only — no plans, designs, specs, or session artifacts", while root `CLAUDE.md` mandates that plans live in `docs/plans/`. Both `docs/plans/` and `docs/audits/` exist and are tracked, and the `docfx.json` exclude list is mentioned nowhere under `docs/`. *Reconcile: the rule is about what ships, and `docfx.json` excludes those trees.*
- **W-M68** `docs/getting-started/developer-guide.md:92-106` — lists 14 endpoints and omits **three**: Docs (5004), Valkey (6379) and the Hangfire dashboard (`api/src/Wallow.Api/Extensions/HangfireExtensions.cs:43` maps `/hangfire`, and `onboarding.md:198` already links it). *Add all three.*
- **W-M69** `Modules.Configuration` names no module (there are seven: Announcements, ApiKeys, Branding, Identity, Inquiries, Notifications, Storage), but **the doc is faithfully mirroring shipped config** — `api/src/Wallow.Api/appsettings.json:86` really carries `"Modules.Configuration": true`. *Fix both or neither; a doc-only fix leaves a fork's real appsettings carrying a dead flag. Trap: the module-flag example at `fork-guide.md:325-331` has **eight** keys and DOES include `Modules.Branding` and `Modules.Identity` — D-BP-19's claim that it omits them is refuted; the finder transcribed a truncated excerpt.*
- **W-M70** `docs/development/api-development.md` carries five distinct defects: `:653` shows bare `opts.UseFluentValidation()` where `Program.cs:263` is `opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration)`; the doc says `appsettings.Development.json` does not define `ReadReplicaConnection` (it does); the schema table omits `auth_audit` (real — `AuthAuditDbContext` is registered in `MigrationService/Program.cs`); "This provides:" lists 5 while `docker-compose.yml` defines 9 services (postgres, mailpit, valkey, alloy, grafana-lgtm, garage, clamav, docs, wallow); and `configuration.md:235` marks `AuthUrl` "Required: Yes" while `appsettings.json` ships `"AuthUrl": ""`. *Fix all five.*
- **W-M71** `pnpm check`, turbo, docfx, `docs-serve` and port 5004 appear **nowhere** in any of the 16 area-D files (independently re-grepped: zero hits for all five terms across `docs/CLAUDE.md`, `docs/index.md`, `docs/getting-started/` and `docs/development/`). `scripts/docs-serve.sh` exists. *The doc set never tells a contributor the command their PR must pass — add it.*
- **W-M72** `docs/index.md` reaches a minority of the site: 13 markdown links; the Development section lists 3 of 10 pages; the entire Integrations section (5 files) and `api/service-accounts.md` are unreachable. Root `CLAUDE.md` describes the repo as a monolith "plus a TypeScript BFF SDK", so omitting the SDK and BFF docs from the landing page misrepresents the product. **Corrections: `docs/index.md` has 13 links, not 12; `toc.yml` has 37 hrefs and 37 files, not "32 entries"/"30 pages".** *Add an Integrations section. Do not attempt "completeness" — every section is a curated three-item highlight list by design.*
- **W-M73** Branding is documented three times and the copies disagree. `branding.json` has exactly seven top-level keys; `configuration.md:26-33` lists all seven correctly; `frontend-setup.md:632-634` calls its five-item list "the **canonical** branding schema", omitting `repositoryUrl` and `docsUrl`, and the example JSON seven lines above at `:616-627` omits those two **plus** `landingPage`, contradicting the sentence below it. *Make configuration.md canonical and link.*
- **W-M74** Five ports tables disagree on membership and none carries the docs site: `developer-guide.md:92-106`, `configuration.md:~655-662`, `onboarding.md:47-53`, `onboarding.md:194-206`, `frontend-setup.md:511-518`. Two of the five are inside `onboarding.md` alone and differ from each other (`:194-206` adds Hangfire and AsyncAPI; `:47-53` does not). *Keep one table; link the rest.*
- **W-M75** `configuration.md:16` claims "This section documents all configuration sections used by Wallow" and `fork-guide.md:349` calls it "the full reference", but `configuration.md` has no `FeatureManagement`, `Plugins` or `OpenIddict`/`Authentication` section — all of which `fork-guide.md` itself documents. *Either complete it or drop the completeness claim.*

**docs/ — architecture and api**

- **W-M76** `docs/architecture/messaging.md:107` and `:92` — cite `AccountController.cs` lines 536 and 726; `EmailVerificationRequestedEvent` is published at **572** and **768** (off by 36 and 42). Method spans: `CompleteExternalRegistration` 438-595, `Register` 698-779. Line 536 is inside `CompleteExternalRegistration` but publishes a different event; 726 is in `Register`'s placeholder-name logic. *Correct both.*
- **W-M77** `docs/architecture/assessment.md` covers 5 of 7 modules while claiming all seven. §3 (`:121-166`) names Notifications, Announcements, Storage, Inquiries and Identity; the §4 tier tables (`:170-191`) score exactly those five; ApiKeys and Branding appear nowhere though both have real domain code. The footer at `:391` claims coverage of all seven. *Either add the two or scope the claim.*
- **W-M78** `docs/architecture/assessment.md` is an undated, unrubriced review published as evergreen guidance — no date, no `status:` line, no disclaimer; `:1-4` goes straight from the title into "This document assesses…". It carries Executive Summary scores at `:9-13`, "Key Gaps & Recommendations / Priority: High" at `:378-382` and "Bottom Line" at `:385-387`, and no section defines the scale, criteria or assessor. `docs/toc.yml:15-16` lists it as the **first** Architecture entry, and `docs/index.md:14` describes it as "design decisions and trade-offs", which reads like an ADR rather than a graded review. *Date it, state the rubric, or move it out of the evergreen Architecture section.*
- **W-M79** `docs/architecture/assessment.md:238-252` — the decision tree is a strict binary ("Is this module wrapping an external system? YES → External Adapter / NO → Traditional DDD"); there are **three** patterns. `api/CLAUDE.md:81-83` states the exception: "Branding and ApiKeys deliberately use direct service/repository-from-controller (no CQRS/Wolverine)." Code agrees — `Wallow.Branding.Application/` has only `DTOs/` + `Interfaces/`; `Wallow.ApiKeys.Application/` only `Interfaces/`; neither has Commands/, Queries/ or EventHandlers/. *Add the third branch.*
- **W-M80** `docs/architecture/module-creation.md:428` — Step 10's test command silently fails for a new module. `resolve_filter()` (`scripts/run-tests.sh:16-40`) is a hardcoded `case` with a default `*) echo "$filter"`, so a new module name is passed to `dotnet test` as a path and fails to resolve. The stated test-project location matches the real layout. *Document adding a new `case` arm.*
- **W-M81** `IJobScheduler` is mislocated in **two** documents, not one: `docs/architecture/module-creation.md:489` and `docs/architecture/assessment.md:197` ("| **Background Jobs** | `Shared.Infrastructure.BackgroundJobs/` | `IJobScheduler` over Hangfire. |"). It lives only at `Shared/Wallow.Shared.Kernel/BackgroundJobs/IJobScheduler.cs`; `Shared.Infrastructure.BackgroundJobs/` holds only `BackgroundJobsExtensions.cs` and `HangfireJobScheduler.cs`. `background-jobs.md:32` gets it right. *Fix both.*
- **W-M82** `docs/architecture/authentication.md:138` — the exchange-ticket snippet predates the `clientId` parameter and uses a constant that does not exist. Real signature: `buildExchangeTicketUrl(origin, ticket, returnUrl: string | AllowListedReturnUrl, clientId?: string)` (`packages/sdk/src/auth-oidc.ts:241-246`, documented at `:235-236`); `AccountController.cs:598-602` takes `[FromQuery] string? clientId = null`; `MfaChallengeForm.tsx:261-262` branches on `scopedClientId`. The constant is `SAME_ORIGIN_BASE` throughout `apps/wallow-auth` (7 files), never `SAME_ORIGIN`. *Update the snippet.*
- **W-M83** Three documents give three answers on handler shape, and the reference module contradicts the guide. Enumeration under `api/src/Modules`: **30** `public static class …Handler` and **54** `public sealed class …Handler(` primary-constructor declarations. **The real boundary is by folder, not by message kind**: every handler under `EventHandlers/` (or `Identity/…/Handlers/`) is static with method injection; every `Commands/` + `Queries/` handler across Identity, Announcements, Storage and Notifications is an instance primary-ctor class — **except Inquiries, whose three `Commands/` handlers (SubmitInquiryHandler, AddInquiryCommentHandler, UpdateInquiryStatusHandler) are static**, while its four `Queries/` handlers are instance classes. Since `module-creation.md:12` nominates Inquiries as the reference implementation, a reader who follows the guide and a reader who copies the reference write different code. `module-creation.md` is also internally inconsistent: its command sample at `:207-217` is an instance primary-ctor class, its domain-event sample at `:252-266` is static with method injection, and the Pre-PR checklist at `:527` says "Handlers use primary constructor pattern" flatly. ***E-BP-01's stated split is wrong — do not reuse it, and the fix cannot point at Inquiries as the example.***
- **W-M84** Two documents name two different reference-implementation modules with no reconciling reading: `module-creation.md:12` and `:539` name Inquiries; `assessment.md:15`, `:176` and `:387` name Notifications ("Reference implementation", "**Use Notifications as your template**"). `assessment.md:191` simultaneously scores Inquiries 7/10 in Tier 3. Both sit under Architecture in `docs/toc.yml`, with assessment listed first. *Pick one.*
- **W-M85** `docs/architecture/module-creation.md`'s Pre-PR checklist covers none of visibility/`ServiceLocationPolicy`, enum-as-string, no-`var`, `[LoggerMessage]`, `partial` controllers, or `dotnet format`, and `:298-306` lists six entity-configuration conventions with no enum rule. **Correction: the checklist has 19 items, not 17 — do not repeat E-BP-08's count.** *Add the missing conventions.*
- **W-M86** `docs/architecture/authorization.md` is server-only: **zero** occurrences of `bc-solutions-coder/auth`, `hasRole`, `hasPermission` or `useCurrentUser`, and no Related Documentation section. The casing asymmetry is real in code — `packages/auth/src/authorization.ts:26-32` lowercases both sides of the role comparison, while `:45-51` does a plain `includes(permission.trim())` — and `packages/auth/CLAUDE.md:23-24` and `:91-96` document it. `authorization.md:325` states only the role half ("comparison is case-insensitive"). *Add the client half and the permission-casing caveat.*

**docs/ — integrations and operations**

- **W-M87** `docs/operations/audit-events.md:19` — names migration class `InitialAuthAudit`; `grep -rn InitialAuthAudit` over the repo returns only that line. Real: `api/src/Shared/Wallow.Shared.Infrastructure.Core/Migrations/AuthAudit/20260801104059_InitialCreate.cs`, `public partial class InitialCreate`. *Rename.*
- **W-M88** `docs/operations/audit-events.md:9-17` — the column table marks `tenant_id` non-nullable and has no `actor_id` row; the migration declares both `nullable: true`, and `ActorId` is load-bearing (only `Handle(MembershipTransitionedEvent …)` populates it). ***The scout's "roughly lines 20-30" is wrong — the table is `:9-17`; `:19` is the migration sentence.*** *Fix nullability, add the row.*
- **W-M89** `docs/operations/audit-events.md:25-39` — both event-type tables omit the membership family; the fifth handler in `AuthAuditEventHandlers.cs` writes `EventType = $"Membership{message.Transition}"`, and its doc comment says the transition is deliberately spelled into the event type "so a membership decision is queried the same way every other audited event is". *Fix together with W-M88 — the same handler supplies `ActorId`.*
- **W-M90** `docs/integrations/typescript-sdk.md:347` (**not `:348` — the finding is off by one**) — shows the CSRF companion cookie as `wallow_bff-csrf`. `packages/sdk/src/csrf.ts:15-19`: the name is `${cookieName}-csrf` and it also gains a `__Host-` prefix whenever the session cookie is Secure. *Correct.*
- **W-M91** `docs/operations/troubleshooting.md:280-283` — prints `Wallow.Shared.Kernel.MultiTenancy.TenantNotResolvedException` as the indexed symptom; `grep -rn "TenantNotResolvedException" api/` returns **nothing**. The Diagnosis line at `:286` (`ITenantContext.IsResolved`) is the real observable. *Re-index the entry on the real symptom.*
- **W-M92** `docs/integrations/integration-cookbook.md:63` — passes a `tanstackStart` option `customViteReactPlugin` that appears nowhere else in the repo (a single grep hit, that line). All three apps (`wallow-web/vite.config.ts:65`, `wallow-auth:89`, `minimal-app:41`) and `frontend-setup.md:248` pass ``router: { routeFileIgnorePattern: String.raw`\.(test|spec)\.(ts|tsx)$` }`` instead — **the omission is what bites**, more than the invented option. *Replace it.*
- **W-M93** `docs/operations/deployment.md:325` vs `:339` — the prose says `SESSION_TTL_SECONDS`, the table says `BFF_SESSION_TTL_SECONDS`. `docker-compose.production.yml:610` is `SESSION_TTL_SECONDS: ${BFF_SESSION_TTL_SECONDS:-}` — both names are right in their own layer and the page never says so. *Say so.*
- **W-M94** `docs/operations/deployment.md:294` — the Observability row lists only `GF_ADMIN_PASSWORD`, omitting `OTEL_TRACE_SAMPLING_RATIO`. `.env.production.example:307` sets `0.1`; `docker-compose.production.yml:435` consumes it as `OpenTelemetry__TraceSamplingRatio: ${OTEL_TRACE_SAMPLING_RATIO:-0.1}`; `observability.md:480` sends readers to `.env.production` to tune it. *Add the row — a silent 90 % trace drop is worth a line.*
- **W-M95** `docs/operations/request-correlation.md:65` — shows the error body with `traceId` nested under `"extensions"`; the API emits it **flattened** (`GlobalExceptionHandler.cs:39` and `:104-107` set `Extensions["traceId"]`, and `ProblemDetails.Extensions` carries `[JsonExtensionData]`). `observability.md:248` shows the correct flattened form. ***Nuance: `packages/sdk/src/server/errors.ts:182-189` and `runtime-config.ts:160-168` deliberately PREFER `extensions.traceId` and probe the flattened form only as fallback — a fixer must NOT "correct" the SDK on the strength of this finding.*** *Fix the doc only.*
- **W-M96** `docs/integrations/bff-pattern.md:893` — asserts the Auth app serves `/connect/authorize` while the API serves token/userinfo/logout "on separate origins". `apps/wallow-auth/src/app/routes/connect/` contains a single `$.ts` splat, and `api-passthrough.server.ts:7` documents "The three splat server routes (`/v1/$`, `/connect/$`, `/.well-known/$`)" — the auth app fronts the whole namespace, as the same file's table row at `:888` already partly says. *Correct `:893`.*
- **W-M97** `docs/operations/troubleshooting.md` is .NET-only in a polyglot monorepo: §5 Test Failures covers Testcontainers, parallel xUnit conflicts, SignalR tests and integration-test auth; §6 Build Issues covers NuGet restore, project references and assembly conflicts. Nothing on `pnpm check`, turbo cache, Vitest browser mode, or the `lint`/`lint:tests` partition. *Add a frontend section. (Minor: the finding says "eight top-level sections"; there are seven numbered plus Quick Reference and Getting Help — immaterial.)*
- **W-M98** `docs/operations/observability.md` — its Related Documentation section lists exactly three links (Developer Guide, Deployment Guide, Messaging Guide) and never mentions `docs/development/logging.md`; the only frontend touch on the page is the port-4318 note at `:457`. *The area-F edit is to add the link; the detail itself may belong in `logging.md`.*

### 3.4 Low

- **W-L1** `README.md:7`, `:9`, `:27` — "production-ready … Fork it. Add your domain modules. Deploy." vs root `CLAUDE.md`'s Deployment Status ("never been deployed anywhere except locally"). Textual conflict; severity is editorial.
- **W-L2** `README.md:175-183` (8 rows: Scalar `:5001/scalar/v1`, Mailpit 8025, GarageHQ 3900, Grafana 3001) vs root `CLAUDE.md`'s Local Development table (4 rows plus a delegation). The four shared rows agree. *Delegate rather than duplicate.*
- **W-L3** Agent files restate rules the CLAUDE.mds own — module roster in 6 places, layer order in 5, no-`var` in 5, the XML `--` rule in 2. Structural observation; the empirical argument is that W-H9, W-H12 and W-H13 **are** this duplication having diverged.
- **W-L4** `.claude/rules/TEAMS.md` — uses "scout"/"synthesizer" with no Wallow definition; "synthesizer" appears nowhere else in the repo and "scout" resolves only via a user-level `~/.claude/agents/bead-scout.md`. Its six lines contain no repo reference, unlike its three siblings.
- **W-L5** `docs-serve.sh` appears only in `.claude/agents/docfx-specialist.md:51-52`; root `CLAUDE.md` publishes http://localhost:5004 with no command, and `package.json`'s 20 scripts include no docs script.
- **W-L6** `.claude/rules/CONVENTIONS.md` (9 lines) delegates C# conventions to `api/CLAUDE.md` at `:8-9` yet keeps the XML `--`/MSB4025 rule at `:3-5`, which is an MSBuild/C# convention by its own logic and is restated at `code-reviewer.md:47`. No TypeScript-side pointer anywhere.
- **W-L7** `Identity/README.md`'s permission list reads as exhaustive; `PermissionType.cs` has **39** consts (the reviewer's "roughly 35" undercounts).
- **W-L8** No file in the 21-file area B set links to any other (`grep -rn "]("` → zero); README/CLAUDE.md pairs never reference each other in any form.
- **W-L9** Global C# conventions are restated per module: `ApiKeys/CLAUDE.md:29` and `:37`, `Inquiries/CLAUDE.md:31`, `Branding/CLAUDE.md:45`, `Announcements/CLAUDE.md:35`, `Notifications/CLAUDE.md:49`. (Nit: `Branding/CLAUDE.md:41` is source-generated **regex**, not `[LoggerMessage]`; `api/CLAUDE.md:102` covers both.)
- **W-L10** Six module docs use six different `##` heading vocabularies; only Announcements (`:40`), Identity (`:52`) and Inquiries (`:53`) carry a `Permissions` section.
- **W-L11** `--` / `-` used as an em dash. Titles: ApiKeys `--`, Branding `-`, Notifications `-` (Announcements, Identity and Inquiries use `—`). Body ` -- ` counts: `ApiKeys/CLAUDE.md` 4, `Branding/CLAUDE.md` 4, `ApiKeys/README.md` 2, `Inquiries/README.md` 1. ***Drop `Branding/README.md` and `Notifications/CLAUDE.md` from the sweep — both are clean (zero occurrences).***
- **W-L12** `api/src/Shared/README.md` duplicates its children's content wholesale (parent Kernel 15-41 vs child 9-53; parent Contracts 43-73 vs child 9-54); the parent's Identity event list is 5 against the child's 11, and it drops the child's Event Design Rules. (Nit: "parent lists 5 settings types, child 6" is loose — parent `:81` names three types plus "repository implementations".)
- **W-L13** `packages/sdk/CLAUDE.md:70` — carries a stale parenthetical "(No tsup; the README is stale)", the only `tsup` hit in the whole package. `packages/sdk/README.md:449-454` already describes the build correctly (Vite 8 library mode + `tsc -p tsconfig.build.json`, "There is no separate bundler config").
- **W-L14** `packages/sdk/README.md:360` — imports `useMutation, useQuery, useQueryClient` from `@tanstack/react-query` directly, where `apps/minimal-app/README.md:120-133` carries the same snippet in facade form with the gloss "An app never depends on `@tanstack/react-query` itself: only the facade does". ***The snippet is correct for an external consumer — caveat it, do not change it.***
- **W-L15** `apps/minimal-app/README.md:78-85` — the "Owns" list gives only `src/routes/`, `src/start.ts`, `src/router.tsx`; the app also has `src/features/hello/HelloCard.tsx` (plus spec) and `src/components/ready-indicator.tsx`. `apps/minimal-app/tsconfig.json` has no `paths` key, so `packages/lint/CLAUDE.md:160-166`'s un-zoned reasoning is correct — "un-zoned" means "no alias map", not "no feature folders".
- **W-L16** `apps/CLAUDE.md` — uses "catalog" for two different things within fourteen lines: pnpm version catalogs at `:73-76` ("the **`start` catalog** in `pnpm-workspace.yaml`", "the sibling **`react` catalog**") and the `@bc-solutions-coder/ui` component library at `:80` and `:86`.
- **W-L17** `packages/env/CLAUDE.md:18` — lists `INTERNAL_ORIGIN_ENV_KEY` in its table but never spells its value, so the doc cannot disambiguate `WALLOW_WEB_INTERNAL_URL` (the self-origin, `packages/env/src/internal-origin.ts:25`) from `WALLOW_API_INTERNAL_URL` (the upstream API, used in `apps/minimal-app/src/lib/api-passthrough.ts:35` and both `playwright.config.ts` files). Nothing here is factually false.
- **W-L18** `apps/wallow-auth/` has no root `README.md` and no `CLAUDE.md` at any spelling, though `apps/wallow-web/README.md` exists and both apps have an `e2e/CLAUDE.md`. Structural, not a doc-accuracy defect.
- **W-L19** Four sites tell the reader `cd docker && docker compose up -d`; `pnpm backend:infra` is defined as exactly that.
- **W-L20** The wallow-auth e2e suite has **three** helper modules, not two — `global-setup.ts` alongside `mailpit.ts` and `totp.ts`.
- **W-L21** `docs/development/frontend-setup.md:200-218` and `:566-592` — repeat the three-line CSS entry, the same `@source "./"` rationale and the same "do not emit a stylesheet `<link>` from `head()`" warning almost verbatim.
- **W-L22** `docs/CLAUDE.md:23-27` — "Adding a New Guide" is three steps ending at "use standard markdown", with no verification step; `scripts/docs-serve.sh` exists and is never named.
- **W-L23** `docs/architecture/module-creation.md:12` and `:539` — cross-reference in-repo code by absolute GitHub URL (`https://github.com/bc-solutions-coder/wallow/tree/main/api/src/Modules/Inquiries`) where `docs/CLAUDE.md`'s Rules require relative paths. The URL is valid, so this is a convention/fork-correctness finding, not a broken link.
- **W-L24** Only 2 of the 10 area-E files have a `## Related Documentation` footer — `caching.md` and `messaging.md`; the other eight have none.
- **W-L25** `docs/architecture/caching.md:66-68` and `docs/architecture/file-storage.md:101-103` — bypass `pnpm backend:infra`. `package.json:7` defines `backend:infra` as exactly `cd docker && docker compose up -d` (`:8` is the `:down` counterpart), so caching.md's form is an undocumented duplicate. file-storage.md's is `cd docker && docker compose --profile clamav up -d` and there is **no pnpm wrapper for the profile**, so its split recommendation is correct.
- **W-L26** `docs/api/service-accounts.md:42`, `:53`, `:65` — use `https://api.yourplatform.com` and the file has **zero** `localhost:5001` occurrences, while `authorization.md:180`/`:289` and `background-jobs.md:27` use `http://localhost:5001`. ***Take only the base-URL change*** — the Diátaxis/placement half is editorial, and `docs/CLAUDE.md` explicitly sanctions the location ("`api/` — API reference docs (service accounts)").
- **W-L27** `docs/architecture/module-creation.md:17` and `:539` — list the modules in a different order (Identity, **Branding**, Storage, Notifications, Announcements, Inquiries, ApiKeys) from the standard used by `messaging.md:7-8`, `authorization.md:146`, `assessment.md:391` and `api/CLAUDE.md:70`. ***Correction: this is two distinct orders across six sites, not "five places in five orders" — four of six already agree. Two lines in one file to reorder.***
- **W-L28** `:sha` vs `:<short-sha>` — `deploy.yml:279` computes `SHORT_SHA="${SHA_TAG:0:7}"` and pushes `:nightly` and `:${SHORT_SHA}`. `versioning.md:69` and `:123` write `:sha`; `deployment.md:497`, `:514`, `:526` write `:<short-sha>`. ***Context: the workflows' own header comments say `:sha` too (`deploy.yml:5`, `publish.yml:6-8`), so the doc inherited the shorthand rather than inventing it.***
- **W-L29** `docs/integrations/typescript-sdk.md:4` — links the package **name** to the Versioning guide instead of to the SDK's own reference.
- **W-L30** `docs/operations/reverse-proxy.md:123-138` — shows `PORT` plus eight OIDC/BFF variables under "seven required BFF variables". The count is **correct** (`loadBffConfigFromEnv` calls `requireEnv` exactly seven times — `config.ts:300-305` plus the `COOKIE_PASSWORD` branch at `:314`); the presentation is ambiguous because the optional `OIDC_METADATA_URL` sits unmarked inside the block. *Mark it optional.*
- **W-L31** `docs/integrations/integration-cookbook.md:63` — writes `wallowStyles()` un-spread; `packages/styles/src/vite.ts:94` returns `PluginOption[]`, and all three apps plus `frontend-setup.md:252` write `...wallowStyles()`. Cosmetic — Vite flattens.

---

## 4. Fix batches

Eight batches. **File ownership is disjoint: every file below has exactly one owning batch.** Agents
can run in parallel without coordination. Each batch names its "do not correct" traps because the
finder files contain wrong numbers that a fixer would otherwise propagate.

### Batch 1 — Module READMEs and CLAUDE.mds under `api/src/Modules/`

**Owns:** `Identity/README.md`, `Identity/CLAUDE.md`, `Identity/Wallow.Identity.Api/README.md`,
`Identity/…/Wallow.Identity.Tests/README.md`, `Announcements/README.md`, `Announcements/CLAUDE.md`,
`ApiKeys/README.md`, `ApiKeys/CLAUDE.md`, `Branding/README.md`, `Branding/CLAUDE.md`,
`Inquiries/README.md`, `Inquiries/CLAUDE.md`, `Notifications/README.md`, `Notifications/CLAUDE.md`,
`Storage/README.md`.
**Findings:** W-C1 (module half), W-C8, W-H1, W-H2, W-H4, W-H5, W-H6 (module half), W-H7, W-H8
(Storage:140), W-M17, W-M18, W-M19, W-M21, W-M25, W-M27, W-M28, W-M30, W-M32 (module half), W-M33,
W-M34, W-M36 (Storage half), W-L7, W-L8, W-L9, W-L10, W-L11.
**Do not "correct" these — they are verified correct:**
- `Wallow.Identity.Api/README.md:115-122`'s `/connect/*` table.
- `Identity/README.md:130`'s `/me` under the Users section (`UsersController.cs:73` is `[HttpGet("me")]`).
- `Branding/README.md` and `Notifications/CLAUDE.md` for ` -- ` — both have zero occurrences.
- `ApiKeys/README.md:44` (correct permission), `ApiKeys/CLAUDE.md:24` (correct mapper location),
  `Branding/CLAUDE.md:36` (correct events claim).
- Each module's `HasDefaultSchema` matches its documented schema; no module `.csproj` references
  another module; `Wallow.Shared.Contracts` has no Branding namespace; neither Identity nor ApiKeys
  has an `EventHandlers/` directory.
- Notifications' "22 handlers" is loose but not wrong (22 handler files, 17 documented events, so
  not 1:1); the consumer-only claim holds.

### Batch 2 — `api/` top-level, Shared, and test-project docs

**Owns:** `api/CLAUDE.md`, `api/src/Wallow.Api/README.md`, `api/src/Shared/README.md`,
`api/src/Shared/Wallow.Shared.Contracts/README.md`, `api/tests/Wallow.Tests.Common/README.md`.
**Findings:** W-C2 (`api/CLAUDE.md:94` half), W-H3, W-H6 (`Wallow.Api/README.md:87`), W-H8
(`Wallow.Api/README.md:90-95`), W-M20, W-M22 (`api/CLAUDE.md` half), W-M23, W-M24, W-M26, W-M29,
W-M31, W-M32 (`api/CLAUDE.md` half), W-M35, W-M36 (`api/CLAUDE.md:62-63` half), W-L12.
**Do not "correct" these:**
- `api/CLAUDE.md:29` — it reads "(E2E is per-app Playwright now — see .claude/rules/E2E.md)" and
  names no suite. Only `:151-152` needs the fix.
- `api/CLAUDE.md:64` and `Shared/README.md:88-90` on Plugins — both correct.
- `api/src/Shared/README.md:28` (`…Plugins`) — real. Only `:7` and `:27` (Workflows) are wrong.
- `Wallow.Api/README.md`'s `ApiVersionRewriteMiddleware` description (`/api/foo → /api/v1/foo`) is
  true **only** under a reverse-proxy topology that is off by default and never mentioned — it needs
  the caveat, not deletion, and not a "leave as is".
- Verified correct: `api/Wallow.slnx` exists; all seven modules have the four-project stack;
  `api/seed.json` roles admin/manager/user; `coverage.runsettings` excludes
  migrations/Program/Startup/generated; `ci.yml:579` `MIN=90`; `PersistMessagesWithPostgresql` at
  `Program.cs:245`; durable inbox/outbox at `Program.cs:303-304` (**not 298**).
- Area B files do not ship to the docs site (`docfx.json`'s content block is `src: docs`), so
  `docs/CLAUDE.md`'s kebab-case/toc/relative-link rules do not govern here.

### Batch 3 — Root governance, `.claude/`, `.beads/`

**Owns:** `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, root `CLAUDE.md`, `.oxlintrc.json`,
`.claude/rules/TESTING.md`, `.claude/rules/CONVENTIONS.md`, `.claude/rules/TEAMS.md`,
`.claude/rules/E2E.md`, `.claude/agents/*.md` (all six), `.beads/README.md`.
**Findings:** W-H9, W-H10, W-H11, W-H12, W-H13, W-H14 (README + CONTRIBUTING halves), W-H15 (root
`CLAUDE.md` + `.oxlintrc.json` halves), W-H16, W-M1, W-M2 (`SECURITY.md` half), W-M3, W-M4, W-M5,
W-M6, W-M7, W-M8, W-M9, W-M10, W-M11, W-M12, W-M13, W-M14, W-M15, W-M16 (root `CLAUDE.md` +
`CONTRIBUTING.md` halves), W-M53, W-L1, W-L2, W-L3, W-L4, W-L5, W-L6.
**Citation warning — read before touching `.claude/agents/`:** every `.claude/agents/*.md` line
number in the Area A **reviewer** findings file is fabricated. Corrected map:
`enterprise-architect.md` :671→**66**, :642→**37**; `csharp-developer.md` :285-286→**148-149**,
:150→**13**; `docfx-specialist.md` :383→**29**; `code-reviewer.md` :92→**90**, :46→**45**.
File lengths: code-reviewer 135, csharp-developer 216, docfx-specialist 77,
dotnet-benchmark-designer 89, dotnet-concurrency-specialist 82, enterprise-architect 123.
**Do not "correct" these:**
- `docfx.json`'s dead `claude/**` glob — both lenses call it a non-defect. Its `audits/**` exclude
  is why this audit correctly never ships.
- All 20 `package.json` scripts and the `check` composition; `.nvmrc`=24; `packageManager`
  pnpm@10.20.0; `.gitattributes` `merge=ours` on exactly appsettings\*.json, branding.json,
  docker/.env, docker/.env.example, seed.json; `.claude/agents/` has exactly 6 files;
  `scripts/run-e2e.sh` absent and `scripts/e2e.sh` present with all three e2e dirs;
  postgres:18-alpine; `knip.json`'s ignore list is exactly 3 paths and root `ignoreDependencies` is
  `@arethetypeswrong/cli` + `publint`; API 5001 (`launchSettings.json:7`), docs 5004
  (`docker-compose.yml:188`, `docs-serve.sh:8`); Scalar at `Program.cs:500-508`.
- `.claude/rules/E2E.md` is accurate as written.
- e2e locators: **97** `getByTestId` occurrences and zero other `getBy*` — the reviewer's "74" is
  wrong; the testid-only conclusion holds.
- **A-BP-11's "seven oxlint configs" is wrong — the answer is six.**

### Batch 4 — Frontend app and package docs

**Owns:** `apps/CLAUDE.md`, `apps/wallow-web/README.md`, `apps/minimal-app/README.md`,
`apps/wallow-auth/e2e/CLAUDE.md`, `apps/wallow-web/e2e/CLAUDE.md`, `packages/sdk/README.md`,
`packages/sdk/CLAUDE.md`, `packages/env/CLAUDE.md`, `packages/logger/CLAUDE.md`,
`packages/testing/CLAUDE.md`, `packages/navigation/CLAUDE.md`, `packages/config/CLAUDE.md`,
`packages/ui/CLAUDE.md`, `packages/forms/CLAUDE.md`, `packages/lint/CLAUDE.md`,
`packages/query/CLAUDE.md`, `packages/auth/CLAUDE.md`, `packages/utils/CLAUDE.md`,
`scripts/fork-smoke/README.md`.
**Findings:** W-C7, W-C9, W-H15 (`packages/lint/CLAUDE.md` half), W-H17, W-H18, W-H19, W-H20,
W-H21, W-H22, W-M37 through W-M52, W-M54, W-L13, W-L14, W-L15, W-L16, W-L17, W-L18.
**Do not "correct" these:**
- **Do not copy** reviewer 10's "six browser projects", C-BP-07's "eight `createVitestProjects`
  consumers", or its two-item unwired set. Definitive: **8 browser projects, 3 unwired, 7 real
  `createVitestProjects` consumers, 6 declared `@bc-solutions-coder/testing` dependents.**
- **Do not copy** reviewer 18's "roughly 70 % of checkable integers had drifted" — it rests on a
  sample of seven, two of which the reviewer itself counted wrong. Its two correct data points DO
  hold: 60 component folders under `packages/ui/src/components/`, exactly five lacking stories.
- **Do not repeat** reviewer 1's "grep for `setSsrRequestContextResolver` returns nothing" — it
  survives in `.oxlintrc.json:66,191,263`, a plan, and `packages/sdk/README.md:344`.
- `packages/lint/CLAUDE.md:62`'s "the three package configs" is **correct** in its context (the
  shell-extraction paragraph = ui/forms/navigation).
- `packages/sdk/README.md:360`'s direct react-query import is correct for an external consumer.
- Verified correct: the 13 `packages/*` names; `packages/env`'s three subpaths; ports 3000/3002/3010;
  the absence of `run-e2e.sh` and `Wallow.E2E.Tests`; `packages/query/src/`'s four files.

### Batch 5 — `docs/` getting-started and development

**Owns:** `docs/CLAUDE.md`, `docs/index.md`,
`docs/getting-started/{developer-guide,fork-guide,onboarding,configuration}.md`,
`docs/development/{testing,testing-e2e,frontend-setup,component-library,forms,frontend-state,database-development,database-migrations,api-development,logging}.md`.
**Findings:** W-C2 (developer-guide + fork-guide + onboarding halves), W-C5 (fork-guide +
developer-guide halves), W-H14 (fork-guide half), W-H23 (fork-guide half), W-H24, W-H25, W-H26,
W-M2 (fork-guide half), W-M22 (testing.md + developer-guide halves), W-M44 (forms.md half), W-M55
through W-M75, W-L19, W-L20, W-L21, W-L22.
**Do not "correct" these:**
- **`docs/toc.yml` is COMPLETE in both directions** — 37 `.md` hrefs, 37 non-excluded `.md` files on
  disk, scripted `comm` both ways returns empty. Finder counts of "32 entries"/"30 pages" are wrong;
  the clean verdict is right. **Do not touch `toc.yml`.**
- **REFUTED:** the module-flag example at `fork-guide.md:325-331` does **not** omit Identity and
  Branding — it includes `"Modules.Branding": true` and `"Modules.Identity": true`.
- **Do not adopt** D-BP-06's Diátaxis restructuring proposal, or "delete onboarding's
  architecture/testing/FAQ sections" — editorial, not verifiable defects. Keep only the
  promoted-broken-start-path and two-internal-ports-tables halves.
- **Do not adopt** D-BP-21's "zero findings" framing for `api-development.md` (it carries W-M70) or
  `frontend-state.md` (subject of W-M64). Its sourcing-discipline observation at
  `api-development.md:3-5` is worth keeping.
- `docs/index.md` was mis-cleared by one reviewer — it **is** in scope (Dapper at `:20`, plus W-M72).
- `docs/CLAUDE.md`'s Structure block: the architecture/ and development/ lines are **complete** (9
  and 10, matching disk). It omits exactly two files.
- `docs/development/database-migrations.md:286` and `:68-88` are correct — copy from them.
- Verified correct: `docfx.json`'s excludes; the `.gitattributes` `merge=ours` set; the wallow-auth
  e2e spec list; the root command inventory.

### Batch 6 — `docs/architecture` and `docs/api`

**Owns:** `docs/architecture/{module-creation,assessment,authentication,authorization,background-jobs,caching,file-storage,messaging,realtime}.md`,
`docs/api/service-accounts.md`.
**Findings:** W-C1 (module-creation + authorization + authentication halves), W-C2
(module-creation half), W-C5 (module-creation half), W-C6, W-H23 (assessment half), W-H27, W-H28,
W-H29, W-H30, W-M32 (architecture half), W-M36 (messaging half), W-M76 through W-M86, W-L23, W-L24,
W-L25, W-L26, W-L27.
**Do not "correct" these:**
- `background-jobs.md`'s five cron expressions are correct (re-checked against `Program.cs:609-637`:
  `*/5 * * * *`, `*/5 * * * *` feature-gated, `0 */4 * * *`, `0 * * * *`, `Cron.Daily()`), as are
  its job file paths — but the file is **not** zero-findings, see W-H30.
- `background-jobs.md:32` locates `IJobScheduler` correctly.
- `authorization.md:272` and `:289` (`/v1/identity/mfa/admin/{userId}/clear-lockout`) are correct.
- `file-storage.md:115-117` states the route rule correctly — copy it for W-C1.
- `authentication.md`'s external-OAuth ticket bullet is not contradicted by any call site.
- `assessment.md`'s per-module content claims are sound (`RetentionPolicy.cs`, `EmailAddress.cs`,
  `EmailContent.cs` all exist at the stated paths) — only coverage and the missing date are wrong.
- **Do not repeat:** E-BP-08's "17 checklist items" (it is **19**), E-BP-17's "five orders" (**two**),
  E-BP-01's message-kind handler split (the boundary is by **folder**), E-3's "32 route attributes"
  (**31**), or E-BP-20's "Low" severity for the non-compiling walkthrough.
- **Do not adopt** E-BP-22's uneven-depth complaint as a fix-phase item — editorial; nothing in
  those sections is wrong.

### Batch 7 — `docs/operations` and `docs/integrations`

**Owns:** `docs/operations/{audit-events,deployment,observability,reverse-proxy,troubleshooting,versioning,request-correlation}.md`,
`docs/integrations/{external-auth,asyncapi,bff-pattern,typescript-sdk,integration-cookbook}.md`.
**Findings:** W-C1 (external-auth half), W-C3, W-C4 (deployment.md half), W-H31 through W-H38,
W-M16 (versioning.md half), W-M2 (versioning.md half), W-M87 through W-M98, W-L28, W-L29, W-L30,
W-L31.
**Do not "correct" these — all three are REFUTED:**
- **F-R-23** `createApiProxy` one-arg vs two-arg: `packages/sdk/src/server/proxy.ts:676-682` gives
  `store` a default (`store: SessionStore = new CookieSessionStore({ … })`), so both forms compile.
  `typescript-sdk.md:301-305` and `bff-pattern.md:554-555` already say so. **Nothing to fix.**
- **F-BP-16** Valkey image tag: `deployment.md:76` and `docker-compose.production.yml:142` are both
  `valkey/valkey:8.1-alpine`. The `8-alpine` at `troubleshooting.md:600` is a
  Testcontainers/`docker pull` context matching `docker/docker-compose.yml` (dev) and
  `caching.md:48,179`. Two stacks, both accurate. **No divergence.**
- **F-BP-18** `deployment.md:251-254`'s "two things" is two colon-separated **categories** (the SMTP
  password; the deployment-identity values), mirroring `scripts/prod-secrets.sh:15-21`'s own "Two
  kinds of value". The doc is not miscounting. Residue worth one sentence only: the parenthetical
  omits `AUTH_PUBLIC_URL`, `WEB_PUBLIC_URL`, `SMTP_HOST` and `SMTP_FROM_ADDRESS`, which the script's
  output block (`:126-128`) also prints. ("13 generatable secrets" is correct.)
- The reviewer's **withdrawn** `getCurrentUser` finding must not reappear —
  `packages/sdk/src/auth-extras.ts` exports it.
- Verified correct: `trivy-action@v0.36.0` (`publish.yml:109`); the `docker buildx imagetools create`
  retag path; the 30-minute polling window; grafana-lgtm `127.0.0.1:3001:3000` (`:247`); API →
  `http://alloy:4317` (`:431`); wallow-auth/web → `http://alloy:4318` (`:517`, `:619`); all six
  observability C# source paths; the four Grafana dashboards; the troubleshooting Quick-Reference
  error-code table `:905-915` (matches `GlobalExceptionHandler.cs:62-93`); `docs/toc.yml:56-81`
  registers all twelve area-F pages; `OTEL_TRACE_SAMPLING_RATIO=0.1` at `.env.production.example:307`;
  the AsyncAPI unpkg pin `@asyncapi/react-component@3.0.2` with SRI
  (`AsyncApiEndpointExtensions.cs:45,49`); `bcordes-bff` at `api/seed.json:221`; `depends_on: - alloy`;
  the release-please two-component manifest structure.
- `bff-pattern.md:486` and `:490` (cookie rotation) and `deployment.md:337-338` (derived cookie name)
  are **correct** — they are the sources to copy from, not targets.

### Batch 8 — Shipped config files (not docs)

**Owns:** `docker/.env.production.example`, `docker/docker-compose.production.yml`.
**Findings:** W-C4 (config half — the `{"k2":…,"k1":…}` rotation example at
`.env.production.example:157` and `docker-compose.production.yml:586`).
**Note:** This is a real config defect, not a documentation one, and it is what a fork actually
copies. It must land with batch 7's `deployment.md:322` fix or the divergence just moves.
**Do not** rewrite `wallow.dev` to `example.com` here without a decision — see the F-BP-03 rescope
note in §5.

### Sequencing

Batches 1–8 have no file overlap and can run fully in parallel. Two soft dependencies:

- Batches 3 and 7 both touch the **commit-type divergence** (W-M16). Batch 3 owns root `CLAUDE.md`
  and `CONTRIBUTING.md`; batch 7 owns `versioning.md`. Root `CLAUDE.md` is the source of truth —
  batch 3 should not change it, only the two copies.
- Batch 3 and batch 4 both touch the **oxlint census** (W-H15). Batch 3 owns root `CLAUDE.md` and
  `.oxlintrc.json`; batch 4 owns `packages/lint/CLAUDE.md`. Six is the answer in both.
- Batch 8 must not land before batch 7 has the corrected rotation text, or vice versa — either
  order works, but they must both land.

---

## 5. Appendix — partial and refuted findings

One line each. **Nothing here is actionable as filed.** Partial findings need the stated rescope
before a fixer touches them; refuted findings must not be actioned at all.

### Rescope before actioning (14)

| ID | Why it is only partial | What survives |
|---|---|---|
| F-R-22 | `typescript-sdk.md:340` lists `TRACE` as ungated and `:379-381` describes `isSafeMethod` as GET/HEAD/OPTIONS — but `packages/sdk/server/csrf.ts:18-23` gates only POST/PUT/PATCH/DELETE, so TRACE genuinely IS ungated and `:340` is factually true | A wording/consistency nit, **not** a security-control error. Downgrade to Low. |
| F-R-26 | Both Vite examples hand-roll what `wallowAppConfig({defaultPort})` supplies — but `packages/config/package.json:4` is `"private": true` and nothing publishes it, so the cookbook's out-of-workspace audience cannot install it | The hand-rolled form is **correct** for the cookbook; it needs one sentence saying so. Route the real fix to `frontend-setup.md` (batch 5). |
| F-BP-03 | `wallow.dev` (15× deployment.md, 2× bff-pattern.md, 1× reverse-proxy.md, 0× typescript-sdk.md) is the **shipped default** in `docker/.env.production.example` and the committed `appsettings.json` cookie domain; rewriting deployment.md to `example.com` would make it disagree with the file it documents | Keep only `bff-pattern.md`'s self-inconsistency (`wallow.dev` at `:114` vs `wallow.example.com` at `:236` for the same issuer). The RFC 2606 concern belongs to `.env.production.example` or a "shipped placeholder" label. |
| F-BP-04 (third leg) | `reverse-proxy.md:140` says "seven **required**" and immediately links out for the optional knobs; omitting the optional `COOKIE_PASSWORDS`/`COOKIE_HOST_PREFIX` is not drift | Keep the single-source-of-truth recommendation; **drop the reverse-proxy accusation**. The real defect there is W-L30. |
| F-BP-06 | SDK release model is a **gap**, not a contradiction: release-please owns the `packages/sdk` version and the `sdk-v*` tag, and `typescript-sdk.md:130-133` is correct that a platform tag does not publish the SDK | Neither doc is wrong; neither states the handoff. State the two-stage model once in `versioning.md` and link from `typescript-sdk.md`. "Simply incompatible" overstates it. |
| F-BP-13 | `docs/index.md`'s "3 of 7 operations" half is not a defect — every section is a curated three-item highlight list (3 of ~9 architecture, 3 of ~10 development) | The "0 of 5 integrations" half is real: there is no Integrations section at all. Narrow to that (folded into W-M72). |
| F-BP-12 | Correct as filed, but routed — `docs/CLAUDE.md` is batch 5's file, not batch 7's | Folded into W-M66. |
| D-BP-06 | Its Diátaxis restructuring proposal and "delete onboarding's architecture/testing/FAQ sections" are editorial | Only the promoted-broken-start-path (folded into W-C2) and two-internal-ports-tables (W-M74) halves. |
| D-BP-08 | Counts wrong: `docs/index.md` has 13 links, not 12; `toc.yml` has 37 hrefs and 37 files, not "32 entries"/"30 pages" | The coverage argument survives with corrected numbers (W-M72). |
| D-BP-19 | Claims the module-flag example omits Identity and Branding; `fork-guide.md:325-331` has eight keys and includes both. Finder transcribed a truncated excerpt | Nothing. Superseded by reviewer L3 → W-M69. |
| B-BP-09 | Proposes `/api/v1/<module-path>` as the standard; that contradicts `openapi/v1.json` | The observation that the convention is undocumented survives; the proposed standard does not. Use `/v1/…`. |
| B-BP-17 | Proposes moving `Identity/README.md:130`'s `/me`; `UsersController.cs:73` really is `[HttpGet("me")]` | Only "add `Me` to the `Identity/CLAUDE.md` controller inventory" (W-M25). |
| B-BP-11 | Claims `api/CLAUDE.md:29` shares `:151-152`'s e2e gap; `:29` names no suite at all | Only the `:151-152` fix (W-M31). |
| E-BP-19 | Its Diátaxis/placement half is editorial and `docs/CLAUDE.md` explicitly sanctions the location | Only the base-URL change (W-L26). |

### Refuted — do not action (6)

| ID | Verdict |
|---|---|
| F-R-23 | `createApiProxy(config)` is a valid one-argument call — `proxy.ts:676-682` defaults `store`. Both docs already say so. **Nothing to fix.** |
| F-BP-16 | `valkey:8.1-alpine` (production) and `valkey:8-alpine` (dev/Testcontainers) describe two different stacks, both accurately. **No divergence.** |
| F-BP-18 | `deployment.md:251-254`'s "two things" is two categories, mirroring `prod-secrets.sh:15-21`. **Not miscounting.** |
| Scout B2 | "The Clients row is the one row still correct" — it is not; the doc's `/api/v1/identity/clients` 404s against the real `/v1/identity/clients`. |
| A-BP-03 (support) | ".husky has prepare-commit-msg only" — `.husky/` holds `_`, post-checkout, post-merge, pre-commit, pre-push and prepare-commit-msg. Restate as "**no commit-msg hook**". The commit-type finding itself stands. |
| Scout F (area F) | "0 broken cross-doc links or dead pointers" — refuted by three dead symbol pointers (W-M87, W-M91, W-M92). The relative-`.md`-link half of the claim does hold. |

### Withdrawn by their own finder

- The Area F reviewer's `getCurrentUser` finding — `packages/sdk/src/auth-extras.ts` exports it. The
  self-correction is right; it must not reappear.

---

## 6. Non-doc issues — file as beads, do not fix in this pass

1. **Wire `browserSetupFiles` for the three unwired browser projects** (`apps/minimal-app`,
   `packages/logger`, `packages/testing`). `createVitestProjects()`'s `browserSetupFiles` defaults
   to `[]` (`vitest-projects.ts:128,160`), making the guard strictly opt-in. The doc's own argument
   — "an opt-in guard cannot catch the file that forgot" — indicts the gap; fixing prose alone
   leaves the hole.
2. **Remove `"Modules.Configuration"` from `api/src/Wallow.Api/appsettings.json:86`** — it names no
   module. Pairs with W-M69; a doc-only fix leaves a fork's real appsettings carrying a dead flag.
3. **Drop the unused `Wallow.Identity.Api → Wallow.Identity.Infrastructure` ProjectReference**
   (`Wallow.Identity.Api.csproj:18`). It is the only Api→own-Infrastructure reference in the repo and
   the arch test passes only because NetArchTest reads IL, not project references.
4. **Remove the five unused Dapper `PackageReference`s** from the module Infrastructure csprojs —
   `grep` finds zero Dapper usages in `api/src`.
5. **Delete `api/tests/Modules/Billing/Wallow.Billing.Tests/`** — it holds only `bin/` and `obj/`,
   is untracked (`git ls-files` → nothing), absent from `api/Wallow.slnx`
   (`grep -c Billing` → 0), and there is no `api/src/Modules/Billing`. Stale build output.
   *(Per `.claude/rules/CONVENTIONS.md`, confirm before deleting.)*
6. **Fix three stale `DashboardNav` mentions in `packages/ui` source comments** —
   `src/sidebar-surface.test.ts:31`, `src/components/navigation-menu/navigation-menu.test.tsx:166`,
   `src/components/theme-toggle/theme-toggle.stories.tsx:139`. Pairs with W-H26.
7. **Storage module has no `CLAUDE.md`** (the other six do). `api/CLAUDE.md:77-78` explicitly
   enumerates the six, so the gap is recorded rather than silently dropped. Storage does have the
   full stack, 5 commands + 5 queries, both providers, `IStorageProvider`, and the
   `{tenantId}/{bucket}/{path}/{fileId}{extension}` key format at `README:129`.
8. **`apps/wallow-auth` has no root `README.md` and no `CLAUDE.md`** at any spelling, though
   `apps/wallow-web/README.md` exists and both apps have an `e2e/CLAUDE.md`.
9. **Decide whether `ClientsController` should narrow from `AdminAccess` to the purpose-built
   `ServiceAccountsRead/Write/Manage`** (`PermissionType.cs:73-75` declares all three and nothing
   uses them). W-H2 documents the current behaviour; this bead decides whether the behaviour is
   right.

Two further candidates worth considering as `wallow/*` lint rules rather than prose, since both
W-H20 and W-H21 describe constraints whose enforcing specs were deleted: a rule banning
`node:fs`-free browser-bundle imports in `packages/logger/src/index.ts`, and a rule keeping
`packages/env`'s four module lists in sync. `packages/lint/CLAUDE.md` explains the pattern.
