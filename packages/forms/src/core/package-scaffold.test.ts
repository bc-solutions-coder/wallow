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
// right dependency/peer split (react and react-dom are the HOST's to supply;
// sdk, ui, query, react-form and zod are this package's own), the
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

/**
 * Reads one entry out of a named catalog in `pnpm-workspace.yaml`.
 *
 * Shared version ranges live in catalogs, so a manifest assertion that stopped at
 * `"catalog:react"` would assert the indirection and nothing about the version.
 * This follows the ref so the range stays pinned by a test.
 *
 * A regex rather than a YAML parser: this package has no yaml dependency and
 * should not grow one for two lines, and the same read-the-artifact-off-disk
 * idiom is what `docker-workspace-copies.test.ts` already does.
 */
function catalogEntry(catalog: string, packageName: string): string | undefined {
  const workspace: string = readFileSync(join(repoRoot, "pnpm-workspace.yaml"), "utf8");
  const blockPattern = new RegExp(String.raw`^  ${catalog}:\n((?:^ {4}.*\n|^\s*\n)*)`, "mu");
  const block: string = blockPattern.exec(workspace)?.[1] ?? "";
  const entryPattern = new RegExp(
    String.raw`^ {4}"?${packageName.replaceAll("/", String.raw`\/`)}"?:\s*(\S+)\s*$`,
    "mu",
  );

  return entryPattern.exec(block)?.[1];
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

  it("takes sdk, ui, query, react-form and zod as regular dependencies", () => {
    const pkg = readPackageJson();
    const deps = pkg.dependencies as Record<string, string> | undefined;

    expect(deps).toBeDefined();
    // Implementation details of this package: the catalog imports them, apps
    // never have to. Contrast the peers below, which the host app owns.
    expect(deps?.["@bc-solutions-coder/sdk"]).toBe("workspace:*");
    expect(deps?.["@bc-solutions-coder/ui"]).toBe("workspace:*");
    // TanStack Query arrives ONLY through the workspace facade — see the peer
    // rationale below for why that is a regular dependency and not a peer.
    expect(deps?.["@bc-solutions-coder/query"]).toBe("workspace:*");
    // react-form's range lives in the `react` catalog in pnpm-workspace.yaml, so
    // this manifest names the catalog rather than a literal. Follow the
    // indirection instead of just accepting the ref — otherwise "on v1" stops
    // being asserted anywhere and a major bump lands silently.
    expect(deps?.["@tanstack/react-form"]).toBe("catalog:react");
    expect(catalogEntry("react", "@tanstack/react-form")).toMatch(/^\^1\./u);
    // zod v4 — the validation library the catalog's schemas are written against.
    expect(deps?.zod).toMatch(/^\^4\./u);
  });

  it("leaves react and react-dom as the only peer dependencies", () => {
    const pkg = readPackageJson();
    const peers = pkg.peerDependencies as Record<string, string> | undefined;

    // A second copy of React would break hooks, so it and react-dom must still
    // resolve to the host's single instance.
    //
    // react-query used to sit here for the same reason one rung down: a second
    // copy of the QueryClient's context detaches every `useAppForm` mutation
    // from the app's cache ("No QueryClient set"). That argument now lands on
    // @bc-solutions-coder/query instead. The facade is a private workspace
    // package that is the ONE declarer of @tanstack/react-query in the repo, so
    // a `workspace:*` dependency on it cannot fork the version the host resolves
    // the way a peer range could — every consumer is linked to the same
    // directory and therefore to the same context. Peering the facade would only
    // push a private, unpublished package onto app manifests for no gain.
    expect(Object.keys(peers ?? {}).toSorted()).toEqual(["react", "react-dom"]);
  });

  it("keeps dev copies of the peers so it can build and run its own specs", () => {
    const pkg = readPackageJson();
    const devDeps = pkg.devDependencies as Record<string, string> | undefined;

    for (const name of ["react", "react-dom"]) {
      expect(devDeps, name).toHaveProperty(name);
    }
    // The browser-mode specs render ui components, which need the fork theme,
    // and the shared vitest preset supplies the node/Chromium project pair.
    expect(devDeps?.["@bc-solutions-coder/styles"]).toBe("workspace:*");
    expect(devDeps?.["@bc-solutions-coder/testing"]).toBe("workspace:*");
  });

  it("declares @tanstack/react-query in no dependency bucket at all", () => {
    const pkg = readPackageJson();

    // The facade rule, enforced at the manifest: this package may not name
    // react-query anywhere, in any bucket. Under pnpm's strict node_modules that
    // makes a direct `from "@tanstack/react-query"` unresolvable here — the
    // declaration and the import discipline (query-facade.test.ts) are two
    // halves of the same guard, and dropping only the import would leave the
    // door open for the next contributor.
    for (const bucket of ["dependencies", "devDependencies", "peerDependencies"]) {
      const declared = pkg[bucket] as Record<string, string> | undefined;

      expect(Object.keys(declared ?? {}), bucket).not.toContain("@tanstack/react-query");
    }
  });

  it("declares its own typescript", () => {
    const pkg = readPackageJson();
    const deps = {
      ...(pkg.dependencies as Record<string, string> | undefined),
      ...(pkg.devDependencies as Record<string, string> | undefined),
    };

    expect(deps).toHaveProperty("typescript");
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

  // The build/test wiring is NOT asserted by reading `vite.config.ts` and
  // `vitest.config.ts` as text. Those regexes pinned the configs' SOURCE SHAPE —
  // a variable rename or a formatter rewrap failed them while the build still
  // worked, and consolidating the duplicated config into a shared preset was
  // impossible without editing this file first. What the build actually has to
  // do is verified where it is observable: `dist/` contents by the build-output
  // describe below, and pre-bundle entries by their resolvability against the
  // real dep graph (`browser-deps.test.ts`).

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
    "@bc-solutions-coder/query",
    "@tanstack/react-form",
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
