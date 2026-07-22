import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Route-tree codegen contract for wallow-auth (Wallow-w6s6.1.2, T1.2).
 *
 * T1.1 wired the `tanstackRouter()` Vite plugin into web-shell's client/dev
 * seams; this task adds the standalone CLI half so the tree can be regenerated
 * and drift-checked without booting Vite: a per-app `tsr.config.json`, a
 * `routes:generate` script, the `@tanstack/router-cli` devDependency, and the
 * committed `src/routeTree.gen.ts` the generator emits.
 *
 * The acceptance criterion is that `routes:generate` is deterministic (a second
 * run produces no git diff). Determinism hinges on the CLI config MIRRORING the
 * T1.1 plugin options exactly — if `tsr.config.json` and the plugin disagree on
 * routesDirectory / generatedRouteTree / target / autoCodeSplitting, the CLI and
 * the plugin fight and the tree churns. These assertions pin that alignment plus
 * the presence of the generator's committed output.
 *
 * Everything is read through `fs` (never a static import) so tsc does not try to
 * resolve `routeTree.gen.ts` before the generator has produced it — the
 * assertion that it exists is the point.
 */

// apps/wallow-auth/src -> app root (src -> wallow-auth).
const appRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function readAppFile(relativePath: string): string {
  return readFileSync(resolve(appRoot, relativePath), "utf8");
}

interface PackageJson {
  scripts?: Record<string, string>;
  devDependencies?: Record<string, string>;
}

interface TsrConfig {
  target?: string;
  routesDirectory?: string;
  generatedRouteTree?: string;
  autoCodeSplitting?: boolean;
}

function readPackageJson(): PackageJson {
  return JSON.parse(readAppFile("package.json")) as PackageJson;
}

function readTsrConfig(): TsrConfig {
  return JSON.parse(readAppFile("tsr.config.json")) as TsrConfig;
}

describe("wallow-auth tsr.config.json (CLI codegen config)", () => {
  it("exists at the app root", () => {
    expect(existsSync(resolve(appRoot, "tsr.config.json"))).toBe(true);
  });

  it("mirrors the T1.1 plugin's routesDirectory and generatedRouteTree", () => {
    const config: TsrConfig = readTsrConfig();
    expect(config.routesDirectory).toBe("./src/routes");
    expect(config.generatedRouteTree).toBe("./src/routeTree.gen.ts");
  });

  it("mirrors the T1.1 plugin's react target and autoCodeSplitting=false", () => {
    const config: TsrConfig = readTsrConfig();
    // Same target + code-splitting decision the plugin uses, so the CLI and the
    // plugin emit an identical tree (route-based splitting is an explicit
    // non-goal per the F1 plan).
    expect(config.target).toBe("react");
    expect(config.autoCodeSplitting).toBe(false);
  });
});

describe("wallow-auth package.json (routes:generate script + CLI dep)", () => {
  it("adds a routes:generate script that runs the tsr generator", () => {
    const pkg: PackageJson = readPackageJson();
    expect(pkg.scripts?.["routes:generate"]).toBeDefined();
    expect(pkg.scripts?.["routes:generate"]).toMatch(/\btsr\s+generate\b/u);
  });

  it("declares @tanstack/router-cli on the plugin's bundled generator line (1.167.x)", () => {
    const pkg: PackageJson = readPackageJson();
    const cliRange: string | undefined = pkg.devDependencies?.["@tanstack/router-cli"];
    expect(cliRange).toBeDefined();
    expect(cliRange).toMatch(/1\.167\./u);
  });
});

describe("wallow-auth src/routeTree.gen.ts (committed generator output)", () => {
  it("is committed to the working tree", () => {
    expect(existsSync(resolve(appRoot, "src", "routeTree.gen.ts"))).toBe(true);
  });

  it("is real generated output that exports the routeTree", () => {
    // Proves the generator actually ran (not an empty placeholder) — the tree
    // the simplified router.tsx (T1.3) will import.
    const generated: string = readAppFile("src/routeTree.gen.ts");
    expect(generated).toMatch(/export const routeTree\b/u);
  });
});
