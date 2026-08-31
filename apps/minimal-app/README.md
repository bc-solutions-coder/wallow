# example-minimal-app

The runnable **external relying-party example**: a TanStack Start app on its own
origin that consumes Wallow the way a fork's customer would — through the
published `@bc-solutions-coder/sdk` alone, over OIDC and the BFF pattern, with
no other workspace package at runtime.

The full walk-through this app is the runnable form of is the quickstart:
[`docs/integrations/typescript-sdk.md`](../../docs/integrations/typescript-sdk.md).
The pattern it demonstrates is
[`docs/integrations/bff-pattern.md`](../../docs/integrations/bff-pattern.md).

## What it shows

- **The whole BFF in two server routes** (`src/routes/bff/$.ts`,
  `src/routes/api/$.ts`), each delegating to the memoised
  `createWallowBffServer()` preset in `src/lib/bff.server.ts`. `/bff/*` is the
  OIDC tunnel (login, callback, user, logout, front- and back-channel logout
  receivers); `/api/*` is the proxy that attaches the session's bearer token
  server-side, so the browser never holds a token.
- **Server-side sessions via `REDIS_URL`** — set it and the preset stores
  sessions in Valkey/Redis (the `redis` dependency satisfies the SDK's optional
  peer); unset, it falls back to sealed-cookie sessions, which nothing can
  revoke. Production wants the store.
- **One page** (`src/routes/index.tsx`): sign in with a `returnTo`, session
  status from `getCurrentUser`, a typed API call (`usersGetCurrentUser`) through
  the proxy, and a CSRF-gated `logout()` — all through `createWallowSdk`
  instances minted per request in `src/start.ts` and per browser in
  `src/router.tsx`.
- **Anonymous server-to-server calls** (`src/routes/contact.ts` →
  `src/lib/service-client.server.ts`): `POST /contact` reaches the platform as
  the deployment's registered service account via `createServiceClient()`, with
  no user signed in.

The `data-testid="bff-*"` hooks on the page are driven by the three-origin
acceptance suite, `apps/wallow-web/e2e-cross-app/external-origin-login.spec.ts`.

## Run it against the local stack

All commands from the repo root. Node 24 (`.nvmrc`), pnpm via `packageManager`.

1. **Start the backend** — `pnpm backend` (Aspire; the API — which is also the
   OIDC issuer — on `http://localhost:5001` per the README's Local Services
   table).
2. **Register a client**: in wallow-web, under your organization's Clients,
   register a client with redirect URI `http://localhost:3010/bff/callback` and
   post-logout redirect URI `http://localhost:3010/`. Copy the one-time reveal's
   env block.
3. **Configure**: `cp apps/minimal-app/.env.example apps/minimal-app/.env`,
   paste the reveal block over the placeholder lines, and uncomment
   `COOKIE_SECURE=false` (plain-http localhost only). Optionally register a
   service account too and fill the `OIDC_SERVICE_*` trio to enable
   `POST /contact`.
4. **Boot it**:

   ```bash
   pnpm install
   pnpm --filter @bc-solutions-coder/example-minimal-app dev   # http://localhost:3010
   ```

   `vite dev` does not read `.env` files by itself in this setup — export the
   variables into the environment (`set -a; source apps/minimal-app/.env; set +a`)
   or use the Docker image below, which takes them as container env.

There is no `routes:generate` step: the `tanstackStart()` Vite plugin
regenerates `src/routeTree.gen.ts` as a side effect of `vite dev`/`vite build`.

### Production shape

```bash
pnpm --filter @bc-solutions-coder/example-minimal-app build   # .output/ (Nitro)
pnpm --filter @bc-solutions-coder/example-minimal-app start   # node .output/server/index.mjs on :3010
```

Or the container the E2E harness runs (build context is the repo root):

```bash
docker build -f apps/minimal-app/Dockerfile -t wallow-bff-example .
```

`GET /health` answers through the preset, so a misconfigured environment turns
the container unhealthy instead of reporting a healthy app that cannot serve a
login.

## What it deliberately does NOT use

`@bc-solutions-coder/ui`, `styles`, `query`, `auth`, `env`, `testing` — the
private workspace packages an external consumer cannot install. It styles
itself (`src/styles.css`), renders raw elements, and carries no unit specs: its
behavior is pinned end-to-end by the three-origin suite, and its gates are
build, typecheck, lint and the Docker image build.

The one workspace devDependency is `@bc-solutions-coder/config`, used only by
`vite.config.ts` — build-time convenience, never imported by app code. In-repo
the `@bc-solutions-coder/sdk` dependency resolves via `workspace:*`; an external
fork installs the published package from GitHub Packages instead (the quickstart
covers the `.npmrc` and read-token setup).
