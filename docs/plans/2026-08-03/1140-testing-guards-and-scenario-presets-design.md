# Testing guards and scenario presets

**status: active**

Four additions to `@bc-solutions-coder/testing`, extending the pattern the
navigation-escape guard proved out: install once per browser project, fail the
test that leaked, and let a spec that provoked the behaviour deliberately
*consume* the record instead of suppressing the guard. Two are guards that catch
a mistake class automatically; two are scenario helpers that collapse repeated
arrange blocks to one call.

## Why

Every entry in `packages/testing/CLAUDE.md`'s "browser-mode facts that bite"
cost a debugging session. The navigation guard converted one of those classes —
a full-page hand-off killing the runner and blaming a neighbouring file — into
an ordinary, correctly-attributed test failure. The same conversion is available
for console noise and for network traffic no harness owns. Separately, specs
behind guarded routes all repeat the same identity setup, and forms error-path
specs all hand-write the RFC 7807 envelope; one-call presets remove both.

## 1. Console guard — `./console-guard`

Wraps `console.error` and `console.warn` once per browser context, idempotent by
holding a reference to the wrapped console (the same trick `navigation-escape`
uses with `guarded`). Each call is recorded as `{ level, message }`; the
original method still runs, so output remains visible while debugging.

- `installConsoleGuard()` — called from a project's browser setup file.
- `assertNoConsoleNoise()` — the project's `afterEach`; throws naming each
  recorded message, then clears, so one leak fails one test.
- `consumeConsoleErrors()` / `expectConsoleError()` — for a spec that
  deliberately drives an error path (an error boundary, a rejected loader).
  Mirrors the navigation helpers: only what was read is removed, so an
  unconsumed record still fails the test in `afterEach`.

**Both `warn` and `error` fail.** React reports real defects — key warnings,
act warnings, controlled/uncontrolled flips — through `console.error`, and
router noise arrives as `warn`. A genuinely unavoidable third-party warning is
consumed by the spec that provokes it, keeping the exception visible at the
call site. Rollout will surface existing noise; it gets fixed, not allowlisted,
per the pre-release posture.

Wiring: the five browser projects' setup files, plus `packages/ui`'s storybook
project through `.storybook/preview.tsx`'s `beforeEach`/`afterEach` exports
(`storybookTest()` never reads `browserSetupFiles`).

## 2. Network-escape guard — `./network-escape`

Patches `globalThis.fetch` in the browser setup file. The SDK harness injects
its transport into `createWallowSdk` and never touches the global, so anything
arriving here is by definition traffic no harness owns — a screen calling bare
`fetch`, the logger transport, a future dependency. Today that traffic escapes
to the real network and fails as a hang or a connection error attributed to
timing.

- Records `{ method, url }` and answers immediately with a distinctive 503, so
  the failure is instant and named.
- Same-origin requests to Vite/Vitest internals (paths starting `/__vitest` or
  `/@`) pass through untouched.
- `installNetworkEscapeGuard()` / `assertNoNetworkEscape()` /
  `consumeNetworkEscapes()` — the same trio, same wiring as the console guard.

XHR is out of scope: nothing in the workspace uses it.

## 3. Auth scenario presets — on `./sdk-harness`

Two functions that program a harness's current-user route:

- `signedIn(harness, overrides?)` — installs a 200 responder with defaults for
  id and email, `roles: []`, `permissions: []`; returns the installed
  `CurrentUser` so the spec can assert against it.
- `anonymous(harness)` — answers **401**, exercising the SDK's real
  401-softening rather than short-circuiting to `null`.

Both compose with `routeHarness`, owning only the current-user route. The
helper's own spec drives the real `currentUserQuery` from
`@bc-solutions-coder/auth` through the harness — if the generated path or
response shape drifts, the helper's spec fails, not seventeen app specs. No
`mfaPending` until a spec needs it.

## 4. Problem-details responder and `renderScreen`

- `harness.rejectProblem({ status?, detail?, fieldErrors? })` joins
  `rejectJson` on the harness: emits `application/problem+json` with the
  RFC 7807 envelope, PascalCasing the camelCase field keys the spec writes —
  mirroring FluentValidation's output, so `splitServerError`'s real casing fold
  is exercised. Default status 400.
- `renderScreen(route, { at, user, api })` in `./render-with-wallow` — pure
  composition of harness creation, auth preset, per-route responders, and route
  mount. Collapses today's four-step arrange block to one call. Built last,
  since it only composes the pieces above.

## Sequencing

1. Console guard (new entry, new spec, wire six projects, triage existing noise)
2. Network-escape guard (same skeleton; cheap second)
3. Auth scenario presets
4. `rejectProblem` + `renderScreen`

Each step lands with its own specs in `packages/testing`, a row in the
package's CLAUDE.md entry table, and a full `pnpm check`.

## Out of scope

- A pointer-state guard. The testing rules require pointer state to be named at
  the assertion; a blanket reset between files would mask leaks, not catch them.
- Unhandled-rejection capture — Vitest browser mode already reports these.
- `mfaPending` and any other auth scenario beyond signed-in/anonymous.
