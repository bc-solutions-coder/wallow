# @bc-solutions-coder/testing

Private workspace package for Vitest projects, browser assertions, and recording
SDK transports. Use it through the repository workspace. Each subpath has a
specific runtime; browser helpers must stay out of Node-loaded config files.

## Configure test projects

```ts
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

const { node, browser } = createVitestProjects({
  browserSetupFiles: ["./vitest.setup.ts"],
});

export default defineConfig({ test: { projects: [node, browser] } });
```

The Node project runs `.test.ts` and `.ssr.test.tsx` files. The browser project runs
other `.test.tsx` files in headless Chromium. An explicit `nodeTsxSpecs` list replaces
the SSR pattern. The preset pre-bundles shared browser dependencies and grants
clipboard permissions, but supplies no styles or setup guards.

For styled components, pass `wallowStyles()` from `@bc-solutions-coder/styles/vite`
as `browserPlugins`, and import your local Tailwind entry and `virtual:wallow-theme.css`
in the setup file. Keep Tailwind `@source` paths relative to that local stylesheet.
See [browser test setup](../../docs/development/testing.md) for repository guidance.

## Import the appropriate helper

All subpaths below start with `@bc-solutions-coder/testing`.

| Import                           | Runtime         | Purpose                                                              |
| -------------------------------- | --------------- | -------------------------------------------------------------------- |
| Root                             | Node config     | `createVitestProjects`, `mergeOptimizeDeps`, and dependency baseline |
| `/render`                        | Browser tests   | Re-export of `vitest-browser-react`'s `render`                       |
| `/render-with-wallow`            | Browser tests   | Memory router, query cache, and SDK-backed rendering                 |
| `/sdk-harness`                   | Node or browser | Isolated SDK with request recording and programmable responses       |
| `/browser-deps`                  | Node tests      | Check browser dependency declarations and resolution                 |
| `/browser-styles-wiring`         | Node tests      | Check consumer stylesheet configuration                              |
| `/browser-mode-smoke`            | Browser tests   | Check Chromium globals and real layout                               |
| `/contrast`                      | Browser tests   | Read rendered colors and compare contrast                            |
| `/locators`                      | Browser tests   | Find elements and assert DOM tags                                    |
| `/catalog-select`                | Browser tests   | Open a catalog Select and choose an option                           |
| `/theme-wiring`                  | Browser tests   | Assert the rendered fork theme                                       |
| `/form-submission`               | Browser tests   | Capture and cancel a native form submit                              |
| `/invalidation`                  | Tests           | Check predicate invalidation against generated query keys            |
| `/router-stub`                   | Tests           | Verify a router mock's marker                                        |
| `/console-guard`                 | Browser setup   | Record unexpected warnings and errors                                |
| `/network-escape`                | Browser setup   | Reject and record unowned global fetch requests                      |
| `/navigation-escape`             | Browser setup   | Record and, when possible, prevent cross-document navigation         |
| `/node-async-hooks-browser-shim` | Browser alias   | Synchronous context stand-in for uncompiled server imports           |

Install the console, network, and navigation guards in each browser setup file.
Assert their records are empty after each test, and consume records explicitly
when a test expects that event. Each guard's editor documentation names its
install, assertion, and consuming helpers.

## Program an SDK response

```ts
import { usersGetCurrentUser } from "@bc-solutions-coder/sdk";
import { createSdkHarness } from "@bc-solutions-coder/testing/sdk-harness";

const harness = createSdkHarness();
harness.resolveJson({ id: "user-1", email: "alex@example.com" });
const user = await usersGetCurrentUser({ client: harness.client });
const request = harness.last;
```

The real SDK serializes requests and parses responses. The harness records each
request before calling its responder and never sends it over the network.
Responses are not validated against API schemas; supply fixtures that match the
operation under test. `respond()` handles custom responses, `pending()` exercises
loading states, and `reset()` clears calls and restores JSON `{}` at status 200.
`routeHarness()` matches methods and pathname suffixes for multi-operation screens.
Unmatched routes return 404 unless a fallback is supplied.

`renderWithWallow(ui, { harness })` supplies this SDK and a fresh query client in
router context. Program the harness before rendering a component that fetches on
mount. File routes need `{ path, route }` entries and are reparented in place.
Supply either an existing query client or `onUnhandledFailure`, not both.

## Development

```bash
pnpm --filter @bc-solutions-coder/testing test
pnpm --filter @bc-solutions-coder/testing typecheck
pnpm --filter @bc-solutions-coder/testing build
```
