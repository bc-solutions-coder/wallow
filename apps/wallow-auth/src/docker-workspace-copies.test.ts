import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The Dockerfile's COPY list is a hand-maintained mirror of this app's
 * `workspace:*` dependencies, and nothing but a real `docker build` notices when
 * the two drift. That drift has a signature failure: the build context simply
 * lacks the package directory, so `vite build` dies with
 * `UNRESOLVED_IMPORT / Rolldown failed to resolve import @bc-solutions-coder/<pkg>`
 * — after a full workspace install, minutes into CI, and never in `pnpm check`.
 *
 * So this spec reads both files off disk and asserts the mirror. Adding a
 * workspace package to `package.json` without adding its two COPY lines fails
 * here instead of in the image build.
 *
 * Both COPY lines are needed, and their POSITION is the point:
 *
 *  1. `COPY packages/<pkg>/package.json packages/<pkg>/` must precede
 *     `RUN pnpm install --frozen-lockfile` — pnpm resolves `workspace:*` against
 *     the manifests present at install time, and a missing one is an install
 *     error, not a fallback to the registry.
 *  2. `COPY packages/<pkg> packages/<pkg>` must precede the build RUN — the
 *     install only linked the directory, the build needs its sources.
 *
 * Node project: reads files, mounts nothing.
 */

// apps/wallow-auth/src -> repo root (src -> wallow-auth -> apps -> repo).
const srcDir: string = dirname(fileURLToPath(import.meta.url));
const appDir: string = resolve(srcDir, "..");
const repoRoot: string = resolve(appDir, "..", "..");

const SCOPE: string = "@bc-solutions-coder/";

const dockerfile: string = readFileSync(resolve(appDir, "Dockerfile"), "utf8");

/**
 * The workspace packages this app declares — the list the Dockerfile mirrors.
 *
 * DEV dependencies count. `@bc-solutions-coder/config` is one, and every
 * package's own vite.config.ts imports it, so leaving it out of the image breaks
 * the *package* build step with an ERR_MODULE_NOT_FOUND naming a `.vite-temp`
 * config stub — a failure that names neither the app nor a COPY line.
 */
function declaredWorkspacePackages(): string[] {
  const manifest: {
    dependencies?: Record<string, string>;
    devDependencies?: Record<string, string>;
  } = JSON.parse(readFileSync(resolve(appDir, "package.json"), "utf8")) as {
    dependencies?: Record<string, string>;
    devDependencies?: Record<string, string>;
  };

  return Object.entries({ ...manifest.dependencies, ...manifest.devDependencies })
    .filter(([name, range]) => name.startsWith(SCOPE) && range.startsWith("workspace:"))
    .map(([name]) => name.slice(SCOPE.length))
    .toSorted();
}

const packages: string[] = declaredWorkspacePackages();

/** Index of an exact `COPY <source> <destination>` line, or -1 when absent. */
function copyLineIndex(source: string, destination: string): number {
  return dockerfile.search(new RegExp(`^COPY\\s+${source}\\s+${destination}\\s*$`, "mu"));
}

const installIndex: number = dockerfile.search(/^RUN pnpm install --frozen-lockfile\s*$/mu);
const buildIndex: number = dockerfile.search(/^RUN pnpm --filter/mu);

describe("the Dockerfile mirrors this app's workspace dependencies", () => {
  it("declares at least the packages an app cannot run without", () => {
    // A guard on the guard: if the manifest read silently yielded nothing, every
    // case below would pass vacuously. `config` is in the list because it is a
    // devDependency — reading `dependencies` alone drops it and takes the whole
    // package-build hazard with it.
    expect(packages).toEqual(expect.arrayContaining(["config", "sdk", "styles", "ui"]));
  });

  it("has the install and build steps this spec positions the COPY lines against", () => {
    expect(installIndex, "no `RUN pnpm install --frozen-lockfile` line").toBeGreaterThan(-1);
    expect(buildIndex, "no `RUN pnpm --filter ... build` line").toBeGreaterThan(-1);
  });

  it.each(packages)("copies packages/%s/package.json before the install", (pkg: string) => {
    expect(existsSync(resolve(repoRoot, "packages", pkg)), `packages/${pkg} does not exist`).toBe(
      true,
    );

    const index: number = copyLineIndex(`packages/${pkg}/package.json`, `packages/${pkg}/`);

    expect(index, `Dockerfile has no manifest COPY for packages/${pkg}`).toBeGreaterThan(-1);
    expect(index).toBeLessThan(installIndex);
  });

  it.each(packages)("copies the packages/%s sources before the build", (pkg: string) => {
    const index: number = copyLineIndex(`packages/${pkg}`, `packages/${pkg}`);

    expect(index, `Dockerfile has no source COPY for packages/${pkg}`).toBeGreaterThan(-1);
    expect(index).toBeGreaterThan(installIndex);
    expect(index).toBeLessThan(buildIndex);
  });
});
