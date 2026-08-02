**status: active**

# BFF CSRF + Path-Based Topology Remediation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix every finding from the 2026-07-28 TanStack/BFF audit (`docs/plans/2026-07-28/2358-tanstack-bff-csrf-audit.md`): the CSRF regression that 403s all dashboard mutations in dev, the two path-based production criticals, the dev/prod parity gaps, and all medium/low hardening items — keeping **path-based** (`wallow.dev/api`, `/auth`, `/`) as the supported production topology.

**Architecture:** Three load-bearing changes, then hardening. (1) The SDK's browser CSRF interceptor gains a double-submit-cookie fallback so apps need no bespoke wiring — the cookie the BFF already writes at callback IS the synchronizer token. (2) The SDK's OIDC endpoint pinning becomes path-preserving (`rebase to issuer` instead of `rewrite origin`), which fixes the `/api` prefix loss for path-based deployments in one place. (3) wallow-auth learns a build-time base path (`/auth`) so path hosting works end-to-end, validated by a reference Caddy ingress added to the production compose. Everything follows TDD; SDK first (apps typecheck against `dist/`).

**Tech Stack:** TypeScript (Vitest node projects for SDK unit tests), TanStack Start/Vite/Nitro, Playwright, .NET Aspire AppHost (C#), Docker Compose, Caddy (reference ingress).

**Rules to load before starting:** `.claude/rules/TESTING.md`, `.claude/rules/E2E.md`, `packages/sdk/CLAUDE.md`, `.claude/rules/CONVENTIONS.md` (Task 3.2 touches C#).

**Standing commands:**

```bash
pnpm --filter @bc-solutions-coder/sdk build        # after every SDK change, before app typecheck
pnpm --filter @bc-solutions-coder/sdk test         # SDK vitest
pnpm check                                          # full quality gate before each push
```

Branch: `git checkout -b fix/bff-csrf-and-path-topology` off `main`.

---

## Phase 1 — CSRF regression (unblocks dev today)

### Task 1.1: SDK — double-submit cookie fallback in the CSRF interceptor

The interceptor (`packages/sdk/src/csrf.ts:67-74`) only stamps `x-csrf-token` when `setCsrfToken()` was called; the only caller left is `bff-demo.tsx`. The fallback source already exists: the non-HttpOnly `${cookieName}-csrf` cookie written at the OIDC callback (`handlers.ts:494`), already read by `logout()` via `readCsrfCookie()` (`auth.ts:117-134`). Move that reader into `csrf.ts` and use it as the interceptor's fallback. This also neutralizes finding M1 (module-global token store leaking across SSR renders): gate the module store behind a `typeof document` check so the server bundle can never serve one user's token to another.

**Files:**
- Modify: `packages/sdk/src/csrf.ts`
- Modify: `packages/sdk/src/auth.ts` (delete its private `readCsrfCookie`, import from `./csrf`)
- Test: `packages/sdk/src/csrf.test.ts` (create if absent — node project, stub `document`)

**Step 1: Write failing tests**

```ts
// packages/sdk/src/csrf.test.ts
import { afterEach, describe, expect, it, vi } from "vitest";
import { readCsrfCookie, setCsrfToken, wireCsrfInterceptor } from "./csrf";

/** Minimal client double capturing the registered interceptor. */
function captureInterceptor() {
  let interceptor: ((request: Request) => Request) | undefined;
  wireCsrfInterceptor({
    interceptors: { request: { use: (fn) => { interceptor = fn; } } },
  });
  if (interceptor === undefined) throw new Error("interceptor not registered");
  return interceptor;
}

function withDocumentCookie(cookie: string): void {
  vi.stubGlobal("document", { cookie });
}

afterEach(() => {
  setCsrfToken(null);
  vi.unstubAllGlobals();
});

describe("wireCsrfInterceptor", () => {
  it("falls back to the double-submit cookie when no token was set", () => {
    withDocumentCookie("wallow_bff-csrf=cookie-token; other=1");
    const intercept = captureInterceptor();
    const request = intercept(new Request("http://x/api/things", { method: "POST" }));
    expect(request.headers.get("x-csrf-token")).toBe("cookie-token");
  });

  it("prefers an explicitly set token over the cookie", () => {
    withDocumentCookie("wallow_bff-csrf=cookie-token");
    setCsrfToken("explicit-token");
    const intercept = captureInterceptor();
    const request = intercept(new Request("http://x/api/things", { method: "POST" }));
    expect(request.headers.get("x-csrf-token")).toBe("explicit-token");
  });

  it("prefers a __Host- prefixed cookie over a bare-named one", () => {
    // Stale localhost jars can hold both names (Aspire vs compose runs).
    withDocumentCookie("wallow_bff-csrf=stale; __Host-wallow_bff-csrf=fresh");
    const intercept = captureInterceptor();
    const request = intercept(new Request("http://x/api/things", { method: "POST" }));
    expect(request.headers.get("x-csrf-token")).toBe("fresh");
  });

  it("sends no header on safe methods and when no source exists", () => {
    const intercept = captureInterceptor();
    const get = intercept(new Request("http://x/api/things"));
    const post = intercept(new Request("http://x/api/things", { method: "POST" }));
    expect(get.headers.get("x-csrf-token")).toBeNull();
    expect(post.headers.get("x-csrf-token")).toBeNull(); // no document, no token
  });

  it("ignores the module store outside the browser (SSR isolation)", () => {
    setCsrfToken("leaked-from-another-request"); // no document stubbed = server
    const intercept = captureInterceptor();
    const request = intercept(new Request("http://x/api/things", { method: "POST" }));
    expect(request.headers.get("x-csrf-token")).toBeNull();
  });
});
```

**Step 2: Run to verify failure**

Run: `pnpm --filter @bc-solutions-coder/sdk exec vitest run src/csrf.test.ts`
Expected: FAIL — `readCsrfCookie` is not exported; fallback/`__Host-` behavior missing.

**Step 3: Implement**

In `csrf.ts`: move `CSRF_COOKIE_SUFFIX` + `readCsrfCookie()` here from `auth.ts`, export both. `readCsrfCookie` collects ALL `-csrf`-suffixed cookies and prefers a `__Host-`-prefixed name (fixes M8's first-match-wins). Resolution inside the interceptor:

```ts
function resolveToken(): string | null {
  const inBrowser: boolean = typeof document !== "undefined";
  if (!inBrowser) {
    return null; // SSR: the module store is process-global — never trust it.
  }
  return csrfToken ?? readCsrfCookie();
}

export function wireCsrfInterceptor(client: CsrfInterceptorClient): void {
  client.interceptors.request.use((request: Request): Request => {
    const token: string | null = resolveToken();
    if (token !== null && !isSafeMethod(request.method)) {
      request.headers.set("x-csrf-token", token);
    }
    return request;
  });
}
```

In `auth.ts`: delete the private `readCsrfCookie`/`CSRF_COOKIE_SUFFIX`, `import { getCsrfToken, readCsrfCookie } from "./csrf"`, keep `resolveCsrfToken` order (explicit option → module → cookie).

Deliberately **no** new `/bff/user` fetch on wallow-web's app path (YAGNI): the cookie is rewritten at every callback, so it is always the live synchronizer token — including after a re-login that would stale the module store. Note this in the commit message.

**Step 4: Verify**

Run: `pnpm --filter @bc-solutions-coder/sdk exec vitest run src/csrf.test.ts src/index.test.ts` → PASS.
Run: `pnpm --filter @bc-solutions-coder/sdk build && pnpm --filter @bc-solutions-coder/sdk test` → PASS (auth.test.ts logout ordering must still pass).

**Step 5: Commit** — `fix(sdk): fall back to the double-submit cookie in the CSRF interceptor`

### Task 1.2: Live verification in dev

**Step 1:** `pnpm backend` (Aspire). Log in at `http://localhost:3000` (`admin@wallow.dev`, creds per `api/seed.json`).
**Step 2:** Perform one dashboard mutation (e.g. create an organization). Expected: succeeds — no 403 `CSRF_INVALID`. DevTools: the POST carries `x-csrf-token` matching the `wallow_bff-csrf` cookie.
**Step 3:** Logout. Expected: completes without "Logout failed".

### Task 1.3: E2E — authenticated mutation + logout coverage (closes the CI blind spot)

**Files:**
- Modify: `apps/wallow-web/e2e-cross-app/login-journey.spec.ts` (or sibling spec in same dir)

Read `.claude/rules/E2E.md` first. Extend the journey after the existing dashboard assertion: perform one real mutation through the UI (submit the inquiry form or create an organization — whichever has stable `data-testid`s; add `{page}-{element}` testids if missing), assert the app-level success signal (created row visible), then logout and assert the signed-out state. Header comment: backend-dependent. Run against Aspire: `pnpm backend` up, then `pnpm --filter ./apps/wallow-web test:e2e:cross-app`. Expected: PASS with Task 1.1; FAILS on `main` without it (verify once against a stash to prove the spec catches the regression).

**Commit** — `test(web): cover an authenticated mutation and logout in the cross-app journey`

---

## Phase 2 — Path-based production topology (criticals P1, P2)

### Task 2.1: SDK — path-preserving OIDC endpoint pinning (P1)

`discover()` pins browser-facing endpoints with `rewriteOrigin(endpoint, new URL(issuer).origin)` (`packages/sdk/src/server/oidc.ts:104-110,167-177`) — `origin` discards the issuer's `/api` path. Verified live: OpenIddict advertises endpoints from the request base, so metadata fetched from `http://wallow-api:8080/...` advertises `http://wallow-api:8080/connect/*`, and pinning must produce `https://wallow.dev/api/connect/*`.

**Files:**
- Modify: `packages/sdk/src/server/oidc.ts`
- Test: `packages/sdk/src/server/oidc.test.ts` (extend; `openid-client` is already mocked here — unique issuer per test, discovery cache is keyed by metadata URL)

**Step 1: Failing tests** — the environment matrix:

| issuer | advertised endpoint | expected pinned |
|---|---|---|
| `http://localhost:3002` (dev) | `http://localhost:5001/connect/authorize` | `http://localhost:3002/connect/authorize` |
| `http://localhost:5050` (e2e) | `http://host.docker.internal:5050/connect/authorize` | `http://localhost:5050/connect/authorize` |
| `https://wallow.dev/api` (prod) | `http://wallow-api:8080/connect/authorize` | `https://wallow.dev/api/connect/authorize` |
| `https://wallow.dev/api` (prod, metadata fetched with `/api` PathBase) | `http://wallow-api:8080/api/connect/authorize` | `https://wallow.dev/api/connect/authorize` (no double prefix) |

Same four cases for `end_session_endpoint`. Also pin `buildLogoutUrl`'s fallback path (`oidc.ts:339-345`) through the same helper.

**Step 2:** Run `pnpm --filter @bc-solutions-coder/sdk exec vitest run src/server/oidc.test.ts` → FAIL (prod cases).

**Step 3: Implement** — replace `rewriteOrigin` with:

```ts
/**
 * Re-base an advertised endpoint onto the public issuer, preserving the
 * issuer's PATH as a prefix (a path-hosted issuer like https://host/api
 * must yield https://host/api/connect/*). If the advertised endpoint already
 * carries the issuer's path prefix (discovery fetched through the same
 * PathBase), it is not prefixed twice.
 */
function rebaseToIssuer(endpoint: string, issuer: string): string {
  const source: URL = new URL(endpoint);
  const base: URL = new URL(issuer);
  const issuerPath: string = base.pathname.replace(/\/+$/u, "");
  const alreadyPrefixed: boolean =
    issuerPath !== "" &&
    (source.pathname === issuerPath || source.pathname.startsWith(`${issuerPath}/`));
  const path: string = alreadyPrefixed ? source.pathname : `${issuerPath}${source.pathname}`;
  return `${base.origin}${path}${source.search}`;
}
```

Swap both call sites in `discover()` (and the logout fallback). Token/userinfo endpoints stay verbatim (backchannel, server-reachable by construction).

**Step 4:** SDK tests + build → PASS. **Step 5: Commit** — `fix(sdk): preserve the issuer path when pinning browser-facing OIDC endpoints`

### Task 2.2: wallow-auth — base-path support (P2)

wallow-auth must be servable at `https://wallow.dev/auth/...` with the prefix intact (no ingress stripping — SSR HTML, `/_build/*` assets, and browser `fetch`es must all carry `/auth`). Introduce ONE knob, `AUTH_BASE_PATH` (default empty = current behavior; prod sets `/auth`). Because it's a build-time knob for Vite, the production image must be built with it.

**Files:**
- Modify: `apps/wallow-auth/vite.config.ts` — `base: process.env.AUTH_BASE_PATH ? \`${process.env.AUTH_BASE_PATH}/\` : "/"` and pass the same value into TanStack Start's router basepath option (check the installed `@tanstack/react-start` plugin signature for `router.basepath` / `spa.basepath` — pinned version in `apps/wallow-auth/package.json`).
- Modify: `apps/wallow-auth/src/start.ts` (browser/SSR SDK baseUrl: origin + base path instead of bare origin, `apps/wallow-auth/src/start.ts:55-58`)
- Modify: `apps/wallow-auth/src/routes/*` passthrough splats — they codegen under the basepath automatically once the router knows it; verify `/auth/v1/**` reaches `handleApiPassthrough` and the passthrough still forwards the UNPREFIXED `/v1/**` upstream (strip the base before `passthrough.handle`, or configure the passthrough prefix list — decide from `packages/sdk/src/server/passthrough.ts:50-54` behavior in a spike test first).
- Modify: `apps/wallow-auth/Dockerfile` — `ARG AUTH_BASE_PATH` → `ENV` before `pnpm build`.
- Test: `apps/wallow-auth/e2e/routes.spec.ts` still green with default empty base; add a vitest node spec for whatever base-resolution helper is extracted.

**Steps:** (1) Spike: build with `AUTH_BASE_PATH=/auth`, `pnpm --filter ./apps/wallow-auth start`, `curl -I localhost:3002/auth/login` → 200 and asset URLs prefixed; write down what actually needed changing. (2) Encode that as tests. (3) Implement minimally. (4) Both `pnpm --filter ./apps/wallow-auth test` and `test:e2e` pass with base unset (default behavior unchanged). (5) Commit — `feat(auth): support serving under a base path for path-hosted deployments`.

**Warning:** this is the highest-uncertainty task of the plan (TanStack Start basepath support varies by version). If the spike shows the pinned Start version cannot do it cleanly, STOP and surface it — options are upgrading Start or reverting the topology decision — do not hand-roll path rewriting middleware.

### Task 2.3: Reference ingress + loopback ports in production compose (validates P1+P2; fixes M5, M9)

**Files:**
- Modify: `docker/docker-compose.production.yml` — add a `caddy` service (path routing per the file's own header: `/api/*` → `wallow-api:8080` with prefix KEPT (API uses `UsePathBase("/api")`), `/auth/*` → `wallow-auth:8080` prefix KEPT, everything else → `wallow-web:8080`; stamps `X-Forwarded-Proto`). Change the three app `ports:` mappings to `127.0.0.1:` bindings (they stay reachable for debugging but not exposed; Caddy owns 80/443).
- Create: `docker/caddy/Caddyfile.example`
- Modify: `docker/.env.production.example` — document the ingress, that `X-Forwarded-Proto: https` is REQUIRED from any replacement ingress, and switch the secret recipe from `openssl rand -base64 32` to `openssl rand -hex 32` (fixes L4's `/ + =` breakage in `REDIS_URL`).

**Verify:** `docker compose -f docker-compose.production.yml --env-file .env.production config` parses; full-stack smoke in Task 5.4. **Commit** — `feat(docker): reference Caddy ingress, loopback port bindings, hex secrets for production compose`

---

## Phase 3 — Dev/prod parity (M2, M3, P3)

### Task 3.1: `COOKIE_SECURE=false` in the e2e compose stack (M2)

**Files:** Modify `docker/docker-compose.test.yml` — add `COOKIE_SECURE: "false"` to `wallow-web` AND `bff-example` environment blocks, with the same WebKit comment as `AppHost/Program.cs:92-95`. Also fix the stale `bff-example` header comment claiming the SDK "rewrites the backchannel token_endpoint" (it uses it verbatim — the audit verified endpoints derive from the request base, so it is already reachable).
**Verify:** `./scripts/e2e.sh` (full backend-dependent runner) → green. **Commit** — `fix(docker): disable Secure cookies in the plain-http e2e stack`

### Task 3.2: Valkey-backed sessions under Aspire (M3)

Aspire injects `ConnectionStrings__Redis`; the app reads only `REDIS_URL` (`apps/wallow-web/src/lib/bff.ts:66`), so `pnpm backend` silently runs cookie sessions while e2e/prod run Valkey. Fix in the AppHost (keep the app's contract single-var). Read `.claude/rules/CONVENTIONS.md`; run `dotnet format api/Wallow.slnx` before committing.

**Files:** Modify `api/src/Wallow.AppHost/Program.cs` — on the wallow-web resource, compose a `redis://` URL from the valkey resource, e.g.:

```csharp
.WithEnvironment(context =>
{
    // bff.ts selects the Valkey session store only when REDIS_URL is set; the
    // Aspire reference alone injects ConnectionStrings__Redis, which the Node
    // host never reads, silently diverging dev (cookie sessions) from prod.
    EndpointReference endpoint = valkey.Resource.PrimaryEndpoint;
    context.EnvironmentVariables["REDIS_URL"] = ReferenceExpression.Create(
        $"redis://:{valkey.Resource.PasswordParameter}@{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}");
})
```

(Exact valkey resource/password API: check the `AddValkey` builder in the installed Aspire.Hosting.Valkey version; if no password parameter exists in dev, `redis://{host}:{port}` suffices.)
**Verify:** `pnpm backend`; wallow-web logs/`bff.ts` path shows the Valkey store selected (add a one-line startup log there if none exists); login + mutation still work; logout truly revokes (back-button shows signed-out).
**Commit** — `fix(apphost): hand wallow-web a REDIS_URL so dev uses Valkey sessions like prod`

### Task 3.3: CI runs the cross-app journey (P3)

**Files:** Modify `scripts/e2e.sh` (after the wallow-auth suite: bring up the same `docker-compose.test.yml` stack, run `pnpm --filter ./apps/wallow-web test:e2e` and `test:e2e:cross-app` with `E2E_BASE_URL=http://localhost:5053`), and `.github/workflows/ci.yml` `e2e-tests` job if artifacts/paths need extending.
**Verify:** `./scripts/e2e.sh` locally end-to-end (this now also exercises Task 1.3's mutation spec against the compose stack — the first CI-shaped proof of the whole login+mutate+logout loop). **Commit** — `ci: run the wallow-web and cross-app e2e suites in the e2e job`

---

## Phase 4 — SDK/app hardening (M4, M6, M7, M10)

### Task 4.1: Validate `COOKIE_PASSWORD` length at config load (M4)

**Files:** `packages/sdk/src/server/config.ts` + `config.test.ts`.
TDD: failing test — 31-char password → `loadBffConfigFromEnv` throws with the other problems in the ONE aggregated error (`Invalid BFF environment configuration`); 32-char passes. Implement: `const MIN_COOKIE_PASSWORD_LENGTH = 32;` push onto `problems` when shorter (iron-webcrypto seals fail below 32 — today that's a 500 mid-login-callback instead of a boot error). Commit — `fix(sdk): fail at boot on a too-short COOKIE_PASSWORD`

### Task 4.2: Client-IP forwarding through the BFF `/api` proxy (M6)

The passthrough already has the seam (`CLIENT_IP_HEADER` appended to `X-Forwarded-For`, then stripped — `passthrough.ts:157-174`); the BFF proxy forwards only `content-type`/`accept` (`proxy.ts:553`) so the API rate-limits all wallow-web users as one client (the exact bug Wallow-tt5j fixed for wallow-auth).
**Files:** `packages/sdk/src/server/proxy.ts` (+ its test), `apps/wallow-web/src/routes/api/$.ts`.
TDD: failing proxy test — request carrying `x-wallow-client-ip: 1.2.3.4` reaches upstream with `x-forwarded-for` ending in `1.2.3.4`, seam header stripped, and (mirroring the passthrough) `x-forwarded-proto`/`-host` set from the inbound URL only when absent. Reuse/extract the passthrough's `applyForwardedHeaders` rather than duplicating. App side: stamp `request.ip` onto `CLIENT_IP_HEADER` in `routes/api/$.ts` exactly as `apps/wallow-auth/src/lib/api-passthrough.ts:53-62` does (mutate, don't clone — srvx request class, see that file's comment). Commit — `fix(sdk): forward the real client IP through the BFF api proxy`

### Task 4.3: Honor `X-Forwarded-Proto` for the SSR baseUrl (M7)

**Files:** `apps/wallow-web/src/start.ts` (and the identical derivation in `apps/wallow-auth/src/start.ts` / `apps/examples/minimal-app` if present) + node-project spec.
TDD: request `http://wallow.dev/x` with `x-forwarded-proto: https` → SDK baseUrl `https://wallow.dev/api`; without the header → unchanged. Implement in the middleware: scheme = `request.headers.get("x-forwarded-proto") ?? url.protocol`; behind the Task 2.3 ingress this makes SSR and browser query keys share one baseUrl so hydration reuse works (generated keys embed baseUrl). Commit — `fix(web): derive the SSR origin scheme from x-forwarded-proto`

### Task 4.4: Document the issuer/origin coupling + topology contract (M10, M9)

**Files:** `docs/integrations/bff-pattern.md`, `docs/getting-started/fork-guide.md` (+ `docs/toc.yml` untouched — existing pages).
Document: issuer differs per environment (dev = auth origin :3002, e2e = API :5050, prod = `wallow.dev/api`); what breaks when a fork changes one origin; `Authentication__CookieDomain` widening the identity cookie to all subdomains; the ingress MUST send `X-Forwarded-Proto: https`; response_mode must remain a top-level GET redirect (SameSite=Lax tx cookie — L6). Commit — `docs: document the OIDC issuer/origin and ingress contract`

---

## Phase 5 — Lows (L1, L2, L3, L5, L6) + final verification

### Task 5.1: Idempotent anonymous logout + `no-store` on `/bff/user` (L1, L2)

**Files:** `packages/sdk/src/server/handlers.ts` + `handlers.test.ts`.
TDD (three failing tests): (1) `POST /bff/logout` with NO session → 204/redirect + session cookies cleared, NOT 403 (an anonymous logout has nothing to protect; today the user sees "Logout failed"); CSRF check still enforced whenever a session EXISTS — including sessions minted without `csrfToken`, which stay fail-secure. (2) authenticated logout unchanged. (3) `/bff/user` 200 carries `Cache-Control: no-store` (body holds identity claims + CSRF token). Commit — `fix(sdk): idempotent anonymous logout and no-store on /bff/user`

### Task 5.2: `COOKIE_PASSWORD` rotation via a keyed secret map (L3)

**Files:** `packages/sdk/src/server/config.ts`, `session.ts` (+ tests), `docs/integrations/bff-pattern.md`.
Support optional `COOKIE_PASSWORDS` (JSON object `{"v2":"<32+ chars>","v1":"..."}`): iron-webcrypto accepts a password map — newest id seals, all ids unseal — so rotating stops mass-401ing every session. `COOKIE_PASSWORD` alone keeps working (wrap as `{"1": value}`). TDD: seal under v1, unseal with `{v2,v1}` map succeeds; new seals carry v2. Keep `txstate.ts` on the same path. Commit — `feat(sdk): support cookie-password rotation via COOKIE_PASSWORDS`

### Task 5.3: In-process refresh mutex for the cookie session store (L5)

**Files:** `packages/sdk/src/server/store/cookie.ts` + test.
`withRefreshLock` is a no-op, so two concurrent tab refreshes in ONE process can double-spend a one-time refresh token. TDD: two concurrent `withRefreshLock(sessionId, fn)` calls for the same id run `fn` sequentially (second sees the first's result); different ids run concurrently. Implement a per-sessionId promise-chain mutex (Map, entry deleted on settle). Document that multi-INSTANCE cookie-store deployments still need the Valkey store (unchanged tradeoff). Commit — `fix(sdk): serialize same-session refreshes in the cookie store`

### Task 5.4: Full-stack verification sweep

1. `pnpm check` (root: format, lint, typecheck, tests, build, exports) → green.
2. `./scripts/run-tests.sh` (backend suites — AppHost changed) → green.
3. `./scripts/e2e.sh` (now includes wallow-web + cross-app per Task 3.3) → green.
4. **Path-topology smoke (the point of Phase 2):** build prod images (`AUTH_BASE_PATH=/auth` for wallow-auth), `docker compose -f docker/docker-compose.production.yml --env-file docker/.env.production up --build` with a local `.env.production` (hex secrets, `API_PUBLIC_URL=http://localhost/api`, `AUTH_PUBLIC_URL=http://localhost/auth`, `WEB_PUBLIC_URL=http://localhost`, `COOKIE_SECURE=false` for the http-only smoke). Through the Caddy ingress on `http://localhost`: complete login → dashboard → one mutation → logout. This exercises P1 (authorize URL carries `/api`), P2 (`/auth` UI serves), M6 (client IP), M7 (baseUrl), Task 1.1 (CSRF) in one pass.
5. `bd` bookkeeping: file/close beads per finding as tasks complete; `git pull --rebase && bd dolt push && git push`.

---

## Execution notes

- **Order matters:** Phase 1 → 2 → 3 → 4 → 5; within phases, tasks are independent. SDK tasks (1.1, 2.1, 4.1, 4.2, 5.1–5.3) each end with `pnpm --filter @bc-solutions-coder/sdk build` so app typechecks see fresh `dist/`.
- **Do not** re-introduce any deleted SDK surface (see `packages/sdk/CLAUDE.md` — barrels are guard-tested).
- **Conventional commits** as given per task; SDK changes ship independently via `sdk-v*` tags, so keep SDK commits self-contained.
- **Highest-risk task:** 2.2 (Start basepath). Spike first; escalate rather than hand-roll.
- Findings ↔ tasks: §1→1.1–1.3 · P1→2.1 · P2→2.2 · P3→3.3 · M1→1.1 · M2→3.1 · M3→3.2 · M4→4.1 · M5→2.3 · M6→4.2 · M7→4.3 · M8→1.1 · M9→2.3+4.4 · M10→4.4 · L1→5.1 · L2→5.1 · L3→5.2 · L4→2.3 · L5→5.3 · L6→4.4.
