**status: active**

# Backlog triage and sequencing — 2026-08-27

The last commit and the last bead touch are both from 2026-08-05; this plan re-enters the
backlog after a ~3-week gap. Every open bead below was **re-verified against the working
tree today**, not taken on trust. Verdicts are `KEEP`, `CLOSE` (already true), `MERGE`,
or `CORRECT` (premise is wrong and the bead must be edited before anyone works it).

## 0. The tree is dirty, and one closed bead's fix is in it

`git status` shows nine modified files spanning **two unrelated topics**, neither committed:

| Files | Bead | State |
| --- | --- | --- |
| `DeleteBucketHandler.cs`, `DeleteFileHandler.cs` + their two test files | **Wallow-rqqx** | **CLOSED, but the fix is not on `main`** |
| 4 comment-only edits in `packages/ui`, `packages/forms` | **Wallow-4djs** | in_progress; bead note says "sweep done (not committed)" |
| `docs/getting-started/developer-guide.md` (`bd note` row + `.beads/README.md` caveat) | — | unattached docs edit |

`git show HEAD:...DeleteFileHandler.cs` still has the old `DeleteAsync`-before-`SaveChanges`
ordering. So Wallow-rqqx is marked closed while the correctness fix it describes exists only
in this working tree. **This is the single highest-priority item** — a machine failure loses a
closed bug's fix, and `Wallow-41it` is filed as a follow-on to an ordering that isn't on `main`.

**Action:** split into two or three commits and push before starting anything else.

## 1. Verdicts

### CLOSE — already true, no work left

| Bead | P | Evidence |
| --- | --- | --- |
| **Wallow-x271** delete stale `Wallow.Billing.Tests` | P3 | `api/tests/Modules/Billing` does not exist. Done by something else. |
| **Wallow-6r58** thread base path into wallow-web/minimal-app branding | P3 | Precondition still unmet (neither app has a prefix knob), the 2026-08-02 audit already voted CLOSE, and its file paths are stale — it cites `apps/examples/minimal-app`, but the app is `apps/minimal-app`. A conditional filed against a trigger that has not fired and paths that no longer resolve. |
| **Wallow-4djs** stale `DashboardNav` comments | P3 | The sweep is done in the working tree; only `docs/plans/**` (historical, correctly frozen) and one deliberate "pre-extraction" mention in `packages/navigation/CLAUDE.md` remain. Close on commit. |

### MERGE — exact duplicates

**Wallow-yhfc** (P2, bug) and **Wallow-tmy4** (P3, task) are the same defect: `resolve_filter()`
in `scripts/run-tests.sh` has `*) echo "$MODULE_FILTER" ;;`, so a typo'd module name reaches
`dotnet test` as a path. Both confirmed live. The *second* argument (tier) already validates
loudly at `scripts/run-tests.sh:76-80` — that arm is the template for this fix.

**Action:** keep **Wallow-yhfc** (has the docs-audit provenance), fold `tmy4`'s acceptance
criteria into it, close `tmy4` as duplicate.

### CORRECT — the bead's stated premise is wrong

| Bead | What the bead says | What is actually true |
| --- | --- | --- |
| **Wallow-7zav** (P2, decision) | "nothing in the codebase consumes any of them" | **False.** `ServiceAccountsRead/Write/Manage` are consumed by `ScopePermissionMapper.cs:49-51` (scope→permission) and granted in `RolePermissionMapping.cs:43-45`. Only the *controller guard* doesn't check them. So option B ("delete the three members") would break scope mapping and role grants — the decision is now effectively one-sided (narrow, or accept the gap), not the fifty-fifty the bead frames. |
| **Wallow-luni** (P2, bug) | fix direction: "extend the alias to cover `use-sync-external-store/shim/with-selector`" | **The code now argues the opposite.** `packages/config/src/vite/app.ts:67-74` uses anchored regexes *specifically so that* `with-selector` is NOT swallowed, because React ships no `useSyncExternalStoreWithSelector` and the alias would rewrite it to a nonexistent `react/with-selector`. The bug (one `__require("react")` chunk per zoned app, mechanism confirmed in the bead notes) is real; the fix direction is not. Needs a new one — likely `ssr.noExternal` or a shim, not `resolve.alias`. |
| **Wallow-ut4w** (P3, decision) | decide whether the comment-free-JSON rule earns its keep | **Half-resolved already.** `scripts/fork-smoke/README.md:46-54` now states the rule is unenforced, its justification is gone, and "adding comments would be an improvement, not a violation". The decision is made in prose; only the follow-through (add the comments, delete the sentence) is left. Downgrade to a 10-minute chore. |

### KEEP — verified live, premise intact

| Bead | P | Verified today |
| --- | --- | --- |
| **Wallow-gwy2** e2e.sh reuses stale app image | P1 | No `--build` anywhere in `scripts/e2e.sh`; `E2E_SKIP_IMAGE_BUILD` still gates only the .NET publish (`:82`). |
| **Wallow-tvn3** four non-proxy-aware `request.ip` sites | P2 | All four confirmed at the exact cited lines; `trustProxy` appears nowhere. |
| **Wallow-qck0** `WallowApiFactory` process-global env | P2 | Six `SetEnvironmentVariable` at `:75-86`, six nulled at `:120+`; both collection fixtures confirmed (`PerformanceTuningTests.cs:92`, `Integration/ApiIntegrationTestCollection.cs:5`). |
| **Wallow-nggf** three unwired vitest browser projects | P2 | `apps/minimal-app`, `packages/logger`, `packages/testing` — none has a `vitest.setup.ts`. |
| **Wallow-vpci** five unused Dapper refs | P3 | Five `PackageReference Include="Dapper"` in Announcements/Inquiries/Notifications/ApiKeys/Branding Infrastructure, pin at `Directory.Packages.props:75`, zero source usages. Note `Directory.Build.props:35` also carries a `DAP005` NoWarn to sweep. |
| **Wallow-uz0w** duplicate MigrationWorker tests | P3 | Both at `MigrationServiceTests.cs:60` and `:145`. |
| **Wallow-okkk** styles not `"private": true` | P3 | Confirmed absent from `packages/styles/package.json`. |
| **Wallow-hr0p** Storage module CLAUDE.md | P3 | Only `README.md` present. |
| **Wallow-bqoq** wallow-auth README + CLAUDE.md | P3 | Neither exists at any spelling. |
| **Wallow-x5da**, **Wallow-xzy1.6** | P2 | Load-dependent flakes; not statically verifiable. Both specs/configs still present. Keep, but see §3. |
| **Wallow-41it**, **Wallow-m4u7**, **Wallow-qi90.2**, **Wallow-whsz**, **Wallow-joo0**, **Wallow-q2no**, **Wallow-lgto**, **Wallow-pb92**, **Wallow-xzy1.2** | P2/P3 | Premises unchanged; all are genuine deferred work with no urgency. |

### New — found during this triage

**`apps/tanstack-min/` is a gitignored orphan.** Zero tracked files, contents are only
`node_modules/` and `public/`, no `package.json`, so pnpm does not resolve it. Same class as
the Billing directory `Wallow-x271` was filed for. Last referenced by commit `08d402c8`
("rename tanstack-min app"). File a one-line chore, or just delete it.

**Plan status hygiene.** Several finished plans are still `status: active` — notably
`2026-08-03/1206-turborepo-implementation.md` and `-results.md` (Phases 1-2 shipped per
Wallow-m4u7) and `2026-08-03/1148-testing-guards-implementation.md`. Not worth a bead; fix
in passing.

## 2. Sequencing

Ordered by *risk retired per hour*, not by priority label.

### Step 0 — land what's already written (today, ~20 min)

1. Commit the Storage ordering fix + its two tests → `fix(storage): commit removals before deleting objects` (cites Wallow-rqqx).
2. Commit the comment sweep → `chore(ui): retarget stale DashboardNav comments at AppNav`; close **Wallow-4djs**.
3. Commit the developer-guide edit → `docs: document bd note and the untracked .beads/README`.
4. `./scripts/run-tests.sh storage` + `pnpm check`, then `git push` **and** `bd dolt push`.

Nothing else starts until this is on `origin`.

### Step 1 — triage mutations (today, ~15 min) — **DONE 2026-08-27**

Close `x271`, `6r58`; merge `tmy4` into `yhfc`; rewrite the premise paragraphs on `7zav` and
`luni`; downgrade `ut4w`; file the `tanstack-min` chore. All are `bd` operations.

Executed, with every verdict re-verified against the tree first:

| Bead | Action taken |
| ---- | ------------ |
| `x271` | **Closed.** Acceptance already satisfied — `api/tests/Modules/Billing` is gone from disk. |
| `6r58` | **Closed**, after satisfying the close-precondition the 2026-08-02 audit attached to it. Its guidance was relocated to `apps/wallow-auth/src/shared/lib/branding.ts` (a "copy this module" paragraph naming both apps) and to a new *Giving another app a path prefix* subsection in `docs/getting-started/fork-guide.md`. |
| `tmy4` → `yhfc` | **Merged.** `yhfc`'s acceptance now carries the enumerated shorthand list and tmy4's `api/CLAUDE.md` requirement; `tmy4` closed as a duplicate. Noted on `yhfc` that the tier arm at `scripts/run-tests.sh:76-80` is already the shape to copy. |
| `7zav` | **Premise rewritten.** The "nothing consumes them" claim was false: the three are scope-mapped, role-granted and seeded, and merely never *checked* (0 `HasPermission` sites). Delete is now the expensive option, so the decision is one-sided rather than a genuine fork. |
| `luni` | **Fix direction rewritten** into the description, where the wrong one lived. Both dead ends (inert `ssr.external`, no ESM entry to re-alias) and the three remaining options are now in the description rather than buried in notes. |
| `ut4w` | **Downgraded and retargeted** to a chore. The decision it asked for was already made in prose at `scripts/fork-smoke/README.md:46-54`; only documenting the load-bearing `extends` and trimming the README remain. Retitled to match. |
| `9wqq` | **Filed** — the untracked `apps/tanstack-min/` orphan (0 tracked files, no `package.json`). Filed rather than deleted, since it may hold local work. |

Also in passing: the three finished `2026-08-03` plans still marked `status: active`
(`1206-turborepo-implementation`, `1206-turborepo-results`, `1148-testing-guards-implementation`)
are now `completed`.

### Step 2 — the P1 that makes every other verification trustworthy — **DONE 2026-08-27**

**Wallow-gwy2** (stale E2E image). Everything downstream — the two flakes, any E2E-backed
confidence in a fix — is worth less while a green E2E run can be testing three-week-old code.
Fix direction one from the bead (`up -d --wait --build` for the two services with a build
block) is closest to how the script already reasons, and layer caching keeps a no-op rebuild
cheap. Update `.claude/rules/E2E.md` with whichever knob survives as the reuse opt-in.

Done in `14467eef`. Two things the bead had wrong, found while fixing it: there are **four**
services with a build block (`garage`, `wallow-auth`, `wallow-web`, `bff-example`), not two, and
`--build` had to be gated on `E2E_SKIP_IMAGE_BUILD` or CI would rebuild the five images
`docker-images-app` had just restored from cache. The knob is now "reuse whatever `:test` images
exist" rather than "skip the `dotnet publish`". Measured: unchanged tree rebuilds in **0s** (source
`COPY` and the build layer both `CACHED`, vs 43s cold), a one-line source change re-runs the build
in **8s**. Image ID is not a usable oracle here — BuildKit provenance gives a fresh ID on every
build, including fully cached ones.

### Step 3 — one afternoon of correctness with a shared root cause

**Wallow-tvn3** (trusted proxy, four call sites, one resolution). The bead is explicit that
four local patches are the wrong shape. This is the largest genuine *correctness* item in the
backlog and the one most likely to be forgotten before a first deploy. Land the trusted-peer
resolution in a shared module, then thread it through all four sites in one change.

### Step 4 — cheap wins, batchable into one or two commits

`vpci` (Dapper + the `DAP005` NoWarn), `okkk` (`"private": true`), `uz0w` (collapse duplicate
tests), `yhfc` (fail-loud module name), `ut4w` (add the comments). Each is minutes; together
they are one `chore:` sweep per toolchain. Doing them as a batch keeps the backlog honest
without burning a session on any one of them.

### Step 5 — authoring work, do when writing suits you

`bqoq` (wallow-auth README + CLAUDE.md) and `hr0p` (Storage CLAUDE.md). Both are pure
authoring against an existing template; neither blocks anything.

### Step 6 — the flakes, together, under load

**Wallow-x5da** and **Wallow-xzy1.6** are separate defects but share a reproduction harness:
both only appear under concurrent full-workspace `turbo run test`, and both point at the same
`fileParallelism` question raised in `docs/plans/2026-08-03/1639-proxy-trust-react-dupe-nav-flake.md`.
Chase them in one session with a repeat-under-load loop, not separately. Budget for the
possibility that the answer is a vitest config change that fixes both.

### Deferred, deliberately — do not pull these forward

`m4u7` (turbo remote cache — local benefit already banked, nothing blocked), `qi90.2` (DLQ
observability — nothing deployed), `whsz` (front-channel logout — needs spec work OpenIddict
7.6 doesn't provide), `q2no` (BFF response validation — bead's own prerequisite is an OpenAPI
fidelity fix), `joo0` (concurrent e2e isolation — only bites with two agents at once),
`41it` (orphan sweep — disk cost, and it wants Step 0 landed first), `lgto`/`pb92` (lint
rules replacing deleted source-reading specs — real, but the invariants they guard are not
currently being violated), `xzy1.2` (Dockerfile package-build step — needs a real docker
build to settle), `luni` (real bug, but needs a new fix direction first), `7zav` (decision,
now one-sided — record it and move on).

## 3. What this leaves

After Steps 0-2: **3 beads closed, 1 merged, 3 corrected, 1 filed.** Open count goes 28 → 24,
and every remaining bead has a premise verified today. After Steps 3-5 the backlog is
~15 beads, all genuinely deferred rather than merely unexamined.
