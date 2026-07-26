import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Acceptance-criteria guard for Wallow-0q2s.6.1 (scaffold packages/ui + Tailwind
// @source wiring). The package is a private, browser-only React component
// library consumed by the apps, so its "correctness" at the scaffold stage is
// its wiring: an sdk-style package.json shape (with react/react-dom peer deps
// this time), the './source.css' passthrough export + `files` entry that ships
// the Tailwind @source declaration, the workspace-baseline tsconfig pair, a Vite
// library-mode build, the browser-capable vitest preset, a placeholder barrel,
// and — critically — the one @import line added to BOTH apps' CSS entries so
// Tailwind v4 scans ui's component sources.
//
// These specs read files off disk (mirroring packages/testing/src/
// package-scaffold.test.ts) and assert the target shape described on the bead.
// They intentionally FAIL until the green phase completes the scaffold.

const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = join(packageDir, "..", "..");

function readPackageJson(): Record<string, unknown> {
  return JSON.parse(readFileSync(join(packageDir, "package.json"), "utf8")) as Record<
    string,
    unknown
  >;
}

function readText(absolutePathParts: string[]): string {
  return readFileSync(join(...absolutePathParts), "utf8");
}

function readConfigText(relativePath: string): string {
  return readFileSync(join(packageDir, relativePath), "utf8");
}

function stripLineComments(source: string): string {
  return source.replaceAll(/^\s*\/\/.*$/gmu, "");
}

describe("packages/ui scaffold", () => {
  it("is the private, unpublished @bc-solutions-coder/ui ESM package", () => {
    const pkg = readPackageJson();

    expect(pkg.name).toBe("@bc-solutions-coder/ui");
    expect(pkg.private).toBe(true);
    expect(pkg.type).toBe("module");
    // Fork-internal component library: never published (like packages/testing),
    // so it carries NO publishConfig.
    expect(pkg).not.toHaveProperty("publishConfig");
  });

  it("exposes dist entry points plus '.' and './source.css' exports", () => {
    const pkg = readPackageJson();

    expect(pkg.main).toBe("./dist/index.js");
    expect(pkg.module).toBe("./dist/index.js");
    expect(pkg.types).toBe("./dist/index.d.ts");

    const exportsMap = pkg.exports as Record<string, unknown>;
    expect(exportsMap).toBeDefined();
    expect(exportsMap["."]).toEqual({
      types: "./dist/index.d.ts",
      import: "./dist/index.js",
    });
    // Raw file passthrough (no types/import keys) mirroring packages/styles'
    // './styles.css' export — this is what apps import for Tailwind @source.
    expect(exportsMap["./source.css"]).toBe("./source.css");
  });

  it("ships dist and the source.css asset via the files array", () => {
    const pkg = readPackageJson();
    const files = pkg.files as string[] | undefined;

    expect(files).toBeDefined();
    expect(files).toContain("dist");
    expect(files).toContain("source.css");
  });

  it("declares react and react-dom as peer dependencies with matching dev deps", () => {
    const pkg = readPackageJson();
    const peers = pkg.peerDependencies as Record<string, string> | undefined;
    const devDeps = pkg.devDependencies as Record<string, string> | undefined;

    expect(peers).toBeDefined();
    expect(peers).toHaveProperty("react");
    expect(peers).toHaveProperty("react-dom");
    // The apps supply react at runtime; the package still needs it locally to
    // build and to run its own browser-mode component specs.
    expect(devDeps).toHaveProperty("react");
    expect(devDeps).toHaveProperty("react-dom");
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

  it("extends the workspace base tsconfig", () => {
    const tsconfig = JSON.parse(stripLineComments(readConfigText("tsconfig.json"))) as {
      extends?: string;
    };

    expect(tsconfig.extends).toBe("../../tsconfig.base.json");
  });

  it("provides a declaration-only tsconfig.build.json narrowed to the entry", () => {
    expect(existsSync(join(packageDir, "tsconfig.build.json"))).toBe(true);

    const buildConfig = JSON.parse(stripLineComments(readConfigText("tsconfig.build.json"))) as {
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
    expect(viteConfig).toMatch(/lib\s*:/u);
    expect(viteConfig).toMatch(/src\/index\.ts/u);
    expect(viteConfig).toMatch(/formats\s*:\s*\[\s*["']es["']\s*\]/u);
  });

  it("uses the shared createVitestProjects preset for browser-mode specs", () => {
    const vitestConfig = readConfigText("vitest.config.ts");
    // ui's own specs render real components, so it must adopt the node +
    // headless-Chromium split from @bc-solutions-coder/testing (like
    // apps/wallow-auth), NOT a bare node-only environment.
    expect(vitestConfig).toMatch(/@bc-solutions-coder\/testing/u);
    expect(vitestConfig).toMatch(/createVitestProjects/u);
  });

  it("has a placeholder src/index.ts barrel", () => {
    expect(existsSync(join(packageDir, "src", "index.ts"))).toBe(true);
  });

  it('ships a root source.css declaring @source "./src" relative to itself', () => {
    const sourceCssPath = join(packageDir, "source.css");
    expect(existsSync(sourceCssPath)).toBe(true);

    const sourceCss = readFileSync(sourceCssPath, "utf8");
    // Tailwind v4 resolves @source relative to the declaring stylesheet, so this
    // MUST be the package-root source.css pointing at ./src (mirrors how
    // packages/styles ships styles.css at package root).
    expect(sourceCss).toMatch(/@source\s+["']\.\/src["']/u);
  });

  it("is imported by both apps' Tailwind CSS entries so ui sources are scanned", () => {
    const importLine = /@import\s+["']@bc-solutions-coder\/ui\/source\.css["']/u;

    const authStyles = readText([repoRoot, "apps", "wallow-auth", "src", "styles.css"]);
    const webStyles = readText([repoRoot, "apps", "wallow-web", "src", "styles.css"]);

    expect(authStyles).toMatch(importLine);
    expect(webStyles).toMatch(importLine);
  });
});
