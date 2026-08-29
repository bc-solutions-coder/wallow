# minimal-app — the external own-domain relying party (PROTOTYPE, #127)

> **Prototype branch.** This is `apps/minimal-app` rebuilt as the thing bcordes.dev will be:
> a site on its own origin that signs users in through Wallow and depends on **nothing from
> this repo except the published `@bc-solutions-coder/sdk`**. Read it as a proposal to react
> to, not as finished code.

## What it demonstrates

| Concern                                  | Where                                                                 | How                                                                                                                                  |
| ---------------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Install the SDK the way an outsider does | `package.json`                                                        | the only `@bc-solutions-coder/*` dependency is `sdk`; every other workspace package is `private: true` and cannot be installed       |
| Register through the org-owner journey   | `.env.example`                                                        | is the one-time reveal's env block verbatim, plus two deployment lines (`REDIS_URL`, `COOKIE_SECURE`)                                |
| Mount the BFF                            | `src/routes/bff/$.ts`, `src/routes/api/$.ts`, `src/lib/bff.server.ts` | two catch-all server routes delegating to `createWallowBffServer`; Valkey session store when `REDIS_URL` is set                      |
| Sign in / out, call the API as the user  | `src/routes/index.tsx`                                                | `loginRedirect`, `logout`, `getCurrentUser`, one generated operation through `/api`                                                  |
| Back-channel logout                      | nothing                                                               | zero app code: once #115 lands, `handleBff` serves `/bff/backchannel-logout`; the RP registers the container-reachable URL           |
| M2M for anonymous actions                | `src/routes/contact.ts`, `src/lib/service-client.server.ts`           | `POST /contact` written against the decided `createServiceClient` (`@bc-solutions-coder/sdk/server/service`, stubbed on this branch) |
| Run on its own origin in e2e             | `Dockerfile`, `docker/docker-compose.test.yml`                        | `bff-example` builds this app instead of wearing wallow-web's image                                                                  |

## Proposed split: example vs quickstart

**The example owns (code someone copies):** the two catch-all routes, `bff.server.ts`,
the per-request `createWallowSdk` middleware, the service-client wrapper, the home page's
login/logout/API buttons, `.env.example`, `Dockerfile`.

**The quickstart owns (prose someone reads once):** `.npmrc` + `NODE_AUTH_TOKEN` for GitHub
Packages; the registration journey and what each reveal line means; `OIDC_METADATA_URL` and
when it differs from `OIDC_ISSUER`; why `REDIS_URL` is mandatory in production (multi-replica

- back-channel logout); which redirect/logout URIs to register (`/bff/callback`, post-logout,
  front-channel, back-channel with the server-reachable host); `COOKIE_SECURE=false` is local
  only; the CSRF double-submit rule for mutations; that the service account is a separate
  registration with its own reveal.

## Gaps this prototype surfaced (SDK / platform)

1. **node-redis bridging is boilerplate.** `bff.server.ts` is a paste of wallow-web's — ~40
   lines every consumer copies. `createWallowBffServer` should accept a node-redis client
   directly (or `REDIS_URL` alone and connect itself).
2. **Client IP stamping needs a private package.** The first-party apps resolve the request
   origin and stamp `CLIENT_IP_HEADER` via `@bc-solutions-coder/env`; an external RP cannot,
   so the API rate-limits all of an RP's users as one address. Either publish the helpers or
   fold them into `sdk/server`.
3. **`createServiceClient` does not exist** (#121 decided it); `packages/sdk/src/server/service.ts`
   on this branch is a throwing stub with the decided signature.
4. **Seed cannot express a service account**, so the M2M leg cannot run in e2e until #121's
   seed shape exists.
5. **Doc fallout:** minimal-app stops being the "wire the shared packages" bootstrap skeleton
   that `apps/CLAUDE.md`, `docs/development/frontend-setup.md` and the README describe.
   Either that role moves to a doc, or a fork keeps a separate skeleton.

## Run it

```bash
cp .env.example .env   # paste your reveal block
pnpm --filter @bc-solutions-coder/example-minimal-app dev   # http://localhost:3010
```

6. **Lint facade rule.** The workspace bans importing `@tanstack/react-query` directly in favour
   of the private `@bc-solutions-coder/query` facade. An external RP has no facade, so this app
   turns `no-restricted-imports` off — a tell that the facade's "shared client defaults" are
   another thing the quickstart must spell out (or the SDK's `./query` entry must absorb).
