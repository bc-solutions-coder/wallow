**status: completed**

> **Outcome (2026-08-27).** Steps 1–4 all executed. The 25-run uncapped sweep executed the
> navigation suite in every run and Wallow-x5da fired zero times (P ≈ 0.03 against the
> historical 13% rate) — the "does not fire" branch was taken: no fix claimed, bead parked
> open as "trap armed", acceptance criteria amended on the bead. Wallow-xzy1.6's exact
> mocker signature reproduced 3/25 and proved to be a LOUD failure (victim file dies at
> 0 tests), refuting the unification theory below on both ends; it closed on the
> module-delivery-saturation causal story with `--concurrency=1` as the mitigation.
> Residual risks (post-completion wedge, first-run-after-dep-change re-optimization kill)
> are filed as Wallow-r6zh. Full numbers live in the two beads' 2026-08-27 notes.

# Nav-leak forensics and measurement — Wallow-x5da + Wallow-xzy1.6

Design and plan for resolving the two linked full-workspace flakes:

- **Wallow-x5da** — `packages/navigation/src/app-shell.toggle.test.tsx` > "collapses it
  again when the toggle is activated twice" fails ~2/15 uncapped full-workspace runs with a
  vetoed navigation escape to `/dashboard/organizations` plus `isNavCollapsed` stuck `false`.
- **Wallow-xzy1.6** — both app suites die with the vi.mock "error when mocking a module"
  unhandled rejection under uncapped concurrent turbo runs. Reproduced and diagnosed: a
  module-delivery failure through the Vite-dev-server-behind-Playwright-routing path under
  load, the same class as Wallow-pr34's `Failed to fetch dynamically imported module`.

The beads' own notes say whoever picks up either should pick up both; the surviving link is
the unification theory (module delivery fails under load → the `vi.mock` Link stub is
silently absent → a real anchor is the one thing in the fixture that can reach the
Navigation API).

## Ground rules inherited from the beads — do NOT re-draft these

Every one of these carries a recorded refutation or a recorded "suppression, not fix"
verdict on Wallow-x5da. They are restated here so nobody spends budget on them again:

1. **No wait-between-clicks spec fix.** Drafted once, withdrawn: Playwright's `Stable` +
   `Receives Events` actionability checks sit exactly on the failure that fix would guard
   against, so the in-document click-geometry mechanism cannot occur.
2. **No `motion-safe:` gating of the rail's `transition-[width]`.** Grepped: the bare
   transition is the majority convention across `packages/ui`; gating would be a one-off and
   would suppress, not fix.
3. **No `test.browser.fileParallelism: false`.** It removes load, not co-mounted iframes (of
   which there are none). A flake that vanishes under it proves load sensitivity, nothing
   else.
4. **No stub reordering.** The onClick-before-spread ordering defect in 4 other specs is
   real but refuted as this bead's cause (the toggle spec never clicks a Link); it is
   tracked as Wallow-xg9t.2 and stays there.
5. **No cross-iframe work.** One iframe per orchestrator page; the theory has no substrate.

## The gap the design must close

The unification theory explains how a REAL navigation is possible (stub absent → live
anchor) but not what ACTIVATES the anchor — the toggle spec's five clicks all target
`dashboard-nav-toggle`, never a link. And the reproduced xzy1.6 failure mode is a loud
unhandled rejection that kills a suite, not a silent stub absence. So the theory's two
load-bearing steps — "the stub can be absent silently" and "an unclicked anchor can
navigate" — are both unverified. The design tests them deterministically before spending
any sweep budget, and arms a permanent trap so any recurrence explains itself.

## Design

### Step 1 — Deterministic probes (throwaway, no sweeps)

Both are one-run experiments; anything built here is labelled throwaway and deleted.

- **Probe A — stub-less render.** A temporary browser spec in `packages/navigation/src/`
  renders `ShellFixture` WITHOUT the `vi.mock("@tanstack/react-router")` stub.
  - If the real `Link` throws outside a `RouterProvider`: a silently-absent stub produces a
    render crash, not the observed signature (assertion failure + escape). The unification
    theory is then dead as an explanation for x5da, and that is decisive knowledge — the
    candidate board is empty and Step 2's trap becomes the only honest path forward.
  - If it renders live anchors: click the first destination row and confirm the guard
    vetoes an escape to `/dashboard/organizations` — the theory's downstream half is then
    proven, and the open question narrows to the activation path.
- **Probe B — forensics availability.** A throwaway spec provokes an escape (the pattern
  `navigation-escape.test.tsx` already uses) and dumps what the pinned Chromium's
  `NavigateEvent` actually carries — `sourceElement`, `userInitiated`, `navigationType` —
  so Step 2 knows how sharp the trap can be. Feature-detect; do not assume `sourceElement`
  exists.

Record both outcomes on Wallow-x5da (`bd note`) before proceeding.

### Step 2 — Forensic trap in the navigation-escape guard (ships, permanent)

`packages/testing/src/navigation-escape.ts`: extend the `NavigationEscape` record with a
forensics snapshot captured **synchronously inside `vetoNavigation`** — by the `afterEach`
that reports, the DOM under test is gone. Captured fields (all feature-detected, `undefined`
when unavailable):

- the initiating element, when the browser reports one (`event.sourceElement`): tag name,
  `data-testid`, and whether it carries the `data-router-stub` marker from Step 3;
- `event.userInitiated` and `event.navigationType` — a real click on an anchor versus a
  programmatic navigation is exactly the discrimination the theories need;
- the rail state at veto time (`[data-nav-open]` attribute value, when a rail is mounted);
- `document.activeElement`'s tag/testid.

`assertNoNavigationEscape` and `expectNavigationEscape` render the snapshot into their
messages. The `NavigationEscape` interface change is additive only — wallow-auth's nine
deliberate-hand-off specs and every other consumer keep working unchanged.

Extend the guard's proof spec, `packages/navigation/src/navigation-escape.test.tsx`, to
assert the snapshot is populated on a provoked escape.

Payoff: the next firing of x5da's signature — in a sweep here or in CI — names which
element navigated, whether the stub was applied, and whether a user gesture initiated it.
The ~13% heisenbug becomes self-diagnosing evidence instead of a triage cost.

### Step 3 — Stub-applied hardening (ships)

Two mechanical changes across the 13 `packages/navigation` specs that carry the
`vi.mock("@tanstack/react-router")` Link stub:

- the stubbed anchor gains `data-router-stub="true"` (feeds Step 2's discrimination);
- a per-file `beforeEach` assertion that the mocked `Link` really is the stub (marker
  property on the stub function). If the mocker ever silently serves the real module, every
  test in the file fails immediately with a "router stub not applied" message instead of a
  mystery navigation leak.

Deliberately NOT centralizing the stub into `@bc-solutions-coder/testing`: an async
`vi.mock` factory that dynamically imports a shared stub adds a second module-delivery
dependency to exactly the path xzy1.6 shows saturating. The stub stays in-file; only the
marker and assertion are added. Wallow-xg9t.2's ordering fixes stay in Wallow-xg9t.2.

### Step 4 — Measured reproduction, then disposition

Only after Steps 2–3 ship. Sweep at the OLD concurrency, where every recorded reproduction
of both signatures occurred:

- `turbo run test --force --continue` with **no** `--concurrency` flag (the root `test`
  script's `--concurrency=1` cap must be bypassed by invoking turbo directly);
- each run wrapped in `timeout` (the Wallow-pr34 hang wedged a prior sweep for 28 minutes);
- logs kept per run; grep every log for **both** beads' signatures plus the new forensic
  output: the toggle-spec FAIL line, "Navigation escaped", `/dashboard/organizations`,
  "error when mocking a module", `VitestBrowserClientMocker`, and per-package "Test Files"
  summary lines to prove each suite actually executed (the earlier 21-run sweep was
  contaminated by suites dying before reaching the specs under measurement);
- target ~25 clean runs. Prior honest baseline is 6 clean runs (P(zero) ≈ 0.43 at the
  documented 13% rate); 25 more drives combined P(zero-if-live) to ~1%.

Decision tree:

- **x5da fires** → the forensic snapshot identifies the mechanism → design the targeted fix
  on that evidence (amend this plan) → acceptance per the bead: ≥ 20 consecutive clean
  full-workspace runs at the same uncapped concurrency, run count and machine-load
  conditions documented.
- **x5da does not fire in ~25 clean runs** → do NOT claim a fix. Record the arithmetic on
  the bead, record that Wallow-pr34's `--concurrency=1` removed the trigger condition in
  the supported configuration, and recommend parking x5da as "trap armed — any recurrence
  now self-diagnoses", explicitly amending the acceptance criteria rather than pretending
  to meet them.
- **xzy1.6** rides the same sweep (it fired on run 4 of the last uncapped sweep, so it
  plausibly reproduces). Its mechanism is already diagnosed at the module-delivery layer
  and its notes conclude the lever IS the pr34 cap. If the sweep adds nothing new, close it
  with that causal story: root cause module-delivery saturation, mitigation
  `--concurrency=1`, residual risk documented. If a sweep run produces a NEW signature at
  the mocker layer, note it and reassess.

## Files touched (shipping parts only)

| File | Change |
| ---- | ------ |
| `packages/testing/src/navigation-escape.ts` | Forensics snapshot at veto time; message rendering |
| `packages/navigation/src/navigation-escape.test.tsx` | Assert snapshot populated on provoked escape |
| 13 `packages/navigation/src/*.test.tsx` stub carriers | `data-router-stub` marker + stub-applied `beforeEach` assertion |

Probes and sweep scripts are throwaway (scratchpad), never committed.

## Quality gates

`pnpm check` after Steps 2–3; the sweep in Step 4 is its own gate. TESTING.md rules apply:
browser-mode specs stay in the browser project, no jsdom, helpers stay in
`@bc-solutions-coder/testing`, spec comments per `packages/testing/CLAUDE.md`.

## Bead updates

- Step 1 outcomes → `bd note Wallow-x5da`.
- Steps 2–3 shipped → note on both beads.
- Step 4 sweep methodology + numbers → note on both beads, mirroring the honesty standard
  the prior sweeps set (state contamination, sample sizes, load conditions).
