import { statSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import viteConfig from "../../vite.config";
import { forkBranding } from "./branding";

/**
 * A root-relative `<img src="/piggy-icon.svg">` is only half the fix: something
 * has to answer that URL. `server.ts` already serves `dist/client` at the root
 * with the right content type (`static-assets.ts`), so all this app owes is
 * getting the icon INTO `dist/client` — and getting it from the shared package,
 * not from a copy of its own, which is what makes api/branding.json the one
 * place a fork swaps the icon.
 *
 * Vite's `publicDir` is that mechanism: its contents are copied to the build
 * root verbatim and unhashed (Wallow-do5e), and the dev server serves the same
 * directory. This app has no public/ directory of its own, so pointing
 * `publicDir` at the package costs nothing and adds no copy step.
 */
const brandAssetsDir: string = fileURLToPath(
  new URL("../../../../packages/styles/assets/", import.meta.url),
);

describe("the wallow-auth client build", () => {
  it("takes its static assets from the shared styles package", () => {
    expect(viteConfig.publicDir).toBeDefined();
    expect(resolve(String(viteConfig.publicDir))).toBe(resolve(brandAssetsDir));
  });

  it("keeps no brand asset copy of its own", () => {
    // Two copies of the icon is two places a fork has to remember to rebrand,
    // and the drift is silent.
    const appPublicDir: string = fileURLToPath(new URL("../../public", import.meta.url));

    expect(statSync(join(appPublicDir, forkBranding.appIcon), { throwIfNoEntry: false })).toBe(
      undefined,
    );
  });

  it("copies the assets into the directory the host serves at the root", () => {
    // server.ts reads dist/client; publicDir lands at the root of build.outDir,
    // so the two have to be the same directory or the icon builds into somewhere
    // nothing serves.
    expect(viteConfig.build?.outDir).toBe("dist/client");
    expect(viteConfig.build?.copyPublicDir).not.toBe(false);
  });
});
