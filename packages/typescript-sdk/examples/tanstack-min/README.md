# tanstack-min — Wallow BFF reference example

A minimal, drop-in example that wires the `@wallow/sdk` **Backend-for-Frontend
(BFF)** tunnel into a TanStack Start-style server. It shows the full
same-origin OIDC flow: browser login → authenticated `/api` call with silent
token refresh → logout, with tokens held server-side in an httpOnly sealed
cookie (never exposed to JavaScript).

> This example is not published. It is exercised by the .NET E2E harness
> (`tests/Wallow.E2E.Tests/Flows/BffFlowTests.cs`) via
> `docker/docker-compose.test.yml`, and serves as copy-paste reference wiring.

## Layout

| Path | Role |
| --- | --- |
| `server.ts` | Host that mounts `createBffHandlers` at `/bff/*`, `createApiProxy` at `/api/**`, serves `public/`, and listens on `PORT` (default `3000`). |
| `src/app.ts` | Browser entry using `configureWallowClient()`, `login()`, `logout()`, `getUser()`. |
| `public/index.html` | The DOM the E2E test drives (`data-testid` selectors). |
| `Dockerfile` | Containerizes the example for the E2E stack. |

## Endpoints

| Route | Handler |
| --- | --- |
| `GET /health` | Liveness for the E2E `DockerComposeFixture`. |
| `/bff/login` | Start OIDC login (PKCE), redirect to Wallow.Auth. |
| `/bff/callback` | OIDC redirect URI; exchanges the code and seals the session. |
| `GET /bff/user` | Current user (`200` + claims, or `401` when anonymous). |
| `/bff/logout` | Clear the session and redirect through end-session. |
| `/api/**` | Reverse proxy to the Wallow API with a `Bearer` token + silent refresh. |

## Environment variables

The SDK reads config via `loadBffConfigFromEnv()`. These are the **actual keys**
consumed by `@wallow/sdk/server` (`src/server/config.ts`):

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `OIDC_ISSUER` | yes | — | OIDC issuer base URL (e.g. `http://localhost:5001`). |
| `OIDC_CLIENT_ID` | yes | — | Confidential client id — `bcordes-bff` for the seeded dev client. |
| `OIDC_CLIENT_SECRET` | yes | — | Confidential client secret — `bcordes-bff-secret` in dev. |
| `OIDC_REDIRECT_URI` | yes | — | Callback URL — `http://localhost:3000/bff/callback`. |
| `OIDC_POST_LOGOUT_REDIRECT_URI` | yes | — | Post-logout URL — `http://localhost:3000/`. |
| `BFF_API_BASE_URL` | yes | — | Downstream API base URL — `http://localhost:5001`. |
| `COOKIE_PASSWORD` | yes | — | Seal/unseal password for the session cookie (>= 32 chars). |
| `OIDC_SCOPES` | no | `openid profile email offline_access` | Space-separated scopes. |
| `COOKIE_NAME` | no | `wallow_bff` | Sealed session cookie name. |
| `OIDC_METADATA_URL` | no | `${OIDC_ISSUER}/.well-known/openid-configuration` | Server-reachable discovery URL. Set this when the browser and server reach the OP under different hostnames (reverse proxy, container network, split-horizon DNS). The server fetches discovery here and uses its `token_endpoint` for the backchannel, while the browser-facing authorize/end-session URLs are pinned to the public `OIDC_ISSUER` origin. |
| `PORT` | no | `3000` | Listen port. |

> **Split-horizon note (container networks).** In the E2E stack the browser
> reaches the OP at `http://localhost:5050` while the example container reaches
> it at `http://host.docker.internal:5050`. `OIDC_ISSUER` stays on the
> browser-facing origin and `OIDC_METADATA_URL` points at the container-reachable
> one — the same pattern `Wallow.Web` uses with `Authority` + `MetadataAddress`.

> Note: an earlier design draft referenced `BFF_ISSUER`, `BFF_CLIENT_ID`,
> `BFF_REDIRECT_URI`, `BFF_POST_LOGOUT_REDIRECT_URI`, and `BFF_COOKIE_PASSWORD`.
> The shipped SDK uses the `OIDC_*` / `COOKIE_*` names above — those are
> authoritative.

## Run locally

```bash
# from packages/typescript-sdk
npm install && npm run build   # build @wallow/sdk first

# from packages/typescript-sdk/examples/tanstack-min
npm install
export OIDC_ISSUER=http://localhost:5001
export OIDC_CLIENT_ID=bcordes-bff
export OIDC_CLIENT_SECRET=bcordes-bff-secret
export OIDC_REDIRECT_URI=http://localhost:3000/bff/callback
export OIDC_POST_LOGOUT_REDIRECT_URI=http://localhost:3000/
export BFF_API_BASE_URL=http://localhost:5001
export COOKIE_PASSWORD=dev-cookie-password-change-me-32chars
npm run build && npm start   # http://localhost:3000
```

## E2E

Run the full flow through the containerized stack:

```bash
./scripts/run-e2e.sh
```
