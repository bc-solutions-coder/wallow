import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Acceptance-criteria guard for Wallow-0q2s.8.1 (scaffold packages/web-shell).
// The package is a fork-internal standalone-host + config-preset toolkit, so its
// "correctness" at the scaffold stage is its wiring: package.json shape, the
// sdk-style build/test/typecheck scripts, and the two-entry split that mirrors
// packages/sdk (a browser-safe `.` barrel for the query-client and a Node-only
// `./server` subpath for the host/dev-server/vite-preset pieces). These specs
// read the package's own manifest and config files off disk (mirroring the
// packages/testing/src/package-scaffold.test.ts guard) and assert the target
// shape described on the bead. They intentionally fail until the scaffold is
// completed by the green phase.

const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..");

function readPackageJson(): Record<string, unknown> {
  return JSON.parse(readFileSync(join(packageDir, "package.json"), "utf8")) as Record<
    string,
    unknown
  >;
}

function readConfigText(relativePath: string): string {
  return readFileSync(join(packageDir, relativePath), "utf8");
}

describe("packages/web-shell scaffold", () => {
  it("is the private, unpublished @bc-solutions-coder/web-shell ESM package", () => {
    const pkg = readPackageJson();

    expect(pkg.name).toBe("@bc-solutions-coder/web-shell");
    expect(pkg.private).toBe(true);
    expect(pkg.type).toBe("module");
    // Fork-internal: it is never published, so it carries NO publishConfig
    // (unlike packages/sdk and packages/styles, matching packages/testing).
    expect(pkg).not.toHaveProperty("publishConfig");
  });

  it("exposes dist entry points and a two-entry '.' + './server' export map", () => {
    const pkg = readPackageJson();

    expect(pkg.main).toBe("./dist/index.js");
    expect(pkg.module).toBe("./dist/index.js");
    expect(pkg.types).toBe("./dist/index.d.ts");

    const exportsMap = pkg.exports as Record<string, { types?: string; import?: string }>;
    expect(exportsMap).toBeDefined();
    // Browser-safe barrel (the query-client lands here in 8.2).
    expect(exportsMap["."]).toEqual({
      types: "./dist/index.d.ts",
      import: "./dist/index.js",
    });
    // Node-only subpath (host/dev-server/vite-presets), named './server' to
    // match packages/sdk's node-subpath convention (dist/server/index.js).
    expect(exportsMap["./server"]).toEqual({
      types: "./dist/server/index.d.ts",
      import: "./dist/server/index.js",
    });
  });

  it("defines the sdk-style build/test/typecheck scripts", () => {
    const pkg = readPackageJson();
    const scripts = pkg.scripts as Record<string, string>;

    expect(scripts.build).toBe("vite build && tsc -p tsconfig.build.json");
    expect(scripts.test).toBe("vitest run");
    expect(scripts["test:watch"]).toBe("vitest");
    expect(scripts.typecheck).toBe("tsc --noEmit");
  });

  it("does not copy packages/sdk's TS6 typescript pin", () => {
    const pkg = readPackageJson();
    const deps = {
      ...(pkg.dependencies as Record<string, string> | undefined),
      ...(pkg.devDependencies as Record<string, string> | undefined),
    };

    expect(deps).toHaveProperty("typescript");
    // packages/sdk pins "6.0.3" only for openapi-ts; this package must not copy it.
    expect(deps.typescript).not.toBe("6.0.3");
  });

  it("carries the Vite + tsc build tooling as devDependencies", () => {
    const pkg = readPackageJson();
    const devDeps = (pkg.devDependencies ?? {}) as Record<string, string>;

    expect(devDeps).toHaveProperty("vite");
    expect(devDeps).toHaveProperty("vitest");
    expect(devDeps).toHaveProperty("@types/node");
  });

  it("extends the workspace base tsconfig", () => {
    const tsconfig = JSON.parse(
      readConfigText("tsconfig.json").replaceAll(/^\s*\/\/.*$/gmu, ""),
    ) as {
      extends?: string;
    };

    expect(tsconfig.extends).toBe("../../tsconfig.base.json");
  });

  it("provides a declaration-only tsconfig.build.json narrowed to both entries", () => {
    expect(existsSync(join(packageDir, "tsconfig.build.json"))).toBe(true);

    const buildConfig = JSON.parse(
      readConfigText("tsconfig.build.json").replaceAll(/^\s*\/\/.*$/gmu, ""),
    ) as {
      compilerOptions?: { emitDeclarationOnly?: boolean; rootDir?: string; outDir?: string };
      include?: string[];
      exclude?: string[];
    };

    expect(buildConfig.compilerOptions?.emitDeclarationOnly).toBe(true);
    expect(buildConfig.compilerOptions?.rootDir).toBe("src");
    expect(buildConfig.compilerOptions?.outDir).toBe("dist");
    expect(buildConfig.include).toContain("src/index.ts");
    expect(buildConfig.include).toContain("src/server/index.ts");
    expect(buildConfig.exclude).toContain("**/*.test.ts");
  });

  it("provides a Vite library-mode build config with both named entries", () => {
    expect(existsSync(join(packageDir, "vite.config.ts"))).toBe(true);

    const viteConfig = readConfigText("vite.config.ts");
    // Two lib entries: `index` -> src/index.ts (browser-safe barrel) and
    // `server/index` -> src/server/index.ts (Node subpath), ES output only,
    // bare imports externalized (mirrors packages/sdk).
    expect(viteConfig).toMatch(/lib\s*:/u);
    expect(viteConfig).toMatch(/src\/index\.ts/u);
    expect(viteConfig).toMatch(/src\/server\/index\.ts/u);
    expect(viteConfig).toMatch(/formats\s*:\s*\[\s*["']es["']\s*\]/u);
  });

  it("has placeholder browser-safe and Node entry files", () => {
    expect(existsSync(join(packageDir, "src", "index.ts"))).toBe(true);
    expect(existsSync(join(packageDir, "src", "server", "index.ts"))).toBe(true);
  });
});
