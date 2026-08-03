import { describe, expect, it } from "vitest";

import viteConfig from "../vite.config";

/**
 * The one brand-asset claim this app has to answer for itself.
 *
 * That `wallowStyles()`'s brand-assets plugin points `publicDir` at the shared
 * package is asserted where the plugin lives, in `packages/styles/src/vite.test.ts`.
 * What survives here reads this app's RESOLVED config: `wallowAppConfig()` supplies
 * the key, `packages/config` ships no specs, and an app is free to override it —
 * so the assertion belongs on the config an app actually hands Vite.
 */
describe("the wallow-web client build", () => {
  it("re-enables copyPublicDir on the client environment", () => {
    // Start builds through nitro/vite's two named environments, and nitro does
    // `config.build.copyPublicDir ??= false` on the CLIENT one. That silently
    // drops the publicDir the brand-assets plugin contributes, so `/piggy-icon.svg`
    // 404s in the BUILT app only — the dev server serves publicDir itself and
    // looks fine.
    expect(viteConfig.environments?.client?.build?.copyPublicDir).toBe(true);
  });
});
