**status: active**

# Bug remediation — design

**Epic:** Wallow-4pwv
**Audit:** `docs/plans/2026-07-30/1033-bug-bead-remediation.md`
**Implementation:** `docs/plans/2026-07-30/1102-bug-remediation-implementation.md`

This is the "why" document. It records what was measured, the decisions taken, and the
alternatives rejected. The implementation doc is the "how" and assumes every decision here.

Every number below was measured on the tree at `fb161782`, not recalled from the beads. Two
beads' diagnoses changed as a result — read §1 and §3 before touching either.

---

## 0. Scope

Six bug-typed beads. Five need code; one needs a confirming run.

| Bead        | Defect                                                   | Blast radius                     |
| ----------- | -------------------------------------------------------- | -------------------------------- |
| Wallow-jtdg | docfx output-path collision, unreachable API reference   | Published docs site              |
| Wallow-i3hr | Nested oxlint configs bypass the root config             | `packages/ui`, `packages/forms`  |
| Wallow-s7j6 | Stale test assertion vs. correct AppHost value           | `./scripts/run-tests.sh` is red  |
| Wallow-3q9c | 5 s vitest default vs. a 19 s cold import                | `pnpm test` is red under load    |
| Wallow-ll6c | E2E specs pin compose-only endpoints                     | Local Aspire E2E runs            |
| Wallow-gigs | Cold-start fragility — **already fixed**, needs a run    | None                             |

They share no files. Ordering is by risk, not by dependency.

---

## 1. Wallow-jtdg — the diagnosis in the bead is wrong

### What the bead says

`docfx/toc.yml` wins the output-path collision, `docs/toc.yml` is discarded, and **no page has a
sidebar**.

### What actually happens

It does not reproduce. Measured four ways:

- `dotnet docfx build docfx.json` on a clean `.docfx/_site`, three consecutive times.
- `dotnet docfx docfx.json` — the **full** metadata+build pipeline, which is what
  `.github/workflows/docs.yml:101` and `docker/docs/Dockerfile:21` actually run. The bead's repro
  used `docfx build`, which is not the published path.

In all four runs **`docs/toc.yml` won**. `.docfx/_site/toc.json` carries all seven top-level
sections (Overview, Getting Started, Architecture, Development, Operations, Integrations, API
Reference) and the sidebar renders. docfx version 2.78.5.

The bead was almost certainly written against a stale `.docfx/_site` — the output directory is
never cleaned between builds, which is also why deleted-and-excluded content (`_site/plans/`,
`_site/claude/`, `_site/CLAUDE.html`) is still sitting in it.

### What is still genuinely broken

The headline symptom is gone; three real defects are not.

**1. The build resolves a real collision by an undefined tie-break.** All four warnings persist,
verbatim:

```
docfx/toc.yml: warning: Unable to find either toc.yml or toc.md inside docs/
docfx/toc.yml: warning: Unable to find either toc.yml or toc.md inside api/
warning: DuplicateOutputFiles: ... "docfx/toc.yml, docs/toc.yml"   (x2)
```

Two content entries still write `toc.json` to the site root. docfx picks a winner by a rule we do
not control and do not pin. The sidebar renders **by luck, not by construction** — a docfx bump or
a file-ordering change flips it silently, and the build still exits 0.

**2. The generated .NET API reference has no navigation at all.** `.docfx/api/toc.yml` is 109 KB of
namespaces and types. `.docfx/_site/api/toc.json` **does not exist**. `docs/toc.yml`'s "API
Reference" section points only at the hand-written `api/service-accounts.md`. So the entire
generated API surface — the thing `docfx.json`'s whole `metadata` block exists to produce — is
reachable only by direct URL. This is the more valuable half of the bead and it is not stated in it.

**3. `docfx/toc.yml` is dead config.** Both its hrefs (`docs/`, `api/`) point at directories that
contain no toc. That is precisely what warnings 1 and 2 report.

### Decision: option B (flatten), and the guard is the main deliverable

The bead offered two options. Take **B**: delete the `{ "src": "docfx" }` content entry and
`docfx/toc.yml`, leaving `docs/toc.yml` as the single site-root toc.

Rejected **option A** (nest under `dest: "docs"`): it prefixes every published docs URL with
`/docs`, and `api/branding.json` already publishes `docsUrl: https://bc-solutions-coder.github.io/wallow/`
with `README.md` carrying eight inbound links to the current flat paths. Paying a URL migration to
restore a two-tab split nobody currently sees is the wrong trade.

The change is small enough that the **regression guard is the actual work**. Without it we go back
to a config that renders correctly by accident. With it, the invariant is stated once and checked
on every run. Model it on `packages/sdk/src/query-rule-docs.test.ts`, which already pins prose
contracts from a vitest spec — same mechanism, applied to the built toc.

Wiring the generated API toc into the sidebar is the second deliverable and closes defect 2.

### Not in scope

`docs/integrations/integration-cookbook.md:25` has an `InvalidBookmark` — it links to
`docs/development/frontend-setup.md#1-depend-on-all-five-packages`, which has a wrong path (a
`docs/` prefix from inside `docs/`) and a nonexistent anchor. Found in the same build output, filed
separately as its own bug bead. Do not fold it in.

---

## 2. Wallow-i3hr — the config fix is one line; the fallout is 981 diagnostics

### Confirmed

`packages/ui/.oxlintrc.json` and `packages/forms/.oxlintrc.json` each contain only a `$schema` and
one rule, with **no `extends`**. oxlint's nested-config discovery therefore uses those files alone,
and the root's plugins, categories, rules, `ignorePatterns` and overrides never apply to either
package. `scripts/fork-smoke/.oxlintrc.json` has `"extends": ["../../.oxlintrc.json"]` and is the
correct model.

Still latent, not live: neither package imports `@tanstack/react-query`, and `forms` has spec-level
enforcement via `src/core/query-facade.test.ts` regardless.

### Measured

Added `extends` to both, ran `pnpm exec oxlint packages/ui packages/forms`, reverted:

| Cut            | Count                                                                     |
| -------------- | ------------------------------------------------------------------------- |
| **Total**      | **981** (3 errors, 978 warnings — and `pnpm lint` is `--deny-warnings`)   |
| `packages/ui`  | 961                                                                       |
| `packages/forms` | 20                                                                      |
| tests          | 665                                                                       |
| stories        | 283                                                                       |
| **real src**   | **33**                                                                    |

Top rules: `unicorn/prefer-dom-node-dataset` 443, `react/jsx-max-depth` 378, `no-magic-numbers` 89,
`unicorn/prefer-number-coercion` 29.

The three errors:

- `packages/forms/src/core/query-facade.test.ts:127` — `oxc/no-map-spread`
- `packages/ui/src/components/otp-field/otp-field.stories.tsx:96` — `eslint/no-await-in-loop`
- `packages/ui/src/index.test.ts:406` — `unicorn/prefer-set-has`

### The decisive finding: inheritance is not broken

Root `overrides` **do** inherit through `extends`. Proof: `no-magic-numbers` fires **zero** times in
`.test.*` files under the nested packages, exactly as the root's `**/*.test.*` override dictates,
while firing 23 times in real src. So the fix is not blocked by an oxlint limitation — the 981 are
genuine, previously-unlinted diagnostics.

### Why 940 of the 981 are noise, not debt

Two structural mismatches, both in the root config:

1. **The root test override covers `**/*.test.*` but not `*.stories.tsx`.** In `packages/ui`,
   stories are not demos — per `.claude/rules/TESTING.md` the `storybook` vitest project executes
   every story as a test case. They are test code held to production rules. That is 283 diagnostics.
2. **Two rules are structurally incompatible with a Base UI component library.**
   `unicorn/prefer-dom-node-dataset` (443) fires on `getAttribute("data-*")`, which
   `packages/ui/CLAUDE.md` designates as the documented way to assert component state.
   `react/jsx-max-depth` (378) fires on composite Base UI part trees, which nest by design — the
   rule is already set to 2 and `packages/forms/CLAUDE.md` records splitting `SelectField`'s portal
   into one component per level to satisfy it.

### Decision: fix the root config, then fix the 33

Do not silence the root's categories inside the nested files — that recreates exactly the bug being
fixed, just less visibly. Instead:

1. Add `extends` to both nested configs.
2. Widen the root's test override to `*.stories.tsx`, and disable those two rules for test and story
   files under `packages/ui` and `packages/forms`, with a comment naming the reason.
3. Fix the ~33 real src diagnostics properly.

Each of those exemptions must **narrow by construction** — re-declare the rule minus the entries
that do not apply, never switch a category off wholesale.

### Second half: the guard that would have caught this

`packages/sdk/src/oxlint-guardrails.test.ts` reads only the root `.oxlintrc.json`. Nested configs
are invisible to it, so it could not have caught this and does not cover
`scripts/fork-smoke/.oxlintrc.json` either. Widen it to sweep **every** `.oxlintrc.json` in the tree
and assert each non-root config either extends the root or re-declares a rule minus one entry.
Without this the fourth nested config reintroduces the hole.

---

## 3. Wallow-s7j6 — the AppHost is right, the test is stale

`api/src/Wallow.AppHost/Program.cs:90-91`:

```csharp
.WithEnvironment("OIDC_ISSUER", "http://localhost:3002")
.WithEnvironment("OIDC_METADATA_URL", "http://localhost:5001/.well-known/openid-configuration")
```

This is deliberate and documented. In dev the API's issuer is the **wallow-auth origin** —
`appsettings.Development.json` sets `AuthUrl: http://localhost:3002`, which
`OpenIddictIssuerResolver` uses — so the client must expect `:3002` as the issuer while fetching
discovery from the API directly on `:5001`. The split is the whole point.

`AppHostEnvironmentWiringTests.cs:54` asserts `http://localhost:5001`. The test is wrong.

**Decision:** fix the assertion, and add the `OIDC_METADATA_URL` assertion that is currently
missing. The two values are only correct **as a pair** — an issuer of `:3002` with a metadata URL of
`:3002` would be broken in a way today's test cannot see. Pinning one without the other is what let
this drift in the first place.

Also correct the test's doc comment, which cites `docker-compose.test.yml` as the "known-correct
mirror". It is not a mirror: compose uses `:5050` for both values because the containerised topology
differs. Saying so prevents the next reader from "fixing" the test back.

---

## 4. Wallow-3q9c — a budget problem, not a hang

`packages/testing/src/vitest-projects.ts`'s node project sets no `testTimeout`, so vitest's 5000 ms
default applies. The first test in each app's `__root*` spec pays a one-time cold
`await import("./__root")` Vite transform of the whole route graph: **19 043 ms** measured for
`apps/wallow-auth/src/routes/__root.provider.test.tsx`. The second test in the same file: **1 ms**.

It only fails when the node project competes with headless Chromium for CPU, which is why
`vitest run --project node` alone passes 174/174.

Two things the bead already ruled out, so nobody re-litigates them:

- **Not the `packages/ui` barrel.** Pointing `__root.tsx` at the ui subpaths instead of the
  36-component root barrel changed 19 043 ms to 18 805 ms. The cost is the app's own route graph
  (TanStack Router/Start + react-query).
- **Not a hang.** The tests themselves are ~1 ms. Only the first import is slow.

**Decision:** set `testTimeout: 60_000` on the **node** project inside `createVitestProjects`.

- The shared preset, not per-app: the cost is structural, and every app that SSR-tests a route root
  pays it. Both current apps already do.
- 60 s, not 90 s: it clears the measured 19 s with 3× headroom while still failing a genuine hang in
  a bounded time. The bead's `--testTimeout=90000` probe was a diagnostic, not a proposal.
- Browser project untouched: it does not pay this cost, and widening its budget would mask real
  actionability timeouts, which is exactly how a Playwright-backed suite goes quietly bad.
- Document the number in the module header. An undocumented 60 s reads as superstition later.

**Expected result:** `wallow-auth` 812/815, `wallow-web` 558/559 — the counts the Wallow-m5aq.2.14
Wave 1 gate recorded. The residual failures are Wallow-jx7f specs and are out of scope.

---

## 5. Wallow-ll6c — two of three parts survive

Part 3 (the dev passwordless rate limiter) is **stale**: `appsettings.Development.json` now sets
`Passwordless.RateLimitMaxRequests: 1000`. Drop it.

Parts 1 and 2 are real. Both hardcode values that exist only in
`docker/docker-compose.test.yml`, so a bare `pnpm backend` (Aspire) run cannot pass:

- `apps/wallow-auth/e2e/mailpit.ts:20` — `http://127.0.0.1:8035` is the compose mapping
  (`127.0.0.1:8035:8025`). Under Aspire, mail lands on `:8025`. Affects magic-link, otp-login,
  reset-password, mfa. `E2E_MAILPIT_URL` exists as an override but the **default** is what breaks
  the bare run with `ECONNREFUSED`.
- `apps/wallow-auth/e2e/logout.spec.ts:21` — `http://localhost:5051/after-logout` is the compose
  stack's `AuthUrl`, which `OpenIddictRedirectUriValidator` allow-lists unconditionally. Under
  Aspire the `AuthUrl` is `:3002`, no return link renders, spec fails.

**Decision:** env-first, compose-default — the pattern both `playwright.config.ts` files already use
for `WALLOW_API_INTERNAL_URL`. No new mechanism, no auto-detection of run mode: a knob the runner
sets is debuggable, and mode-sniffing is how E2E suites become unexplainable.

Keep the IPv4 literal `127.0.0.1` in the compose default and keep the comment explaining it —
compose publishes IPv4-only and `localhost` resolves to `::1` first on many hosts, so "simplifying"
it to `localhost` silently breaks the container path.

---

## 6. Wallow-gigs — already fixed

Both filed symptoms are addressed in `apps/wallow-auth/e2e/global-setup.ts`:

- **Cold-compile overrun** → `WARMUP_PATHS` walks every interactive entry route to
  `data-app-ready` on a 120 s budget before any spec runs.
- **Stale dev server hijack via `reuseExistingServer`** → `assertAppTargetsTheExpectedApi()` reads
  the OIDC discovery `issuer` through the app under test, compares it to the issuer read from
  `WALLOW_API_INTERNAL_URL`, and throws with an `lsof` diagnostic on mismatch. It identifies the
  backend without hardcoding a URL, so a correctly-wired dev server is still reused as intended.

**Decision:** close after one confirming local-mode run. No code.

Note for whoever picks it up: `apps/wallow-web/e2e/global-setup.ts` has the warm-up but **not** the
backend-identity check. That is defensible — the bead is auth-specific and wallow-web's local E2E
surface is the backend-free `routes.spec.ts` — but if wallow-web ever grows a backend-dependent
spec, that gap becomes the same bug. Do not fix it under this bead; file it if it matters.

---

## Sequencing

Risk-ordered, not dependency-ordered — nothing here blocks anything else.

1. **Wallow-s7j6** and **Wallow-3q9c** first. Both are a handful of lines, both currently make a
   suite red, and a green baseline makes every later change legible.
2. **Wallow-jtdg** next. Highest user-facing value, and the guard is the real work.
3. **Wallow-i3hr** after that. 981 diagnostics is a real slog; do not start it with other work in
   flight.
4. **Wallow-ll6c** last — it needs a live stack in both modes to verify.
5. **Wallow-gigs** whenever a local-mode E2E run happens for any other reason.
