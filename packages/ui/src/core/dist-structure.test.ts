import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Acceptance-criteria guard for Wallow-m5aq.1.4 (preserveModules build +
// wildcard subpath exports).
//
// packages/ui ships BOTH a root barrel (`@bc-solutions-coder/ui`) and a
// per-component subpath (`@bc-solutions-coder/ui/button`). The subpath half only
// works if the build emits one module per source file instead of merging
// everything into a single `dist/index.js` chunk — a wildcard `"./*"` entry in
// the exports map points at `dist/components/<name>/index.js`, so that file has
// to actually exist. This block asserts the BUILT artifact, which is what an app
// resolves at runtime; the package.json/vite.config.ts declarations that produce
// it are pinned separately in `package-scaffold.test.ts`.
//
// Deliberately resolution-agnostic: these specs describe the contract a consumer
// depends on (one entry file per component, a barrel that re-exports rather than
// inlines), not the internal file-for-file mirror, so the bead's fallback plan
// (an explicit per-component `lib.entry` map, should preserveModules prove
// unusable) satisfies them just as well.

const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const repoRoot = join(packageDir, "..", "..");
const componentsSrcDir = join(packageDir, "src", "components");
const distDir = join(packageDir, "dist");
const distComponentsDir = join(distDir, "components");

/**
 * `dist/` is a build artifact, and `pnpm check` runs `test` BEFORE `build`, so a
 * fresh clone has no `dist/` when these specs execute. Every spec below is
 * skipped in that case rather than failing: their subject genuinely does not
 * exist yet. Run `pnpm --filter @bc-solutions-coder/ui build` first to arm them.
 */
const distIsMissing = !existsSync(join(distDir, "index.js"));

/** Every component that ships a folder entry point, i.e. a `./<name>` subpath. */
function componentNames(): string[] {
  if (!existsSync(componentsSrcDir)) {
    return [];
  }

  return readdirSync(componentsSrcDir, { withFileTypes: true })
    .filter(
      (entry) => entry.isDirectory() && existsSync(join(componentsSrcDir, entry.name, "index.ts")),
    )
    .map((entry) => entry.name)
    .toSorted();
}

/** Every emitted `.js` file under `dist/`, as a path relative to `dist/`. */
function distJsFiles(directory: string = distDir, prefix: string = ""): string[] {
  if (!existsSync(directory)) {
    return [];
  }

  const found: string[] = [];

  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const relativePath = prefix === "" ? entry.name : `${prefix}/${entry.name}`;

    if (entry.isDirectory()) {
      found.push(...distJsFiles(join(directory, entry.name), relativePath));
    } else if (entry.isFile() && entry.name.endsWith(".js")) {
      found.push(relativePath);
    }
  }

  return found.toSorted();
}

/** Every module specifier an emitted bundle imports or re-exports from. */
function moduleSpecifiers(source: string): string[] {
  const specifiers: string[] = [];

  for (const match of source.matchAll(/(?:\bfrom|^\s*import)\s*["']([^"']+)["']/gmu)) {
    const specifier = match[1];
    if (specifier !== undefined) {
      specifiers.push(specifier);
    }
  }

  return specifiers;
}

describe("packages/ui dist structure", () => {
  it.skipIf(distIsMissing)("emits one entry module per component folder", () => {
    const names = componentNames();
    // Guard against a vacuous pass if src/components ever moves.
    expect(names.length).toBeGreaterThan(0);

    const missing = names.filter((name) => !existsSync(join(distComponentsDir, name, "index.js")));

    expect(missing).toEqual([]);
  });

  it.skipIf(distIsMissing)("emits a declaration beside every component entry module", () => {
    // `tsc -p tsconfig.build.json` already emits these by following the barrel
    // -> folder index chain, so this holds before the build change too. It is
    // here because the wildcard export maps `types` and `import` to a matching
    // PAIR: a .js without its .d.ts silently degrades the subpath to `any`.
    const missing = componentNames().filter(
      (name) => !existsSync(join(distComponentsDir, name, "index.d.ts")),
    );

    expect(missing).toEqual([]);
  });

  it.skipIf(distIsMissing)("re-exports the component modules from the root barrel", () => {
    // The root bundle must POINT AT the per-component modules, not inline them.
    // This is what "no chunk merging" means for a consumer: importing the barrel
    // and importing a subpath must reach the same module instance, so component
    // state and `instanceof` checks stay consistent across import styles.
    const barrel = readFileSync(join(distDir, "index.js"), "utf8");

    const referenced = componentNames().filter((name) =>
      moduleSpecifiers(barrel).some((specifier) => specifier.startsWith(`./components/${name}/`)),
    );

    expect(referenced).toEqual(componentNames());
  });

  it.skipIf(distIsMissing)("leaves no hashed chunk files in dist", () => {
    // A merged chunk is Rolldown pulling shared modules into a synthesised file
    // with no stable path, so nothing in the exports map can reference it. Test
    // that structurally rather than by name: every emitted file must trace back
    // to a real `src/` module, which a chunk by definition cannot. Matching a
    // `[name]-[hash].js` pattern instead both flags legitimate modules whose
    // last path segment is hash-shaped (`focus-on-navigate.js`) and misses
    // chunks whose hash is not.
    const chunks = distJsFiles().filter((file) => {
      const sourcePath = join(packageDir, "src", file.slice(0, -".js".length));

      return !existsSync(`${sourcePath}.ts`) && !existsSync(`${sourcePath}.tsx`);
    });

    expect(chunks).toEqual([]);
  });

  it.skipIf(distIsMissing)("resolves every declared wildcard subpath to a real file", () => {
    // Ties the DECLARATION to the ARTIFACT: a typo in the exports map (say
    // `./dist/component/*/index.js`) leaves every spec above green while every
    // consuming import fails. Substitute each component name into the wildcard
    // targets exactly as Node's resolver does and check the result exists.
    const pkg = JSON.parse(readFileSync(join(packageDir, "package.json"), "utf8")) as {
      exports?: Record<string, unknown>;
    };
    const wildcard = pkg.exports?.["./*"] as Record<string, string> | undefined;

    expect(wildcard).toBeDefined();

    const unresolved: string[] = [];

    for (const name of componentNames()) {
      for (const target of Object.values(wildcard ?? {})) {
        const resolved = target.replaceAll("*", name);
        if (!existsSync(join(packageDir, resolved))) {
          unresolved.push(resolved);
        }
      }
    }

    expect(unresolved).toEqual([]);
  });

  it.skipIf(distIsMissing)("is importable by subpath from a consuming app", () => {
    // The end-to-end proof, run through Node's own resolver rather than Vite's:
    // an app that only has the pnpm workspace symlink must be able to import
    // `@bc-solutions-coder/ui/button` and get the Button component. This is the
    // one assertion here that exercises the exports map for real — everything
    // above reads files, this one makes Node resolve them.
    const result = spawnSync(
      process.execPath,
      [
        "-e",
        "import('@bc-solutions-coder/ui/button').then((m) => { console.log(JSON.stringify(Object.keys(m))); });",
      ],
      {
        cwd: join(repoRoot, "apps", "wallow-web"),
        encoding: "utf8",
        timeout: 60_000,
      },
    );

    expect(result.status, result.stderr).toBe(0);
    // Containment, not an exact set: the point is that Node's resolver reaches
    // the subpath and gets the real component back. Every component's subpath
    // also exports its CVA recipe (a runtime value, so it shows up in
    // `Object.keys`), and pinning the exact surface here would re-break this
    // shared gate on each new component for no added coverage.
    expect(JSON.parse(result.stdout.trim())).toEqual(expect.arrayContaining(["Button"]));
  });
});
