import { existsSync, readFileSync, readdirSync } from "node:fs";
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
// These specs read files off disk and assert the target shape described on the
// bead. They intentionally FAIL until the green phase completes the scaffold.

// This guard lives at src/core/ (moved there by Wallow-m5aq.1.3 so the whole
// package follows the core/ + components/ layering it asserts), hence TWO levels
// up to the package root — not one.
const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
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
    // `private: true` is what makes it unpublished, and it is the only thing
    // asserted here. There used to be a second assertion that no
    // `publishConfig` existed at all; the package now carries
    // `publishConfig.exports`, deliberately and in company with all six other
    // workspace packages. In-repo every `exports` entry points at `src/` so
    // consumers resolve from source with no prebuilt `dist/`, and the `dist/`
    // map is the publish-time view. Carrying it uniformly — even on the private
    // members that will never use it — is what stops a package that later drops
    // `private` from publishing a manifest pointing at TypeScript sources.
    expect(pkg.private).toBe(true);
    expect(pkg.type).toBe("module");
  });

  it("exports source.css as a raw file passthrough", () => {
    const pkg = readPackageJson();
    const exportsMap = pkg.exports as Record<string, unknown>;

    // Raw file passthrough (no types/import keys) mirroring packages/styles'
    // './styles.css' export — this is what apps import for Tailwind @source.
    // It is an ASSET, so it must never acquire a conditions object, whatever the
    // code entries around it resolve to.
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

  it("defines the sdk-style test/typecheck scripts", () => {
    const pkg = readPackageJson();
    const scripts = pkg.scripts as Record<string, string>;

    // Containment rather than exact strings: which runner runs is the contract,
    // its flags are not. This package's scripts carry `--configLoader runner`
    // because `vitest.config.ts` imports `@bc-solutions-coder/testing`, which
    // now resolves to TypeScript source — Vite's default config loader
    // externalizes bare specifiers to Node's ESM resolver, which cannot read it.
    expect(scripts.test).toContain("vitest run");
    expect(scripts["test:watch"]).toContain("vitest");
    expect(scripts.typecheck).toBe("tsc --noEmit");
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

  // The build's SHAPE is not asserted here any more — not the lib-mode regexes
  // over `vite.config.ts`, not the `tsconfig.build.json` field values, not the
  // exact `scripts.build` string. Those pinned the config's source text in the
  // repo's largest package, so every behaviour-preserving change to it — a
  // shared lib-mode helper, a shared declaration-build base — had to edit this
  // file first. `dist-structure.test.ts` asserts the same thing where it is
  // observable: the built artifact.

  it("has a placeholder src/index.ts barrel", () => {
    expect(existsSync(join(packageDir, "src", "index.ts"))).toBe(true);
  });

  it("ships a root source.css declaring its @source scan relative to itself", () => {
    const sourceCssPath = join(packageDir, "source.css");
    expect(existsSync(sourceCssPath)).toBe(true);

    const sourceCss = readFileSync(sourceCssPath, "utf8");
    // Tailwind v4 resolves @source relative to the declaring stylesheet, so this
    // MUST be the package-root source.css pointing into its own ./src (mirrors
    // how packages/styles ships styles.css at package root). The EXACT globs are
    // pinned by the layering block below, which narrows this scan to
    // src/components once the folder migration lands.
    expect(sourceCss).toMatch(/@source\s+["']\.\/src/u);
  });

  it("is imported by both apps' Tailwind CSS entries so ui sources are scanned", () => {
    const importLine = /@import\s+["']@bc-solutions-coder\/ui\/source\.css["']/u;

    // Both apps are zoned, so the single Tailwind entry lives in the `app` zone
    // beside the root route that imports it for side effects.
    const authStyles = readText([repoRoot, "apps", "wallow-auth", "src", "app", "styles.css"]);
    const webStyles = readText([repoRoot, "apps", "wallow-web", "src", "app", "styles.css"]);

    expect(authStyles).toMatch(importLine);
    expect(webStyles).toMatch(importLine);
  });
});

// Acceptance-criteria guard for Wallow-m5aq.1.1 (Base UI + CVA rebuild, phase 0).
// @base-ui/react, class-variance-authority and tailwind-merge are implementation
// details of this library: components import them, apps never do. That makes them
// regular `dependencies`, unlike react/react-dom/@tanstack/react-router which the
// host app owns and must therefore stay peers (a second copy of React or the
// router would break hooks/context). These specs read package.json plus the
// installed node_modules copy off disk, so they only pass once the deps are both
// declared AND actually resolved by pnpm.

const RUNTIME_DEPENDENCIES = ["@base-ui/react", "class-variance-authority", "tailwind-merge"];

describe("packages/ui runtime dependencies", () => {
  it("declares base-ui, cva and tailwind-merge as regular dependencies", () => {
    const pkg = readPackageJson();
    const deps = pkg.dependencies as Record<string, string> | undefined;

    expect(deps).toBeDefined();
    for (const name of RUNTIME_DEPENDENCIES) {
      expect(deps).toHaveProperty(name);
    }
  });

  it("keeps the runtime deps out of peer and dev dependencies", () => {
    const pkg = readPackageJson();
    const peers = pkg.peerDependencies as Record<string, string>;
    const devDeps = pkg.devDependencies as Record<string, string>;

    for (const name of RUNTIME_DEPENDENCIES) {
      expect(peers).not.toHaveProperty(name);
      expect(devDeps).not.toHaveProperty(name);
    }
  });

  it("leaves react, react-dom and the router as the only peer dependencies", () => {
    const pkg = readPackageJson();
    const peers = pkg.peerDependencies as Record<string, string>;

    expect(Object.keys(peers).toSorted()).toEqual(["@tanstack/react-router", "react", "react-dom"]);
  });

  it("resolves an installed @base-ui/react on the v1 line", () => {
    // Guards the install itself, not just the declaration: pnpm links the
    // resolved copy into this package's own node_modules.
    //
    // Major line only. Pinning the MINOR here (and the matching `^1.6.` range
    // above) meant a routine Base UI minor bump failed a unit test in this
    // package; v1 is where the API compatibility this library depends on lives.
    const installedManifest = join(packageDir, "node_modules", "@base-ui", "react", "package.json");
    expect(existsSync(installedManifest)).toBe(true);

    const installed = JSON.parse(readFileSync(installedManifest, "utf8")) as { version: string };
    expect(installed.version).toMatch(/^1\./u);
  });

  it("resolves installed copies of class-variance-authority and tailwind-merge", () => {
    for (const name of ["class-variance-authority", "tailwind-merge"]) {
      expect(existsSync(join(packageDir, "node_modules", name, "package.json"))).toBe(true);
    }
  });
});

// Acceptance-criteria guard for Wallow-m5aq.1.3 (migrate the existing flat
// components into per-component folders).
//
// Task 1.3 is a mechanical move with zero behavior change, so it has two halves
// and two guards. The BEHAVIOR half — that the public API survives the barrel
// rewrite, and that every component still does what its co-located spec says —
// is pinned by `src/index.test.ts` plus the twelve component specs riding along
// with their components. The STRUCTURE half is this block: it encodes the
// layering the whole @bc-solutions-coder/ui rebuild depends on, so that phases
// 1+ (37 Base UI + CVA components) cannot quietly regress to flat files.
//
// The layering, per the epic:
//
//   src/core/       layer 0 — cn.ts and friends; imports NOTHING from components
//   src/components/<name>/   layer 1 — one folder per component, each with an
//                            index.ts declaring the folder's public surface
//   src/index.ts    the root barrel — re-exports the folders, nothing else
//
// These specs read the tree off disk rather than importing it, so they describe
// the shape a reviewer can see in `ls`, and they FAIL against the pre-migration
// flat layout by design.

/**
 * Every component that must end up in its own folder, sorted. This is the
 * package's catalog: the same list has to be the folder set on disk, the set of
 * specifiers the root barrel re-exports, and (via package.json's "./*" wildcard)
 * the set of importable subpaths. Each wave of the Base UI rebuild grows it
 * once, at the wave gate, together with `src/index.ts` and `src/index.test.ts` —
 * those three move as a unit, because each of them asserts the set exactly.
 */
const COMPONENT_FOLDERS = [
  "accordion",
  "alert-dialog",
  "autocomplete",
  "avatar",
  "badge",
  "button",
  "card",
  "centered-card-layout",
  "checkbox",
  "checkbox-group",
  "collapsible",
  "combobox",
  "context-menu",
  "dialog",
  "document-styles",
  "drawer",
  "empty-state",
  "error-banner",
  "field",
  "fieldset",
  "focus-on-navigate",
  "fork-attribution",
  "form",
  "input",
  "label",
  "list-card",
  "list-row",
  "menu",
  "menubar",
  "meter",
  "muted-text",
  "navigation-menu",
  "number-field",
  "otp-field",
  "page-container",
  "page-header",
  "popover",
  "preview-card",
  "progress",
  "radio",
  "radio-group",
  "ready-indicator",
  "scroll-area",
  "select",
  "separator",
  "simple-select",
  "slider",
  "switch",
  "tabs",
  "text",
  "textarea",
  "theme-provider",
  "theme-toggle",
  "toast",
  "toggle",
  "toggle-group",
  "toolbar",
  "tooltip",
];

/**
 * Directories under src/components that are test-run artifacts rather than
 * components. Vitest browser mode drops failure screenshots into
 * `__screenshots__/`, which is gitignored — so a red run must not also change
 * what the folder-set assertions below see.
 */
const ARTIFACT_DIRECTORIES = new Set(["__screenshots__"]);

const srcDir = join(packageDir, "src");
const componentsDir = join(srcDir, "components");
const coreDir = join(srcDir, "core");

/** `readdirSync` that yields nothing instead of throwing on a missing directory. */
function readEntries(directory: string): { name: string; isDirectory: boolean; isFile: boolean }[] {
  if (!existsSync(directory)) {
    return [];
  }

  return readdirSync(directory, { withFileTypes: true }).map((entry) => ({
    name: entry.name,
    isDirectory: entry.isDirectory(),
    isFile: entry.isFile(),
  }));
}

/** The component folders on disk, artifact directories excluded, unsorted. */
function componentFolderNames(): string[] {
  return readEntries(componentsDir)
    .filter((entry) => entry.isDirectory && !ARTIFACT_DIRECTORIES.has(entry.name))
    .map((entry) => entry.name);
}

/**
 * Every module specifier a source file imports or re-exports: both
 * `... from "x"` (import/export) and bare side-effect `import "x"`.
 */
function moduleSpecifiers(source: string): string[] {
  const specifiers: string[] = [];

  for (const match of source.matchAll(/(?:\bfrom|^\s*import)\s+["']([^"']+)["']/gmu)) {
    const specifier = match[1];
    if (specifier !== undefined) {
      specifiers.push(specifier);
    }
  }

  return specifiers;
}

describe("packages/ui component layering", () => {
  it("gives every component its own src/components/<name>/ folder", () => {
    const folders = componentFolderNames().toSorted();

    // Exact set, both directions: nothing left behind flat, nothing invented.
    expect(folders).toEqual(COMPONENT_FOLDERS);
  });

  it("keeps each component's implementation and co-located spec inside its folder", () => {
    for (const name of COMPONENT_FOLDERS) {
      // The spec moves WITH the component — every component keeps a co-located
      // *.test.tsx (packages/ui/CLAUDE.md), and those twelve specs are what
      // prove this migration changed no behavior.
      expect(existsSync(join(componentsDir, name, `${name}.tsx`)), `${name}.tsx`).toBe(true);
      expect(existsSync(join(componentsDir, name, `${name}.test.tsx`)), `${name}.test.tsx`).toBe(
        true,
      );
    }
  });

  it("gives every directory under src/components an index.ts", () => {
    // Written against whatever is on disk, not against COMPONENT_FOLDERS, so it
    // keeps holding for the 37-component catalog the later phases add.
    const missing = componentFolderNames().filter(
      (name) => !existsSync(join(componentsDir, name, "index.ts")),
    );

    expect(missing).toEqual([]);
  });

  it("leaves no .tsx file directly under src/", () => {
    // src/ holds only the barrel and the layer directories after the migration;
    // a stray flat component here is the exact regression this rule prevents.
    const strays = readEntries(srcDir)
      .filter((entry) => entry.isFile && entry.name.endsWith(".tsx"))
      .map((entry) => entry.name)
      .toSorted();

    expect(strays).toEqual([]);
  });

  it("re-exports every component folder — and only folders — from the root barrel", () => {
    const barrel = readFileSync(join(srcDir, "index.ts"), "utf8");
    const specifiers = moduleSpecifiers(barrel).toSorted();

    expect(specifiers).toEqual(COMPONENT_FOLDERS.map((name) => `./components/${name}`));
  });

  it("keeps src/core free of any import from src/components", () => {
    // Layer 0 imports nothing from layer 1. Test files are excluded: this guard
    // itself names component paths as DATA, and a spec asserting on the tree is
    // not a production dependency.
    const offenders: string[] = [];

    const authored = readEntries(coreDir).filter(
      (entry) => entry.isFile && !/\.test\.tsx?$/u.test(entry.name),
    );

    for (const entry of authored) {
      const source = readFileSync(join(coreDir, entry.name), "utf8");
      for (const specifier of moduleSpecifiers(source)) {
        if (specifier.startsWith("../components")) {
          offenders.push(`${entry.name} -> ${specifier}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("narrows the Tailwind @source scan to component sources only", () => {
    const sourceCss = readFileSync(join(packageDir, "source.css"), "utf8");

    // Once components live in folders the scan can target them precisely, and
    // must then EXCLUDE story and spec files: their class names are demo-only,
    // and letting Tailwind see them makes both apps emit utilities no shipped
    // component uses.
    expect(sourceCss).toMatch(/@source\s+["']\.\/src\/components\/\*\*\/\*\.tsx["']\s*;/u);
    // The recipe files count as component sources too. Since the CVA rebuild
    // most class names live in *.styles.ts rather than in JSX, and scanning only
    // .tsx silently emitted nothing for them — Switch's h-6/w-11 reached the app
    // as class attributes with no matching CSS rule behind them.
    expect(sourceCss).toMatch(/@source\s+["']\.\/src\/components\/\*\*\/\*\.styles\.ts["']\s*;/u);
    expect(sourceCss).toMatch(
      /@source\s+not\s+["']\.\/src\/components\/\*\*\/\*\.stories\.tsx["']\s*;/u,
    );
    expect(sourceCss).toMatch(
      /@source\s+not\s+["']\.\/src\/components\/\*\*\/\*\.test\.tsx["']\s*;/u,
    );
    // The pre-migration whole-directory scan must be gone, not merely added to.
    expect(sourceCss).not.toMatch(/@source\s+["']\.\/src["']\s*;/u);
  });
});

// Acceptance-criteria guard for Wallow-m5aq.1.4 (preserveModules build +
// wildcard subpath exports).
//
// Task 1.3 gave every component its own source folder; this task makes that
// structure survive the build and become addressable. Two declarations do the
// work, and this block pins both:
//
//   vite.config.ts   preserveModules — emit one module per source file, so
//                    dist/ mirrors src/ instead of collapsing into one chunk
//   package.json     exports "./*" — map @bc-solutions-coder/ui/<name> onto the
//                    per-component module that build now produces
//
// The BUILT artifact those declarations produce is asserted separately in
// `dist-structure.test.ts`, which needs `dist/` on disk and skips without it.
// These specs read config files, so they run on every `pnpm test`.

describe("packages/ui subpath exports", () => {
  it("declares the package free of side effects so apps can tree-shake it", () => {
    const pkg = readPackageJson();

    // Per-component modules are only worth emitting if a bundler is allowed to
    // drop the ones an app never imports. Every component here is a pure
    // function of its props with no module-scope work, so the whole package
    // qualifies — `false`, not a narrowing array.
    expect(pkg.sideEffects).toBe(false);
  });

  it("reaches every component through one wildcard subpath", () => {
    const pkg = readPackageJson();
    const exportsMap = pkg.exports as Record<string, unknown>;

    // One wildcard entry instead of 58 hand-maintained subpaths: adding a
    // component never edits package.json. The TARGETS are deliberately not
    // asserted — where a subpath resolves to is the build's business, and
    // `dist-structure.test.ts` proves the wildcard actually resolves by
    // importing through Node's own resolver.
    expect(Object.keys(exportsMap).toSorted()).toEqual([".", "./*", "./source.css"]);
  });
});

// Acceptance-criteria guard for Wallow-m5aq.1.5 (Storybook 10), the half that
// belongs to THIS file: what adding Storybook is allowed to do to the package's
// dependency graph.
//
// Storybook's preview needs the fork's real theme tokens, which live in
// @bc-solutions-coder/styles — so ui gains a dependency on styles for the first
// time. That edge must stay confined to `.storybook/`. The direction of the
// relationship is the whole reason `./source.css` exists (packages/ui/CLAUDE.md:
// "ui depends on @bc-solutions-coder/styles conceptually, never the reverse"),
// and a single `import "@bc-solutions-coder/styles"` inside src/ would invert
// it: every consuming app would then pull the styles package through the
// component library instead of importing it itself, and the two would race to
// define the same tokens.
//
// The rest of the Storybook wiring — config files, the third Vitest project,
// the install — is pinned in `storybook-setup.test.ts`.

const STYLES_PACKAGE = "@bc-solutions-coder/styles";

/** Every non-spec `.ts`/`.tsx` source file under `src/`, recursively. */
function sourceFilesUnder(directory: string, prefix: string = ""): string[] {
  const found: string[] = [];

  for (const entry of readEntries(directory)) {
    const relativePath = prefix === "" ? entry.name : `${prefix}/${entry.name}`;

    if (entry.isDirectory) {
      found.push(...sourceFilesUnder(join(directory, entry.name), relativePath));
    } else if (entry.isFile && /\.tsx?$/u.test(entry.name) && !/\.test\.tsx?$/u.test(entry.name)) {
      found.push(relativePath);
    }
  }

  return found.toSorted();
}

describe("packages/ui styles dependency", () => {
  it("takes @bc-solutions-coder/styles as a dev dependency only", () => {
    const pkg = readPackageJson();
    const devDeps = pkg.devDependencies as Record<string, string> | undefined;
    const deps = pkg.dependencies as Record<string, string>;
    const peers = pkg.peerDependencies as Record<string, string>;

    // Authoring-time only: the Storybook preview imports it, the shipped
    // library never does. Compare with @base-ui/react above, which components
    // DO import and which is therefore a regular dependency.
    expect(devDeps).toHaveProperty(STYLES_PACKAGE);
    expect(devDeps?.[STYLES_PACKAGE]).toBe("workspace:*");
    expect(deps).not.toHaveProperty(STYLES_PACKAGE);
    expect(peers).not.toHaveProperty(STYLES_PACKAGE);
  });

  it("keeps every src/ module free of a styles import", () => {
    const offenders: string[] = [];

    for (const file of sourceFilesUnder(srcDir)) {
      const source = readFileSync(join(srcDir, file), "utf8");

      for (const specifier of moduleSpecifiers(source)) {
        if (specifier === STYLES_PACKAGE || specifier.startsWith(`${STYLES_PACKAGE}/`)) {
          offenders.push(`${file} -> ${specifier}`);
        }
      }
    }

    // Guard against a vacuous pass if src/ ever moves out from under this file.
    expect(sourceFilesUnder(srcDir).length).toBeGreaterThan(0);
    expect(offenders).toEqual([]);
  });
});
