# Route Surface — Declutter, Anti-Drift Gates, and Hardening

**status: active**

Bead: **Wallow-e5mw** (epic)

Source: the verified route audit at
`docs/audits/2026-07-30/1430-frontend-routes/report.md`. Every claim below traces to a
finding (`F#`, report §3–4) and a fix step (`D#` / `R#`, report §5). Read the audit's §2
per-route inventory before starting any task — it is the map of what exists and why.

Intended runner: `/team-build docs/plans/2026-07-30/1654-route-surface-declutter-and-hardening.md`

## Numbering convention — read this first

Three numbering schemes coexist and **must not be conflated**:

| Prefix | Means | Lives in |
| --- | --- | --- |
| `P1`–`P9`, `P1.T1` | a **feature / task in this plan** | this file |
| `F1`–`F71` | an **audit finding** | report §3 scorecard, §4 detail |
| `D1`–`D10`, `R1`–`R16` | an **audit fix step** | report §5 |

The audit's own plan uses `F` for findings, so this plan deliberately uses `P` for its
features. If a bead says "F12", it means the audit finding, never a feature here.

---

## Problem

The route surface of `apps/wallow-auth` and `apps/wallow-web` was audited end to end after
the Blazor → TanStack Start port: 35 route files, 33 addressable paths, five areas, three
finder lenses per area, every finding adversarially verified before inclusion.

**The expected problem was not the real one.** There is **zero Blazor residue** — all 16
wallow-auth pages are 1:1 ports and every one has a live producer today. Route codegen is
clean in both apps: a real rebuild at `8df6ee62` reproduced both `routeTree.gen.ts` hashes,
with no orphans and no stale entries. Exactly **one** route is genuinely test-only
(`/bff-demo`), and it is React-era scaffolding, not a port artifact.

The actual cost to agents working on this codebase is four things, none of which is a
surplus route:

1. **Comments that lie.** 15 wallow-auth route files still carry Phase-0 placeholder headers;
   seven wallow-web dashboard routes document a `router.tsx` `.update({...})` registration
   scheme that no longer exists; `/health` comments in both apps claim both compose stacks
   probe them when production TCP-probes `/proc/net/tcp` and never issues an HTTP request.
   An agent reading these acts on them.
2. **Path literals with no single source of truth.** The `/api` mount is spelled four ways;
   passthrough prefixes four ways, with nothing importing the SDK's own
   `DEFAULT_PASSTHROUGH_PREFIXES`; `/bff/callback` across seven configs with two unguarded;
   and **the API hardcodes 28 frontend route paths in C# with no cross-language gate at all**.
   That last one is why an emailed `{authUrl}/confirm-email-change` link 404s in production
   (F5) with nothing to catch it.
3. **Per-route boilerplate.** 12 hand-rolled `validateSearch` blocks duplicating
   `readScalar`/`scalarToWireString` and the same `returnUrl` ternary eight times; 15 routes
   hand-wrapping `<AuthLayout>` because there is no pathless layout.
4. **Real defects the port introduced or never fixed** — a basepath-bypassing redirect, a
   silent BFF bypass in the reference production topology, unhardened proxy forwarding.

No finding is Critical. The port broke nothing outright.

## Goals

1. **Nothing in a route file states something untrue.** Comments, docstrings, and inert config
   either describe the current architecture or are deleted.
2. **Every cross-boundary path literal has one definition and a test that proves the copies
   agree** — including across the C#/TypeScript boundary, which today has no gate whatsoever.
3. **The one genuinely test-only route is gone, and the SDK example it carried survives** in
   the docs site rather than in a public unauthenticated route.
4. **A new route cannot silently escape the reachability gate or ship with a stale route
   tree** — both are enforced in CI, not by convention.
5. **The confirmed defects are fixed**, with the High-severity BFF bypass closed by moving the
   browser seam off `/api` behind a single constant.
6. **Nothing regresses.** Every `data-testid` survives, all three Playwright suites stay green,
   and every URL path in `routeTree.gen.ts` is byte-identical unless a task says otherwise.

## Non-goals

- **No route deletions beyond `/bff-demo`.** `$orgId` and `$inquiryId` look deletable and are
  not — they are working detail pages whose navigation was lost. An earlier read of the
  evidence marked `$orgId` for deletion; that would have destroyed a shipped feature.
- No visual or component work in `apps/wallow-web/src/features/**` — that is the 1442 plan's
  territory (see the boundary below).
- No OpenAPI regeneration and no new backend endpoints. P2.T4 **deletes** two backend
  endpoints; that is the only `api/` surface change in this plan.
- No new catalog components.

---

## Boundary with the active 1442 plan

`docs/plans/2026-07-30/1442-web-ui-refinement-and-text-component.md` is **active** and owns
three audit findings. This plan does not touch them:

| Audit | Owned by 1442 | Do not duplicate |
| --- | --- | --- |
| F1, F9 (`$orgId` / `$inquiryId` unreachable) — audit step R3 | F4.T1 | list-row `Link`s |
| F49 (four raw in-app `<a href>`) | F4.T3 | anchor → `Link` conversion |
| F55 (`bff-pattern.md` nav path) | resolved by F4.T3 | — |

**Two hard cross-plan edges:**

- **P7.T4 (`throw notFound()`, R4) must land after 1442's F4.T1.** Until the row links exist,
  a bad id is unreachable and the fix has zero user impact.
- **P7.T5 (route-level `errorComponent`, F33) is adjacent to 1442's F4.T2** (query-level error
  states). Different layers — F33 is `errorComponent` on the route, F4.T2 is `isError` in the
  component — but they touch neighbouring files. P7.T5 is sequenced last in P7 for this reason.
  If 1442's F4 is still in flight when P7 starts, the bead owner coordinates rather than
  merging blind.

---

## Repo facts every agent MUST honour

The decomposer should copy the relevant ones into each bead's `--design`.

- **Quality gate:** `pnpm check` = format:check + lint + typecheck + test + build +
  check:exports. Build the SDK first (`pnpm --filter @bc-solutions-coder/sdk build`) — apps
  typecheck against `dist/`.
- **Backend tests:** always `./scripts/run-tests.sh` (or `... identity`), **never bare
  `dotnet test`** — the script supplies `--settings api/tests/coverage.runsettings`.
- **E2E:** `./scripts/e2e.sh` is the one-command backend-dependent runner for all three
  Playwright suites. Per-app: `pnpm --filter ./apps/wallow-auth test:e2e`. Selectors are
  **always** `data-testid`, `{page}-{element}` kebab-case — never CSS, id, or text selectors.
  Readiness is `[data-app-ready='true']`.
- **Component tests run in real headless Chromium** via Vitest browser mode. Never jsdom,
  never happy-dom, never jest. Never mock `@bc-solutions-coder/ui`.
- **Formatter/linter is oxc** (`oxfmt` + `oxlint --deny-warnings`), never prettier/eslint.
- **C#:** run `dotnet format api/Wallow.slnx` before every commit. Explicit types, never `var`.
  Logging only via `[LoggerMessage]` source-generated partials — never `logger.LogInformation`
  directly. JWT claims only via `ClaimsPrincipalExtensions`, never raw `FindFirst`.
- **`data-testid` is a contract.** `apps/wallow-web/e2e-cross-app/login-journey.spec.ts`
  selects many of them; none may drift without an explicit note on the owning bead.
- **`.gitattributes` marks `appsettings*.json`, `branding.json`, `seed.json`, and
  `docker/.env*` as `merge=ours`** — a fork will **not** receive changes to these on an
  upstream merge. Any task changing them must state the fork-migration consequence on its bead.
- **Library APIs come from ref.tools**, not memory: `mcp__ref-context__ref_search_documentation`
  / `ref_read_url` for TanStack Start/Router, Wolverine, EF Core, Playwright, zod, oxc.
- Commit format: `<type>(<scope>): <description>`, lowercase, imperative, no trailing period,
  first line < 72 chars.

---

## Feature plan

```
P1 truth-pass ─────┬──> P2 deletions
                   │
                   ├──> P3 path literals + /api seam ──┬──> P4 search schemas ──> P5 _auth layout ──┐
                   │                                   │                                            │
                   │                                   └──> P7 route defects ──> P8 hardening        │
                   │                                                                                 │
                   └─────────────────────────────────────────────────────────> P6 CI gates <────────┘
                                                                                     │
                                                                              P9 coverage + docs
```

P6's route-tree drift gate lands **after** P5 and P6.T2, both of which regenerate the trees.
P9 is last because several of its specs are the acceptance gates for earlier features.

---

## P1 — The truth pass

**Audit:** D1 · findings F42, F48, F47, F7, F59, F69
**Depends on:** nothing. **Blocks:** nothing executable — but do it first; it is what every
later agent reads.

### P1.T1 — Purge comments that document a dead architecture

**Change.** Six clusters:

1. `apps/wallow-auth/src/routes/index.tsx:10-18` — the Phase-0 "`/login` isn't registered yet"
   comment, plus placeholder headers in the other 14 wallow-auth route files.
2. The seven wallow-web dashboard route headers claiming `router.tsx` `.update({...})`
   registration and "no dashboard layout route yet", **plus their copies in
   `dashboard/route.test.tsx:176` and `bff-demo.test.tsx:173`** (these are assertions — the
   spec must stay green).
3. `apps/wallow-web/src/routes/bff-demo.tsx:21-22,42-43` — cites `BffFlowTests.cs`, deleted in
   `9c4c940c`, and a `bff-example` container that never starts. (P2 deletes this file; if P2
   runs first, skip.)
4. The `/health` comments in both apps and in `passthrough-routes.test.ts:20-21` — "both
   compose stacks probe it" is false; production TCP-probes `/proc/net/tcp`.
5. `docker/docker-compose.production.yml:517` — names the deleted `apps/wallow-web/server.ts`.
6. `apps/wallow-web/e2e/routes.spec.ts:11-14` — says `/` uses `/bff/user`; `index.tsx:49-53`
   uses `ensureCurrentUser` → `/api/v1/identity/users/me`.

Also: delete the inert `WALLOW_API_INTERNAL_URL` entry at
`apps/wallow-web/playwright.config.ts:9,21` (nothing reads it; the loader wants
`BFF_API_BASE_URL`, set at `:36`), and correct `LogoutScreen.tsx:129-133` — its stated reason
for the anchor is wrong (`/connect/$` **is** in `routeTree.gen.ts:26,104-105`). The anchor
stays; it is right for the CSRF-sink reason, which is what the comment should say.

**Ordering hazard.** `apps/wallow-auth/src/routes/index.tsx`'s Phase-0 comment sits next to the
`redirect({ href })` bug (F3, fixed in P7.T1). Do that file **in P7.T1 or after it**, not here.

**Acceptance**

- No comment in either app's `src/routes/**` references `BffFlowTests`, `bff-example`,
  `server.ts`, `router.tsx` `.update(`, or a Phase-0 registration state.
- `pnpm lint`, `pnpm typecheck`, `pnpm test` green — the two spec copies in cluster 2 are
  assertions and must be updated, not deleted.

### P1.T2 — Re-scope the bd memory that re-seeds the `href` pattern

**Why.** A stored memory tells agents to use `href` rather than `to`. That is what keeps
regenerating F3 (P7.T1). Narrow it to the case it is actually true for, or delete it.

**Acceptance:** `bd memories` no longer returns guidance that would produce
`redirect({ href })` for an in-app navigation; the surviving memory (if any) names the
external/CSRF-sink case it applies to.

---

## P2 — Deletions

**Audit:** D10, D2, D3, D4 · findings F4, F47, F25, F41, F11
**Depends on:** P1 (or land together).

### P2.T1 — Move the `setCsrfToken` example into the docs **before** anything is deleted

**Why.** `/bff-demo` is the **only non-test caller of `setCsrfToken`** — the sole worked
example of a public SDK API. Deleting the route without relocating the example removes the
only demonstration of that API.

**Change.** Add the worked example to `docs/integrations/bff-pattern.md`, following
`docs/CLAUDE.md` (site content only, lowercase-kebab, relative cross-references).

**Acceptance:** the example compiles against the current SDK surface; a reader can follow it
without the route existing. **This task must merge before P2.T2.**

### P2.T2 — Delete `/bff-demo`

**Change.** Remove `apps/wallow-web/src/routes/bff-demo.tsx`, `bff-demo.test.tsx`, its entry in
`apps/wallow-web/e2e/routes.spec.ts`, `apps/wallow-web/README.md:51`,
`.claude/rules/E2E.md:29`, and `docs/development/testing-e2e.md:52`.

**Could break:** the wallow-web backend-free reachability gate — but **not fatally**:
`routes.spec.ts:27` already lists `/`, so the suite retains a target. `.claude/rules/E2E.md`
currently states `/bff-demo` is the only qualifying route; that sentence is now wrong either
way and must be rewritten in this task.

**Acceptance:** `pnpm --filter ./apps/wallow-web test:e2e` green; `./scripts/e2e.sh` green;
no repo-wide reference to `bff-demo` outside git history.

### P2.T3 — Delete the dead `bff-example` compose service

**Change.** Remove the service at `docker/docker-compose.test.yml:294,308-345`, the line at
`docker/CLAUDE.md:9`, and the reference at `apps/wallow-web/Dockerfile:2`.

**Could break:** nothing. `scripts/e2e.sh:119` never starts it, nothing `depends_on` it, and
its only consumer (`BffFlowTests`) is deleted. It is an identical build of `wallow-web`
publishing `3000:3000` **on all interfaces**, unlike every sibling service — so this is also a
small hardening win.

**Acceptance:** `./scripts/e2e.sh` completes with all three suites green.

### P2.T4 — Delete the `/mfa/enroll?enrollToken=` exchange path (frontend **and** backend)

**Decision on record:** the user chose to delete **both halves**.

**Change.** Frontend: remove the `enrollToken` search param
(`apps/wallow-auth/src/routes/mfa/enroll.tsx:26,44`) and the `exchanging` state machine
(`MfaEnrollForm.tsx:421-537`). Backend: remove the `enroll/issue-token` and
`enroll/exchange-token` endpoints.

**Why it is also a defect:** `exchange-token` is `[AllowAnonymous]` and mints an `MfaPartial`
cookie from a query-string token. Deleting it removes that exposure.

**Breaking change.** This removes two public API endpoints. Repo-wide grep found **zero
producers** in `api/src` or either app — only consumers and tests — so nothing in this repo
breaks, but a fork driving enrollment out of band will. **The bead must record this as a
breaking change** and the commit must carry the appropriate conventional-commit marker so
release-please reflects it.

**Acceptance:** `apps/wallow-auth/e2e/mfa.spec.ts:106-121` (the live enrollment path, which
uses `returnUrl` → exchange-ticket, **not** `enrollToken`) stays green;
`./scripts/run-tests.sh identity` green; `dotnet format api/Wallow.slnx` clean.

### P2.T5 — Drop the two unproduced `/error` reason keys

**Change.** Remove `access_denied` and `invalid_request` from `REASON_MESSAGES` in
`apps/wallow-auth/src/features/.../ErrorPage.tsx`. **Keep the `Map`** — it is a deliberate
prototype-pollution defence, not incidental.

**Acceptance:** the API emits only `invalid_redirect_uri` and `not_a_member`; the fallback
message covers anything else. ErrorPage unit spec plus `e2e/routes.spec.ts`'s `/error` entry
green.

---

## P3 — One definition per path literal, and the `/api` seam moves

**Audit:** D6a–d, R6 · findings F60, F27, F24, F23, F2
**Depends on:** P1. **Blocks:** P4, P7, P8 (R9).

D6a–d are four independent clusters and can run in parallel. **P3.T5 (R6) depends on P3.T1.**

### P3.T1 — One constant for the `/api` mount *(parallel)*

**Change.** Four unpinned spellings: `packages/sdk/src/server/bff-server.ts:26`,
`apps/wallow-web/src/routes/api/$.ts:19`, `apps/wallow-web/src/router.tsx:14`,
`apps/wallow-web/src/start.ts:37`. Export one constant from the SDK; add a spec asserting all
four agree.

**Acceptance:** divergence in any one of the four fails a test. Today divergence silently
breaks the browser data path.

### P3.T2 — Import the SDK's passthrough prefix constant *(parallel)*

**Change.** `packages/sdk/src/server/passthrough.ts:53-57` defines
`DEFAULT_PASSTHROUGH_PREFIXES` and **nothing imports it**;
`apps/wallow-auth/src/lib/api-passthrough.ts:66` keeps a second copy and
`apps/wallow-auth/src/routes/passthrough-routes.test.ts:31-38` a third. Import the constant;
assert the route files match it.

**Acceptance:** drift in either direction fails a test. Today it produces indistinguishable
404s.

### P3.T3 — Close the two unguarded `/bff/callback` sites *(parallel)*

**Change.** `/bff/callback` appears at `seed.json:190,218,241`,
`docker-compose.test.yml:111,260,327`, `docker-compose.production.yml:524`,
`apps/wallow-web/playwright.config.ts:33`, `Wallow.AppHost/Program.cs:107`. Three C# tests
already pin seed, test-compose, and AppHost — only **two** sites are unguarded. Extend
`PublicSeedClientRemovalTests`' input set to the production compose file and the playwright
config.

**Acceptance:** `./scripts/run-tests.sh identity` green; changing either newly-guarded file's
callback path fails that test.

### P3.T4 — The cross-language route-path gate *(the highest-leverage task in this plan)*

**Change.** `AccountController.cs`, `AuthorizationController.cs`, `LogoutController.cs`, and
two handlers spell **28** `{authUrl}/<path>` literals with no gate. Add a vitest spec that
extracts every such literal from `api/src` and asserts each against wallow-auth's
`FileRoutesByTo`.

**Why.** This is the gate that would have caught F5 (`/confirm-email-change`, fixed in P7.T3),
and it is the permanent guard for that entire class of break.

**Acceptance:** the spec fails when a C# literal names a path wallow-auth does not serve;
it currently fails on `confirm-email-change` until P7.T3 lands — **land P3.T4 and P7.T3
together, or land P3.T4 with that one case explicitly quarantined and a bead to remove the
quarantine.**

### P3.T5 — Move the browser seam off `/api` (R6, High)

**Decision on record:** the user chose **make the browser seam configurable**, not drop the
ingress matcher.

**Change.** `docker/caddy/Caddyfile.example:63-66` routes `/api/*` to `wallow-api` on the same
host as `WEB_PUBLIC_URL`, while `apps/wallow-web/src/router.tsx:14` hardcodes the browser base
URL to `/api`. Move the browser seam to a distinct mount via P3.T1's single constant, leaving
the Caddyfile unchanged.

**Why it is severe.** The API's `SmartScheme` falls back to the Identity cookie
(`COOKIE_DOMAIN=wallow.dev`), so the collision is a **silent BFF bypass, not a 401**.

**Could break:** this changes the fork's frontend contract. `.gitattributes` does **not** cover
`router.tsx`, so forks receive the change on merge — but a fork with its own reverse proxy
keyed on `/api` must update it. **Record the fork-migration note on the bead and in the
feature's docs task (P9.T3).**

**Verify:** no suite exercises the Caddy topology today. Verification is a manual
`docker-compose.production.yml` bring-up behind `Caddyfile.example`, confirming the browser
data path reaches the BFF. **Flagging that permanent gap is part of this task's output** —
file a follow-up bead for a topology smoke test.

**Depends on:** P3.T1 (all four spellings must be one constant before the value changes).

---

## P4 — One search schema for wallow-auth

**Audit:** D7 · findings F15, F14
**Depends on:** P3. **Blocks:** P5.

### P4.T1 — Replace 12 hand-rolled `validateSearch` blocks with shared zod schemas

**Change.** One shared zod search-schema module adopted by `accept-terms`, `consent`, `error`,
`invitation`, `login`, `logout`, `mfa/challenge`, `mfa/enroll`, `register`, `reset-password`,
`verify-email/index`, `verify-email/confirm`. These duplicate `readScalar` /
`scalarToWireString` and repeat the same `returnUrl` ternary eight times; zod is the documented
path.

**Fix the `client_id` drift in the same step.** `accept-terms.tsx:63`, `register.tsx:41`, and
`mfa/challenge.tsx:51` rename the wire param to `clientId` in `validateSearch`, while
`login.tsx:108` and `consent.tsx:55` keep the wire name — and `login.tsx:39-44` states the
rule. **Keep `client_id` in the schema; destructure-rename at the use site**, as
`consent.tsx:61` already does.

**Could break:** every route's search parsing at once — the highest blast radius in this plan.
The `client_id` drift is currently *latent*: it breaks the moment anything writes search params
via `<Link>`. Fixing it now is right, but it changes three routes' parsed param names.

**Acceptance:** per-route unit specs green; **full** `pnpm --filter ./apps/wallow-auth test:e2e`
— the `login`, `mfa`, `logout`, `signup`, and `forgot`/`reset` specs all push real search
params through these schemas. No wire-level query-string name changes (assert by diffing the
URLs the e2e suite produces).

---

## P5 — The pathless `_auth` layout

**Audit:** D8 · findings F34, F13, F16, F17, F44
**Depends on:** P4. **Blocks:** P6.T3.

### P5.T1 — Introduce `_auth` and hoist four cross-cutting concerns into it

**Change.** Add a pathless `_auth` layout route and move `<AuthLayout>` into it, so the 15 page
routes stop hand-wrapping it (16 files reference `<AuthLayout` today, including `__root.tsx`;
`routeTree.gen.ts` shows every parent is root). While creating it, hoist:

- **Per-client branding.** `useClientBranding` is wired on `/login` only
  (`login.tsx:139,148,161,164` is the sole `<AuthLayout branding>`), though the API forwards
  `client_id` to `/consent`, `/mfa/challenge`, and `/accept-terms`. Eight docblocks already
  record the gap. Branding discontinuity at the consent step is a trust signal.
- **`head()` titles.** Only `__root.tsx:125` sets `head:` today, so every auth screen shares one
  title — WCAG 2.4.2 Level A.
- **`robots: noindex`**, absent everywhere, on an app serving
  `/verify-email/confirm?token=`, `/invitation?token=`, and `/reset-password?token=`.
- **The branding fetch moves into a route `loader`** (out of the component body at
  `login.tsx:139-157`) so `/login` stops brand-flashing. Leave `/invitation`'s auth state
  uncached.

**Nature of the change.** All 15 routes render correctly today — the layout half is prospective
hygiene (a new route can no longer forget the wrapper). The branding, title, and `noindex` half
are real gaps.

**Acceptance:** `git diff` the regenerated `routeTree.gen.ts` and confirm **every `path:`
literal is unchanged** (`_auth` is pathless); `pnpm --filter ./apps/wallow-auth test` green;
full e2e green — `apps/wallow-auth/e2e/routes.spec.ts` covers all 16 paths and is the direct
regression gate. A test proves branding renders on `/consent`, `/mfa/challenge`, and
`/accept-terms`, not just `/login`.

---

## P6 — Gates: route tree, reachability, and the `/dashboard` hole

**Audit:** D9, D5 · findings F67, F20, F70, F71, F10, F65
**Depends on:** P5 and P6.T2 must both land before P6.T3.

### P6.T1 — Derive the reachability array from the route tree

**Change.** `apps/wallow-auth/e2e/routes.spec.ts:10-26` hand-maintains a 16-entry path array.
Derive it from `FileRoutesByTo` so a new route cannot silently escape the gate — server routes
already get this treatment via `passthrough-routes.test.ts:31-38`.

Also record, next to each app's `routeFileIgnorePattern`
(`apps/wallow-auth/vite.config.ts:88`, `apps/wallow-web/vite.config.ts:68`), that it excludes
only `*.test.*` / `*.spec.*` — so **any other colocated helper file becomes a route**.

**Acceptance:** adding a throwaway route file makes the reachability spec fail without editing
the spec.

### P6.T2 — Fill the bare `/dashboard` hole

**Change.** Add `apps/wallow-web/src/routes/dashboard/index.tsx` redirecting to
`/dashboard/apps`. Today `/dashboard` exact-matches the layout with no child (router-core
`router.js:851-856`: exact match, no splat, so no 404) and serves an HTTP 200 blank column —
and an unauthenticated `returnTo` round-trip lands there. Retarget the stale docs example at
`docs/integrations/typescript-sdk.md:412` in the same change.

**Acceptance:** bare `/dashboard` redirects; `login-journey.spec.ts:83,86` already proves
`/dashboard/apps` renders. Add the path to `apps/wallow-web/e2e/routes.spec.ts` behind auth, or
assert the redirect in a unit spec.

### P6.T3 — CI gate on a stale route tree

**Change.** Add `git diff --exit-code` on both `src/routeTree.gen.ts` after the build step in
`.github/workflows/ci.yml`. No such gate exists today — `check` builds after typecheck without
ever comparing.

**There is no current drift.** A real rebuild at `8df6ee62` reproduced both files' md5s, the
import↔file set difference is empty both ways, and all 33 path literals are correct. This gate
protects that state.

**Acceptance:** run the new step locally against a deliberately stale tree; it must fail.
**Depends on:** P5 and P6.T2, both of which regenerate the trees.

---

## P7 — Route correctness defects

**Audit:** R1, R2, R5, R4, R13, R16 · findings F3, F5, F50, F54, F33, F51, F52, F12
**Depends on:** P3 (P7.T3 pairs with P3.T4).

### P7.T1 — `redirect({ href })` bypasses the basepath (High)

**Change.** `apps/wallow-auth/src/routes/index.tsx:17` → `redirect({ to: "/login" })`; update
`index.test.tsx`; fix `apps/wallow-auth/e2e/routes.spec.ts:62`, which hardcodes `href="/login"`
on a link built through `toAppHref` and therefore fails under `AUTH_BASE_PATH=/auth` — a
basepath tripwire pointed the wrong way.

**Why.** router-core 1.171.15's `resolveRedirect` skips `buildLocation` when `href` is set, so
`rewriteBasepath` never runs. Under `AUTH_BASE_PATH=/auth` the front door emits
`Location: /login`, and path-based ingress sends that to wallow-web.

**Currently latent** — the published ghcr image is built root-mounted
(`deploy.yml:145-152`), so the based topology is not the shipped default.

**Also do P1.T1's cluster 1 edit to this file here**, per P1's ordering hazard.

**Acceptance:** `index.test.tsx`, `base-path-wiring.test.ts`, and the prefixed Playwright run
from P9.T2.

### P7.T2 — Eight un-based root-relative hrefs, and the guard's two blind spots

**Change.** Route all eight literals through `toAppHref`: `InvitationScreen.tsx:110`,
`RegisterForm.tsx:639`, `ResetPasswordForm.tsx:198`, `auth-result.ts:100,101,104`,
`MfaEnrollForm.tsx:117`, `MfaChallengeForm.tsx:84`, `ConsentScreen.tsx:114`. Then widen
`base-path-wiring.test.ts:160`, whose regex matches only literal JSX `href=` **and only
`.tsx` files** — which is why `auth-result.ts`'s three sites stayed invisible while the guard
reported green.

**Method:** widen the guard **first**, watch it fail on all eight, then fix and watch it pass.

**Acceptance:** post-signup and post-reset hops work on based builds (already broken today);
root-mounted builds unaffected. Land with P7.T1 — same defect family.

### P7.T3 — The emailed `confirm-email-change` link 404s

**Change.** `AccountController.cs:996-1006` emails `{authUrl}/confirm-email-change` — a path no
route serves: not in `src/routes/`, not in the generated tree, not under `/v1`, `/connect`, or
`/.well-known`. The real handler is `GET /v1/identity/auth/confirm-email-change`. Either point
the email through the passthrough (`{authUrl}/v1/identity/auth/…`) **or** build the frontend
route — the bead owner picks and records the reasoning.

**Could break:** nothing; it is already broken for every user who clicks it.

**Acceptance:** P3.T4's cross-language spec is the permanent gate — **land them together**. Add
a Mailpit-driven e2e spec for the change-email flow to prove the fix end to end.

### P7.T4 — `throw notFound()` on the two detail routes

**Change.** `$orgId.tsx` and `$inquiryId.tsx` never throw; only `__root.tsx:151`'s
`notFoundComponent` exists, so a bad id errors instead of 404-ing.

**Acceptance:** an e2e hit on a bogus id asserts the not-found screen.
**Depends on:** **1442's F4.T1** — zero user impact until the row links exist.

### P7.T5 — Dashboard route resilience

**Change.** Four small independent fixes in wallow-web:

- `dashboard/route.tsx:45-55`'s auth gate has no try/catch. `requireAuth` throws only on
  null/undefined (`sdk/route-context.ts:123-131`), so any other rejection hits the root
  boundary and a transient API failure blanks the app. Soften non-401 failures inside
  `requireAuth` — **and make sure a genuine 401 still redirects.**
- **(F33)** No route sets `errorComponent` (only `__root.tsx:150`), and six named loaders can
  reject; `RootErrorBoundary` shows no error text by design, so any loader rejection blanks the
  app with no context. Add `errorComponent` to the loader-bearing routes. **This is the 1442
  coordination point — see the boundary section.**
- `dashboard/route.tsx:51`'s `returnTo` drops search and hash; include `location.searchStr` and
  the hash (the SDK's `route-context.ts:85-92` encodes whatever it is handed).
- Drop the `Object.assign` on the thrown redirect at `routes/index.tsx:55-57`; assert via the
  `assertRedirect` pattern already used at `index.gate.test.tsx:158`. The `Object.assign` exists
  solely to satisfy `index.gate.test.tsx:141`.

**Acceptance:** `index.gate.test.tsx`, `dashboard/route.test.tsx`, `login-journey.spec.ts`.

### P7.T6 — Invitation accept-success dead-ends on a login form

**Change.** `InvitationScreen.tsx:425`'s `onSuccess` sets `location.href="/"`, whose
`beforeLoad` (`routes/index.tsx:16-18`) redirects even a signed-in user to `/login` — so a
just-accepted invitee lands on a sign-in form with no acknowledgement. **Prefer the
acknowledge-then-redirect variant** (lower risk) over adding a signed-in branch on `/`, which
changes behaviour for bare-origin hits generally.

**Acceptance:** the backend-driven invitation spec from P9.T1, driving a real token through the
accept path.
**Depends on:** P7.T1 also touches `routes/index.tsx` — sequence them.

---

## P8 — Server-route hardening

**Audit:** R7, R8, R9, R10, R11, R12 · findings F6, F26, F8, F36, F57, F35, F7, F37, F38, F32,
F31, F61
**Depends on:** P3 (P8.T3 needs D6b's single prefix list).

### P8.T1 — Hop-by-hop headers and upstream timeouts in the passthrough

**Change.** In `packages/sdk/src/server/passthrough.ts:172-186`: strip the hop-by-hop header set
before forwarding, wrap the `fetch` in try/catch, add `AbortSignal.timeout(...)`, map rejections
to 502/504.

**Why.** Reproduced on Node 24 — `Connection: a, x-foo`, `Keep-Alive`, `Upgrade`, and chunked
POST all make `fetch` throw, and there is no try/catch, so these surface as unhandled 500s
wherever Nitro is exposed directly (dev, Aspire, E2E). Node `fetch` has no default timeout, so a
hung upstream is opaque rather than a 504.

**Could break:** SDK-level, affecting both apps. **Pick the timeout against the slowest real
endpoint** — too aggressive breaks long API calls.

**Acceptance:** SDK unit specs per malformed-header case plus a timeout case; `./scripts/e2e.sh`
for the integration path.

### P8.T2 — Client-IP and XFF header hygiene

**Change.** `apps/wallow-web/src/lib/bff.ts:143-146` and
`apps/wallow-auth/src/lib/api-passthrough.ts:78-89` only ever `set` the client-IP seam header
and never `delete` it, so an attacker-supplied value wins whenever `request.ip` is absent.
`packages/sdk/src/server/proxy.ts:563-570` additionally copies inbound `X-Forwarded-For`
verbatim, making a browser-supplied value the leftmost chain entry. `delete` both inbound before
setting, or pass the client IP out of band in the SDK.

**Not currently live** on wallow-auth — srvx's `request.ip` always resolves on a real request,
so this is hygiene today and becomes live if a fork sets srvx `trustProxy`. Fix it anyway; the
invariant is undocumented.

**Could break:** anything downstream reading the current (spoofable) chain — audit-logging
attribution changes shape.

**Acceptance:** negative-case specs in both apps asserting the header is dropped when absent
upstream.

### P8.T3 — Least-privilege prefix scoping on both proxies

**Decision on record:** the user chose **conservative defaults**, derived from P3.T2's constant.

**Change.** **(a)** wallow-auth's `/v1/$` republishes the entire API at the auth origin — 115
reachable paths where the app uses only `/v1/identity/**` — and
`docker/caddy/Caddyfile.example:57-78` scopes edge policy to `/api/*`, leaving this second path
unpoliced. Narrow `api-passthrough.ts:66` to
`["/v1/identity/**", "/connect/**", "/.well-known/**"]` — deliberately **not**
`/v1/branding/**`. **(b)** wallow-web's `/api` proxy is scoped to the API root, not `/v1`
(`proxy.ts:596-633`: `strippedApiPath` allows any remainder, reaching `/hangfire`, `/scalar`,
health, `/connect/*`); add an upstream path allowlist. **Do not** set
`BFF_API_BASE_URL=.../v1` — that yields `/v1/v1/x`.

**Could break:** the one step here that can break a fork whose UI calls a prefix outside the
allowlist. (b) is gated by a session 401 (`proxy.ts:762-772`) plus CSRF today, so this is
least-privilege rather than closing an open relay.

**Acceptance:** **full** `./scripts/e2e.sh` — the eight backend-dependent wallow-auth specs
exercise the real `/v1` surface and will catch an over-narrow list. The `/v1/branding/**`
exclusion is recorded on the bead as a conscious decision.
**Depends on:** P3.T2.

### P8.T4 — CSRF gate on the cookie-forwarding `/v1` passthrough

**Decision on record:** the user chose **log-only first**, then enforce.

**Change.** `packages/sdk/src/server/passthrough.ts:172-175` relays cookies verbatim; the only
things preventing cross-site abuse are `SameSite=Lax`
(`IdentityInfrastructureExtensions.cs:177,322-347`) and the absence of CORS. Add a
`Sec-Fetch-Site`/`Origin` gate **in log-only mode**, and document the SameSite dependency next
to the route.

**Why it matters:** this is an undocumented cross-repo invariant a fork can silently break by
loosening the API's cookie policy.

**Acceptance:** SDK specs covering same-origin, cross-site, and header-absent requests; full
e2e green (a too-strict gate breaks the OIDC hand-off). **The bead files a follow-up to flip
log-only → enforce** once the logs are clean.

### P8.T5 — `/health` truth-up across both apps and both compose stacks

**Change.** One cluster:

- wallow-auth's `/health` (`src/routes/health.ts:13-16`) returns a constant `ready` and never
  validates `WALLOW_API_INTERNAL_URL`, which the SDK silently defaults to `localhost:5001` when
  unset — a misconfigured fork reports healthy while every proxy call fails. Validate it, as
  wallow-web's route does.
- The apps answer different contracts: `text/plain "ready"` vs
  `Response.json({ status: "ok" })` (`bff-server.ts:169`), and **neither registers HEAD**.
  Standardise on JSON; register HEAD. (`HEAD /health` does **not** 404 today — Start falls back
  `HEAD` → `GET` and strips the body — so this is for monitors expecting explicit registration.)
- wallow-web's `/health` conflates liveness with readiness: `getBffServer()` memoises on success
  (`src/lib/bff.ts:108-115`) and the handler is a constant, so after the first success the probe
  can never observe a Redis, IdP, or API failure. Split liveness from a readiness probe that
  pings the session store.
- Production compose probes TCP listen state (`grep -q ':1F90' /proc/net/tcp`,
  `docker-compose.production.yml:478-479,540-541`) and never issues an HTTP request — so the
  production probe cannot see a broken BFF even though Caddy gates `service_healthy` on it.
  Point the production healthchecks at HTTP `/health`.
- Document the base-path-aware probe URL at `docs/operations/reverse-proxy.md:184` — based
  builds serve under `/auth` (`vite.config.ts:85,92`) and `health.ts` has no base-path handling,
  so a hand-written k8s probe following that table 404s.

**Acceptance:** `./scripts/e2e.sh` (test-compose is the only stack that HTTP-probes today), plus
a manual `docker-compose.production.yml` bring-up confirming the new probes go healthy.

### P8.T6 — Redis client leak in the BFF builder

**Change.** `apps/wallow-web/src/lib/bff.ts:81-86,108-115` does a bare `await connect()` with no
try/catch and drops the reference on rethrow, so a Valkey outage leaks a client per probe (5s
interval). Add try/catch and `destroy()` on the failure path.

**Acceptance:** a unit spec asserting `destroy()` is called when `connect()` rejects. (Leak
magnitude is inferred, not measured — the structure is the defect.)

---

## P9 — Coverage and documentation

**Audit:** R14, R15 · findings F18, F22, F19, F21, F29, F30, F58, F66, F56, F62, F55, F64, F28,
F63, F39, F68
**Depends on:** P7, P8. None of these change product code.

### P9.T1 — Behavioural specs for `/consent` and `/invitation` *(highest value)*

Seven wallow-auth routes are reachability-only (`e2e/routes.spec.ts:11-26` is their sole
reference): `/consent`, `/invitation`, `/accept-terms`, `/privacy`, `/terms`, `/error`, `/`.

- `/consent` is the screen a fork hits first and where scopes are granted. Drive it with the
  seeded **`bcordes-bff`** client (`FirstPartyClients: []` means it reaches consent).
  **Do not use `bcordes-web-client`** — it lives in `_productionExampleClients` and the seeder
  ignores it.
- `/invitation`'s accept path is where P7.T6's dead-end lives; today only the no-token arm is
  exercised.

### P9.T2 — One prefixed (`AUTH_BASE_PATH=/auth`) Playwright run

Unit coverage of the wiring exists (`base-path-wiring.test.ts:57,64,96,128`); what is unguarded
is the prefixed **browser render**, so "renders but never hydrates" under a prefix would ship
undetected. **This is the acceptance gate for P7.T1 and P7.T2.**

### P9.T3 — Documentation corrections

- Document the **fork-migration consequence of P3.T5** (the `/api` seam move) in
  `docs/operations/reverse-proxy.md` and `docs/integrations/bff-pattern.md`.
- Document, next to wallow-auth's passthrough routes, the undocumented chain that makes them
  load-bearing: `AuthorizationController.cs:55,65,70` emits a relative `returnUrl` that resolves
  onto the auth origin's `/connect/$` (`packages/sdk/src/auth-oidc.ts:258`). **These routes look
  deletable and carry every sign-in.**
- Name the dev issuer topology as the **sole** consumer of `/.well-known/$` — both shipped
  stacks override the issuer onto the API (`docker-compose.test.yml:151`,
  `docker-compose.production.yml:390`) and CI skips the discovery probe
  (`scripts/e2e.sh:138-164`).
- Document the back-channel-logout bound in `bff-pattern.md`: `bff-server.ts:149-154` exposes
  only login/callback/user/logout and logout is a browser POST, so production's
  `ValkeySessionStore` revocation capability is paid for and half-used.
- `docs/integrations/bff-pattern.md:56` sends readers down a nonexistent *Settings >
  Applications* path — retarget to `/dashboard/apps` → Register Application. **Confirm 1442's
  F4.T3 has not already fixed this.**

### P9.T4 — Replace source-text assertions with real request assertions

`apps/wallow-web/src/routes/bff-routes.test.ts` uses `readFileSync` + `toContain` with no
`Request` and no `routeTree.gen.ts` check, so it passes on a wrong preset or a route missing
from the tree. `passthrough-routes.test.ts:45-46` and `src/lib/api-passthrough.test.ts:31-34`
make two text assertions a 404-only handler would pass, with the SDK mocked out. `/connect/$`,
`/.well-known/$`, and `/health` have no request-level coverage anywhere.

### P9.T5 — Remaining coverage gaps

- Extend `login-journey.spec.ts` past the list pages — `settings`, `apps/register`, `inquiries`,
  and `inquiries/$inquiryId` have no e2e reference at all. **Combines with 1442's F4.T1.**
- Port wallow-auth's 404 assertions (`e2e/routes.spec.ts:55-68`) to
  `apps/wallow-web/e2e/routes.spec.ts`; `root-not-found` (`__root.tsx:119`) has exactly one
  repo-wide hit — its own definition.
- Add `/health` to the backend-free `e2e/routes.spec.ts`; add a click-through assertion on the
  logout hand-off to `/connect/$` (`logout.spec.ts:43` only string-matches an `href`); add a
  one-line assertion against bare `/bff` and `/api` to settle whether TanStack's `$` splat
  matches a bare prefix — **if it does not, the SDK's root arms (`bff-server.ts:120-123`,
  `proxy.ts:601-607`) are dead code** and a follow-up bead deletes them.
- Unit specs for the three root boundary components (`__root.tsx:95,119,132`) — those testids
  appear nowhere else, so the last-resort UI has zero coverage.
- Align `apps/wallow-web/playwright.config.ts:30`'s `OIDC_ISSUER` default from `:5001` to
  `:3002`, matching `Wallow.AppHost/Program.cs:99-103`.
- Fix `PublicLayout.test.tsx:10,22,62,64`, which stubs `Link` as an `<a>` and asserts only
  `href`, so a real `Link` regression would pass.
- Add a build-output spec proving the client chunks exclude `redis` and `openid-client` — the
  three dynamic-import guards in `routes/{bff/$.ts:22, api/$.ts:23, health.ts:23}` contradict
  `apps/wallow-auth/src/routes/v1/$.ts:3`'s static import, and nothing verifies either choice.

---

## Definition of done

- [ ] `pnpm check` green (format:check + lint + typecheck + test + build + check:exports).
- [ ] `./scripts/run-tests.sh` green — P2.T4 is the only intended backend change.
- [ ] `./scripts/e2e.sh` green (wallow-auth, wallow-web, cross-app).
- [ ] The prefixed (`AUTH_BASE_PATH=/auth`) Playwright run from P9.T2 green.
- [ ] No comment in either app's `src/routes/**` describes an architecture that no longer exists.
- [ ] `/bff-demo` deleted **and** its `setCsrfToken` example live in
      `docs/integrations/bff-pattern.md`.
- [ ] Every cross-boundary path literal has one definition and a failing-on-drift test —
      including the C#↔TypeScript gate (P3.T4).
- [ ] CI fails on a stale `routeTree.gen.ts` and on a route missing from the reachability gate.
- [ ] Every `path:` literal in both `routeTree.gen.ts` files unchanged, except the deliberate
      additions in P2.T2 (removal), P6.T2 (`/dashboard/` index), and P3.T5 (the seam mount).
- [ ] No `data-testid` removed or renamed without an explicit note on the owning bead.
- [ ] P2.T4's breaking change recorded in the commit and reflected by release-please.

## Risks

| Risk | Mitigation |
| --- | --- |
| P4 changes all 12 routes' search parsing at once | Full wallow-auth e2e is the gate; wire-level param names asserted unchanged by diffing produced URLs |
| P3.T5 changes the fork's frontend contract | P3.T1 lands first so it is a one-constant change; fork-migration note is required output of both the bead and P9.T3 |
| P3.T5 has no automated verification (no suite drives the Caddy topology) | Manual production bring-up is task acceptance; a follow-up bead for a topology smoke test is required output |
| P8.T3's allowlist breaks a fork calling an unlisted prefix | Conservative list derived from P3.T2's constant; full `e2e.sh` exercises the real `/v1` surface; `/v1/branding/**` exclusion recorded as a conscious decision |
| P8.T4's CSRF gate breaks the OIDC hand-off | Ships log-only; enforcement is a separate bead gated on clean logs |
| P2.T4 breaks forks driving MFA enrollment out of band | Breaking-change marker in the commit; zero producers in this repo verified by repo-wide grep |
| P7.T5's F33 work collides with 1442's F4.T2 | Sequenced last in P7; bead owner coordinates if 1442 F4 is still in flight |
| P8.T1's timeout is too aggressive and breaks long API calls | Value chosen against the slowest real endpoint, recorded on the bead |
| P5 regenerates every wallow-auth route file | `routeTree.gen.ts` `path:` diff is the acceptance gate; `_auth` is pathless so URLs cannot move |
| Deleting a comment that encodes a live invariant | P1's ordering hazard is explicit: `wallow-auth/src/routes/index.tsx` is edited in P7.T1, not P1.T1 |

## Traceability

Findings with no task in this plan, by design:

- **F40, F45, F46, F53** — Low/informational, no step in the audit's own plan either.
- **F1, F9, F49, F55** — owned by the 1442 plan.
- **F71** — a positive finding (no codegen drift); P6.T3 protects it.
