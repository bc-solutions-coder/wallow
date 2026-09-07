# @bc-solutions-coder/sdk

Wallow's TypeScript API client, browser authentication helpers, Node BFF, and
service-account client. Generated endpoint functions include descriptions of their
permissions, tenant context, inputs, results, and side effects. Those comments
also ship in the package declarations for editor tooltips.

Start with [Connect an external app](../../docs/integrations/external-app.md) for
registration and deployment. The [SDK integration guide](../../docs/integrations/typescript-sdk.md)
contains the full configuration and authentication reference. This README describes
the current checkout; installed releases may expose an older API.

## Entrypoints

| Import                                       | Runtime              | Purpose                                                                     |
| -------------------------------------------- | -------------------- | --------------------------------------------------------------------------- |
| `@bc-solutions-coder/sdk`                    | Browser or SSR       | Isolated API client, generated operations/types, authentication URL helpers |
| `@bc-solutions-coder/sdk/query`              | React, including SSR | Generated query keys/options, mutations, and invalidation filters           |
| `@bc-solutions-coder/sdk/server`             | Node                 | BFF route handlers, API proxy, configuration, and session stores            |
| `@bc-solutions-coder/sdk/server/service`     | Node                 | Service-account client using OAuth client credentials                       |
| `@bc-solutions-coder/sdk/server/passthrough` | Node                 | Reverse proxy without a BFF-owned session                                   |
| `@bc-solutions-coder/sdk/server/forwarded`   | Browser or server    | Trusted-proxy address and request-origin helpers                            |

The query entry requires the optional `@tanstack/react-query` peer. Redis-backed
sessions require the optional `redis` peer. Server entries use web-standard
`Request` and `Response` objects and can be mounted in a Node framework adapter.

## Call an endpoint

After configuring the BFF and signing in, create a browser client:

```ts
import { createWallowSdk, usersGetCurrentUser } from "@bc-solutions-coder/sdk";

const sdk = createWallowSdk({ baseUrl: "/api" });
const user = await usersGetCurrentUser({ client: sdk.client });
```

Pass `sdk.client` on each generated call. Route parameters go under `path`, URL
parameters under `query`, and JSON input under `body`. Operations return response
bodies directly. `getCurrentUser({ client: sdk.client })` is the convenience helper
that returns `null` for an unauthenticated API response.

For SSR, create an instance for each incoming request. Use an absolute BFF URL and
forward that request's cookie header:

```ts
import { createWallowSdk } from "@bc-solutions-coder/sdk";

export function sdkForRequest(request: Request) {
  return createWallowSdk({
    baseUrl: new URL("/api", request.url).href,
    cookieHeader: request.headers.get("cookie") ?? "",
  });
}
```

Use a trusted request origin in deployments behind proxies. `internalOrigin` can
change the transport origin while retaining the public base URL in query keys.
The CSRF interceptor reads browser cookies; SSR writes must forward any required
CSRF header explicitly.

## Use query and mutation helpers

Generated helper names follow the endpoint name: `usersGetCurrentUserOptions`,
`usersGetCurrentUserQueryKey`, or `inquiriesSubmitMutation`. Each helper's comment
includes endpoint context and explains whether it builds a cache key, query
options, or mutation options. Constructing the helper does not send a request.

```ts
import { useQuery } from "@tanstack/react-query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserOptions } from "@bc-solutions-coder/sdk/query";

export function useCurrentUser(sdk: WallowSdk) {
  return useQuery(usersGetCurrentUserOptions({ client: sdk.client }));
}
```

`queriesWithTag(tag)` and `queriesForOperation(queryKey)` create invalidation
filters. They match across argument combinations and client base URLs, so choose
an appropriately isolated query cache for each user request.

## Authentication and errors

`createWallowBffServer()` owns login, callback, user, logout, front-channel logout,
back-channel logout, and the authenticated API proxy. Mount its handlers using the
integration guide. Use `loginRedirect()` to build a full-document login link and
`logout()` to end the browser BFF session. Lower-level OIDC helpers only build URLs
or forms; they do not perform the authentication flow themselves.

For background jobs, `createServiceClient()` returns an authenticated API client
and an `accessToken()` function. Its configuration comes from
`loadServiceConfigFromEnv()`. Client credentials identify a service account rather
than a signed-in user.

Configured generated API calls reject with `ApiFailure` from
`@bc-solutions-coder/api-errors`. Use `isApiFailure()` and read its code, status,
field errors, and request identifiers. Configuration and URL helpers can throw
ordinary errors, and `logout()` can reject with an ordinary `Error`. BFF handlers
return HTTP problem responses on their handled failure paths.

## Development and generation

Run from the repository root:

```bash
pnpm --filter @bc-solutions-coder/sdk generate
pnpm --filter @bc-solutions-coder/sdk build
pnpm --filter @bc-solutions-coder/sdk typecheck
pnpm --filter @bc-solutions-coder/sdk test
```

Generation uses `openapi/v1.json`. To refresh it from a running development API,
set `WALLOW_OPENAPI_URL` when running `tsx scripts/generate.ts` in this package.
Commit the snapshot and generated files together; do not hand-edit them.

The build emits JavaScript and declarations for all six entrypoints, then repairs
missing TanStack symbol imports in query declarations. `pnpm check` verifies
regeneration, package exports, and an external installed-package consumer.

See the [browser Web Push guide](../../docs/development/browser-push.md) for key
provisioning, subscriptions, service workers, and notification click handling.
