**status: active**

# Three bugs: proxy-aware client address, a second SSR React, and the nav toggle flake

Covers `Wallow-tvn3` (P2, bug), `Wallow-luni` (P2, bug) and `Wallow-x5da` (P2, bug), all
children of the `Wallow-xzy1` frontend & build-tooling epic. They are independent — nothing
here sequences one behind another — but they land together because all three are app-server /
build-graph defects in the same two Start apps.

---

## 1. `Wallow-tvn3` — nothing in either app server is proxy-aware

### Verified state (re-checked this session, not taken from the bead)

`trustProxy` appears nowhere in `packages/logger/src`, `packages/sdk/src` or either app's
`src`. The four sites that consume the peer address are exactly the four the bead names, and
all four read the raw srvx `request.ip`:

| Site | Line | What the address is used for |
| --- | --- | --- |
| `apps/wallow-web/src/app/lib/log-ingest.server.ts` | 92 | per-IP ingest rate-limit key + stamped `clientIp` |
| `apps/wallow-auth/src/shared/lib/log-ingest.server.ts` | 52 | same, in the auth app |
| `apps/wallow-web/src/app/lib/bff.server.ts` | 151 | stamps `CLIENT_IP_HEADER` on the outbound API hop |
| `apps/wallow-auth/src/shared/lib/api-passthrough.server.ts` | 79 | same, in the auth app |

Both log-ingest files also declare their **own private copy** of

```ts
interface PeerRequest extends Request { readonly ip?: string | undefined }
```

so the type is duplicated alongside the logic.

The only inbound `x-forwarded-for` reads in the tree are `packages/sdk/src/server/forwarded.ts`
(appends this hop's address to the outbound chain, then deletes the seam header) and
`proxy.ts:519` (a hop-by-hop header list). Neither is a trust decision, so neither is a base to
extend — confirmed by reading both.

Behind `docker/caddy/Caddyfile.example` — the sole externally reachable container — every real
user shares one bucket keyed on Caddy's container address, and every outbound
`x-wallow-client-ip` carries Caddy's address. Caddy stamps `X-Forwarded-For` by default (the
Caddyfile says so explicitly and carries no `header_up` directives), so the data is present and
simply not consulted.

**This is not a regression from `Wallow-dayd`.** Before that fix the handler read
`x-wallow-client-ip` off the *inbound* request, which Caddy does not set, so production already
collapsed to a single bucket — or to an attacker-chosen one. Today's behaviour is strictly
better and simply not yet correct.

### Design

One shared resolution, four consumers. It goes in **`packages/env`** as a new subpath
`@bc-solutions-coder/env/client-address`, beside `request-origin`:

- `packages/env` is chartered as *deployment-derived addressing for Start apps*, zero
  dependencies, subpath-only. A trusted-proxy list is deployment-derived addressing.
- `request-origin.ts` already lives there and already reasons about a `X-Forwarded-*` header,
  so the two sit together and can share a doc voice about which forwarded headers are trusted
  and why.
- Both log-ingest files already import `@bc-solutions-coder/env/request-origin`, so two of the
  four sites gain no new dependency edge at all.
- Putting it in `packages/sdk/src/server` was the alternative and is worse: the SDK's job there
  is the *outbound* hop, and the two log-ingest handlers do not talk to the SDK.

`PeerRequest` moves into the new module and is exported, retiring both private copies.

#### Trust policy — opt-in, but preset in the reference compose stack

Default trusts **nothing**: with no configuration the resolver returns `request.ip` unchanged,
which is byte-for-byte today's behaviour. Trust is enabled by `WALLOW_TRUSTED_PROXIES`,
accepting a comma-separated list of:

- CIDR blocks — `10.0.0.0/8`, `172.16.0.0/12`, `fd00::/8`
- bare addresses — `192.0.2.10`
- the presets `loopback`, `private`, `uniquelocal` (the Express-style names, so the value is
  recognisable to anyone who has configured `trust proxy` before)

`docker/docker-compose.production.yml` sets `WALLOW_TRUSTED_PROXIES: private` on the
`wallow-auth` and `wallow-web` services, so the shipped Caddy stack is correct out of the box.
A fork with a different topology sets its own value. The security posture is the safe one in
both directions: a fork that forgets the variable **over-buckets** (today's bug, no worse)
rather than accepting a forged address.

Resolution, given a request:

1. If `request.ip` is absent or empty → `undefined` (unchanged).
2. If the peer is **not** in the trusted set → return `request.ip`. The header is ignored
   entirely. **This branch is the load-bearing one**, not the header read.
3. If the peer **is** trusted → walk `X-Forwarded-For` right-to-left, discarding trusted
   entries, and return the first untrusted one; if every entry is trusted, return the leftmost.
   Malformed entries fall back to `request.ip`.

Right-to-left is what makes the walk forgery-resistant with multiple proxies: a client can
prepend anything it likes to the chain, but it cannot stop each trusted hop from appending the
address it actually saw.

An env var read once at module scope, like `OTEL_EXPORTER_OTLP_ENDPOINT` beside it — not
per-request. The value cannot change within a process.

### Steps

1. `packages/env/src/client-address.ts` — `PeerRequest`, an exported CIDR/preset parser, and
   `resolveClientAddress(request)`. Add the `./client-address` subpath to both the `exports` and
   `publishConfig.exports` maps in `packages/env/package.json` (both halves — the second is the
   published shape).
2. `packages/env/src/client-address.test.ts`, red first. The load-bearing cases:
   - an **untrusted** peer sending `X-Forwarded-For: 203.0.113.9` gets `request.ip`, not the
     forged value — the acceptance criterion's named test;
   - a trusted peer's forwarded address is honoured;
   - two clients at different addresses behind the same trusted proxy resolve to **different**
     values (the bucket-separation criterion, asserted directly rather than through a limiter);
   - a chain with a spoofed prefix (`evil, 203.0.113.9`) through one trusted hop yields
     `203.0.113.9`;
   - IPv6, including a v4-mapped `::ffff:` peer, and the `[::1]:port` shape srvx can produce;
   - unset / empty / garbage `WALLOW_TRUSTED_PROXIES` all degrade to "trust nothing".
3. Swap all four call sites to `resolveClientAddress`, delete both private `PeerRequest`
   declarations, and rewrite the four doc comments — each currently asserts *"Nothing inbound is
   consulted"*, which stops being true.
4. Extend the two existing app specs (`bff.server.test.ts`, `api-passthrough.server.test.ts`)
   with a trusted-peer case each; they already assert `CLIENT_IP_HEADER` end-to-end, so this is
   an added case, not a new file.
5. `docker/docker-compose.production.yml` — `WALLOW_TRUSTED_PROXIES: private` on both Node
   services, with a comment naming Caddy as the reason. Add the variable to
   `docker/.env.production.example` if the other `WALLOW_*` vars are threaded that way.
6. Docs: a row in `docs/getting-started/configuration.md`'s environment table, and a note in
   `docker/caddy/Caddyfile.example` next to the existing `trusted_proxies` discussion pointing
   out that the *app* has its own list and both must agree.

### Done when

One resolution exists and all four sites use it; an untrusted peer provably cannot forge the
address; two clients behind a trusted proxy land in different buckets. Full `pnpm check`.

### LANDED 2026-08-27

Done, with five corrections to the steps above — five call sites rather than four
(`apps/minimal-app/src/lib/api-passthrough.ts` is the fifth, and it is the one a fork copies), a
`createClientAddressResolver(env)` factory instead of the module-scope env read step 1 assumes
(the package charter forbids it touching the environment), a malformed chain entry SKIPPED
during the walk rather than falling back to `request.ip`, the inbound seam header deleted when
there is no peer to stamp, and the docs row in `docs/operations/reverse-proxy.md` rather than
`configuration.md`. The reasoning for each is in
`docs/plans/2026-08-27/1127-backlog-triage-and-sequencing.md` Step 3.

**This plan stays `active`** — §2 (`Wallow-luni`) and §3 (`Wallow-x5da`) are untouched.

### Deliberately out of scope

`resolveRequestOrigin` honours `x-forwarded-proto` **unconditionally**, with a comment
acknowledging the header is attacker-supplied and arguing the `http`/`https` allowlist keeps it
inert. That is a defensible position and a different question from this bead — but once a
trusted-proxy set exists in the same package, gating the proto read on it becomes nearly free.
File it as a follow-up bead rather than widening this one.

---

## 2. `Wallow-luni` — `with-selector` emits `__require("react")` into the server bundle

### Measurement (the bead demands this before any fix)

Cleaned `.output`, `.nitro` and the nitro caches, then ran `pnpm build`. Result:

| App | chunks containing `__require("react")` |
| --- | --- |
| `apps/minimal-app` | **0** |
| `apps/wallow-auth` | **1** — `.output/server/_ssr/use-app-form-*.mjs` |
| `apps/wallow-web` | **1** — `.output/server/_ssr/isElementDisabled-*.mjs` |

This matches the bead's corrected scope (1 per zoned app, not 3). Caveat to re-run during
implementation: turbo reported `FULL TURBO`, so `.output` was restored from the content-addressed
cache rather than recomputed. Inputs were unchanged, so the artifacts are the ones a fresh build
produces — but the first implementation step re-runs it with caching defeated and re-states the
number before touching anything.

### Mechanism — confirmed, and it is NOT what the bead's fix direction assumes

In both zoned apps the offending module is `use-sync-external-store/shim/with-selector`,
inlined as CJS:

```js
var __require = /* #__PURE__ */ (() => createRequire(import.meta.url))();
...
var React = __require("react");
var shim  = __require("react");
```

`__require` is `createRequire(import.meta.url)` — a **real runtime `require` from
node_modules**. React itself is bundled (`require_react()`, out of
`_libs/@tanstack/react-form+[...].mjs` / `_libs/@tanstack/react-router+[...].mjs`), so this is a
genuine second React instance. Second-React under SSR means `ReactSharedInternals` is null on the
copy the hooks come from, so any SSR call into `useSyncExternalStoreWithSelector` throws — the
same failure mode the existing shim alias was written to fix.

The decisive comparison is `minimal-app`, which has the **identical module** and zero
`__require`:

```js
// apps/minimal-app/.output/server/_libs/@tanstack/react-router+[...].mjs
var React = require_react();   //  <- linked to the bundled React
var shim  = require_react();
```

So the module is fine; the **linkage** differs. In `minimal-app` the module stayed external
through the Vite SSR build and Nitro bundled it, correctly linking `require("react")` to the
bundled React. In the two zoned apps Vite **bundled** it into a service chunk, and because `react`
is external *to that Vite build*, `require('react')` could not be linked statically and degraded
to `createRequire`.

Two consequences for the bead:

- **The existing alias already works.** `var shim = __require("react")` is the aliased
  `use-sync-external-store/shim` — it resolved to `react`, exactly as intended. The alias is not
  the gap.
- **The bead's stated fix direction — "extend the alias to cover
  `use-sync-external-store/shim/with-selector`" — is wrong and must not be implemented.** React
  exports no `useSyncExternalStoreWithSelector`, so aliasing that specifier to `react` produces
  an undefined import. `app.ts:66-71` already says this in a comment; the bead's direction
  contradicts the code it points at. Correct the bead before working it.

### ATTEMPTED AND FAILED: `ssr.external` is inert in this build

This was the plan's first choice. **It does not work, and the failure is instructive enough that
it must not be retried.**

Added `external: ["use-sync-external-store"]` to the `ssr` block, cleared `.output`, all
`.nitro` dirs and `.turbo/cache`, rebuilt with `pnpm build --force` (`Cached: 0 cached, 14
total`). Counts unchanged — and the chunk hashes were **byte-identical**
(`use-app-form-Bn7Jiumh.mjs`, `isElementDisabled-BXrKiEPY.mjs`), meaning zero effect, not partial
effect.

**Control probe**, to distinguish "wrong specifier" from "option ignored": temporarily added
`@base-ui/react` to the same list and rebuilt `wallow-web` with `node_modules/.vite` cleared too.
The ssr environment genuinely re-ran (`building ssr environment for production`, 1188 modules
transformed) and `isElementDisabled-BXrKiEPY.js` was still emitted at an identical 167.82 kB, with
no base-ui chunk in `_libs/`. So `ssr.external` is **ignored outright** for this environment.

In Vite, a `noExternal: true` short-circuits the `external` list entirely, which is the likeliest
cause — set by the Start plugin or by nitro's service environment, not by anything in this repo.

The **alias fallback is dead too**: `use-sync-external-store@1.6.0` ships **no ESM build**. Every
entry in its `exports` map resolves to CJS (`index.js`, `with-selector.js`, `shim/index.js`,
`shim/with-selector.js`, all re-exporting from `cjs/`). There is no ESM entry point to aim an
alias at.

`optimizeDeps.exclude` is also not the lever: this is a build-time externalization split, not
dep pre-bundling. The earlier inference that `isElementDisabled-*` was an optimizeDeps artifact
name was **wrong** — the name simply derives from the chunk's entry module, and the same base name
appears in the client build, the SSR build and the dev dep cache.

The tree was left **pristine** (`git checkout`); no inert config was committed.

### Refined mechanism

Vite's ssr pass leaves `react` **external**, so a CJS `require('react')` inside a module Vite
**bundles** cannot be linked statically and degrades to `createRequire(import.meta.url)`. Nitro
then rewrites ESM `import ... from "react"` to its own bundled `require_react()` — visible at the
top of the chunk:

```js
import { i as require_react } from "../_libs/@tanstack/react-form+[...].mjs";
```

— but it **cannot rewrite an already-emitted `createRequire` call**. `minimal-app` escapes only
because its copy stays external through the Vite pass and reaches Nitro intact.

### Remaining options — none verified, all with real blast radius

1. `ssr.noExternal: ["react", "react-dom"]` — bundle React into the Vite ssr pass so the CJS
   require links to it. Follows the same shape as the existing react-query `noExternal` entry.
   `react-dom` must move with it or react-dom loads its own React, so this trades one duplication
   risk for another and must be measured, not assumed.
2. Find and unset whatever sets `noExternal: true` for the ssr service environment, which would
   make `ssr.external` viable again. Requires reading the Start plugin's and nitro's `config`
   hooks; the highest-information option and the one most likely to yield a clean fix.
3. Vendor a tiny ESM `with-selector` in `packages/config` and alias the specifier to it. ~20 lines
   of vendored React code — genuine last resort.

**Before any of these**, settle whether the bug is reachable. The chunk is emitted, but nothing
has yet shown that `useSyncExternalStoreWithSelector` is actually *called* during SSR. If it is
not, this is latent and P2 may be generous; if it is, there should be an observable SSR failure to
reproduce and the fix gets a real regression test. That question is cheap to answer and changes
how much blast radius is justified — none of options 1-3 is worth taking blind.

### Steps

1. Determine reachability under SSR (above). Record the answer on the bead either way.
2. Read the Start plugin's and nitro's Vite `config` hooks to find what sets `noExternal: true`
   for the ssr service environment — this decides between options 1-3.
3. Apply the option the evidence supports; rebuild all three apps cache-defeated and grep every
   `.output/server` for `__require("react")`. Expect 0.
4. Boot the built server for both zoned apps and confirm SSR still renders full documents. The
   `9895 → 2621 body chars` collapse recorded in the existing `app.ts` comment is the regression
   signature — measure body length on `wallow-auth`'s `/login` the same way, and check `react-dom`
   did not itself duplicate if option 1 was taken.
5. Extend the `app.ts` comment block with what was found — that block exists precisely because
   none of these invariants has a spec (vitest never builds the Nitro bundle) and a regression
   surfaces as a blank page, not a red test.

### Done when

A clean, cache-defeated `pnpm build` leaves no chunk in any app's `.output/server` containing
`__require("react")`, and both zoned apps still SSR a full document.

---

## 3. `Wallow-x5da` — `app-shell.toggle.test.tsx` still leaks a real navigation

_Scoped per the session decision: land the causal diagnosis and the fix it implies, run as many
full-workspace suites as the session allows, and report the **honest measured count** on the bead.
The bead stays open until the 20-run bar in its acceptance criteria is actually met — a short
green streak is explicitly not evidence at a ~13% per-run failure rate._

### The bead's leading theory is refuted — read this before spending anything on it

`Wallow-tkyq`'s scout proposed a **cross-iframe pointer-misrouting race**: a pointer event
dispatched for file A landing in a concurrently-mounted file B iframe. That cannot happen, on
evidence from the installed sources (`vitest` 4.1.10, `@vitest/browser` 4.1.10):

- `@vitest/browser/dist/client/__vitest_browser__/orchestrator-*.js:69` — `createTesters()`
  opens with `this.iframes.forEach((iframe) => iframe.remove())`.
- `vitest/dist/chunks/cli-api.*.js:2619` — `BrowserPool.runNextTest` calls
  `orchestrator.createTesters({ ..., files: [file] })`. **One file per call.**

So an orchestrator page holds exactly one test iframe at a time. Parallelism is across *pages*
(one per session, `getThreadsCount`), and CDP input is dispatched per page target. Both facts
independently verified this session by reading the files. **Close this theory on the bead.**

The **shared-`useNavStore`-singleton** alternative is also dead. `useNavStore` *is* a module-level
singleton (`nav-store.ts:53`), but each spec file gets a fresh iframe, hence a fresh document and
module registry — every file holds its own instance, and sibling nav specs cannot reach the toggle
file's store. The same reasoning kills bleed of the guard's module-level `escapes` array
(`navigation-escape.ts:37`). Within the file, `beforeEach` already resets the store
(`app-shell.toggle.test.tsx:45`).

### The strong candidate: a same-iframe coordinate collision during the rail's 200 ms transition

`app-nav.tsx:223`:

```
w-16 data-[nav-open=true]:w-64 ... transition-[width] duration-200
```

Unconditional — **not** `motion-safe:` gated, so it animates in every environment, headless
Chromium included. Verified by reading the line.

Geometry, with `<main>` as `flex-1 p-6` (`app-shell.tsx:258`) and the toggle `size-9`:

- rail collapsed (`w-16` = 64px) → toggle box `x[88,124] y[24,60]`, centre **(106, 42)**
- rail expanded (`w-64` = 256px) → the menu root is `px-4 py-4`, `NavHeader` renders `null` (the
  fixture passes no header), and the first destination row is `px-3 py-2 text-sm` = 36px tall,
  occupying `x[16,240] y[16,52]`

**(106, 42) lands inside the first destination row** — `dashboard-nav-organizations`,
`href="/dashboard/organizations"`, byte-identical to the vetoed URL in every recorded failure.

Why this file and no other: at `w-16` the rail ends at x=64, so the toggle's coordinates only
collide *after* a first click has started the expansion. Across all 13 nav spec files,
`app-shell.toggle.test.tsx:66-67` is the only place in the repo issuing a **second click while
that transition is in flight** — two bare `await userEvent.click(...)` calls back to back, with
nothing awaiting a settled width in between. That is exactly the file that flakes.

This explains the failed assertion (`isNavCollapsed` false, not true) and the destination
together, which no other candidate does.

**The honest gap.** Playwright's `setupHitTargetInterceptor`
(`playwright-core/lib/coreBundle.js`) lists `click` among its intercepted events and, on a hit
mismatch, calls `preventDefault` + `stopPropagation` + `stopImmediatePropagation` and then
*retries* the action. A clean mis-hit therefore predicts a retry (pass) or a 15 s timeout — not
the observed ~160 ms fast failure. A split-target variant (mousedown on the toggle at the old
position, mouseup on the link after the width shift) explains the **lost toggle** exactly, since
Chromium fires `click` on the nearest common ancestor and neither handler runs — but it does not
by itself explain how the anchor's default navigation survived. That step is not closed, and the
plan must not pretend it is.

### Steps

1. **Establish the geometry deterministically, without chasing the flake.** A throwaway spec that
   clicks the toggle once, then reads `document.elementFromPoint(106, 42)` while the transition is
   in flight. If it returns the Organizations anchor, the collision is proven in a single run.
   Note that headless scales the tester container by `min(1, parentW/1280, parentH/800)` via
   `transform: scale` (`setIframeViewport`), so read the coordinates back rather than trusting the
   arithmetic above.
2. **Close the open gap** — how the anchor's default survived. Temporarily add a capture-phase
   `click`/`mousedown`/`mouseup` listener in `packages/navigation/vitest.setup.ts` logging
   `type`, `clientX/Y`, `target.outerHTML` and `defaultPrevented`, then loop
   `pnpm --filter @bc-solutions-coder/navigation test` under artificial CPU load until it fires.
   `DEBUG=vitest:browser` adds the orchestrator's iframe lifecycle lines. Remove the listener
   before committing.
3. **Fix.** Two one-line levers, both testable; prefer whichever step 1-2 actually implicates:
   - have the spec await a settled `data-nav-open` / width between the two clicks — fixes the
     spec, leaves the product alone;
   - gate the rail on `motion-safe:transition-[width]` and run Chromium with reduced motion —
     fixes the *class* of races and is arguably right anyway, since an unconditional transition
     ignores `prefers-reduced-motion`.

   These are not equivalent and the choice should follow the evidence. If the collision is real,
   the second is the better product change and the first is the better *test* change; doing both
   is defensible.
4. **Measure honestly.** Run as many consecutive full-workspace suites as the session allows
   (`pnpm -r test`, or the `--no-bail` json-reporter variant `Wallow-uhef` used to dodge pnpm's
   abort-on-first-failure), and record the **actual count and machine-load conditions** on the
   bead the way `Wallow-uhef` did. At a ~13% per-run rate, five clean runs happen by chance about
   half the time — the bead's 20-run bar exists for a reason. **The bead stays open** until that
   bar is genuinely met; a partial streak gets reported as a partial streak.

### Not to be re-opened

- **The router-`Link` stub `onClick`-ordering theory.** Confirmed real, but proven not to be the
  mechanism here — the file's five clicks all target `dashboard-nav-toggle`, a plain button, never
  a `Link`. Tracked separately as `Wallow-xg9t.2`.
- **The `Wallow-tkyq` guard stays.** `packages/testing/src/navigation-escape.ts` +
  `packages/navigation/vitest.setup.ts` fully succeeded at their actual job — converting a
  runner-wide teardown into one attributed failure. This bead is the *other* half of
  `Wallow-tkyq`'s acceptance criteria, the half that was never satisfied.

### A note on `fileParallelism`

`packages/navigation/vitest.config.ts` overrides none of the browser knobs; the shared preset
(`packages/testing/src/vitest-projects.ts:161-167`) sets only `enabled`, `provider`, `headless`
and `instances`. `test.browser.fileParallelism` defaults to `true` and feeds `getThreadsCount`.
Setting it `false` yields one tab — it removes **load**, not co-mounted iframes, of which there
are none. So if the flake vanishes under it, that is evidence for a load-sensitive timing race,
**not** for cross-iframe routing, and it is a suppression lever rather than a fix. Do not ship it
as one. (`test.browser.isolate` is deprecated in v4 in favour of top-level `test.isolate`; both
default `true`.)

---

## Order of work

`tvn3` and `luni` are independent of each other and of `x5da`; any order works. Suggested:
**`luni` first** (smallest, and its verification is a build-and-grep that can run in the
background), then **`tvn3`** (the largest, and the only one with a security consequence), with
**`x5da`** worked alongside since its cost is wall-clock on test runs rather than editing.

Each bead gets its own conventional commit. `pnpm check` and `./scripts/run-tests.sh` gate the
lot; `git push` plus `bd dolt push` close the session.
