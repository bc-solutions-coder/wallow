# Connect an external app to Wallow

Use this guide when your app lives in another repository and runs on its own domain.
You need a running Wallow deployment and a Node 24 or newer app server. Your app owns
its UI, server, container, and session store. Wallow supplies identity and platform APIs.

The browser calls your app's BFF on the app's own origin. The BFF keeps tokens on the
server and calls Wallow with those tokens. Login redirects the browser to Wallow and
then back to your app. You do not share cookies between the two domains.

## 1. Identify the Wallow deployment

Ask the platform operator for the public OIDC issuer and the API base URL. Use the
issuer advertised by the deployment's discovery document, including any path prefix.
Do not infer the issuer from the login UI hostname: the auth UI and the OIDC endpoints
can have different URLs. For example, a deployment can serve its API and issuer under
`https://platform.example.com/api` and its login UI under `/auth`.

Confirm that setup is complete and that you can sign in to Wallow's management UI.
For a local Wallow checkout, follow the [developer guide](../getting-started/developer-guide.md)
to start the API and frontends first.

Keep these addresses separate:

- `OIDC_ISSUER`: the public issuer used for browser redirects and token identity.
- `BFF_API_BASE_URL`: the Wallow API base reachable from your app server. SDK requests
  such as `/v1/identity/users/me` are appended to this base.
- Your app origin, such as `https://app.example.com`: the browser's destination after login.

If the BFF needs internal discovery, set `OIDC_METADATA_URL` to the full discovery
URL, including `/.well-known/openid-configuration`. This changes where discovery is
fetched. Token, userinfo, and JWKS URLs advertised there must also be reachable from
the BFF. The SDK pins authorization and end-session redirects to the public issuer.
It does not rewrite every backend URL to `BFF_API_BASE_URL`.

## 2. Create an organization and register an application

Sign in to Wallow, create an organization, and open its clients screen. Register an
**application**, which is a confidential OIDC client bound to that organization.
Use your app's name and register these URLs on your app's domain:

| Field | Example |
| --- | --- |
| Redirect URI | `https://app.example.com/bff/callback` |
| Post-logout redirect URI | `https://app.example.com/` |
| Back-channel logout URI | `https://app.example.com/bff/backchannel-logout` |

Supply the post-logout URI even though the registration form permits leaving it blank;
the SDK requires it. Organization registration exposes back-channel logout, not a
front-channel logout field.

Use exact URLs, including paths and trailing slashes. The identity server must be able
to reach the back-channel logout URL. Use a separate registration for local development,
with your local origin and port.

Select the API scopes the app needs. Include `openid profile email offline_access`
for user sign-in and refresh, plus the allowed API scopes for your operations.
For the example's current-user request, include `users.read`.

Copy the one-time credential reveal and its environment block before leaving the page.
The organization binding comes from registration; your app does not choose another
organization by changing an environment variable. Users must satisfy that organization's
membership and enrollment policy. New organizations default to invite-only. Invite
your users or configure the intended enrollment policy before testing with a new user.
Application credentials do not grant a user membership.

## 3. Install a matching SDK

Use the [SDK installation instructions](typescript-sdk.md#installation) to configure
GitHub Packages access. Install `@bc-solutions-coder/sdk`,
`@bc-solutions-coder/api-errors`, and the SDK's supported Redis peer, `redis@^4.7.0`.
Pin the Wallow package versions and commit your app's lockfile.

Before choosing versions, check the selected package's release and successful
`package-publish.yml` run. A merge to Wallow's `main` branch, a platform Docker release,
and a successful SDK publication are separate events. The version in a checkout's
`package.json` does not prove that its current source has been published.

The SDK depends on `api-errors`; that dependency must be available too. Maintainers
must resolve generated-contract drift, merge the relevant release changes, and verify
package publication before telling consumers to install a new version. See
[package release stages](../operations/versioning.md#a-published-package-releases-in-two-stages).

### Use a local Wallow checkout before publication

To validate the source in a sibling folder, build and pack the two public packages.
Run these commands from the Wallow root:

```bash
pnpm install --frozen-lockfile
pnpm exec turbo run build --filter=@bc-solutions-coder/sdk...
mkdir -p /tmp/wallow-packages
pnpm --dir packages/api-errors pack --pack-destination /tmp/wallow-packages
pnpm --dir packages/sdk pack --pack-destination /tmp/wallow-packages
```

In your app repository, install the two emitted `.tgz` files by their exact paths,
along with `redis@^4.7.0`, using your package manager. Install both archives in the
same operation. Use `pnpm pack`, which rewrites Wallow's workspace dependency protocols
and applies its published exports. Import the public package names as usual.

This validates a local build, not a published release. Replace temporary archive paths
with pinned registry versions before handing your app's build to CI, or supply the
archives as explicit build inputs. Do not import `packages/sdk/src` from the sibling
checkout or copy its `workspace:` dependencies into your app.

## 4. Mount the BFF and connect your UI

Follow [mounting the BFF](typescript-sdk.md#server-setup-mounting-the-bff) to create a
server-only, lazily initialized `createWallowBffServer()` instance and mount both
`/bff/*` and `/api/*`. Preserve the incoming request, including the runtime's peer
address, when passing it to the SDK. Keep server imports out of your browser bundle.

Use `createWallowSdk({ baseUrl: "/api" })` for browser API calls. Use
`loginRedirect("/").href` for full-document sign-in links, `getCurrentUser({ client: sdk.client })` to read
the session, and
`logout()` for sign-out. Pass the instance's `client` to generated API operations.
For SSR, create a separate SDK instance for every incoming request and forward that
request's cookies. Never share an authenticated SDK instance across users.

The reference implementation is `apps/minimal-app` in the Wallow checkout:

| File under `apps/minimal-app/` | What to adapt |
| --- | --- |
| `src/lib/bff.server.ts` | BFF instance and health handler |
| `src/routes/bff/$.ts` and `src/routes/api/$.ts` | TanStack Start route adapters |
| `src/start.ts` and `src/router.tsx` | Request-scoped SSR and browser SDK instances |
| `src/routes/index.tsx` | Sign-in, session, typed request, and sign-out |
| `src/lib/service-client.server.ts` | Optional service-account request |

Use your app's framework configuration and styling. The example's Vite config imports
the private `@bc-solutions-coder/config` package; it is not an external-app starter you
can copy unchanged. Wallow's `auth`, `ui`, `styles`, `query`, `env`, and `testing`
workspace packages are also private. External apps use the public SDK and API-errors
packages with their own framework and UI dependencies.

Keep CSRF enabled. The SDK handles the token for its browser-to-BFF mutations and
logout. Custom server routes and server functions still need their own authorization,
input validation, and CSRF protection where they use browser cookies. A service-account
helper does not secure the public route that calls it.

## 5. Configure the container at runtime

Start with the registration reveal. For an app on `https://app.example.com`, the runtime
environment has this shape. Replace placeholders with your deployment's actual values:

```dotenv
OIDC_ISSUER=https://platform.example.com/api
OIDC_CLIENT_ID=<application client id>
OIDC_CLIENT_SECRET=<application client secret>
OIDC_REDIRECT_URI=https://app.example.com/bff/callback
OIDC_POST_LOGOUT_REDIRECT_URI=https://app.example.com/
OIDC_SCOPES=openid profile email offline_access users.read
BFF_API_BASE_URL=https://platform.example.com/api
COOKIE_PASSWORD=<random value of at least 32 characters>
REDIS_URL=redis://<session-store-host>:6379
```

Use Valkey or Redis for production sessions. Make it reachable from every app replica.
Keep the cookie password stable across restarts and consistent across replicas; see
[cookie password rotation](bff-pattern.md#rotating-the-cookie-password).
Keep `SESSION_TTL_SECONDS` at or below the issued refresh-token lifetime.

Supply these values through your deployment's runtime environment or secret store.
Do not expose them through `VITE_*` variables or embed them during the frontend build.
For plain HTTP local development only, set `COOKIE_SECURE=false`.

The package-registry read token is a separate **build-time** credential. Use the
[Docker build-secret recipe](typescript-sdk.md#installation) for installation. The
running app needs its OIDC and session secrets, not a GitHub package token.

Your image must contain your app's built server and runtime dependencies. The
`apps/minimal-app/Dockerfile` builds from the entire Wallow workspace; adapt your own
Dockerfile to install the published packages instead of copying Wallow's private workspace.

Route your app domain over HTTPS to that container. Forward `/bff/*` and `/api/*` to
its server, alongside the UI. Configure trusted proxy addresses when your runtime sits
behind ingress; see the [SDK environment reference](typescript-sdk.md#environment-variables).
For SSR self-fetches, configure `internalOrigin` for the app's own listener, not the
Wallow backend. The example uses its local server port.

## 6. Add a service account only for app-owned operations

For work without a signed-in user, such as accepting a contact inquiry, register a
separate **service account** in the same organization. Grant only the needed scopes.
Add its revealed credentials to the app server's runtime environment:

```dotenv
OIDC_SERVICE_CLIENT_ID=<service account client id>
OIDC_SERVICE_CLIENT_SECRET=<service account secret>
OIDC_SERVICE_SCOPES=inquiries.write
```

Use `createServiceClient()` from `@bc-solutions-coder/sdk/server/service` and pass its
`client` to generated operations. It shares `OIDC_ISSUER`, `BFF_API_BASE_URL`, and the
optional discovery and Redis configuration. See the
[service client reference](typescript-sdk.md#service-accounts-createserviceclient).
Do not use this identity for requests that need the signed-in user's permissions.

## 7. Verify the deployed integration

Run these checks against your external app and the intended Wallow deployment:

1. Open the app while signed out. Its session endpoint, `/bff/user`, returns `401`.
2. Follow sign-in. Confirm that the browser reaches the public issuer and returns to
   the exact registered callback. Complete consent and any membership steps.
3. Confirm that `/bff/user` now returns the session and a typed current-user API call
   succeeds through the app's `/api` route. Browser API traffic stays on the app origin.
4. Perform an authorized mutation. Confirm that SDK CSRF handling succeeds, and that
   the same cookie-authenticated mutation without its CSRF header is rejected.
5. Sign out using the SDK helper. Confirm that the app session becomes unauthorized.
6. Sign in again, end the Wallow session elsewhere, and confirm that back-channel
   logout invalidates the app's server-side session without relying on an iframe.
7. If configured, submit an anonymous inquiry and verify that it reaches the correct
   organization through the service account.
8. Restart the app and test through each replica with the shared session store.

A successful build or configuration health response alone does not prove discovery,
credentials, Redis connectivity, membership, or logout delivery. Complete the login and
API checks before treating an app as integrated.

For an agent working in another repo, provide this guide, the Wallow checkout path or
revision, the target issuer and API URL, and the selected SDK version. Keep secret
values in the deployment environment. Ask the agent to adapt the listed example files
and report the results of these checks.
