import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Guard for the wallow-auth container build (Wallow-0q2s.5.4).
 *
 * The app depends on `@bc-solutions-coder/styles` via `workspace:*` and its
 * `src/styles.css` + vite pipeline consume the package's `./styles.css` and
 * `./vite` (wallowStyles()) exports. The Dockerfile therefore MUST land the
 * styles package into the image the same way it lands the SDK, or
 * `pnpm install --frozen-lockfile` (styles is an unresolvable workspace dep)
 * and `vite build` (imports the package's built dist) both fail in-image.
 *
 * These are fast static assertions over the Dockerfile text — a stand-in for
 * the slow full `docker build -f apps/wallow-auth/Dockerfile .` proof, which is
 * the ultimate acceptance criterion (see the bead note for how to run it).
 *
 * The four load-bearing facts, in the order the build layers must respect:
 *   1. the styles MANIFEST is copied before `pnpm install` (so the frozen
 *      lockfile resolves the workspace dep);
 *   2. the full styles SOURCE is copied before the build step;
 *   3. `api/branding.json` is copied before the build step (it is a build-time
 *      input the styles package statically imports);
 *   4. the styles package is built BEFORE the app (the app's vite build imports
 *      the styles package's dist output, not its source).
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
const stylesBuildAt: number = pos(/pnpm\s+--filter\s+@bc-solutions-coder\/styles\s+build/u);
const appBuildAt: number = pos(/pnpm\s+--filter\s+@bc-solutions-coder\/wallow-auth\s+build/u);

describe("the wallow-auth Dockerfile styles wiring", () => {
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

  it("copies api/branding.json before the build step", () => {
    expect(brandingCopyAt).toBeGreaterThanOrEqual(0);
    expect(brandingCopyAt).toBeLessThan(appBuildAt);
  });

  it("builds the styles package before the app", () => {
    expect(stylesBuildAt).toBeGreaterThanOrEqual(0);
    expect(appBuildAt).toBeGreaterThanOrEqual(0);
    expect(stylesBuildAt).toBeLessThan(appBuildAt);
  });

  it("no longer carries the stale claim that nothing imports api/branding.json", () => {
    // Since wallow-web now consumes @bc-solutions-coder/styles (which statically
    // imports api/branding.json), the comment asserting wallow-web needs no
    // branding.json COPY "because nothing in wallow-web imports it" is false and
    // must be removed.
    expect(dockerfile).not.toMatch(/nothing in wallow-web imports/iu);
    expect(dockerfile).not.toMatch(/no equivalent line/iu);
  });
});
