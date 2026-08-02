# Query + Auth Consolidation Implementation Plan

**status: active**
**verified against main @ 33290840** — re-verified twice since authoring (10:45): once at `5b8867c5`
(13 commits), then again after the `@bc-solutions-coder/forms` merge (13 more). Both rounds of
amendments are folded in below.

> **PREREQUISITE — `main` cannot build either app image today.** The forms merge added
> `"@bc-solutions-coder/forms": "workspace:*"` to both apps but never updated their Dockerfiles,
> which still copy only `sdk`/`styles`/`testing`/`ui`/`web-shell`. Verified by running both builds:
> `Error: [vite]: Rolldown failed to resolve import "@bc-solutions-coder/forms"` — wallow-web from
> `RegisterAppForm.tsx`, wallow-auth from `ForgotPasswordForm.tsx`. That breaks CI's
> `docker-images-app` job and the `e2e-tests` job downstream of it, i.e. the gate Task 10 depends
> on. Fix it FIRST as its own `fix(docker): copy packages/forms into the app images` commit — not
> inside Task 7, which would bury a production-path fix in a `feat!` refactor.

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
> Design doc: `docs/plans/2026-07-29/1038-query-auth-consolidation-design.md`

**Goal:** All react-query usage flows through a new `@bc-solutions-coder/query` facade package; shared authN/authZ (current-user query, hooks, guards) lives in a new `@bc-solutions-coder/auth` package; `packages/web-shell` is deleted; wallow-auth stops hand-rolling mutations and gains `api.ts` seams.

**Architecture:** `packages/query` = full re-export of `@tanstack/react-query` + `createQueryClient()` (moved from web-shell). `packages/auth` = `currentUserQuery`/`useCurrentUser`/`hasRole`/`hasPermission`/`ensureCurrentUser`, depends on `query` + `sdk`. Apps depend on both and drop their direct `@tanstack/react-query` dependency. oxlint `no-restricted-imports` enforces the facade.

**Tech Stack:** pnpm workspace, Vite 8 lib-mode + `tsc` declarations, vitest (node env for the new packages), oxlint/oxfmt, TanStack Query v5 / Start / Router.

---

## Deviations from the design doc (verified against the repo)

1. **web-shell is `private: true`, version 0.0.0, NOT in `release-please-config.json`, NOT in `scripts/check-exports.sh`.** The new packages mirror that: private, unpublished, no release-please entries, no publish workflow. The deletion is still `feat!` (fork-facing breaking change).
2. **`scripts/fork-smoke` needs no code change.** It consumes only the sdk + styles packed tarballs and documents its `new QueryClient()` at `scripts/fork-smoke/src/router.tsx:14-17`. A private `@bc-solutions-coder/query` cannot reach it, so the app itself is untouched — but `scripts/fork-smoke/README.md:27` names `web-shell` in its package list and gets the rename with every other doc in Task 7 (otherwise Task 7's grep gate trips on it).
3. `check:exports` DOES gain the two new packages (precedent: private `packages/testing` is already in it — it validates exports-map hygiene via local pack, publish not required).

## Facts the implementer needs

- **Read first:** `packages/sdk/CLAUDE.md`, `.claude/rules/TESTING.md`, `docs/development/frontend-state.md`. Backend untouched — no C# rules apply.
- Build order: apps typecheck against `dist/`. After creating/changing a package run `pnpm --filter <pkg> build`. `pnpm -r build` orders by workspace graph automatically.
- Formatter is **oxfmt** (`pnpm format`), linter **oxlint** (`pnpm lint`, `--deny-warnings`). Run both before each commit.
- Conventions: explicit types instead of `var`/inference where the surrounding code does (see any file in `packages/web-shell/src`); comment density in this repo is high and rationale-focused — preserve existing comments when moving code verbatim.
- Generated artifact names live in `packages/sdk/src/generated/@tanstack/react-query.gen.ts` (210 exports: `{op}Options`, `{op}Mutation`, `{op}QueryKey`). **Never guess a name — grep that file.**
- Generated mutation variables are the **full request object** (`{ body, path, query }`), not a bare body. Converting a hand-rolled `mutationFn` changes every `mutation.mutate(...)` call-site shape. Component specs will catch mistakes.
- `pnpm test` component specs run in real headless Chromium (vitest browser mode). Never jsdom.
- Single-version check after dependency moves: `pnpm why @tanstack/react-query` must show exactly one version (`5.x`) across the workspace — two instances would break QueryClientProvider context identity. It already resolves to a single `5.101.2` today (`pnpm-lock.yaml` has one entry), so this is a **regression check**, not a fix this plan delivers.
- **Line numbers in this plan are symbol anchors, not offsets.** The wallow-auth components moved twice in the day this plan was written; locate each site by the quoted symbol and re-derive the line. The two surveys that matter: `grep -rn "mutationFn" apps/wallow-auth/src` and `grep -rn "retry: false" apps/wallow-auth/src`.
- `@tanstack/react-query` is an **optional peer** of the SDK (`packages/sdk/package.json`), and four hand-written SDK files import it for real: `src/route-context.ts`, `src/route-context.test.ts`, `src/query/invalidations.ts`, `src/generated-query-surface.test.ts`. The facade does not replace that peer declaration — a version bump has to move both in step.

---

### Task 1: Scaffold `packages/query`

**Files:**
- Create: `packages/query/package.json`, `packages/query/tsconfig.json`, `packages/query/tsconfig.build.json`, `packages/query/vite.config.ts`, `packages/query/vitest.config.ts`
- Copy (git mv): `packages/web-shell/src/query-client.ts` → `packages/query/src/query-client.ts`, `packages/web-shell/src/query-client.test.ts` → `packages/query/src/query-client.test.ts`, `packages/web-shell/src/devtools-gating.test.ts` → `packages/query/src/devtools-gating.test.ts`
- Create: `packages/query/src/index.ts`, `packages/query/src/index.test.ts`, `packages/query/CLAUDE.md`

Do NOT delete the rest of web-shell yet (apps still import it until Tasks 5–7).

**Step 1: Copy the web-shell scaffolding files verbatim, then edit**

`tsconfig.json`, `tsconfig.build.json`, `vite.config.ts`, `vitest.config.ts` are copied from `packages/web-shell/` unchanged except comments referring to "web-shell" → "query". The `devtools-gating.test.ts` repo-root resolution (`src -> package -> packages -> repo`) is the same depth — no path edits needed; update only its prose ("lives in web-shell" → "lives in the query package, the shared TanStack Query facade").

`packages/query/package.json`:

```json
{
  "name": "@bc-solutions-coder/query",
  "version": "0.0.0",
  "private": true,
  "description": "Wallow shared TanStack Query facade: full @tanstack/react-query re-export + the QueryClient factory",
  "type": "module",
  "main": "./dist/index.js",
  "module": "./dist/index.js",
  "types": "./dist/index.d.ts",
  "exports": {
    ".": {
      "types": "./dist/index.d.ts",
      "import": "./dist/index.js"
    }
  },
  "scripts": {
    "build": "vite build && tsc -p tsconfig.build.json",
    "test": "vitest run",
    "test:watch": "vitest",
    "typecheck": "tsc --noEmit"
  },
  "dependencies": {
    "@tanstack/react-query": "^5.101.2"
  },
  "devDependencies": {
    "@types/node": "^24.0.0",
    "typescript": "^5.6.0",
    "vite": "^8.1.4",
    "vitest": "^4.1.10"
  }
}
```

`packages/query/src/index.ts`:

```ts
/**
 * @bc-solutions-coder/query — the single place TanStack Query enters the
 * workspace.
 *
 * Apps and packages import `useQuery`/`useMutation`/`QueryClient`/... from
 * here, never from `@tanstack/react-query` directly (enforced by the repo-root
 * `no-restricted-imports` rule). One manifest — this package's — declares the
 * react-query version, so an upgrade is a one-line change every consumer
 * inherits together, and the workspace can never split into two provider/hook
 * instances that don't share a QueryClient context.
 */
export * from "@tanstack/react-query";
export { createQueryClient } from "./query-client";
```

**Step 2: Write the failing facade test**

`packages/query/src/index.test.ts`:

```ts
import * as tanstack from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import * as facade from "./index";

/**
 * The facade is a FULL re-export: every symbol @tanstack/react-query ships is
 * reachable from the facade by identity, so a consumer never has a reason to
 * import the underlying package (which the repo-root lint rule forbids).
 */
describe("@bc-solutions-coder/query facade", () => {
  it("re-exports every @tanstack/react-query runtime export by identity", () => {
    const names = Object.keys(tanstack).filter((name) => name !== "default");
    expect(names.length).toBeGreaterThan(50);
    for (const name of names) {
      expect(facade[name as keyof typeof facade], name).toBe(
        tanstack[name as keyof typeof tanstack],
      );
    }
  });

  it("adds the workspace client factory on top of the re-export", () => {
    expect(typeof facade.createQueryClient).toBe("function");
    expect(Object.hasOwn(tanstack, "createQueryClient")).toBe(false);
  });
});
```

**Step 3: Install, run tests — expect green (index.ts already written), build**

```bash
pnpm install
pnpm --filter @bc-solutions-coder/query test        # 2 files, all pass
pnpm --filter @bc-solutions-coder/query build       # dist/index.js + index.d.ts
pnpm --filter @bc-solutions-coder/query typecheck
```

**Step 4: Write `packages/query/CLAUDE.md`**

Model it on `packages/web-shell/CLAUDE.md` (which this package replaces): one browser-safe `.` entry; states the facade rule ("consumers import react-query symbols from here — the repo lint rule bans the underlying package"); notes `createQueryClient`'s policy (retry disabled, fresh client per call) and that the devtools-gating sweep lives here.

**Step 5: Commit**

```bash
git add packages/query
git commit -m "feat(query): add @bc-solutions-coder/query react-query facade"
```

---

### Task 2: Scaffold `packages/auth`

**Files:**
- Create: `packages/auth/package.json`, `tsconfig.json`, `tsconfig.build.json`, `vite.config.ts`, `vitest.config.ts` (copy from `packages/query/`, rename)
- Create: `packages/auth/src/current-user.ts`, `src/use-current-user.ts`, `src/authorization.ts`, `src/ensure-current-user.ts`, `src/index.ts`
- Test: `packages/auth/src/current-user.test.ts`, `src/authorization.test.ts`
- Create: `packages/auth/CLAUDE.md`

**Step 1: package.json**

```json
{
  "name": "@bc-solutions-coder/auth",
  "version": "0.0.0",
  "private": true,
  "description": "Wallow shared authN/authZ layer: the canonical current-user query, hooks, and role/permission helpers",
  "type": "module",
  "main": "./dist/index.js",
  "module": "./dist/index.js",
  "types": "./dist/index.d.ts",
  "exports": {
    ".": {
      "types": "./dist/index.d.ts",
      "import": "./dist/index.js"
    }
  },
  "scripts": {
    "build": "vite build && tsc -p tsconfig.build.json",
    "test": "vitest run",
    "test:watch": "vitest",
    "typecheck": "tsc --noEmit"
  },
  "dependencies": {
    "@bc-solutions-coder/query": "workspace:*",
    "@bc-solutions-coder/sdk": "workspace:*"
  },
  "peerDependencies": {
    "react": "^19.0.0"
  },
  "devDependencies": {
    "@types/node": "^24.0.0",
    "react": "^19.2.7",
    "typescript": "^5.6.0",
    "vite": "^8.1.4",
    "vitest": "^4.1.10"
  }
}
```

The SDK must be built first (`pnpm --filter @bc-solutions-coder/sdk build`) or typecheck fails against a missing `dist/`.

**Step 2: Move the canonical current-user query**

`packages/auth/src/current-user.ts` is `apps/wallow-web/src/lib/current-user.ts` moved **verbatim** — keep its entire doc comment (adjust the first line to note it now serves every app) — with one import change: `queryOptions` comes from `@bc-solutions-coder/query` instead of `@tanstack/react-query`. The exported names stay `currentUserQuery` and `CurrentUser` (rename of wallow-web's type is already `CurrentUser`).

This is THE canonical semantics for all apps: generated key, `getCurrentUser` 401-softening (`null` = anonymous), `sub` rename, `staleTime: 30_000`. wallow-auth's copy (no staleTime, no `sub`) is the one that dies.

**Step 3: Write the failing tests**

`packages/auth/src/current-user.test.ts` (node env; real SDK, stubbed global fetch — no SDK mocking):

```ts
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";
import { afterEach, describe, expect, it, vi } from "vitest";

import { currentUserQuery, type CurrentUser } from "./current-user";

const BASE_URL = "http://api.test";

function sdkAgainst(response: Response): WallowSdk {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (): Promise<Response> => response),
  );
  return createWallowSdk({ baseUrl: BASE_URL });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("currentUserQuery", () => {
  it("uses the GENERATED key — one cache entry shared with every generated artifact", () => {
    const sdk: WallowSdk = createWallowSdk({ baseUrl: BASE_URL });
    expect(currentUserQuery(sdk.client).queryKey).toEqual(
      usersGetCurrentUserQueryKey({ client: sdk.client }),
    );
  });

  it("holds a resolved user for 30s so ensureQueryData gates don't refetch per navigation", () => {
    const sdk: WallowSdk = createWallowSdk({ baseUrl: BASE_URL });
    expect(currentUserQuery(sdk.client).staleTime).toBe(30_000);
  });

  it("resolves the user with `sub` filled from `id` so the SDK claim guards can read it", async () => {
    const sdk: WallowSdk = sdkAgainst(
      new Response(JSON.stringify({ id: "u1", email: "a@b.c", roles: ["Admin"] }), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );
    const user = (await currentUserQuery(sdk.client).queryFn!(
      {} as never,
    )) as CurrentUser | null;
    expect(user?.sub).toBe("u1");
    expect(user?.roles).toEqual(["Admin"]);
  });

  it("resolves null on 401 — anonymous is the ANSWER, not an error", async () => {
    const sdk: WallowSdk = sdkAgainst(new Response(null, { status: 401 }));
    const user = await currentUserQuery(sdk.client).queryFn!({} as never);
    expect(user).toBeNull();
  });
});
```

(If `queryFn!({} as never)` fights the QueryFunction context type, invoke it with a minimal `{ queryKey, signal }` stub instead — assertion targets are the resolved values, not the context.)

`packages/auth/src/authorization.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { hasPermission, hasRole } from "./authorization";

describe("authZ helpers", () => {
  const user = { sub: "u1", roles: ["Admin"], permissions: ["inquiries:read"] };

  it("answers role membership", () => {
    expect(hasRole(user, "Admin")).toBe(true);
    expect(hasRole(user, "Operator")).toBe(false);
  });

  it("answers permission membership", () => {
    expect(hasPermission(user, "inquiries:read")).toBe(true);
    expect(hasPermission(user, "inquiries:write")).toBe(false);
  });

  it("treats anonymous (null/undefined) and claimless users as having nothing", () => {
    expect(hasRole(null, "Admin")).toBe(false);
    expect(hasPermission(undefined, "inquiries:read")).toBe(false);
    expect(hasRole({ sub: "u2" }, "Admin")).toBe(false);
  });
});
```

Run: `pnpm --filter @bc-solutions-coder/auth test` — expect FAIL (modules missing).

**Step 4: Implement**

`packages/auth/src/authorization.ts`:

```ts
import type { CurrentUser } from "./current-user";

/**
 * AuthZ readers over the current-user payload. `roles`/`permissions` come
 * straight from `UsersController.GetCurrentUser`; a missing array (old API,
 * anonymous user) answers `false` rather than throwing, because "can't prove
 * it" and "not allowed" are the same branch in a UI affordance — the server
 * enforces the real thing regardless.
 */
export function hasRole(user: CurrentUser | null | undefined, role: string): boolean {
  return user?.roles?.includes(role) ?? false;
}

export function hasPermission(
  user: CurrentUser | null | undefined,
  permission: string,
): boolean {
  return user?.permissions?.includes(permission) ?? false;
}
```

`packages/auth/src/use-current-user.ts`:

```ts
import { useQuery } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";

import { currentUserQuery } from "./current-user";

/**
 * The signed-in user as a hook. Pass the request-scoped client off the router
 * context (`useRouteContext({ from: "__root__" }).sdk.client`) — this package
 * deliberately imports no router, mirroring the SDK's `route-context.ts`.
 */
export function useCurrentUser(client: WallowSdk["client"]) {
  return useQuery(currentUserQuery(client));
}
```

`packages/auth/src/ensure-current-user.ts`:

```ts
import type { QueryClient } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";

import { currentUserQuery, type CurrentUser } from "./current-user";

/** Inputs {@link ensureCurrentUser} reads off a route's `beforeLoad` context. */
export interface EnsureCurrentUserOptions {
  readonly queryClient: QueryClient;
  readonly client: WallowSdk["client"];
}

/**
 * Resolve the current user through the request's query cache — the
 * `beforeLoad` half of the auth gate. Compose with the SDK's `requireAuth`
 * (re-exported from this barrel) to turn `null` into a login redirect.
 */
export function ensureCurrentUser(
  options: EnsureCurrentUserOptions,
): Promise<CurrentUser | null> {
  return options.queryClient.ensureQueryData(currentUserQuery(options.client));
}
```

`packages/auth/src/index.ts`:

```ts
/**
 * @bc-solutions-coder/auth — the shared authN/authZ layer every app imports
 * instead of hand-rolling its own current-user probe.
 *
 * The SDK guard trio (`requireAuth`/`loginRedirect`/`isAdmin`) is re-exported
 * so an app's auth imports come from ONE package.
 */
export { currentUserQuery, type CurrentUser } from "./current-user";
export { useCurrentUser } from "./use-current-user";
export { hasPermission, hasRole } from "./authorization";
export { ensureCurrentUser, type EnsureCurrentUserOptions } from "./ensure-current-user";
export {
  isAdmin,
  loginRedirect,
  requireAuth,
  type LoginRedirectOptions,
  type RequireAuthOptions,
  type WallowUser,
} from "@bc-solutions-coder/sdk";
```

Note: `useCurrentUser` is a one-line composition; its rendering behavior is covered by the app component suites that already exercise these screens — no browser-mode project in this package (node env only, like packages/query).

**Step 5: Run tests + build**

```bash
pnpm install
pnpm --filter @bc-solutions-coder/auth test        # all pass
pnpm --filter @bc-solutions-coder/auth build && pnpm --filter @bc-solutions-coder/auth typecheck
```

**Step 6: Write `packages/auth/CLAUDE.md`** (surface table, "no router import" rule, canonical current-user semantics: generated key / 401→null / sub rename / 30s staleTime).

**Step 7: Commit**

```bash
git add packages/auth
git commit -m "feat(auth): add @bc-solutions-coder/auth shared authn/authz package"
```

---

### Task 3: Route `packages/testing` through the facade

**Files:**
- Modify: `packages/testing/package.json` (drop `@tanstack/react-query` from devDependencies AND peerDependencies; add `"@bc-solutions-coder/query": "workspace:*"` to dependencies)
- Modify: `packages/testing/src/render-with-wallow.tsx` (the `import { QueryClient, QueryClientProvider }` line below the file's doc block) and `packages/testing/src/render-with-wallow.test.tsx` (its `import { QueryClient, useQuery }` line) — import from `@bc-solutions-coder/query`

**This is where the `optimizeDeps` decision gets made — see the Facts bullet.** `packages/testing/vitest.config.ts` lists `"@tanstack/react-query"` in the shared browser-mode `optimizeDeps.include`, and TWO specs pin it: `packages/testing/src/browser-optimize-deps.test.ts` and `packages/testing/src/vitest-projects.test.ts` (both assert it among the `extras`). Do not blindly rewrite those strings to the facade. The recommended resolution:

- **Keep** `@tanstack/react-query` in `optimizeDeps.include` — it is still the module Vite pre-bundles, and the facade re-exports it rather than replacing it.
- **Add** `@bc-solutions-coder/query` to `ssr.noExternal` (and to `optimizeDeps.include` only if a browser run reload-loops without it), because a linked workspace package is not pre-bundled by default.
- Update both pinning specs to assert the pair, with a comment saying why both names are present.

Prove it empirically rather than by reasoning: run `pnpm --filter @bc-solutions-coder/testing test` and one app's browser project. A missing entry shows up as a mid-run reload that drops the runner, not as a clean failure.

**Steps:** edit → `pnpm install` → `pnpm --filter @bc-solutions-coder/testing test && ... build && ... typecheck` → verify single version: `pnpm why @tanstack/react-query` (one 5.x resolution) → commit:

```bash
git commit -m "refactor(testing): consume react-query through the query facade"
```

---

### Task 3b: Route `packages/forms` through the facade

`@bc-solutions-coder/forms` landed after this plan was written and is a first-class react-query
consumer, so it needs the same treatment as `packages/testing` — without it, Task 8's lint rule
fails the whole package.

**Files:**
- Modify: `packages/forms/package.json` — drop `@tanstack/react-query` from `devDependencies` AND `peerDependencies`; add `"@bc-solutions-coder/query": "workspace:*"` to `dependencies`.
- Modify: `packages/forms/src/form/use-app-form.ts` (`import { useMutation, type UseMutationOptions }`) — the package's only runtime import of react-query.
- Modify the seven specs that import `QueryClient`/`QueryClientProvider`/`UseMutationOptions`: `src/form/use-app-form.test.tsx`, `src/form/use-app-form.sdk-mutation.test.tsx`, and `src/fields/{text,password,select,checkbox,textarea}-field.test.tsx`.
- Modify: `packages/forms/vitest.config.ts` — same `optimizeDeps` treatment as Task 3.

**The scaffold spec is the trap.** `packages/forms/src/core/package-scaffold.test.ts` pins react-query
in FOUR places, and all four fail the moment the manifest changes:

1. an EXACT-list assertion that `peerDependencies` sorts to `["@tanstack/react-query", "react", "react-dom"]`;
2. a `devDependencies` presence check over the same three names;
3. a regex asserting `vitest.config.ts` mentions `@tanstack/react-query` among the package's own runtime deps;
4. an `INSTALLED` list of names that must resolve in `node_modules`.

Rewrite all four for the facade, and rewrite the peer-list rationale comment with it: it currently
argues react-query must be a peer so the host's single `QueryClient` context is shared. That
argument now lands on the facade — one `workspace:*` dependency on a private package that itself
owns the react-query version, which is the same call Task 3 makes for `packages/testing`. Say so in
the comment rather than deleting it.

**Steps:** edit → `pnpm install` → `pnpm --filter @bc-solutions-coder/forms test && ... build && ... typecheck` → commit:

```bash
git commit -m "refactor(forms): consume react-query through the query facade"
```

---

### Task 4: Migrate `apps/wallow-web`

**Files:**
- Modify: `apps/wallow-web/package.json` — remove `@bc-solutions-coder/web-shell` and `@tanstack/react-query`; add `"@bc-solutions-coder/auth": "workspace:*"` and `"@bc-solutions-coder/query": "workspace:*"`.
- Modify every `from "@tanstack/react-query"` import → `from "@bc-solutions-coder/query"`. Known sites (re-grep to be sure): `src/router.tsx`, `src/routes/__root.tsx`, `src/test/invalidation.ts`, `src/features/organizations/components/{OrganizationDetail,MemberList,OrganizationList,CreateOrganizationForm}.tsx`, `src/features/inquiries/components/{InquiryDetail,InquiryList,CreateInquiryForm}.tsx`, `src/features/apps/components/{AppList,RegisterAppForm}.tsx`, `src/features/mfa/components/{MfaSettingsSection,MfaEnrollFlow}.tsx`, `src/features/settings/components/ProfileSection.tsx`.
- Modify `src/router.tsx:2` — `createQueryClient` now from `@bc-solutions-coder/query`.
- Modify: `apps/wallow-web/vitest.config.ts` — its `optimizeDeps` list names `@tanstack/react-query`; apply the Task 3 resolution.

Note on the three forms-migrated components: `CreateOrganizationForm`, `RegisterAppForm` and
`CreateInquiryForm` now render through `useAppForm`, but they still import `useQueryClient` from
react-query for post-submit invalidation — so they stay in the swap list above. The import list is
unchanged in shape by the forms migration.
- Delete: `apps/wallow-web/src/lib/current-user.ts` (and its co-located test if one exists). Repoint every importer (`grep -rn "current-user" apps/wallow-web/src`) — expect `src/routes/dashboard/route.tsx`, `src/routes/index.tsx`, possibly others — to `import { currentUserQuery, type CurrentUser } from "@bc-solutions-coder/auth"`. Where a `beforeLoad` does `queryClient.ensureQueryData(currentUserQuery(context.sdk.client))`, optionally tighten to `ensureCurrentUser({ queryClient: context.queryClient, client: context.sdk.client })` — same cache entry either way.

**Steps:**

1. `sed`-swap the imports, edit package.json, `pnpm install`.
2. `pnpm --filter @bc-solutions-coder/wallow-web typecheck` — expect clean (the facade's types are identity re-exports).
3. `pnpm --filter @bc-solutions-coder/wallow-web test` — full suite green, unchanged snapshots.
4. Commit: `git commit -m "refactor(web): consume react-query via the facade and auth via @bc-solutions-coder/auth"`

---

### Task 5: Migrate `apps/wallow-auth` (the substantive task)

**5a — mechanical facade swap.** Same as Task 4: package.json (drop web-shell + `@tanstack/react-query`, add `query` + `auth`), swap every `from "@tanstack/react-query"` import (see survey list: `src/router.tsx`, `src/routes/__root.tsx`, `src/routes/login.tsx`, `src/routes/invitation.tsx`, 13 feature components + `ExternalProviders.test.tsx`), `createQueryClient` from `@bc-solutions-coder/query` in `src/router.tsx`. Typecheck + test, commit:
`refactor(wallow-auth): consume react-query through the query facade`

**5b — delete the duplicated current-user probe.** `src/routes/invitation.tsx`, the `const authQuery = useQuery({ ... })` inside `InvitationRoute()`: replace the inline `useQuery({ queryKey: usersGetCurrentUserQueryKey(...), queryFn, retry: false })` with `useCurrentUser(sdk.client)` from `@bc-solutions-coder/auth`; drop the now-unused `getCurrentUser`/`usersGetCurrentUserQueryKey` imports. The auth-probe rationale comment stays, trimmed to note the canonical query owns the 401-softening. Behavior deltas are deliberate and safe: gains 30s staleTime + `sub`; `retry: false` was already the global default. Run the invitation specs, commit:
`refactor(wallow-auth): adopt the shared current-user query`

**5c — replace the 9 hand-rolled mutations with generated factories** (was 11 before the forms merge
— see the scope note below). The transformation, using `PasswordLoginForm.tsx`'s `const mutation = useMutation({ mutationFn: async (credentials: Credentials) => ... })` as the exemplar:

Before:
```ts
const mutation = useMutation({
  mutationFn: async (credentials: Credentials): Promise<unknown> =>
    await accountLogin({ client: sdk.client, body: credentials }),
});
// ...
mutation.mutate({ email, password, rememberMe }, { onSuccess: onAuthResult, onError });
```

After:
```ts
const mutation = useMutation(accountLoginMutation({ client: sdk.client }));
// ...
mutation.mutate({ body: { email, password, rememberMe } }, { onSuccess: onAuthResult, onError });
```

**The variables shape changes** from bare body to `{ body, path?, query? }` at every `.mutate(...)` call site — update them together with the hook. Where the old `mutationFn` carried a justified type comment (untyped anonymous C# response), keep the comment beside the `useMutation` call; the generated factory's return type governs, and any narrowing stays at the call site (`onSuccess`) or in the feature's `panel`/result modules where it already lives.

**Scope shrank from 11 sites to 9 (12 operations to 10):** `ForgotPasswordForm` and
`ResetPasswordForm` migrated to `@bc-solutions-coder/forms` and no longer hold a `mutationFn` at all.
Both deliberately use `useAppForm`'s **no-mutation escape hatch** — forgot-password swallows every
failure for anti-enumeration, reset-password owns its own status-code branching — and their headers
say so. **Do not convert them, and do not give them a generated mutation:** re-introducing the
mutation path would restore exactly the error surface those comments state must never reach the page.
They are out of scope for 5c.

The 9 remaining sites, anchored by the `mutationFn`'s parameter signature rather than by line — every
one of these files moved after this plan was written. Run `grep -rn "mutationFn" apps/wallow-auth/src`
to locate them, and again after the rewrite to confirm none survive.

| File | `mutationFn` anchor | Raw op today | Generated replacement |
| --- | --- | --- | --- |
| `features/login/components/PasswordLoginForm.tsx` | `(credentials: Credentials)` | `accountLogin` | `accountLoginMutation` |
| `features/login/components/MagicLinkLoginForm.tsx` | `(address: string)` | `accountSendMagicLink` | `accountSendMagicLinkMutation` |
| `features/login/components/MagicLinkLoginForm.tsx` | `(value: string)` | `accountVerifyMagicLink` (GET) | **special — see below** |
| `features/login/components/OtpLoginForm.tsx` | `(address: string)` | `accountSendOtp` | `accountSendOtpMutation` |
| `features/login/components/OtpLoginForm.tsx` | `(value: string)` | `accountVerifyOtp` | `accountVerifyOtpMutation` |
| `features/register/components/RegisterForm.tsx` | `(request: RegisterRequest)` | `accountRegister` | `accountRegisterMutation` |
| `features/invitation/components/InvitationScreen.tsx` | `(accepted: string)` | `invitationsAccept` | `invitationsAcceptMutation` |
| `features/mfa-challenge/components/MfaChallengeForm.tsx` | `(attempt: { code, useBackupCode })` | `accountVerifyMfaChallenge` | `accountVerifyMfaChallengeMutation` |
| `features/mfa-enroll/components/MfaEnrollForm.tsx` | `()` and `(attempt: {...})` | `mfaEnrollTotp`, `mfaConfirmEnrollment` | `mfaEnrollTotpMutation`, `mfaConfirmEnrollmentMutation` |

Longer term these nine are candidates for the forms package too — `useAppForm` takes a generated
`{operation}Mutation` as its `mutation` option, which is precisely this conversion plus the RFC 7807
split. That is NOT this plan's job; file a bead.

**`accountVerifyMagicLink` special case:** it's a GET, so the generator emits `accountVerifyMagicLinkOptions`, not a Mutation. Do not keep the hand-rolled `useMutation`. Replace the imperative verify with `queryClient.fetchQuery(accountVerifyMagicLinkOptions({ client: sdk.client, query: { token } }))` (exact arg shape from the generated file) — generated key + generated fn, no hand-rolled cache wiring, still imperative where the flow needs it. Document with one comment line.

Also delete the 8 dead `retry: false` overrides — `ConsentScreen.tsx`, `InvitationScreen.tsx` (two),
`ExternalProviders.tsx`, `LogoutScreen.tsx`, `MfaChallengeForm.tsx`, `VerifyEmailConfirm.tsx`,
`routes/login.tsx` (the `useClientBranding` query; leave the prose comment above it that explains
why an unresolved client must not gate the form). `routes/invitation.tsx`'s died in 5b. Find them
with `grep -rn "retry: false" apps/wallow-auth/src`. They are dead because `createQueryClient()`
already sets it globally and mutations never retried.

Work feature-by-feature; after each: `pnpm --filter @bc-solutions-coder/wallow-auth test` (component specs are the safety net for the `.mutate` shape change). Commit per feature or as one:
`refactor(wallow-auth): use generated mutation factories instead of hand-rolled mutationFn`

**5d — add the `api.ts` seams.** The survey needs **two** greps, not one, because the features split across both SDK entries:

- `grep -rln "@bc-solutions-coder/sdk/query" apps/wallow-auth/src` → `login`, `register`, `consent`, `invitation`, `logout`, `mfa-challenge`, `verify-email` (plus `routes/login.tsx` and `routes/invitation.tsx`).
- `grep -rln "@bc-solutions-coder/sdk\"" apps/wallow-auth/src` → the features importing **raw**
  operations, which never appear in the first grep: `mfa-enroll` (5c converts it to generated
  factories, so its seam re-exports from `/query` like the rest) plus `forgot-password` and
  `reset-password`.

  **`forgot-password` / `reset-password` are the exception in this task.** 5c no longer touches them,
  so they own no generated artifact — the raw op is called directly inside `useAppForm`'s `onSubmit`.
  They still get a seam (the "`api.ts` is the feature's only data import" rule is the point of 5d),
  but it re-exports the raw operation from `@bc-solutions-coder/sdk`, not from
  `@bc-solutions-coder/sdk/query`:
  `export { accountForgotPassword } from "@bc-solutions-coder/sdk";`. Say in the seam's doc comment
  *why* it differs — the form uses the no-mutation escape hatch, so there is no `{op}Mutation` to
  re-export — otherwise the next reader will "fix" it into a generated factory and undo 5c's
  exclusion. The `api.test.ts` identity assertion is unchanged in shape
  (`expect(api.accountForgotPassword).toBe(sdk.accountForgotPassword)` off a namespace import); the
  `apps/**/src/features/*/api.test.ts` override disables `no-restricted-imports` outright there, so
  importing the raw entry in the test is already legal.

The five feature dirs added after this plan was written (`accept-terms`, `error`, `not-found`,
`privacy`, `terms`) call no generated operations and need no seam; re-confirm with the greps rather
than assuming.

For each of those ten feature dirs, create `src/features/<name>/api.ts` mirroring `apps/wallow-web/src/features/organizations/api.ts` (thin re-export seam, doc comment included) exporting exactly the generated artifacts that feature uses, and a co-located `api.test.ts` mirroring wallow-web's identity-assertion pattern (`expect(api.x).toBe(query.x)` via namespace import — the lint override `apps/**/src/features/*/api.test.ts` already whitelists it). Repoint components to `import { ... } from "../api"`. Routes (`routes/login.tsx`, `routes/invitation.tsx`) import from the owning feature's seam.

Run the full app suite, commit: `refactor(wallow-auth): add features/*/api.ts seams matching wallow-web`

---

### Task 6: Migrate `apps/examples/minimal-app`

**Files:** `package.json` (swap web-shell + `@tanstack/react-query` → `"@bc-solutions-coder/query": "workspace:*"`), `src/router.tsx` (both imports), `src/routes/__root.tsx:16` (`QueryClient` type import).

Check `src/sdk-wiring.test.ts` (it greps app source for hook names) still passes. `pnpm --filter @bc-solutions-coder/example-minimal-app test && ... typecheck`, commit:
`refactor(minimal-app): consume react-query through the query facade`

---

### Task 7: Delete `packages/web-shell`

**Step 1:** `grep -rn "web-shell" --exclude-dir=node_modules --exclude-dir=dist --exclude-dir=.git .` — this is the work-list for Steps 3–4, not yet an assertion. Every hit outside `CHANGELOG.md`, `pnpm-lock.yaml` and `docs/plans/**`/`docs/audits/**` (which stay as history) must be resolved before Step 6. Re-run it at the end: only those historical hits may remain.

**Step 2:** `git rm -r packages/web-shell`, `pnpm install` (lockfile update).

**Step 3 (do this BEFORE the doc prose — it is build-breaking, and CI builds it): update both app Dockerfiles.**

`apps/wallow-auth/Dockerfile` and `apps/wallow-web/Dockerfile` each reference web-shell three times:
the manifest COPY (`COPY packages/web-shell/package.json packages/web-shell/`) that precedes
`pnpm install --frozen-lockfile`, the full-dir COPY (`COPY packages/web-shell packages/web-shell`),
and the "all five packages" / `@bc-solutions-coder/{sdk,styles,testing,ui,web-shell}` comment
between them. Replace each web-shell COPY with **both** `packages/query` and `packages/auth`.

**These Dockerfiles are already broken on `main` — see the PREREQUISITE at the top of this plan.**
They never gained `packages/forms`, so both images fail today. Land that as its own `fix(docker)`
commit BEFORE Task 1; by the time this step runs, each file should therefore reference **six**
packages (`sdk`, `styles`, `testing`, `ui`, `forms`, `web-shell`) and this step takes it to **seven**
(`sdk`, `styles`, `testing`, `ui`, `forms`, `query`, `auth`). If the prerequisite has NOT landed, do
not fold it in here — stop and land it separately, so a green/red bisect can still tell the
pre-existing break apart from this refactor. Fix the count word in the comment to match whatever the
list actually holds.

Both new packages must be present at manifest-copy time or `--frozen-lockfile` fails on an
unresolvable `workspace:*` link — and this breaks even for an app that depends on neither directly,
because Task 3 gives `packages/testing` a dep on `@bc-solutions-coder/query` and Task 3b gives
`packages/forms` one. Missing the full-dir COPY fails later, at `vite build`, with
`UNRESOLVED_IMPORT` (`Rolldown failed to resolve import` under Vite 8) — exactly the failure mode the
missing `packages/forms` produces on `main` right now.

This is on the CI critical path, not a nicety: `docker/docker-compose.test.yml` builds `wallow-auth`,
`wallow-web` and `bff-example` from these two Dockerfiles, and `.github/workflows/ci.yml` builds both
images for the `e2e-tests` job. Verify with an actual image build (`docker build -f
apps/wallow-web/Dockerfile -t wallow-web-react:test .`) before moving on — the root `pnpm build` in
Step 6 will NOT catch a broken Dockerfile.

**Step 4:** Update the live docs that name it:
- Root `CLAUDE.md`: repo-layout table — delete the web-shell row, add rows for `packages/query/` and `packages/auth/`; update the SDK-build note if it names web-shell.
- `apps/CLAUDE.md`: **three** sites now, and the count has already moved once — the forms merge rewrote the opener to "the **six** `@bc-solutions-coder` workspace packages (`sdk`, `styles`, `ui`, `forms`, `web-shell`, `testing`)", so this task takes it **six → seven** (`sdk`, `styles`, `ui`, `forms`, `query`, `auth`, `testing`), keeping the "`forms` is the only optional one" caveat. Also the later sentence about "the web-shell `./server` presets" being gone, and the app table's minimal-app row ("wiring all five shared packages") — that row's **five** is correct both before and after (minimal-app takes neither `forms` nor `auth`; `web-shell` → `query` is a one-for-one swap), so leave the number and check only that the surrounding prose doesn't name web-shell. Read the file rather than trusting either count.
- `docs/development/frontend-setup.md`: re-point web-shell mentions at `@bc-solutions-coder/query` (the package table row and the `workspace:*` dependency snippet), adding a row for `@bc-solutions-coder/auth`.
- `apps/examples/minimal-app/src/features/hello/HelloCard.tsx`: **rendered UI prose** — "This minimal app wires all five shared packages — sdk, styles, ui, testing, and web-shell". Not pinned by a test, but it ships in the demo screen. minimal-app does **not** take `forms` and does not take `auth`, so this stays **five**: swap `web-shell` → `query` and leave the count alone.
- `scripts/fork-smoke/README.md`: its package list names web-shell (see Deviation #2 — README only, no code change).
- `apps/wallow-web/README.md`, `apps/examples/minimal-app/README.md` (three mentions incl. the "private workspace packages" note), `packages/sdk/src/server/internal-origin.ts` (comment), the three apps' `vite.config.ts` (web-shell appears in header comments; also check `optimizeDeps`/`ssr.noExternal` lists and swap for the new package names).

**Step 5:** Add the new packages to `scripts/check-exports.sh`:

```bash
packages=(packages/sdk packages/styles packages/testing packages/query packages/auth)
```

**Step 6:** `pnpm build && pnpm typecheck && pnpm test` at the root — everything must be green with web-shell gone.

**Step 7:** Commit with the breaking-change marker:

```bash
git commit -m "feat!: delete @bc-solutions-coder/web-shell

BREAKING CHANGE: @bc-solutions-coder/web-shell is removed. createQueryClient
now ships from @bc-solutions-coder/query, which also re-exports the full
@tanstack/react-query surface; shared auth state moved to
@bc-solutions-coder/auth."
```

---

### Task 8: Lint enforcement

**Files:**
- Modify: `.oxlintrc.json`
- Modify: `packages/sdk/src/oxlint-guardrails.test.ts` (READ IT FIRST — it pins the config AND runs the real oxlint binary over violating/compliant snippets)

**Step 1:** In `.oxlintrc.json` `no-restricted-imports.paths`, append:

```json
{
  "name": "@tanstack/react-query",
  "message": "Import TanStack Query through @bc-solutions-coder/query — the facade owns the version and defaults, and keeps one QueryClient context instance across the workspace."
},
{
  "name": "@bc-solutions-coder/web-shell",
  "message": "web-shell is deleted. createQueryClient ships from @bc-solutions-coder/query."
}
```

**Step 2:** Append an override exempting the places that legitimately import the real package (the facade itself, and the four hand-written SDK files whose optional-peer typing predates it).

**Do NOT write `"no-restricted-imports": "off"` for `packages/sdk/**`.** oxlint has no per-name
partial disable, so turning the rule off would also drop the existing bans — the deleted
module-global client exports, the retired query slices and key registry, and the `dist/`/`src/`
deep-import patterns — across all SDK source, silently reopening exactly what bead Wallow-pu6a.5.8
closed. Instead **re-declare** the rule in the override, copying the root rule's `paths` and
`patterns` verbatim **minus** the new `@tanstack/react-query` entry, and scope `files` to the facade
plus only the four files that need it:

```json
{
  "files": [
    "packages/query/**",
    "packages/sdk/src/route-context.ts",
    "packages/sdk/src/route-context.test.ts",
    "packages/sdk/src/query/invalidations.ts",
    "packages/sdk/src/generated-query-surface.test.ts"
  ],
  "rules": {
    "no-restricted-imports": ["error", { "paths": [ /* the two root entries, verbatim */ ], "patterns": [ /* the two root pattern groups, verbatim */ ] }]
  }
}
```

(`packages/sdk/src/generated/**` is already in `ignorePatterns`, so generated code needs no entry.
The existing api.test.ts/index.test.ts override stays. Note the `@bc-solutions-coder/web-shell` ban
from Step 1 belongs in the re-declared copy too — nothing in the SDK or the facade may import it.)

**Step 3:** Extend `oxlint-guardrails.test.ts` — but read how it works first, because the obvious
version of this test cannot pass.

`restrictedImportDiagnostics()` writes each fixture into `mkdtempSync(join(tmpdir(), ...))`, i.e.
**outside the repo**. An `overrides.files` glob such as `packages/query/**` or
`packages/sdk/src/route-context.ts` can therefore never match a fixture path, so any assertion of
the form "a `packages/sdk/` file importing the type PASSES" would fail no matter how correct the
config is. Split the coverage:

- **Through the real binary** (existing tmpdir mechanism, path-agnostic): importing `useQuery` from
  `@tanstack/react-query` must produce a `no-restricted-imports` diagnostic; importing it from
  `@bc-solutions-coder/query` must produce none. Same for `@bc-solutions-coder/web-shell`.
- **Declaratively** (in the `describe` block that already reads `.oxlintrc.json` via
  `readRuleEntry()`): assert an override entry exists whose `files` cover the facade and the four
  SDK files, whose re-declared rule still carries the SDK `paths`/`patterns`, and whose `paths` do
  **not** include `@tanstack/react-query`. That is what proves the exemption is narrow rather than
  a blanket `"off"`.

`packages/forms/**` needs **no** exemption: Task 3b routes it through the facade, so the ban applies
to it like any app. If `pnpm lint` flags a forms file here, 3b is incomplete — fix 3b, do not widen
this override.

**Step 4:** `pnpm --filter @bc-solutions-coder/sdk test -- oxlint-guardrails` then root `pnpm lint` (must be clean — proves Tasks 3, 3b and 4–7 missed no import). Commit:
`feat(lint): ban direct @tanstack/react-query imports outside the facade`

---

### Task 9: Docs + prose-pin tests

**Files:**
- Modify: `docs/development/frontend-state.md`, root `CLAUDE.md` (Frontend state boundary section), `packages/sdk/src/query-rule-docs.test.ts`
- Modify (added by the forms merge, both name react-query and both go stale here): `docs/development/forms.md`, `packages/forms/CLAUDE.md`

**Step 1:** Extend `query-rule-docs.test.ts` with failing pins (mirroring its existing style): both the CLAUDE.md section and frontend-state.md must (a) contain `@bc-solutions-coder/query` and state that react-query is imported only through it, (b) frontend-state.md must contain `@bc-solutions-coder/auth` and `currentUserQuery`. Run — expect FAIL.

**Step 2:** Update the prose:
- `CLAUDE.md` → Frontend state boundary: add that react-query symbols (`useQuery`, `useMutation`, `QueryClient`, …) are imported from `@bc-solutions-coder/query`, never `@tanstack/react-query` (lint-enforced), and auth state (current user, roles/permissions, route gating) comes from `@bc-solutions-coder/auth`.
- `docs/development/frontend-state.md`: same rule spelled out; replace the doc's canonical current-user snippet (the `queryOptions({ ...usersGetCurrentUserOptions... })` example that the code never matched) with the real thing: `currentUserQuery` from `@bc-solutions-coder/auth`, noting the 401→null softening and 30s staleTime rationale. Update the "adding a query" checklist to route imports through the facade and feature seams.
- `docs/development/forms.md`: its post-submit-invalidation example imports `useQueryClient` from `@tanstack/react-query` — a copy-pasteable snippet that Task 8's lint rule makes illegal. Re-point it at `@bc-solutions-coder/query`. This is the one doc that teaches the pattern for new forms, so leaving it is worse than a stale sentence: it hands the next author a lint error.
- `packages/forms/CLAUDE.md`: the peer-dependency paragraph names `@tanstack/react-query`. After Task 3b the package consumes the facade, so state the peer as `@bc-solutions-coder/query` (matching whatever Task 3b actually put in `peerDependencies`) and say the underlying package is reached only through it.

**Step 3:** `pnpm --filter @bc-solutions-coder/sdk test -- query-rule-docs` — PASS. Commit:
`docs: state the query-facade and auth-package rules`

---

### Task 10: Full verification

```bash
pnpm check                                          # format:check + lint + typecheck + test + build + check:exports
pnpm why @tanstack/react-query                      # exactly one 5.x resolution
docker build -f apps/wallow-web/Dockerfile -t wallow-web-react:test .    # Task 7 Step 3 — pnpm check cannot catch this
docker build -f apps/wallow-auth/Dockerfile -t wallow-auth-react:test .
./scripts/e2e.sh                                    # full backend-dependent stack — the primary gate for 5c
```

**Both `docker build`s and `./scripts/e2e.sh` fail on `main` today, before any of this plan's work**
— the missing `packages/forms` COPY (see the PREREQUISITE header). Land the `fix(docker)` commit
first, and confirm both images build on unmodified `main`, so that a failure at this step is
unambiguously attributable to this plan. Building them once up front is also the cheapest way to know
the prerequisite actually worked.

**E2E is the primary verification for Task 5c, not a formality.** The wallow-auth suite has grown
well past what this plan originally assumed: `apps/wallow-auth/e2e/` now holds `signup`, `login`,
`logout`, `otp-login`, `magic-link`, `forgot-password`, `reset-password` and `mfa` specs alongside
the backend-free `routes` gate, plus a `global-setup.ts` (`apps/wallow-web/e2e/` gained one too).
Those specs drive almost exactly the flows whose `.mutate(...)` variables shape changes in 5c — if
a body/path/query mapping is wrong, this is what catches it.

Use `./scripts/e2e.sh` rather than driving the stack by hand; it was reworked after this plan was
written (`E2E_UP_SERVICE`, container-mode wallow-auth via `E2E_BASE_URL`, `E2E_SKIP_IMAGE_BUILD`)
and it runs all three suites — wallow-auth, wallow-web, and the wallow-web cross-app login journey —
against the containerised stack it brings up itself. Because it builds the app images, it also
double-checks Task 7 Step 3. The per-app `pnpm --filter ./apps/<app> test:e2e` runs stay useful for
a fast local loop on one suite, but they are not the gate.

Then session completion per CLAUDE.md: file beads for leftovers, `git pull --rebase && bd dolt push && git push`, verify `git status` clean.

---

## Task order & commit summary

0. `fix(docker): copy packages/forms into the app images` — **PREREQUISITE, not part of this plan's
   refactor.** Fixes a break that already exists on `main`; land and verify it before Task 1 so the
   `feat!` below is bisectable.
1. `feat(query): add @bc-solutions-coder/query react-query facade`
2. `feat(auth): add @bc-solutions-coder/auth shared authn/authz package`
3. `refactor(testing): consume react-query through the query facade`
4. `refactor(forms): consume react-query through the query facade` (Task 3b)
5. `refactor(web): consume react-query via the facade and auth via @bc-solutions-coder/auth`
6. `refactor(wallow-auth): facade swap → shared current-user → generated mutations → api seams` (4 commits)
7. `refactor(minimal-app): consume react-query through the query facade`
8. `feat!: delete @bc-solutions-coder/web-shell` (BREAKING CHANGE footer)
9. `feat(lint): ban direct @tanstack/react-query imports outside the facade`
10. `docs: state the query-facade and auth-package rules`

The numbering above is commit order; the task headings keep their original numbers (Task 3b sits
between Tasks 3 and 4). Tasks 3, 3b and 4–7 must all land before Task 8 — the lint rule is what
proves none of them missed an import, so it cannot go earlier.
10. verification + push

Out of scope (explicitly): `scripts/fork-smoke` (out-of-workspace, tarball-only), `WallowRouterContext` adoption (user declined), release-please/publish wiring (new packages are private), any backend change.
