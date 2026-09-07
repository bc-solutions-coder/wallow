# @bc-solutions-coder/env

Private workspace helpers for deployment URLs and browser-published settings. Add
`"@bc-solutions-coder/env": "workspace:*"` to a consuming workspace package.
There is no registry installation or root import; use the subpaths below.

The helpers read supplied records, not `process.env`. A server can pass its environment
inside server-only code without including those values in the client bundle.

```ts
import { resolveInternalOrigin } from "@bc-solutions-coder/env/internal-origin";
import {
  normalizeBasePath,
  stripBasePath,
  toViteBase,
  withBasePath,
} from "@bc-solutions-coder/env/base-path";

const prefix = normalizeBasePath("/auth/"); // "/auth"
toViteBase(prefix); // "/auth/"
stripBasePath("/auth/v1/settings", prefix); // "/v1/settings"
withBasePath("https://example.com", prefix); // "https://example.com/auth"
resolveInternalOrigin({ PORT: "3000" }); // "http://localhost:3000"
```

`resolveInternalOrigin` tries `WALLOW_WEB_INTERNAL_URL`, a digits-only `PORT`, then an
optional request origin. It returns `undefined` if none is available. The explicit
origin is not URL-validated or whitespace-trimmed. This is the app's self-fetch address;
`WALLOW_API_INTERNAL_URL` names a different service and is not read here.

The auth URL helpers preserve one deployment value across SSR and hydration:

```ts
import {
  authUrlScript,
  readInjectedAuthUrl,
  resolveAuthUrl,
} from "@bc-solutions-coder/env/auth-origin";

const authUrl = resolveAuthUrl({ WALLOW_AUTH_URL: "https://example.com/auth/" });
const scriptSource = authUrlScript(authUrl);
// Render scriptSource in a script element before browser hydration.
const browserAuthUrl = readInjectedAuthUrl(globalThis);
```

`resolveAuthUrl` trims whitespace and trailing slashes, falling back to
`http://localhost:3002` for an unset or blank value. It does not validate the URL.
`readInjectedAuthUrl` returns `undefined` until a nonblank string has been published.

For other document settings, `./published-global` exports
`publishedGlobalScript(name, value)` and `readPublishedGlobal(name, scope)`. The writer
returns script source with less-than characters escaped and uses `null` when JSON
serialization fails. The reader returns an unvalidated value. Render script text on the
server rather than assigning a server global shared by requests.
