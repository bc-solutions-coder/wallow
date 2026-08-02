**status: completed**

# TanStack Apps + BFF SDK — Token/CSRF Audit Report

Audit date: 2026-07-28. Read-only audit by a 5-agent team (SDK internals, wallow-web/Aspire
wiring, wallow-auth/OIDC journey, production parity, session history), with load-bearing
claims independently re-verified against source and against the live dev API (empirical
check of OpenIddict discovery behavior on localhost:5001).

Scope: `packages/sdk` (server + browser CSRF/session), `apps/wallow-web`,
`apps/wallow-auth`, `api/src/Wallow.AppHost`, `docker/docker-compose.test.yml`,
`docker/docker-compose.production.yml`.

---

## 1. Root cause of the reported bug (CSRF mismatch at localhost:3000 under `pnpm backend`)

**Confirmed regression from commit `cc8311e3` ("migrate apps to tanstack start and
streamline sdk"). Not an Aspire, cookie, or key problem.**

Mechanism:

- The BFF mints the CSRF synchronizer token at the OIDC callback and hands the browser a
  copy only in the `GET /bff/user` response body
  (`packages/sdk/src/server/handlers.ts:480-511`).
- The browser-side interceptor stamps `x-csrf-token` on POST/PUT/PATCH/DELETE **only when
  `setCsrfToken()` has been called** — it has no fallback to the `wallow_bff-csrf`
  double-submit cookie (`packages/sdk/src/csrf.ts:67-74`).
- Before the migration, `apps/wallow-web/src/app.ts` fetched `/bff/user` and called
  `setCsrfToken()` on the main app path. That file was deleted; the only remaining caller
  in the whole workspace is the demo route `apps/wallow-web/src/routes/bff-demo.tsx:94,102`.
- The dashboard resolves the current user via the generated `usersGetCurrentUser` through
  `/api/users/me` (`apps/wallow-web/src/lib/current-user.ts:55-64`), which never returns
  the BFF CSRF token.
- Result: every dashboard mutation (`organizationsCreateMutation`,
  `inquiriesSubmitMutation`, `mfa*Mutation`, …) POSTs with **no** `x-csrf-token` header →
  the `/api` proxy gate (`packages/sdk/src/server/proxy.ts:769-783`) answers
  `403 {"title":"CSRF token mismatch or missing","code":"CSRF_INVALID"}`, surfaced verbatim
  in the UI.

Why it appears "only under `pnpm backend`": that is the only interactive mode where login
can complete at all (plain `pnpm dev` has no `OIDC_*`/`COOKIE_*` env, so `/bff/*` throws at
config load). Aspire runs the apps proxy-less (`isProxied: false`,
`api/src/Wallow.AppHost/Program.cs:63,77`) — no origin rewriting is involved.

Why CI never caught it: no E2E test performs an authenticated mutation. The cross-app
journey spec stops at "dashboard renders"; the CI e2e job runs only the wallow-auth suite.

**Fix directions (not applied — audit was read-only):**

1. Give `wireCsrfInterceptor` a fallback that reads the double-submit cookie (it exists
   precisely for this bootstrap; today only `logout()` reads it), and/or
2. Re-learn the token on the main app path: after auth, fetch `/bff/user` and call
   `setCsrfToken(user.csrfToken)` (restores the pre-migration behavior), and
3. Add an E2E spec that performs one authenticated mutation (would have caught this).

---

## 2. Production-blocking findings (would break on deploy, path-based topology)

### P1 — CRITICAL: browser-facing OIDC authorize/logout URLs lose the `/api` prefix

Verified empirically: OpenIddict advertises discovery **endpoints from the incoming
request base**, not the configured issuer (live check against the dev API: issuer field
`http://localhost:3002/`, endpoints all `http://localhost:5001/connect/*`). In production,
discovery is fetched from `http://wallow-api:8080/.well-known/openid-configuration`
(`docker-compose.production.yml:488` — no `/api` prefix → empty PathBase), so endpoints
come back as `http://wallow-api:8080/connect/*`. The SDK then pins browser-facing
endpoints to `new URL(issuer).origin` (`packages/sdk/src/server/oidc.ts:167-177`), and
`origin` **discards the issuer's `/api` path** → the browser is sent to
`https://wallow.dev/connect/authorize`, which the documented path routing sends to
wallow-web's catch-all, not the API. Login dead-ends.

Cheapest fix: set `OIDC_METADATA_URL=http://wallow-api:8080/api/.well-known/openid-configuration`
(the `/api` PathBase then flows into every advertised endpoint; backchannel token endpoint
stays container-reachable). Robust fix: make the SDK's endpoint pinning preserve the
issuer's path, not just its origin. The subdomain topology is unaffected.

### P2 — CRITICAL: wallow-auth cannot be served under `/auth`

No `base` in `apps/wallow-auth/vite.config.ts` (verified) and the browser SDK uses the
bare origin (`apps/wallow-auth/src/start.ts:55-58`). Under `https://wallow.dev/auth/...`,
asset and `/v1/*` fetches resolve against the root and land on wallow-web. The production
compose header itself admits the app "serves at root". Path-based hosting of the login UI
is broken before P1 even matters; subdomain hosting avoids it.

### P3 — HIGH: none of this is CI-covered

CI's e2e job runs only the wallow-auth suite (`scripts/e2e.sh`). The wallow-web cross-app
journey needs an external stack and is not wired into CI; the path-based production
topology is exercised nowhere; no authenticated mutation is tested anywhere (see §1).

---

## 3. Medium findings (robustness / dev-prod parity / best practice)

- **M1 — Module-global CSRF token store** (`packages/sdk/src/csrf.ts:40`): module scope is
  shared in the SSR bundle. Latent today (only browser code sets it), but any future
  server-side caller leaks one user's token into another's render. `create-sdk.ts` was
  built to kill exactly this class of singleton; the CSRF store didn't get the memo.
- **M2 — `COOKIE_SECURE` inconsistent across environments.** Aspire sets `false` with an
  explicit WebKit rationale; `docker-compose.test.yml` leaves it unset → `Secure` +
  `__Host-` cookies over `http://localhost:5053`, passing only via Chromium's
  localhost-trustworthy exemption. WebKit or any non-localhost plain-HTTP host silently
  drops every cookie → login 400s at the callback with no visible error.
- **M3 — Dev never exercises the production session store.** Aspire's
  `WithReference(valkey, "Redis")` injects `ConnectionStrings__Redis`, but the app reads
  `REDIS_URL` (`apps/wallow-web/src/lib/bff.ts:66`). `pnpm backend` silently runs
  stateless sealed-cookie sessions while e2e/prod run Valkey-backed sessions (real logout
  revocation, refresh locking). Divergent code paths between dev and prod.
- **M4 — `COOKIE_PASSWORD` length unvalidated** (`config.ts:137` checks presence only).
  iron-webcrypto needs ≥32 chars; a short one boots fine and then 500s mid-login-callback
  on the first `seal`, violating the config loader's fail-at-boot contract.
- **M5 — Production binds container ports on 0.0.0.0 while the API trusts any proxy.**
  `ports: "8080:8080"`-style mappings (prod compose) + cleared
  `KnownProxies`/`KnownIPNetworks` (`api/src/Wallow.Api/Program.cs:376-385`): anyone who
  can reach the host directly bypasses TLS and can spoof `X-Forwarded-*`. Bind to
  loopback/a proxy network (test compose already does `127.0.0.1:`).
- **M6 — The `/api` BFF proxy drops the client IP.** It forwards only
  `content-type`/`accept`/`x-request-id` (`proxy.ts:553`). wallow-auth's passthrough was
  explicitly fixed to stamp `x-wallow-client-ip` (Wallow-tt5j) because the API otherwise
  rate-limits everything as one client; every authenticated wallow-web call still has that
  problem.
- **M7 — SSR baseUrl scheme mismatch behind the TLS-terminating proxy.**
  `apps/wallow-web/src/start.ts:64` derives the origin from `request.url`; without
  trust-proxy handling Nitro sees `http://wallow.dev` while the client uses `https://…`.
  Generated query keys embed `baseUrl`, so SSR-hydrated queries silently refetch on mount
  in production. Perf/correctness, not security.
- **M8 — `readCsrfCookie` suffix-matches any `-csrf` cookie, first match wins**
  (`auth.ts:124-134`). Cannot bypass the synchronizer check, but stale/foreign localhost
  cookies (e.g. e2e stack vs Aspire, different names `__Host-wallow_bff-csrf` vs
  `wallow_bff-csrf`) can cause nuisance 403s on logout.
- **M9 — Undocumented dependency on the external proxy sending
  `X-Forwarded-Proto: https`** (interacts with M5/M7); the repo ships no ingress, so this
  contract lives nowhere.
- **M10 — Issuer/auth-origin coupling is fragile and undocumented.** Dev issuer =
  auth origin (:3002); test = API (:5050); prod = `wallow.dev/api` with
  `Authentication__CookieDomain=wallow.dev` papering over the difference — and widening
  the identity cookie to every subdomain. A fork changing one origin breaks login in a way
  local testing can't reproduce. Related: if a real ingress forwards `/api` as PathBase,
  OpenIddict's `returnUrl` becomes `/api/connect/authorize?…`, which wallow-auth's
  passthrough allowlist (`/v1`, `/connect`, `/.well-known`) does not proxy → 404 mid-login.

## 4. Low findings

- **L1** — Anonymous `POST /bff/logout` answers 403 `CSRF_INVALID` instead of an
  idempotent success; a signed-out user clicking logout sees an error
  (`handlers.ts:530-537`, `auth.ts:172-175`). Also: a session minted without `csrfToken`
  (older build) can never logout via the UI — permanent 403 until cookies are cleared.
- **L2** — `/bff/user` (identity claims + CSRF token) sets no `Cache-Control: no-store`.
- **L3** — No key-ring rotation for `COOKIE_PASSWORD`; rotating it mass-401s every session.
- **L4** — Prod `REDIS_URL: redis://:${VALKEY_PASSWORD}@valkey:6379` breaks if the
  password contains `/ + =` — exactly what the recommended `openssl rand -base64 32`
  can emit. Use hex or URL-encode.
- **L5** — `CookieSessionStore.withRefreshLock` is a no-op: multi-instance cookie-store
  deployments can double-spend one-time refresh tokens (sporadic 401 after refresh).
  Documented tradeoff; Valkey store has a real NX lock.
- **L6** — The whole callback flow depends on `response_mode` staying a top-level GET
  redirect: `SameSite=Lax` tx cookies would not ride a cross-site `form_post`. Nothing
  pins or documents this.

## 5. What is done right (best-practice checklist)

The architecture itself is sound and largely exemplary BFF practice:

- Browser never holds a token; token set lives server-side; silent refresh under a store
  lock (Valkey NX) inside the expiry skew.
- Session cookies: `HttpOnly`, `SameSite=Lax`, `__Host-` prefix when secure, `Path=/`,
  **no `Domain`** (host-only, sibling subdomains can't clobber), chunked with stale-chunk
  cleanup.
- CSRF: synchronizer token bound to the session + constant-time compare with
  length pre-check; validated on all state-changing methods before refresh/forward;
  GET/HEAD exempt (correct); the API itself is bearer-only so CSRF correctly lives in the
  BFF layer only.
- OIDC: auth-code + PKCE S256 + `state` + `nonce` + sealed single-use tx cookie
  (Max-Age 600); token exchange pinned to `config.redirectUri`, never the incoming request
  URL (correct behind TLS termination); `returnTo`/`returnUrl` sanitized on both sides;
  60-second single-use signed exchange ticket (Redis NX) bridging login UI → cookie.
- Logout: POST-only, CSRF-gated, destroys the server-side session **before** clearing
  cookies.
- Ops: `BFF_COOKIE_PASSWORD` hard-required at compose parse (`:?`) — no random-per-restart
  secret; DataProtection keys and OpenIddict certs persist; Valkey AOF means restarts
  don't log users out; proxy path allowlists prevent the bearer-attaching proxy becoming
  an open relay; `SameSite=Lax` is the right choice at every hop (verified per-hop,
  including the cross-site top-level GET callback).

## 6. Recommended order of work

1. **Fix the CSRF regression** (§1): cookie fallback in the interceptor + re-learn from
   `/bff/user` on the app path; add one authenticated-mutation E2E. Unblocks dev today.
2. **Decide the production topology.** If path-based stays the default: fix P1 (metadata
   URL with `/api`, or path-preserving pinning) and P2 (base-path support in wallow-auth) —
   or flip the documented default to subdomains and demote path-based to "unsupported".
3. **Close the parity gaps**: M2 (`COOKIE_SECURE=false` in the test compose), M3
   (`REDIS_URL` under Aspire), then wire the cross-app journey into CI (P3).
4. Sweep the mediums (M1, M4–M10), then lows opportunistically.

Suggested tracking: file one bead per numbered finding; §1 + P1 + P2 are release-blocking.
