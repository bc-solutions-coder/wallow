# Query + Auth Consolidation — Design

**status: active**

Consolidate all react-query usage out of the apps into a single facade package
(`@bc-solutions-coder/query`) and extract shared authN/authZ functionality into
`@bc-solutions-coder/auth`, so every app uses the query system and auth state uniformly.

## Motivation (survey findings)

- wallow-auth hand-rolls all 11 of its mutations with custom `mutationFn`s wrapping raw SDK
  operations, even though generated `{op}Mutation()` factories exist for every one; wallow-web
  uses the generated factories everywhere.
- The current-user query is duplicated (`apps/wallow-web/src/lib/current-user.ts`,
  `apps/wallow-auth/src/routes/invitation.tsx`) — the two copies share a cache key but have
  different staleTime/retry/resolved-shape semantics.
- wallow-web follows a `features/*/api.ts` seam convention; wallow-auth has none.
- 8 dead `retry: false` per-site overrides in wallow-auth (the shared client already sets it).
- `packages/web-shell` exports exactly one symbol (`createQueryClient`).
- Every app declares its own direct `@tanstack/react-query` dependency and imports hooks
  directly, so nothing structurally enforces uniform usage.

## Decisions (agreed in brainstorming)

| Decision | Choice |
| --- | --- |
| End state | Full facade — apps never import `@tanstack/react-query` directly |
| Facade home | New `packages/query` (`@bc-solutions-coder/query`) |
| web-shell | Deleted; `createQueryClient()` absorbed into `packages/query` |
| Facade width | **Full re-export** (`export * from "@tanstack/react-query"`) |
| Auth home | New `packages/auth` (`@bc-solutions-coder/auth`) |
| Ride-along fixes | wallow-auth mutations → generated factories; wallow-auth `api.ts` seams; shared current-user query |
| Out of scope | `WallowRouterContext` adoption (offered, not selected); moving sdk/query artifacts (they stay in the SDK with the regen pipeline) |

## 1. Package architecture

```
packages/query   @bc-solutions-coder/query   NEW — owns react-query
packages/auth    @bc-solutions-coder/auth    NEW — authN/authZ hooks & queries
packages/web-shell                           DELETED (absorbed into query)
```

Dependency direction: `auth → query + sdk`; apps depend on `query`, `auth`, `sdk`.
`packages/sdk` is untouched — its generated `./query` artifacts and curated `invalidations`
stay where they are.

### `@bc-solutions-coder/query`

- `export * from "@tanstack/react-query"` — the single place react-query enters the codebase.
  Apps drop their direct `@tanstack/react-query` dependency; the query package alone declares
  the version.
- `createQueryClient()` moves here from web-shell unchanged (`retry: false`), with its tests
  (including the devtools-gating test).
- React is a peer dependency; `@tanstack/react-query` is a regular dependency of this package
  only.

### `@bc-solutions-coder/auth`

- `currentUserQuery(client)` — the single canonical definition replacing both app copies.
  One cache key, one semantics: 401 resolves to `null` (anonymous is an answer, not an
  error — the documented reason both apps hand-rolled it), `staleTime: 30_000`, one
  `WallowUser` shape.
- `useCurrentUser()` hook for components.
- AuthZ helpers over the user payload: `hasRole(user, role)`, `hasPermission(user, perm)`,
  and a `useAuthorization()` convenience hook (exact shape pinned during planning from what
  the current-user endpoint actually returns).
- Route-guard glue: `ensureCurrentUser(context)` for `beforeLoad`/loaders composing
  `queryClient.ensureQueryData(currentUserQuery(...))` with the SDK's existing
  `requireAuth`/`loginRedirect`, so all apps gate routes identically.

## 2. Enforcement

- oxlint `no-restricted-imports`: `@tanstack/react-query` banned everywhere except
  `packages/query` and the SDK's generated code (`react-query.gen.ts` cannot be changed).
  Follows the existing pattern that bans retired sdk/query exports.
- Surface-pinning tests in `packages/query` (createQueryClient behavior + re-export sanity)
  and `packages/auth` (export surface), mirroring `sdk/query/index.test.ts`.
- `packages/testing`'s `render-with-wallow` imports from `@bc-solutions-coder/query`, so test
  infrastructure also goes through the facade.

## 3. App migration

**wallow-web** — mechanical: swap ~16 import sites to `@bc-solutions-coder/query`; delete
`lib/current-user.ts` in favor of `@bc-solutions-coder/auth`; `features/*/api.ts` seams stay.

**wallow-auth** — the substantive cleanup:

- Replace the 11 hand-rolled `useMutation({ mutationFn })` sites with generated
  `{op}Mutation()` factories. Where a site has a genuine typing reason (the
  untyped-anonymous-response cases like `PasswordLoginForm`), the narrowing moves into the
  seam file with a documented comment — not an ad-hoc `mutationFn` in the component.
- Add `features/*/api.ts` seams matching wallow-web's convention, with identity re-export
  tests.
- Delete the 8 dead `retry: false` overrides.
- `routes/invitation.tsx`'s current-user copy → `@bc-solutions-coder/auth`.

**minimal-app** — swap `QueryClient` type imports and router wiring to the query package.
**fork-smoke stays untouched** (verified during planning): it consumes only the sdk + styles
packed tarballs, and the new packages are private — its documented `new QueryClient()`
workaround stands.

## 4. Docs, tests, release

- Update `docs/development/frontend-state.md`, root `CLAUDE.md` (repo-layout table, build
  order), and the prose-pinning test `packages/sdk/src/query-rule-docs.test.ts`. New rules:
  react-query is imported only via `@bc-solutions-coder/query`; auth state comes from
  `@bc-solutions-coder/auth`.
- New packages get vitest suites per `TESTING.md` (browser mode for hooks) and
  `check:exports` coverage (publint/attw).
- **Breaking change**: web-shell deletion is `feat!` — forks that import it must migrate.
  Verified during planning: web-shell is `private: true` with no release-please or publish
  wiring, so the new packages mirror that (private workspace packages, no publish pipeline);
  they are added to `scripts/check-exports.sh` for exports-map hygiene (precedent: the
  private `packages/testing` is already checked there).
- Build order: apps typecheck against `dist/`, so the order becomes sdk → query → auth → apps.

## Testing strategy

Existing app vitest suites and both Playwright e2e suites are the regression net — the
wallow-auth mutation rewrite is the only behavior-adjacent change, and `login.spec.ts` plus
the cross-app journey suite cover exactly those flows. New unit tests: query surface pinning,
auth package hooks (browser mode), seam identity tests in wallow-auth.
