import { readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { forkBranding } from "@bc-solutions-coder/styles";
import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import type { Plugin, UserConfig } from "vite";
import { describe, expect, it } from "vitest";

import viteConfig from "../vite.config";

/**
 * A root-relative `<img src="/piggy-icon.svg">` is only half the fix: something
 * has to answer that URL. Start's nitro output serves `.output/public` at the
 * root, so all this app owes is getting the icon INTO that directory — and
 * getting it from the shared package, not from a copy of its own, which is what
 * makes api/branding.json the one place a fork swaps the icon.
 *
 * Vite's `publicDir` is that mechanism: its contents are copied to the build
 * root verbatim and unhashed (Wallow-do5e), and the dev server serves the same
 * directory. This app has no public/ directory of its own, so pointing
 * `publicDir` at the package costs nothing and adds no copy step. That wiring now
 * lives inside the shared package's `wallowStyles()` factory — the brand-assets
 * plugin sets `publicDir` through its `config()` hook (Wallow-0q2s.5.2) rather
 * than the app declaring a raw `publicDir` field — so this guard asserts the
 * behaviour through that new seam.
 */
const brandAssetsDir: string = fileURLToPath(
  new URL("../../../packages/styles/assets/", import.meta.url),
);

/** This app's own root — the directory holding `vite.config.ts` and `public/`. */
const appDir: string = fileURLToPath(new URL("../", import.meta.url));

/** Resolve the `publicDir` the shared brand-assets plugin contributes via its
 * `config()` hook, or `undefined` if the plugin declares none. */
function brandAssetsPublicDir(): string | undefined {
  const flatten = (option: unknown): Plugin[] => {
    if (Array.isArray(option)) {
      return option.flatMap((entry: unknown): Plugin[] => flatten(entry));
    }
    if (option !== null && typeof option === "object" && "name" in option) {
      return [option as Plugin];
    }
    return [];
  };

  const plugin: Plugin | undefined = flatten(wallowStyles()).find(
    (candidate: Plugin): boolean => candidate.name === "wallow:brand-assets",
  );
  const hook: unknown = plugin?.config;
  const handler: unknown =
    typeof hook === "function" ? hook : (hook as { handler?: unknown })?.handler;
  if (typeof handler !== "function") {
    return undefined;
  }

  const config: UserConfig = (handler as () => UserConfig).call(plugin);
  return config.publicDir === undefined ? undefined : String(config.publicDir);
}

describe("the wallow-auth client build", () => {
  it("takes its static assets from the shared styles package", () => {
    const publicDir: string | undefined = brandAssetsPublicDir();

    expect(publicDir).toBeDefined();
    expect(resolve(String(publicDir))).toBe(resolve(brandAssetsDir));
  });

  it("keeps no brand asset copy of its own", () => {
    // Two copies of the icon is two places a fork has to remember to rebrand,
    // and the drift is silent.
    const appPublicDir: string = join(appDir, "public");

    // `statSync(..., { throwIfNoEntry: false })` answers `undefined` for a path
    // that does not exist AND for a path whose PARENT does not exist, so a wrong
    // `appPublicDir` would make the assertion below pass vacuously. Stat the icon
    // where it IS first: that proves the name and the stat call both work, so the
    // `undefined` below means "no second copy" rather than "nothing resolved".
    expect(
      statSync(join(brandAssetsDir, forkBranding.appIcon), { throwIfNoEntry: false }),
    ).toBeDefined();
    expect(statSync(join(appPublicDir, forkBranding.appIcon), { throwIfNoEntry: false })).toBe(
      undefined,
    );
  });

  it("keeps import protection covering every zone, not just src/app", () => {
    // Regression guard for the `srcDirectory: "src/app"` narrowing: dropping
    // `importProtection.include` silently disables env-boundary enforcement for
    // features/ and shared/, and nothing else in the suite would notice.
    const text: string = readFileSync(join(appDir, "vite.config.ts"), "utf8");

    expect(text).toMatch(/srcDirectory:\s*"src\/app"/u);
    expect(text).toMatch(/importProtection:\s*\{\s*include:\s*\["src\/\*\*"\]\s*\}/u);
  });

  it("re-enables copyPublicDir on the client environment", () => {
    // Start builds through nitro/vite's two named environments, and nitro does
    // `config.build.copyPublicDir ??= false` on the CLIENT one. That silently
    // drops the publicDir the brand-assets plugin contributes, so `/piggy-icon.svg`
    // 404s in the BUILT app only — the dev server serves publicDir itself and
    // looks fine. Setting it back is the whole reason this key exists; deleting
    // it reintroduces a bug no dev-server check can catch.
    expect(viteConfig.environments?.client?.build?.copyPublicDir).toBe(true);
  });
});
