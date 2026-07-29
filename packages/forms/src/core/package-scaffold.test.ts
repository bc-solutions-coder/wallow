import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Acceptance-criteria guard for Wallow-ov6w.1.1 (scaffold packages/forms).
//
// packages/forms is a private, never-published workspace package that owns form
// authoring: it binds TanStack Form state onto @bc-solutions-coder/ui's
// components. At the scaffold stage it has no behaviour, so its "correctness" is
// entirely its wiring — a ui-style package.json with a single root export, the
// right dependency/peer split (react, react-dom and react-query are the HOST's
// to supply; sdk, ui, react-form and zod are this package's own), the
// workspace-baseline tsconfig pair, a Vite library-mode build reduced to the one
// barrel entry, and the shared node + headless-Chromium vitest preset with the
// pre-bundle list its future component specs need.
//
// These specs read files off disk and assert the target shape described on the
// bead. They intentionally FAIL against the red-phase placeholder scaffold.

// This guard lives at src/core/, mirroring packages/ui's, so TWO levels up
// reaches the package root — not one.
const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const repoRoot = join(packageDir, "..", "..");

function readPackageJson(): Record<string, unknown> {
  return JSON.parse(readFileSync(join(packageDir, "package.json"), "utf8")) as Record<
    string,
    unknown
  >;
}

function readConfigText(relativePath: string): string {
  return readFileSync(join(packageDir, relativePath), "utf8");
}

function stripLineComments(source: string): string {
  return source.replaceAll(/^\s*\/\/.*$/gmu, "");
}

describe("packages/forms scaffold", () => {
  it("is the private, unpublished @bc-solutions-coder/forms ESM package", () => {
    const pkg = readPackageJson();

    expect(pkg.name).toBe("@bc-solutions-coder/forms");
    expect(pkg.version).toBe("0.0.0");
    expect(pkg.private).toBe(true);
    expect(pkg.type).toBe("module");
    // Fork-internal, like packages/ui and packages/testing: never published, so
    // it carries NO publishConfig.
    expect(pkg).not.toHaveProperty("publishConfig");
  });

  it("exposes dist entry points behind a single '.' export", () => {
    const pkg = readPackageJson();

    expect(pkg.main).toBe("./dist/index.js");
    expect(pkg.module).toBe("./dist/index.js");
    expect(pkg.types).toBe("./dist/index.d.ts");

    const exportsMap = pkg.exports as Record<string, unknown> | undefined;
    expect(exportsMap).toBeDefined();
    expect(exportsMap?.["."]).toEqual({
      types: "./dist/index.d.ts",
      import: "./dist/index.js",
    });
    // Unlike packages/ui there is no per-component wildcard and no CSS asset:
    // forms exposes one curated barrel, so consumers cannot reach past it into
    // internals that the catalog is meant to hide.
    expect(Object.keys(exportsMap ?? {})).toEqual(["."]);
  });

  it("ships only dist and declares itself free of side effects", () => {
    const pkg = readPackageJson();

    expect(pkg.files).toEqual(["dist"]);
    expect(pkg.sideEffects).toBe(false);
  });

  it("defines the ui-style build/test/typecheck scripts", () => {
    const pkg = readPackageJson();
    const scripts = pkg.scripts as Record<string, string>;

    expect(scripts.build).toBe("vite build && tsc -p tsconfig.build.json");
    expect(scripts.test).toBe("vitest run");
    expect(scripts["test:watch"]).toBe("vitest");
    expect(scripts.typecheck).toBe("tsc --noEmit");
  });

  it("takes sdk, ui, react-form and zod as regular dependencies", () => {
    const pkg = readPackageJson();
    const deps = pkg.dependencies as Record<string, string> | undefined;

    expect(deps).toBeDefined();
    // Implementation details of this package: the catalog imports them, apps
    // never have to. Contrast the peers below, which the host app owns.
    expect(deps?.["@bc-solutions-coder/sdk"]).toBe("workspace:*");
    expect(deps?.["@bc-solutions-coder/ui"]).toBe("workspace:*");
    expect(deps?.["@tanstack/react-form"]).toMatch(/^\^1\./u);
    // zod v4 — the validation library the catalog's schemas are written against.
    expect(deps?.zod).toMatch(/^\^4\./u);
  });

  it("leaves react, react-dom and react-query as the only peer dependencies", () => {
    const pkg = readPackageJson();
    const peers = pkg.peerDependencies as Record<string, string> | undefined;

    // A second copy of React or of the QueryClient's context would break hooks
    // and detach every mutation from the app's cache, so all three must resolve
    // to the host's single instance.
    expect(Object.keys(peers ?? {}).toSorted()).toEqual([
      "@tanstack/react-query",
      "react",
      "react-dom",
    ]);
  });

  it("keeps dev copies of the peers so it can build and run its own specs", () => {
    const pkg = readPackageJson();
    const devDeps = pkg.devDependencies as Record<string, string> | undefined;

    for (const name of ["@tanstack/react-query", "react", "react-dom"]) {
      expect(devDeps, name).toHaveProperty(name);
    }
    // The browser-mode specs render ui components, which need the fork theme,
    // and the shared vitest preset supplies the node/Chromium project pair.
    expect(devDeps?.["@bc-solutions-coder/styles"]).toBe("workspace:*");
    expect(devDeps?.["@bc-solutions-coder/testing"]).toBe("workspace:*");
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
    expect(buildConfig.exclude).toContain("**/*.test.tsx");
  });

  it("builds the single barrel entry in Vite library mode, ES output only", () => {
    expect(existsSync(join(packageDir, "vite.config.ts"))).toBe(true);

    const viteConfig = readConfigText("vite.config.ts");
    expect(viteConfig).toMatch(/lib\s*:/u);
    expect(viteConfig).toMatch(/src\/index\.ts/u);
    expect(viteConfig).toMatch(/formats\s*:\s*\[\s*["']es["']\s*\]/u);
    // preserveModules keeps dist/ mirroring src/ so a consuming bundler can drop
    // catalog fields the app never imports.
    expect(viteConfig).toMatch(/preserveModules\s*:\s*true/u);
    expect(viteConfig).toMatch(/preserveModulesRoot\s*:\s*["']src["']/u);
  });

  it("externalizes every non-relative import from the library build", () => {
    const viteConfig = readConfigText("vite.config.ts");

    // react/react-dom/react-query are peers and ui/sdk are workspace packages the
    // app already has: bundling any of them in would duplicate a runtime.
    expect(viteConfig).toMatch(/external\s*:/u);
    expect(viteConfig).toMatch(/startsWith\(["']\.["']\)/u);
  });

  it("declares no per-component entries — the barrel is the only entry", () => {
    const viteConfig = readConfigText("vite.config.ts");

    // packages/ui's config enumerates src/components/<name>/index.ts as extra
    // entries to back its "./*" subpath export. forms has no such export, so
    // that helper must be DELETED rather than carried over from the template.
    expect(viteConfig).not.toMatch(/componentEntries/u);
    expect(viteConfig).not.toMatch(/src\/components/u);
  });

  it("uses the shared createVitestProjects preset for browser-mode specs", () => {
    const vitestConfig = readConfigText("vitest.config.ts");

    // The catalog's specs render real ui components, so this package needs the
    // node + headless-Chromium split from @bc-solutions-coder/testing (like
    // apps/wallow-auth), NOT a bare node-only environment.
    expect(vitestConfig).toMatch(/@bc-solutions-coder\/testing/u);
    expect(vitestConfig).toMatch(/createVitestProjects/u);
    expect(vitestConfig).toMatch(/projects\s*:\s*\[\s*node\s*,\s*browser\s*\]/u);
  });

  it("pre-bundles the ui, form and validation runtimes for the browser project", () => {
    const vitestConfig = readConfigText("vitest.config.ts");

    expect(vitestConfig).toMatch(/extraBrowserOptimizeDeps/u);
    // Left to on-the-fly discovery, Vite pre-bundles a Base UI subpath into a
    // chunk carrying its own copy of React and the first spec that renders the
    // part dies on "Cannot read properties of null (reading 'useRef')" — so
    // every subpath the rendered ui components import has to be listed, exactly
    // as packages/ui/vitest.config.ts does.
    expect(vitestConfig).toMatch(/@base-ui\/react\/field/u);
    expect(vitestConfig).toMatch(/@base-ui\/react\/input/u);
    // Same failure mode for this package's own runtime deps: a mid-run reload
    // after the first import drops the test runner.
    for (const dep of ["@tanstack/react-form", "@tanstack/react-query", "zod"]) {
      expect(vitestConfig, dep).toMatch(
        new RegExp(`["']${dep.replaceAll("/", String.raw`\/`)}["']`, "u"),
      );
    }
  });

  it("has a placeholder src/index.ts barrel", () => {
    expect(existsSync(join(packageDir, "src", "index.ts"))).toBe(true);
  });

  it("carries ui's oxlint override so catalog fields may spread passthrough props", () => {
    const oxlintrcPath = join(packageDir, ".oxlintrc.json");
    expect(existsSync(oxlintrcPath)).toBe(true);

    // Every catalog field forwards the caller's remaining props onto the ui
    // component it wraps; that spread is the point of the layer.
    const oxlintrc = JSON.parse(readFileSync(oxlintrcPath, "utf8")) as {
      rules?: Record<string, unknown>;
    };
    expect(oxlintrc.rules?.["react/jsx-props-no-spreading"]).toBe("off");
  });

  it("stays out of the check:exports gate, like packages/ui", () => {
    const checkExports = readFileSync(join(repoRoot, "scripts", "check-exports.sh"), "utf8");

    // That gate runs publint + attw over the PUBLISHED packages (sdk, styles,
    // testing). forms is private and ships no tarball, so adding it there would
    // fail the gate on a package no consumer can install.
    expect(checkExports).not.toMatch(/packages\/forms/u);
  });
});

describe("packages/forms installed dependencies", () => {
  // Guards the install itself, not just the declaration: pnpm links the resolved
  // copies into this package's own node_modules, so these only pass once
  // `pnpm install` has actually run against the finished manifest.
  const INSTALLED = [
    "@bc-solutions-coder/sdk",
    "@bc-solutions-coder/ui",
    "@tanstack/react-form",
    "@tanstack/react-query",
    "react",
    "react-dom",
    "zod",
  ];

  it("resolves every declared runtime and peer dependency", () => {
    const missing = INSTALLED.filter(
      (name) => !existsSync(join(packageDir, "node_modules", name, "package.json")),
    );

    expect(missing).toEqual([]);
  });

  it("resolves zod on the v4 line", () => {
    const manifestPath = join(packageDir, "node_modules", "zod", "package.json");
    expect(existsSync(manifestPath)).toBe(true);

    const installed = JSON.parse(readFileSync(manifestPath, "utf8")) as { version: string };
    expect(installed.version).toMatch(/^4\./u);
  });
});

describe("packages/forms build output", () => {
  // `dist/` is a build artifact and `pnpm check` runs `test` BEFORE `build`, so
  // a fresh clone has none when these execute. Skipped rather than failed in
  // that case: run `pnpm --filter @bc-solutions-coder/forms build` to arm them.
  const distDir = join(packageDir, "dist");
  const distIsMissing = !existsSync(join(distDir, "index.js"));

  it.skipIf(distIsMissing)("emits the barrel entry the exports map points at", () => {
    expect(existsSync(join(distDir, "index.js"))).toBe(true);
    expect(existsSync(join(distDir, "index.d.ts"))).toBe(true);
  });
});
