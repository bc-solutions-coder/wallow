# Testing Guards and Scenario Presets Implementation Plan

**status: completed**

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
> Design: `docs/plans/2026-08-03/1140-testing-guards-and-scenario-presets-design.md`

**Goal:** Add a console guard, a network-escape guard, auth scenario presets, an RFC 7807
responder, and a `renderScreen` composition to `@bc-solutions-coder/testing`.

**Architecture:** The two guards copy `src/navigation-escape.ts`'s anatomy exactly — module-level
record, idempotent install, `assertNo*` for a project's `afterEach` (clear-then-throw), and
consume helpers that remove only what was read. The scenario helpers ride the existing
`./sdk-harness` entry (like `routeHarness` does) so no new subpath is needed for them; only the
two guards get new entries.

**Tech Stack:** Vitest 4 browser mode (headless Chromium), the real `createWallowSdk` over the
harness transport, TanStack Router memory history.

**Rules that bind every task:**

- Read `packages/testing/CLAUDE.md` and `.claude/rules/TESTING.md` first. No jsdom, no mocking
  the SDK module, no source-reading tests.
- Test comments: max 8 lines per header, present tense, no bead IDs, no history verbs.
- Build the SDK before anything typechecks: `pnpm --filter @bc-solutions-coder/sdk build`.
- Run a subset with `pnpm --filter @bc-solutions-coder/testing exec vitest run <path> --configLoader runner`.
- Conventional commits, `feat(testing): ...` / `test(...)` / `chore(...)`.

---

### Task 1: Console guard module

**Files:**
- Create: `packages/testing/src/console-guard.ts`
- Create: `packages/testing/src/console-guard.test.ts` (node project — no browser API needed)
- Modify: `packages/testing/package.json` (both `exports` maps: add `./console-guard`)
- Modify: `packages/testing/vite.config.ts` (add `"console-guard": "src/console-guard.ts"`)

**Step 1: Write the failing test**

`packages/testing/src/console-guard.test.ts`. Model the structure on
`src/navigation-escape.test.tsx`. The guard mutates the shared `console`, so every test must
restore state: capture `console.error`/`console.warn` in `beforeEach`, reassign them and call
`clearConsoleNoise()` in `afterEach`. Cover:

1. `installConsoleGuard()` then `console.error("boom")` → `assertNoConsoleNoise()` throws, message
   contains `CONSOLE_NOISE_MESSAGE` and `"boom"`; a second `assertNoConsoleNoise()` does NOT throw
   (clear-before-throw, one leak fails one test).
2. `console.warn` records too, tagged `level: "warn"`.
3. The original method still runs: replace `console.error` with a recording spy BEFORE
   `installConsoleGuard()`; after the guarded call, the spy received the arguments.
4. `consumeConsoleNoise()` drains and returns all entries; `assertNoConsoleNoise()` then passes.
   With nothing recorded it rejects, message contains `NO_CONSOLE_NOISE_MESSAGE` (use a short
   `{ timeout: 250 }`).
5. `expectConsoleError("boom")` returns the matching entry and consumes everything; it rejects
   when only a non-matching entry was recorded.
6. Install is idempotent: calling `installConsoleGuard()` twice then `console.error("x")` records
   ONE entry.
7. Non-string arguments format readably: `console.error(new Error("kaput"))` → recorded message
   contains `"kaput"`.

**Step 2: Run, verify it fails**

Run: `pnpm --filter @bc-solutions-coder/testing exec vitest run src/console-guard.test.ts --configLoader runner`
Expected: FAIL — cannot resolve `./console-guard`.

**Step 3: Implement**

`packages/testing/src/console-guard.ts` — follow `navigation-escape.ts`'s doc-comment style
(explain the misattribution problem: React reports defects through `console.error`, and today they
scroll by silently; a setup-file guard converts them into a failure on the test that produced
them). Complete implementation:

```ts
import { vi } from "vitest";

/** The two levels the guard records. React reports real defects through `error`. */
export type ConsoleNoiseLevel = "error" | "warn";

/** One recorded console call. */
export interface ConsoleNoise {
  readonly level: ConsoleNoiseLevel;
  readonly message: string;
}

export const CONSOLE_NOISE_MESSAGE = "Console noise leaked from the test";
export const NO_CONSOLE_NOISE_MESSAGE = "No console noise was recorded";

const noise: ConsoleNoise[] = [];

/** The `console` whose methods are wrapped — a new browser context gets a new one. */
let guarded: Console | undefined;

function formatArgument(argument: unknown): string {
  if (typeof argument === "string") return argument;
  if (argument instanceof Error) return argument.stack ?? String(argument);
  try {
    return JSON.stringify(argument) ?? String(argument);
  } catch {
    return String(argument);
  }
}

function record(level: ConsoleNoiseLevel, args: readonly unknown[]): void {
  noise.push({ level, message: args.map(formatArgument).join(" ") });
}

export function installConsoleGuard(): void {
  if (guarded === globalThis.console) return;
  guarded = globalThis.console;

  for (const level of ["error", "warn"] as const) {
    const original = console[level].bind(console);
    console[level] = (...args: unknown[]): void => {
      record(level, args);
      original(...args);
    };
  }
}

export function consoleNoise(): readonly ConsoleNoise[] {
  return [...noise];
}

export function clearConsoleNoise(): void {
  noise.length = 0;
}

/** Options shared by the consuming helpers. */
export interface ConsumeConsoleNoiseOptions {
  readonly timeout?: number;
}

/**
 * Wait for at least one entry, then drain and return everything recorded.
 * Consuming, not clearing — an entry nobody reads still fails the test in
 * `afterEach`, so a deliberate error path cannot silently suppress a second one.
 */
export async function consumeConsoleNoise(
  options: ConsumeConsoleNoiseOptions = {},
): Promise<readonly ConsoleNoise[]> {
  await vi.waitFor(
    () => {
      if (noise.length === 0) {
        throw new Error(
          `${NO_CONSOLE_NOISE_MESSAGE}. Nothing this test did wrote to console.error or console.warn.`,
        );
      }
    },
    options.timeout === undefined ? undefined : { timeout: options.timeout },
  );

  const consumed: ConsoleNoise[] = [...noise];
  noise.splice(0, consumed.length);
  return consumed;
}

/**
 * Consume everything and answer with the first error-level entry containing
 * `substring`. Everything is consumed, not just the match: React logs an error
 * boundary catch as several entries, and leaving the others behind would fail
 * the test in `afterEach` over noise it deliberately provoked.
 */
export async function expectConsoleError(
  substring: string,
  options: ConsumeConsoleNoiseOptions = {},
): Promise<ConsoleNoise> {
  const consumed = await consumeConsoleNoise(options);
  const match = consumed.find(
    (entry) => entry.level === "error" && entry.message.includes(substring),
  );

  if (match === undefined) {
    const lines = consumed.map((entry) => `  [${entry.level}] ${entry.message}`).join("\n");
    throw new Error(`No console.error containing ${JSON.stringify(substring)} among:\n${lines}`);
  }

  return match;
}

/** Throw naming every entry, then clear — what a project's `afterEach` calls. */
export function assertNoConsoleNoise(): void {
  if (noise.length === 0) return;

  const lines = noise.map((entry) => `  [${entry.level}] ${entry.message}`).join("\n");
  clearConsoleNoise();

  throw new Error(
    `${CONSOLE_NOISE_MESSAGE}. Fix the cause, or — for a spec that deliberately drives an error path — consume it with consumeConsoleNoise()/expectConsoleError():\n${lines}`,
  );
}
```

Add to `packages/testing/package.json` in BOTH `exports` and `publishConfig.exports` (src/dist
patterns respectively, exactly like `./navigation-escape`), and add the vite entry.

**Step 4: Run, verify it passes**

Same command. Expected: PASS. Then `pnpm --filter @bc-solutions-coder/testing build` and
`pnpm --filter @bc-solutions-coder/testing typecheck` — both clean.

**Step 5: Commit**

`feat(testing): add console guard with consume-based error assertions`

---

### Task 2: Wire the console guard into every browser project, triage noise

**Files:**
- Modify: `apps/wallow-web/vitest.setup.ts`, `apps/wallow-auth/vitest.setup.ts`,
  `packages/ui/vitest.setup.ts`, `packages/forms/vitest.setup.ts`,
  `packages/navigation/vitest.setup.ts`
- Modify: `packages/ui/.storybook/preview.tsx`

**Step 1: Wire the five setup files**

In each, beside the existing navigation-guard wiring:

```ts
import {
  assertNoConsoleNoise,
  installConsoleGuard,
} from "@bc-solutions-coder/testing/console-guard";

installConsoleGuard();

afterEach(() => {
  assertNoConsoleNoise();
});
```

Register a SEPARATE `afterEach` per guard — hooks are independent, so a console failure cannot
stop the navigation assertion from clearing its own record (and vice versa), which would
otherwise leak a failure into the next test.

**Step 2: Wire the storybook project**

`packages/ui/.storybook/preview.tsx` exports named `beforeEach`/`afterEach` typed as
`Preview["beforeEach"]` / `Preview["afterEach"]` (it cannot use `browserSetupFiles` —
`storybookTest()` builds its own project). Add `installConsoleGuard()` inside the existing
`beforeEach` body and `assertNoConsoleNoise()` inside the existing `afterEach` body, keeping
whatever they already do.

**Step 3: Run the full frontend suite and triage**

Run: `pnpm --filter @bc-solutions-coder/sdk build && pnpm -r test`
Expected: some failures naming real console noise (React key warnings, act warnings, router
complaints). For each: **fix the cause** in the component or spec. Only if a message is provably
third-party and unfixable does the provoking spec consume it via `consumeConsoleNoise()` with a
comment naming the library. Do not add any allowlist to the guard itself.

**Step 4: Re-run until green, then commit**

`test: wire the console guard into every browser project`
(Include the noise fixes in this commit, or split `fix(...)` commits per package if a fix touches
component source.)

---

### Task 3: Network-escape guard module

**Files:**
- Create: `packages/testing/src/network-escape.ts`
- Create: `packages/testing/src/network-escape.test.tsx` (browser project — needs a real
  `location.origin`; the file renders nothing but the node convention cannot apply, same
  precedent as `navigation-escape.test.tsx`)
- Modify: `packages/testing/package.json`, `packages/testing/vite.config.ts` (add
  `./network-escape`)

**Step 1: Write the failing test**

Restore `globalThis.fetch` from the guard's exported record in `afterEach` (the module exposes
`uninstallNetworkEscapeGuard()` for exactly this — its own spec is the one place that must undo
the patch). Cover:

1. Install, then `await fetch("http://evil.test/steal")` → resolves (does NOT hang or reject)
   with status 503; `assertNoNetworkEscape()` throws naming `http://evil.test/steal` and `GET`;
   a second call does not throw.
2. Method is recorded: `fetch(url, { method: "POST" })` → entry has `method: "POST"`.
3. Same-origin `/__vitest`-prefixed and `/@`-prefixed paths pass through to the real fetch and
   record nothing (assert `networkEscapes()` stays empty; the response status is the server's
   business, not the spec's).
4. `consumeNetworkEscapes()` drains; `assertNoNetworkEscape()` then passes; rejects on empty with
   `{ timeout: 250 }`.
5. Idempotent: two installs, one fetch, one entry.

**Step 2: Run, verify it fails**

Run: `pnpm --filter @bc-solutions-coder/testing exec vitest run src/network-escape.test.tsx --configLoader runner`
Expected: FAIL — cannot resolve `./network-escape`.

**Step 3: Implement**

`packages/testing/src/network-escape.ts`. Header comment: the SDK harness injects its transport
into `createWallowSdk` and never touches the global, so anything reaching `globalThis.fetch` is
traffic no harness owns; unguarded it escapes to the real network and fails as a hang blamed on
timing. Structure mirrors `console-guard.ts`:

```ts
import { vi } from "vitest";

export interface NetworkEscape {
  readonly method: string;
  readonly url: string;
}

export const NETWORK_ESCAPE_MESSAGE = "A request escaped to the real network";
export const NO_NETWORK_ESCAPE_MESSAGE = "No request escaped to the real network";

/** Same-origin path prefixes the Vite/Vitest machinery owns; these pass through. */
const PASSTHROUGH_PREFIXES: readonly string[] = ["/__vitest", "/@"];
const BLOCKED_STATUS = 503;

const escapes: NetworkEscape[] = [];

/** The real fetch, held while the guard is installed. */
let originalFetch: typeof globalThis.fetch | undefined;

function isPassthrough(url: URL): boolean {
  return (
    url.origin === globalThis.location.origin &&
    PASSTHROUGH_PREFIXES.some((prefix) => url.pathname.startsWith(prefix))
  );
}

export function installNetworkEscapeGuard(): void {
  if (originalFetch !== undefined) return;
  const real = globalThis.fetch.bind(globalThis);
  originalFetch = real;

  globalThis.fetch = (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const request = new Request(input, init);
    const url = new URL(request.url);

    if (isPassthrough(url)) {
      return real(input, init);
    }

    escapes.push({ method: request.method, url: request.url });
    return Promise.resolve(
      Response.json(
        { title: `${NETWORK_ESCAPE_MESSAGE}: ${request.method} ${request.url}` },
        { status: BLOCKED_STATUS },
      ),
    );
  };
}

/** Restore the real fetch. Only the guard's own spec has business calling this. */
export function uninstallNetworkEscapeGuard(): void {
  if (originalFetch === undefined) return;
  globalThis.fetch = originalFetch;
  originalFetch = undefined;
}
```

Then `networkEscapes()` / `clearNetworkEscapes()` / `consumeNetworkEscapes(options)` /
`assertNoNetworkEscape()` — byte-for-byte the same shapes as the console guard's record helpers
(waitFor on empty, drain-by-count, clear-before-throw naming each `method url`). The
`assertNoNetworkEscape` message should say what to do: "program the harness for this operation,
or consume the escape if the spec provoked it deliberately."

**Step 4: Run, verify it passes; build + typecheck clean**

**Step 5: Commit** — `feat(testing): add network-escape guard blocking unharnessed fetch`

---

### Task 4: Wire the network guard into the six projects

Same six files as Task 2, same pattern (`installNetworkEscapeGuard()` + its own separate
`afterEach(assertNoNetworkEscape)`; storybook via `preview.tsx`).

Run: `pnpm -r test`. A failure here means a spec was reaching the real network — fix it by
programming its harness (that is the guard doing its job). Then commit:
`test: wire the network-escape guard into every browser project`

---

### Task 5: Auth scenario presets

**Files:**
- Create: `packages/testing/src/auth-scenarios.ts`
- Create: `packages/testing/src/auth-scenarios.test.ts` (node project — the harness works there,
  and the SDK pipeline is what's under test, not a DOM)
- Modify: `packages/testing/src/sdk-harness.ts` (re-export block at the bottom, exactly where
  `routeHarness`/`failsWith`/`neverSettles` are re-exported from `./harness-routes`)

**Step 1: Write the failing test**

No mocking: build a real harness, apply a scenario, then drive the SDK's REAL generated
`usersGetCurrentUser`/`getCurrentUser` (import from `@bc-solutions-coder/sdk` — already a
dependency; do NOT add a dependency on `@bc-solutions-coder/auth`). This is the drift alarm: if
the generated path or shape changes, this spec fails instead of seventeen app specs. Cover:

1. `signedIn(harness)` → `getCurrentUser({ client: harness.client })` resolves a user whose
   `id` and `email` are the documented defaults, `roles: []`, `permissions: []`. The returned
   value from `signedIn` deep-equals what the pipeline resolved.
2. Overrides merge: `signedIn(harness, { user: { roles: ["Admin"] } })` → resolved user carries
   the role AND still has the default email.
3. `anonymous(harness)` → `getCurrentUser` resolves `null` — proving the response is a real 401
   the SDK's softening absorbed, not a hand-delivered null.
4. Extra routes compose: `signedIn(harness, { routes: { "GET /v1/identity/users/me/organizations": [] } })`
   answers both the current-user route and the extra one (drive the extra with a raw
   `harness.fetch` call and assert its JSON).
5. An unprogrammed route still 404s (routeHarness's default), so a forgotten operation stays
   loud.

**Step 2: Run, verify it fails** (cannot resolve `./auth-scenarios`)

**Step 3: Implement**

```ts
import type { CurrentUserResponse } from "@bc-solutions-coder/sdk";

import { failsWith, routeHarness, type HarnessRoutes } from "./harness-routes";
import type { SdkHarness } from "./sdk-harness";

/** The one path both scenarios own — `usersGetCurrentUser`'s, pinned by this file's spec. */
export const CURRENT_USER_PATH = "GET /v1/identity/users/me";

const DEFAULT_USER: CurrentUserResponse = {
  id: "user-1",
  email: "user@wallow.test",
  roles: [],
  permissions: [],
};
```

(Confirm `CurrentUserResponse`'s required members against
`packages/sdk/src/generated/types.gen.ts` and fill every required field in `DEFAULT_USER` —
the spec in Step 1 will catch a miss.)

```ts
export interface SignedInOptions {
  /** Fields to merge over the default user. */
  readonly user?: Partial<CurrentUserResponse>;
  /** Additional routes, keyed like {@link HarnessRoutes}. */
  readonly routes?: HarnessRoutes;
}

export interface AnonymousOptions {
  readonly routes?: HarnessRoutes;
}

/** Program `harness` so the current-user read answers `user`. Returns the installed user. */
export function signedIn(harness: SdkHarness, options: SignedInOptions = {}): CurrentUserResponse {
  const user: CurrentUserResponse = { ...DEFAULT_USER, ...options.user };
  routeHarness(harness, { [CURRENT_USER_PATH]: user, ...options.routes });
  return user;
}

/**
 * Program `harness` so the current-user read answers 401 — exercising the SDK's
 * real anonymous-softening rather than hand-delivering a null.
 */
export function anonymous(harness: SdkHarness, options: AnonymousOptions = {}): void {
  routeHarness(harness, {
    [CURRENT_USER_PATH]: failsWith({ title: "Unauthorized", status: 401 }, 401),
    ...options.routes,
  });
}
```

Re-export from `sdk-harness.ts` beside the `harness-routes` re-exports:
`export { anonymous, signedIn, CURRENT_USER_PATH, type SignedInOptions, type AnonymousOptions } from "./auth-scenarios";`
(Check how `harness-routes` symbols are currently re-exported and copy that form. If
`sdk-harness.ts` does not re-export them and consumers import `./harness-routes` some other way,
follow whatever the actual precedent is.)

**Step 4: Run, verify it passes; typecheck**

**Step 5: Commit** — `feat(testing): add signed-in and anonymous auth scenario presets`

---

### Task 6: `rejectProblem` + `problemDetails` on the harness

**Files:**
- Modify: `packages/testing/src/sdk-harness.ts`
- Modify: `packages/testing/src/sdk-harness.test.ts`

**Step 1: Write the failing test**

In the existing spec file's style:

1. `harness.rejectProblem({ fieldErrors: { emailAddress: ["Required"] } })`, then drive a real
   generated mutation (pick any POST operation the spec file already uses) and catch: the error
   satisfies `isWallowError`, `status` is 400, and `fieldErrors` carries the key
   **`EmailAddress`** — PascalCased exactly as FluentValidation emits, so the forms layer's
   camelCase fold is exercised for real downstream.
2. `status: 422` and `detail: "No."` come through.
3. The standalone `problemDetails({...})` builder returns the same body shape, usable inside
   `routeHarness` via `failsWith(problemDetails({...}), 400)`.
4. The response `content-type` is `application/problem+json`.

**Step 2: Run, verify the new cases fail**

**Step 3: Implement**

In `sdk-harness.ts`:

```ts
/** Options for {@link problemDetails} / {@link SdkHarness.rejectProblem}. */
export interface ProblemDetailsOptions {
  readonly status?: number;
  readonly title?: string;
  readonly detail?: string;
  /** camelCase field name -> messages; keys are PascalCased as the API emits them. */
  readonly fieldErrors?: Readonly<Record<string, readonly string[]>>;
}

function toApiPropertyName(fieldName: string): string {
  return fieldName.charAt(0).toUpperCase() + fieldName.slice(1);
}

/** An RFC 7807 body shaped as ASP.NET Core's ValidationProblemDetails emits it. */
export function problemDetails(options: ProblemDetailsOptions = {}): Record<string, unknown> {
  const status = options.status ?? ERROR_STATUS;
  return {
    title: options.title ?? "One or more validation errors occurred.",
    status,
    ...(options.detail === undefined ? {} : { detail: options.detail }),
    errors: Object.fromEntries(
      Object.entries(options.fieldErrors ?? {}).map(([field, messages]) => [
        toApiPropertyName(field),
        messages,
      ]),
    ),
  };
}
```

Add `rejectProblem: (options?: ProblemDetailsOptions) => void;` to the `SdkHarness` interface
(doc: "All subsequent requests fail with an RFC 7807 body; see {@link problemDetails}") and to
the factory:

```ts
rejectProblem: (options: ProblemDetailsOptions = {}): void => {
  const body = problemDetails(options);
  respond(() =>
    new Response(JSON.stringify(body), {
      status: options.status ?? ERROR_STATUS,
      headers: { "content-type": "application/problem+json" },
    }),
  );
},
```

(Adapt to the factory's actual internal shape — read how `rejectJson` is implemented and mirror
it.)

**Step 4: Run, verify it passes**

**Step 5: Commit** — `feat(testing): add RFC 7807 problem-details responder to the sdk harness`

---

### Task 7: `renderScreen`

**Files:**
- Modify: `packages/testing/src/render-with-wallow.tsx`
- Modify: `packages/testing/src/render-with-wallow.test.tsx`

**Step 1: Write the failing test**

Using a `createRoute`/`RouteMount`-style route exactly as the existing spec builds them:

1. `renderScreen(mount, { user: {}, api: { "GET /v1/widgets": [{ id: "w1" }] } })` — the route's
   component reads both the current user and the widgets through the router context SDK; both
   render. `result.user.email` is the preset default.
2. `user: "anonymous"` → the component's `getCurrentUser` read resolves null (render a branch on
   it and assert the anonymous branch).
3. `at` defaults to the mounted route's path (a `RouteMount` at `/settings` renders without an
   explicit `at`).
4. Everything `renderWithWallow` returns is still there: `harness`, `queryClient`, `router`.

**Step 2: Run, verify the new cases fail**

**Step 3: Implement**

In `render-with-wallow.tsx` (imports: `anonymous`, `signedIn`, `routeHarness` types from their
source modules):

```ts
/** Options for {@link renderScreen}. */
export interface RenderScreenOptions {
  /** Initial location. Defaults to the mounted route's path when it names one. */
  readonly at?: string;
  /** `"anonymous"` for a 401 current-user read; overrides for a signed-in one. Omit to leave the route unprogrammed. */
  readonly user?: Partial<CurrentUserResponse> | "anonymous";
  /** Per-route responders, keyed like {@link HarnessRoutes}. */
  readonly api?: HarnessRoutes;
}

export type RenderScreenResult = RenderWithWallowResult & {
  /** The signed-in user the harness answers with, when one was installed. */
  readonly user: CurrentUserResponse | undefined;
};

/** One-call arrange: harness + auth scenario + route responders + mounted route. */
export function renderScreen(
  route: MountableRoute,
  options: RenderScreenOptions = {},
): RenderScreenResult {
  const harness = createSdkHarness();

  let user: CurrentUserResponse | undefined;
  if (options.user === "anonymous") {
    anonymous(harness, { routes: options.api });
  } else if (options.user !== undefined) {
    user = signedIn(harness, { user: options.user, routes: options.api });
  } else if (options.api !== undefined) {
    routeHarness(harness, options.api);
  }

  const path = options.at ?? (isRouteMount(route) ? route.path : DEFAULT_RENDER_PATH);
  const result = renderWithWallow(null, { harness, path, routes: [route] });
  return Object.assign(result, { user });
}
```

**Step 4: Run, verify it passes**

**Step 5: Commit** — `feat(testing): add renderScreen one-call arrange for route specs`

---

### Task 8: Documentation, full gate, close out

**Files:**
- Modify: `packages/testing/CLAUDE.md` — add table rows for `./console-guard` and
  `./network-escape` (imported at "A browser project's setup file"), extend the `./sdk-harness`
  and `./render-with-wallow` rows with the new helpers, and add one "facts that bite" bullet per
  guard (what fails, how to consume deliberately). Keep the existing voice; no history verbs.
- Modify: `docs/plans/2026-08-03/1140-testing-guards-and-scenario-presets-design.md` and this
  plan — flip both to `**status: completed**`.

**Steps:**

1. Update the docs.
2. Run: `pnpm check` (from the repo root). Expected: every gate green. Fix anything it names.
3. Commit: `docs(testing): document the guard pair and scenario presets`
4. Session completion per root `CLAUDE.md`: file beads for any follow-up (e.g. migrating existing
   app specs onto `renderScreen`), then `git pull --rebase && bd dolt push && git push`, and
   verify both remotes moved.
