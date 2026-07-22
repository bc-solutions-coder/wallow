import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Guard for the wallow-web container build (Wallow-0q2s.5.4).
 *
 * wallow-web depends on `@bc-solutions-coder/styles` via `workspace:*` and its
 * `src/styles.css` + vite pipeline consume the package's `./styles.css` and
 * `./vite` (wallowStyles()) exports; `src/lib/branding.ts` re-exports the
 * package's branding module, which statically imports `api/branding.json`. The
 * Dockerfile currently copies only packages/sdk + the app and has NO
 * api/branding.json COPY, so `pnpm install --frozen-lockfile` (styles is an
 * unresolvable workspace dep) and `vite build` (imports the styles dist +
 * inlines branding.json) both fail in-image.
 *
 * These are fast static assertions over the Dockerfile text — a stand-in for
 * the slow full `docker build -f apps/wallow-web/Dockerfile .` proof, which is
 * the ultimate acceptance criterion (see the bead note for how to run it).
 *
 * The four load-bearing facts, in the order the build layers must respect:
 *   1. the styles MANIFEST is copied before `pnpm install`;
 *   2. the full styles SOURCE is copied before the build step;
 *   3. `api/branding.json` is copied before the build step;
 *   4. the styles package is built BEFORE the app.
 */

const dockerfile: string = readFileSync(
  fileURLToPath(new URL("../../Dockerfile", import.meta.url)),
  "utf8",
);

/** Character offset of the first match, or -1 when the pattern is absent. */
function pos(pattern: RegExp): number {
  return dockerfile.search(pattern);
}

const installAt: number = pos(/RUN\s+pnpm\s+install/u);
const stylesManifestAt: number = pos(
  /COPY\s+packages\/styles\/package\.json\s+packages\/styles\//u,
);
const stylesSourceAt: number = pos(/COPY\s+packages\/styles\s+packages\/styles(?!\/)/u);
const brandingCopyAt: number = pos(/COPY\s+api\/branding\.json\s+api\/branding\.json/u);
const stylesBuildAt: number = pos(
  /pnpm\s+--filter\s+(@bc-solutions-coder\/styles|['"]?\.\/packages\/\*['"]?)\s+build/u,
);
const appBuildAt: number = pos(/pnpm\s+--filter\s+@bc-solutions-coder\/wallow-web\s+build/u);

describe("the wallow-web Dockerfile styles wiring", () => {
  it("copies the styles package manifest before pnpm install", () => {
    expect(stylesManifestAt).toBeGreaterThanOrEqual(0);
    expect(installAt).toBeGreaterThanOrEqual(0);
    expect(stylesManifestAt).toBeLessThan(installAt);
  });

  it("copies the full styles package source before the build step", () => {
    expect(stylesSourceAt).toBeGreaterThanOrEqual(0);
    expect(appBuildAt).toBeGreaterThanOrEqual(0);
    expect(stylesSourceAt).toBeGreaterThan(installAt);
    expect(stylesSourceAt).toBeLessThan(appBuildAt);
  });

  it("copies api/branding.json (a build-time input) before the build step", () => {
    expect(brandingCopyAt).toBeGreaterThanOrEqual(0);
    expect(brandingCopyAt).toBeLessThan(appBuildAt);
  });

  it("builds the styles package before the app", () => {
    expect(stylesBuildAt).toBeGreaterThanOrEqual(0);
    expect(appBuildAt).toBeGreaterThanOrEqual(0);
    expect(stylesBuildAt).toBeLessThan(appBuildAt);
  });

  it("carries no stale claim that nothing in wallow-web imports api/branding.json", () => {
    expect(dockerfile).not.toMatch(/nothing in wallow-web imports/iu);
  });
});
