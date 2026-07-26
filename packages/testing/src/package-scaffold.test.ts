import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Acceptance-criteria guard for Wallow-0q2s.1.1 (scaffold packages/testing).
// The package is a fork-internal build/test toolkit, so its "correctness" at the
// scaffold stage is its wiring: package.json shape, the sdk-style build/test/
// typecheck scripts, ownership of the browser-mode test deps that move out of
// each app, and the Vite-library + declaration-only tsc build tooling. These
// specs read the package's own manifest and config files off disk (mirroring the
// existing apps/wallow-auth/src/vitest-browser-mode-deps.test.ts guard pattern)
// and assert the target shape described on the bead. They intentionally fail
// until the scaffold is completed by the green phase.

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

describe("packages/testing scaffold", () => {
  it("is the private, unpublished @bc-solutions-coder/testing ESM package", () => {
    const pkg = readPackageJson();

    expect(pkg.name).toBe("@bc-solutions-coder/testing");
    expect(pkg.private).toBe(true);
    expect(pkg.type).toBe("module");
    // Fork-internal: it is never published, so it carries NO publishConfig
    // (unlike packages/sdk and packages/styles).
    expect(pkg).not.toHaveProperty("publishConfig");
  });

  it("exposes dist entry points and a single '.' export map", () => {
    const pkg = readPackageJson();

    expect(pkg.main).toBe("./dist/index.js");
    expect(pkg.module).toBe("./dist/index.js");
    expect(pkg.types).toBe("./dist/index.d.ts");

    const exportsMap = pkg.exports as Record<string, { types?: string; import?: string }>;
    expect(exportsMap).toBeDefined();
    expect(exportsMap["."]).toEqual({
      types: "./dist/index.d.ts",
      import: "./dist/index.js",
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

  it("owns the browser-mode test dependencies that move out of the apps", () => {
    const pkg = readPackageJson();
    const deps = {
      ...(pkg.dependencies as Record<string, string> | undefined),
      ...(pkg.devDependencies as Record<string, string> | undefined),
    };

    expect(deps).toHaveProperty("vitest");
    expect(deps).toHaveProperty("@vitest/browser-playwright");
    expect(deps).toHaveProperty("vitest-browser-react");
    expect(deps).toHaveProperty("playwright");
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

  it("extends the workspace base tsconfig", () => {
    const tsconfig = JSON.parse(
      readConfigText("tsconfig.json").replaceAll(/^\s*\/\/.*$/gmu, ""),
    ) as {
      extends?: string;
    };

    expect(tsconfig.extends).toBe("../../tsconfig.base.json");
  });

  it("provides a declaration-only tsconfig.build.json narrowed to the entry", () => {
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
    expect(buildConfig.exclude).toContain("**/*.test.ts");
  });

  it("provides a Vite library-mode build config", () => {
    expect(existsSync(join(packageDir, "vite.config.ts"))).toBe(true);

    const viteConfig = readConfigText("vite.config.ts");
    // Single '.' export -> one named lib entry `index` pointing at src/index.ts,
    // ES output only, bare imports externalized (mirrors packages/sdk).
    expect(viteConfig).toMatch(/lib\s*:/u);
    expect(viteConfig).toMatch(/src\/index\.ts/u);
    expect(viteConfig).toMatch(/formats\s*:\s*\[\s*["']es["']\s*\]/u);
  });

  it("has a placeholder src/index.ts entry", () => {
    expect(existsSync(join(packageDir, "src", "index.ts"))).toBe(true);
  });
});
