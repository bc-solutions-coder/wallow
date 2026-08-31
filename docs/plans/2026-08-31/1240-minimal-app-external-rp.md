**status: completed**

# minimal-app as the external RP example, three-origin acceptance, and quickstart (#151)

Parent spec #131; all blockers (#136, #138, #142, #146, #147, #148, #149) shipped. This is
the acceptance gate for the whole spec: `apps/minimal-app` becomes the runnable external
own-domain relying party, the e2e `bff-example` service builds it, the three-origin suite
runs the full journey, and the quickstart is rewritten around the registration reveal.

The accepted prototype (branch `prototype/minimal-app-external-rp`, 14e1d3b3) is the
starting material, **updated to the shipped SDK**: `createWallowBffServer()` now reads
`REDIS_URL` itself through the optional `redis` peer (no hand-rolled node-redis adapter),
`createServiceClient()` is real (`@bc-solutions-coder/sdk/server/service`, five-var env
contract), and the back-channel logout handler is routed at `/bff/backchannel-logout` with
nothing for the consumer to write.

## 1. `apps/minimal-app` rework

Replace the passthrough-bootstrap topology with the BFF RP topology; the app depends on the
published `@bc-solutions-coder/sdk` alone among workspace packages (plus the build-time-only
`config` devDependency).

- **Routes** (TanStack Start, mirroring `apps/wallow-web/src/app/routes/{bff,api}/$.ts`):
  `src/routes/bff/$.ts` and `src/routes/api/$.ts` — one `ANY` server handler each, dynamic
  `import()` of `src/lib/bff.server.ts` inside the handler. `src/routes/contact.ts` — POST
  only, dynamic import of `src/lib/service-client.server.ts`. Keep `src/routes/health.ts`
  (compose healthcheck) delegating to `handleHealth()`.
- **`src/lib/bff.server.ts`**: thin memoised `createWallowBffServer()` (build on first use,
  never cache the failure) exporting `handleBffRequest` / `handleApiRequest` /
  `handleHealthRequest`. No manual Redis adapter — the SDK's `REDIS_URL` path is exactly
  what the example demonstrates; the app lists `redis` as a dependency so the lazy import
  resolves. `handleApi` gets the srvx request passed through unchanged.
- **`src/lib/service-client.server.ts`**: memoised `createServiceClient()`; 503 when
  `OIDC_SERVICE_CLIENT_ID` is unset; real `inquiriesSubmit({ client: service.client, body })`
  (the generated op — POST `/v1/inquiries`, `SubmitInquiryRequest` body), mapping the
  contact form fields onto name/email/phone/company/projectType/budgetRange/timeline/message.
- **Home page `src/routes/index.tsx`**: test ids `bff-user-status`, `bff-user-email`,
  `bff-login` (anchor from `loginRedirect("/").href`), `bff-logout` (SDK `logout()`),
  `bff-call-api` → `bff-api-result` (typed `usersGetCurrentUser`), `contact-send` →
  `contact-result` (fetch POST `/contact`). `getCurrentUser` drives the status;
  `isWallowError` renders failures. `__root.tsx` keeps `data-app-ready="true"` and
  `charSet: "utf-8"` (prototype's `"utf8"` trips `unicorn/text-encoding-identifier-case`).
- **`src/router.tsx` / `src/start.ts`**: per-request `createWallowSdk({ baseUrl:
  <origin>/api, cookieHeader })` following wallow-web's current shape (post-#149 —
  whatever origin/client-IP helpers wallow-web uses today, minimal-app copies).
- **Deps**: `@bc-solutions-coder/sdk` `workspace:*`, `@tanstack/react-query` +
  react-router/start via catalog, react/react-dom, `redis`. Drop `ui`, `testing`, `query`,
  `env`, `styles` — an external consumer cannot install the private packages. Standalone
  `src/styles.css`. Direct `@tanstack/react-query` import needs a scoped
  `.oxlintrc.json` override (read `packages/lint/CLAUDE.md` first; targeted rule override,
  not a blanket `"no-restricted-imports": "off"`).
- **No unit tests.** The bootstrap-skeleton specs (brand-assets, browser-deps, HelloCard)
  exist to pin the six-package floor this app no longer represents; its behavior is pinned
  end-to-end by the three-origin suite. Remove specs, vitest configs, and the `test`
  script (turbo skips absent scripts).
- **`Dockerfile` + `.dockerignore`**: modeled on `apps/wallow-web/Dockerfile`
  (`--platform=$BUILDPLATFORM` build stage, root manifests + only the package.json/source
  pairs the app needs — `packages/sdk`, `packages/config` — frozen-lockfile filtered
  install, runtime stage copies `.output` only, `EXPOSE 3010` / `ENV PORT=3010`).
- **`.env.example`** IS the reveal's application env block (`OIDC_ISSUER`,
  `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`,
  `OIDC_POST_LOGOUT_REDIRECT_URI`, `OIDC_SCOPES`, `BFF_API_BASE_URL`, `COOKIE_PASSWORD`)
  plus the commented service-account trio, `REDIS_URL`, and commented
  `COOKIE_SECURE=false` (local-only).
- **README** rewritten: what the example shows, run-against-local-stack instructions,
  pointer to the quickstart.

## 2. E2E stack wiring

- **`docker/docker-compose.test.yml` `bff-example`**: build dockerfile →
  `apps/minimal-app/Dockerfile`; port `127.0.0.1:${E2E_BFF_PORT:-3003}:3010` (adds the
  `127.0.0.1` prefix every other app service uses); healthcheck →
  `http://localhost:3010/health`; add service-account env
  (`OIDC_SERVICE_CLIENT_ID=sa-wallow-nightly-sync`,
  `OIDC_SERVICE_CLIENT_SECRET=nightly-sync-secret`, `OIDC_SERVICE_SCOPES=inquiries.write`).
- **Seeder override**: `Clients__1__BackchannelLogoutUri:
  "http://bff-example:3010/bff/backchannel-logout"` — the container-reachable URL; the API
  runs in Development in this stack, where
  `Identity:BackchannelLogout:AllowPrivateNetworkHosts` is already true, so delivery is
  not gated. `api/seed.json` itself gains no back-channel URI (no bff-example exists in
  the Aspire dev stack).
- **`api/seed.json`**: `sa-wallow-nightly-sync` scopes gain `inquiries.write` (additive —
  `service-account.spec.ts` keeps asserting `organizations.read`). `ScopePermissionMapper`
  maps the scope straight to `InquiriesWrite`, which is what `POST /v1/inquiries` requires.
- **CI**: `.github/workflows/ci.yml` builds/saves `wallow-bff-example` beside the other
  app images so the e2e job with `E2E_SKIP_IMAGE_BUILD=1` doesn't rebuild it uncached.
- Any new compose `${VAR}` gets a paired `docker/.env.example` entry (`pnpm lint:env`).

## 3. Three-origin suite

`apps/wallow-web/e2e-cross-app/external-origin-login.spec.ts` extends to the full
acceptance journey (read `apps/wallow-web/e2e/CLAUDE.md` first; `data-testid` only). The
suite file becomes `test.describe.serial` — later stages (suspension) poison earlier ones,
so ordering is load-bearing.

1. **Anonymous contact** (before any login; plain `request` context, no cookies): POST
   `{BFF}/contact` → success payload; proves the service-account path.
2. **Sign-in at the external origin**, `returnTo=/`: branded consent (display name
   "BFF Example", "by Wallow", fork footer), approve, land on `/`, `bff-user-status`
   authenticated + email, typed API call via `bff-call-api` → `bff-api-result`.
3. **Back-channel logout**: with the same browser (same OP session), sign in at wallow-web,
   block `**/bff/frontchannel-logout*` in the bff-example origin via `page.route` (the
   front-channel iframe is disabled — only back-channel can revoke), log out at the
   wallow-web origin, then `{BFF}/bff/user` → 401.
4. **Suspension**: as the signed-in admin at wallow-web, resolve org id from `/bff/user`
   and the client list (`/api/v1/identity/organizations/{org}/clients`), POST the
   org-surface suspend for `bff-example-client` with the CSRF token from `/bff/user`; a
   fresh login attempt at `{BFF}/bff/login` shows the fork error screen with no redirect
   back to the RP.

`service-account.spec.ts` and `login-journey.spec.ts` stay as they are.

## 4. Docs

- **`docs/integrations/typescript-sdk.md`** quickstart rewritten around the reveal:
  `.npmrc` + read token (classic PAT for humans, any repo's `GITHUB_TOKEN` in CI, Docker
  via build secret — never `ARG`/`ENV`); paste the reveal block; two routes +
  `createServiceClient()`; `OIDC_METADATA_URL` vs `OIDC_ISSUER`; Valkey mandatory in
  production (replicas + back-channel; AUTH + TLS); which URIs to register
  (`/bff/callback`, post-logout, front-channel, server-reachable back-channel);
  `COOKIE_SECURE=false` local-only; CSRF double-submit; `COOKIE_PASSWORD` ≥ 32 chars +
  `COOKIE_PASSWORDS` rotation ("rotate at deploy time"); service account = separate
  registration with its own reveal; `SESSION_TTL_SECONDS` ≤ refresh lifetime. Point at
  `apps/minimal-app` as the runnable form.
- **`docs/integrations/bff-pattern.md`**: correct the "consent repeats without
  offline_access" row.
- **Fallout**: `docs/development/frontend-setup.md` stops calling minimal-app the
  bootstrap skeleton (the flat-shape bootstrap steps stand on their own);
  `README.md` repo-layout row; `apps/CLAUDE.md` (passthrough → BFF example, dep floor);
  `docs/getting-started/fork-guide.md` mention checked for accuracy.

## Seams under test (pre-agreed)

1. **The extended `external-origin-login.spec.ts`** is the acceptance seam — the four
   serial stages above, at the browser/public-HTTP boundary only.
2. **Existing `service-account.spec.ts`** pins that the seed change is additive.
3. **minimal-app carries no unit specs** — it is example code pinned by the e2e journey;
   its gates are build/typecheck/lint and the Docker image build.
4. Backend seed change is data-only; existing seeder suites cover the binding.

Gates: `pnpm check`, `docker build` of `apps/minimal-app/Dockerfile`,
`./scripts/e2e.sh`, backend suites untouched by code (seed-only change — a targeted
`./scripts/run-tests.sh seeder` sanity run).

## Out of scope

- SDK changes (the shipped #148/#149 surface is consumed as-is; prototype-era SDK stubs
  are superseded).
- A separate demo page in wallow-web (`/bff-demo` stays; nothing here removes it).
- Aspire dev-stack hosting of minimal-app.
