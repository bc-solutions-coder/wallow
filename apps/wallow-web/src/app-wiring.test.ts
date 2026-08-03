import { fileURLToPath } from "node:url";

import { currentUserQuery } from "@bc-solutions-coder/auth";
import { REQUEST_ID_HEADER as LOGGER_REQUEST_ID_HEADER } from "@bc-solutions-coder/logger/server";
import type { WallowSdk } from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";
import { REQUEST_ID_HEADER } from "@bc-solutions-coder/sdk/server";
import {
  browserPreBundleList,
  describeBrowserPreBundleList,
} from "@bc-solutions-coder/testing/browser-deps";
import { assertBrowserStylesWiring } from "@bc-solutions-coder/testing/browser-styles-wiring";
import { describe, expect, it } from "vitest";

import viteConfig from "../vite.config";
import vitestConfig from "../vitest.config";

/**
 * Everything holding wallow-web together that no rendered spec can reach: the build
 * config, the vitest harness, and the two package seams this app is the only place
 * able to pin. Each `describe` below names the guard it is — with these merged into
 * one file, the `describe` is what tells a reader what broke.
 *
 * The browser half is `app-wiring.browser.test.tsx`. Both stay directly under `src/`:
 * `wallow/zone-dag` exempts single-segment paths as `ROOT_ZONE`, and that exemption
 * is the only thing permitting the `../vite.config` imports above.
 *
 * Node project: mounts nothing.
 */

/** This app's root — the directory holding `package.json`, `vite.config.ts` and `src/`. */
const appDir: string = fileURLToPath(new URL("..", import.meta.url));

/** The facade's specifier, for the harness assertions that name it as a string. */
const FACADE = "@bc-solutions-coder/query";
/** The auth package that rides on the facade, linked the same way. */
const AUTH = "@bc-solutions-coder/auth";

describe("the wallow-web client build", () => {
  it("re-enables copyPublicDir on the client environment", () => {
    // Start builds through nitro/vite's two named environments, and nitro does
    // `config.build.copyPublicDir ??= false` on the CLIENT one. That silently
    // drops the publicDir the brand-assets plugin contributes, so `/piggy-icon.svg`
    // 404s in the BUILT app only — the dev server serves publicDir itself and
    // looks fine.
    //
    // That the plugin points `publicDir` at the shared package is asserted where
    // the plugin lives, in `packages/styles/src/vite.test.ts`. This side reads the
    // config an app actually hands Vite, which an app is free to override.
    expect(viteConfig.environments?.client?.build?.copyPublicDir).toBe(true);
  });
});

// The pre-bundle guard: every `optimizeDeps.include` entry has to be declared and
// has to resolve. An unresolvable one is a WARNING Vite ignores, so the list can
// look complete while pre-bundling nothing — the machinery and the full
// explanation live in `@bc-solutions-coder/testing/browser-deps`.
describeBrowserPreBundleList({ packageDir: appDir, config: vitestConfig });

// The on-disk half of the browser project's styling wiring. The rendered half is in
// `app-wiring.browser.test.tsx`; this one names the pieces that have to stay wired,
// so removing one fails with a message saying WHICH.
assertBrowserStylesWiring({ appDir });

describe("the vitest harness resolves the facade explicitly", () => {
  it("pre-bundles the facade and the auth package, which pnpm merely LINKS", () => {
    // A linked workspace package is not pre-bundled by default, and both of these
    // are imported from browser-project specs (a component reading a query; the
    // home-gate spec reading the current-user query). Unnamed, Vite discovers
    // them mid-run and reloads.
    const extras: readonly string[] = browserPreBundleList(vitestConfig);

    expect(extras).toContain(FACADE);
    expect(extras).toContain(AUTH);
  });

  it("inlines the facade for the node project instead of externalizing it", () => {
    // The node project runs the SSR-side route specs; without `ssr.noExternal`
    // the linked facade is externalized to a bare Node import instead of being
    // transformed.
    const noExternal = vitestConfig.ssr?.noExternal;

    expect(Array.isArray(noExternal) ? noExternal : []).toContain(FACADE);
  });
});

describe("the auth package as this app resolves it", () => {
  it("keys the current-user query with the generated key the profile read uses", () => {
    // `src/features/settings/api.ts` re-exports `usersGetCurrentUserQueryKey`, so
    // the profile screen and the route gates share ONE cache entry. A hand-rolled
    // key (`['user','current']`) silently loses that.
    expect(currentUserQuery(fakeClient()).queryKey).toEqual(
      usersGetCurrentUserQueryKey({ client: fakeClient() }),
    );
  });
});

describe("the logger mirrors the SDK's correlation header", () => {
  it("reads the correlation id from the header the proxy writes", () => {
    // The logger declares zero dependencies — importing the SDK for one string
    // would drag an OIDC client into every consumer of a logging package — so the
    // string is duplicated by design. This app depends on both packages, which
    // makes it the one place the duplication can be pinned. Drift is silent
    // rather than loud: records simply arrive with no correlation id.
    expect(LOGGER_REQUEST_ID_HEADER).toBe(REQUEST_ID_HEADER);
  });
});

/**
 * The only thing a query KEY needs off a client: the base URL it is scoped to.
 * Building the key never issues a request, so a real transport would add nothing.
 */
function fakeClient(): WallowSdk["client"] {
  return { getConfig: () => ({ baseUrl: "https://wallow.test/api" }) } as WallowSdk["client"];
}
