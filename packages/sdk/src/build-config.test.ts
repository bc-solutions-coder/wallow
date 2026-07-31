import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// packages/sdk/src -> packages/sdk
const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const viteConfigPath: string = resolve(packageRoot, "vite.config.ts");
const packageJsonPath: string = resolve(packageRoot, "package.json");

function readFile(path: string): string {
  return readFileSync(path, "utf8");
}

interface PackageJson {
  scripts?: Record<string, string>;
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
}

function readPackageJson(): PackageJson {
  return JSON.parse(readFile(packageJsonPath)) as PackageJson;
}

// These assertions lock the acceptance surface of the tsup -> Vite 8 library
// mode migration (bead Wallow-ve7q.1.1). They are static config-surface checks;
// the runtime build-output and subpath-import verification (build step 5-7) is exercised
// separately in the green/verify phase.
describe("SDK bundler migration (tsup -> Vite 8 library mode)", () => {
  it("has a vite.config.ts at the package root", () => {
    expect(existsSync(viteConfigPath)).toBe(true);
  });

  it("configures Vite library mode with both public entry points", () => {
    const config: string = readFile(viteConfigPath);
    // Vite lib build, not an app/html build.
    expect(config).toMatch(/lib\s*:/);
    // Both subpath entries the package.json exports map points at.
    expect(config).toMatch(/src\/index\.ts/);
    expect(config).toMatch(/src\/server\/index\.ts/);
    // ESM only, matching the previous tsup format: ["esm"].
    expect(config).toMatch(/formats\s*:\s*\[\s*["']es["']\s*\]/);
  });

  it("externalizes non-relative imports so deps are not bundled", () => {
    const config: string = readFile(viteConfigPath);
    expect(config).toMatch(/external/);
  });

  it("build script bundles with vite and emits declarations via tsc", () => {
    const pkg: PackageJson = readPackageJson();
    const build: string = pkg.scripts?.build ?? "";
    expect(build).toContain("vite build");
    // Declarations still come from the dedicated tsc build config.
    expect(build).toContain("tsc -p tsconfig.build.json");
  });

  it("declares vite as a devDependency", () => {
    const pkg: PackageJson = readPackageJson();
    const devDeps: Record<string, string> = pkg.devDependencies ?? {};
    expect(devDeps).toHaveProperty("vite");
  });
});
