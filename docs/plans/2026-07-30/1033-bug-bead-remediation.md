**status: active**

# Bug-bead remediation plan (2026-07-30 audit)

Audit of all 16 open beads, then a work plan for the 6 bug-typed ones. Every claim below was
re-verified against the tree at `fb161782` before being kept; three beads did not survive.

## Part 1 — Audit results

Database: 2413 total, 2396 closed, **15 open + 1 in progress**. There is no `bug` *label* in this
repo — `bd list --label bug` returns nothing. "Bug" is the `issue_type`, and 6 open beads carry it.

### Bug-typed beads

| Bead             | P   | Verdict                | Evidence                                                                                            |
| ---------------- | --- | ---------------------- | --------------------------------------------------------------------------------------------------- |
| **Wallow-jtdg** | P1  | CONFIRMED              | `docfx.json` still has two `build.content` entries emitting `toc.json` to the site root              |
| **Wallow-i3hr** | P2  | CONFIRMED              | `packages/ui/.oxlintrc.json` + `packages/forms/.oxlintrc.json` have no `extends`                     |
| **Wallow-s7j6** | P2  | CONFIRMED + duplicate  | test `:54` expects `:5001`; `Wallow.AppHost/Program.cs:90` sets `:3002`. Same defect as Wallow-nafw |
| **Wallow-3q9c** | P2  | CONFIRMED              | `packages/testing/src/vitest-projects.ts` sets no `testTimeout` on the node project                  |
| **Wallow-ll6c** | P3  | CONFIRMED (2 of 3)     | `e2e/mailpit.ts:20` and `e2e/logout.spec.ts:21` still hardcode compose ports; part 3 is stale        |
| **Wallow-gigs** | P3  | **RESOLVED — close**   | `apps/wallow-auth/e2e/global-setup.ts` now fixes both halves (see below)                             |

**Wallow-gigs is done.** It filed two symptoms: Vite cold-compile overrunning the readiness budget,
and a stale dev server on `:3002` being hijacked via `reuseExistingServer`. Both are addressed in
`apps/wallow-auth/e2e/global-setup.ts` — a 120 s route-by-route warm-up to `data-app-ready`, and
`assertAppTargetsTheExpectedApi()`, which compares the OIDC discovery `issuer` served through the
app against the one read from `WALLOW_API_INTERNAL_URL` and throws with a `lsof` diagnostic on
mismatch. Close after one confirming suite run.

**Wallow-ll6c part 3 is stale.** `api/src/Wallow.Api/appsettings.Development.json` now carries
`Passwordless.RateLimitMaxRequests: 1000`. Drop that sub-item; parts 1 and 2 stand.

### Non-bug beads

| Bead             | P   | Verdict                                                                                                      |
| ---------------- | --- | ------------------------------------------------------------------------------------------------------------ |
| Wallow-nafw     | P2  | **DUPLICATE of Wallow-s7j6** — same stale assertion, same fix. Close as dup                                   |
| Wallow-hjhd     | P3  | **STALE** — `oxfmt --check apps/wallow-web/README.md` passes today. Close                                     |
| Wallow-8via     | P2  | CONFIRMED — `packages/styles/src/asset-urls.ts` has no base-path awareness by construction                    |
| Wallow-ftji     | P2  | CONFIRMED — `Wallow.Api/Program.cs:192` still `AllowedButWarn`                                                |
| Wallow-td30     | P2  | CONFIRMED — `POST /v1/inquiries/{id}/comments` 201 is the **only** untyped 2xx in the whole OpenAPI snapshot  |
| Wallow-joo0     | P3  | CONFIRMED — `scripts/e2e.sh:44` hardcodes `--project-name wallow-test`                                        |
| Wallow-sqi2     | P3  | CONFIRMED — dead guard; `apps/wallow-web/src/bff-surface.test.ts` does not exist                              |
| Wallow-q2no     | P3  | Valid — spike, open by design                                                                                 |
| Wallow-lrlm     | P3  | Valid — design polish, open by design                                                                         |
| Wallow-e7wd     | P3  | In progress, **unblocked** — `chore/post-blazor-cleanup` @ `38cc58c0` exists locally and on origin            |

Net: **13 real open beads**, 3 to close (nafw dup, hjhd stale, gigs resolved).

## Part 2 — Work plan for the bug beads

Four independent workstreams. Nothing here shares a file with anything else, so they can land in any
order or in parallel.

### 1. Wallow-jtdg — docs sidebar never reaches the built site (P1, do first)

The one user-visible defect: no sidebar on any docs page.

**Take option B** (flatten). `docfx/toc.yml`'s two hrefs (`docs/`, `api/`) both point at directories
that hold no toc, and option A additionally re-prefixes every published docs URL. B keeps URLs.

1. Delete the `{ "files": ["toc.yml"], "src": "docfx" }` content entry from `docfx.json`, leaving
   `docs/toc.yml` as the single site-root toc.
2. Fold an `API Reference` section into `docs/toc.yml` pointing at the generated `api/` output.
3. Delete `docfx/toc.yml`.
4. Rebuild: `dotnet docfx build docfx.json` must emit **zero** `DuplicateOutputFiles` and zero
   `Unable to find either toc.yml or toc.md` warnings.
5. Assert `.docfx/_site/toc.json` contains every `docs/toc.yml` entry — spot-check `BFF Pattern` and
   `Integration Cookbook` — and that a rendered page shows the sidebar.
6. **Regression guard** (the bead's real ask): a doc-assertion spec in the style of
   `packages/sdk/src/query-rule-docs.test.ts` that parses `docs/toc.yml` and the built `toc.json` and
   fails when a source entry is missing. Without it the next collision is invisible again — the build
   still exits 0.

Note: the working tree already carries an uncommitted `docfx.json` change (excluding `docs/audits/**`).
Keep it; it is unrelated and correct.

### 2. Wallow-s7j6 (+ Wallow-nafw) — stale AppHost OIDC assertion (P2)

`AppHost/Program.cs:90-91` is right and documented: the dev issuer is the wallow-auth origin
(`appsettings.Development.json` → `AuthUrl: http://localhost:3002` → `OpenIddictIssuerResolver`),
while discovery is fetched from the API directly. The **test** is stale.

1. `AppHostEnvironmentWiringTests.cs:54` → expect `http://localhost:3002`.
2. Add the missing assertion for `OIDC_METADATA_URL == http://localhost:5001/.well-known/openid-configuration`
   — that pairing is the whole point and is currently unpinned.
3. Update the test's doc comment, which cites `docker-compose.test.yml` as the mirror; that file uses
   `:5050` for both, so say explicitly that the Aspire and compose topologies differ.
4. `dotnet format api/Wallow.slnx`, then `./scripts/run-tests.sh`.
5. Close Wallow-nafw as a duplicate.

### 3. Wallow-i3hr — nested oxlint configs bypass the root config (P2)

Latent config debt, not a live violation: neither `ui` nor `forms` imports react-query today, and
`forms` has spec-level enforcement (`src/core/query-facade.test.ts`). Fix the config anyway — the
next import into `ui` would be unlinted.

1. Add `"extends": ["../../.oxlintrc.json"]` to `packages/ui/.oxlintrc.json` and
   `packages/forms/.oxlintrc.json`, matching `scripts/fork-smoke/.oxlintrc.json`, which already does
   this correctly.
2. Run `pnpm lint` (`--deny-warnings`) and expect new diagnostics in both packages — the root's
   `pedantic`/`style` categories have never applied there. Fix or scope them; do **not** silence the
   root categories wholesale in the nested file, which would recreate the bug.
3. **Second half, same root cause:** `packages/sdk/src/oxlint-guardrails.test.ts` reads only the root
   config. Widen it to sweep every `.oxlintrc.json` in the tree (root, `scripts/fork-smoke`,
   `packages/ui`, `packages/forms`) and assert each either extends the root or re-declares a rule
   minus one entry — never switches it off. That is what would have caught this.

### 4. Wallow-3q9c — node-project `__root` specs blow the 5 s default (P2)

Measured: the first test in `apps/wallow-auth/src/routes/__root.provider.test.tsx` costs 19 043 ms
(cold `await import("./__root")` Vite transform of the whole route graph); the second costs 1 ms. It
is a budget problem, not a hang, and it is not caused by the ui barrel — the bead proved that by
swapping to ui subpaths for no change.

1. Set an explicit `testTimeout` on the **node** project in
   `packages/testing/src/vitest-projects.ts`'s `createVitestProjects` — the shared owner of the
   split. 60 000 ms clears the measured 19 s with headroom while still failing a genuine hang.
2. Leave the browser project alone; it does not pay this cost.
3. Verify: `pnpm --filter @bc-solutions-coder/wallow-auth test` → 812/815 and
   `pnpm --filter @bc-solutions-coder/wallow-web test` → 558/559, the counts the Wave 1 gate
   recorded. The residual failures are Wallow-jx7f specs, out of scope here.
4. Document the number in the preset's module header so it is not "mysterious 60 s" later.

### 5. Wallow-ll6c — backend-dependent auth E2E specs pin compose ports (P3)

Follow the pattern `playwright.config.ts` already uses for `WALLOW_API_INTERNAL_URL`: env first,
compose value as fallback.

1. `apps/wallow-auth/e2e/mailpit.ts:20` — keep `E2E_MAILPIT_URL` as the override, but source the
   fallback from the run mode rather than hardcoding `127.0.0.1:8035`, so a bare Aspire run does not
   die with `ECONNREFUSED`. Keep the IPv4 literal for the compose default and keep the comment
   explaining why (`localhost` resolves to `::1` first).
2. `apps/wallow-auth/e2e/logout.spec.ts:21` — derive `ALLOWED_REDIRECT_URI` from an env knob
   (e.g. `E2E_AUTH_ORIGIN`) defaulting to `http://localhost:5051`, since the allow-listed value is
   the API's configured `AuthUrl` and that differs under Aspire.
3. Drop part 3 from the bead — the dev rate limiter is already raised to 1000.
4. Verify both ways: `./scripts/e2e.sh` (container) and `pnpm backend` + the wallow-auth suite (local).

## Sequencing

- **Now:** #1 (P1, user-visible), then #2 and #4 — both are small, both currently make a suite red.
- **Next:** #3, whose step 2 has unknown fallout until `pnpm lint` is actually run.
- **Then:** #5, which needs a live stack in both modes to verify.
- **Bead hygiene, immediately:** close Wallow-nafw (dup), Wallow-hjhd (stale), and Wallow-gigs
  (resolved, after one confirming run).
