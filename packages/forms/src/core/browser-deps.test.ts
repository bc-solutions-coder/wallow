/*
 * Pre-bundle guard for Wallow-ov6w.2.5 (public barrel + package quality gate).
 *
 * `vitest.config.ts` lists every dependency the browser project must pre-bundle
 * in `optimizeDeps.include`. That list is not an optimisation: left to
 * on-the-fly discovery, Vite pre-bundles a Base UI subpath into a chunk carrying
 * its OWN copy of React and the first spec that renders the part dies on
 * `Cannot read properties of null (reading 'useRef')`, or the mid-run
 * "dependencies optimized: ..." reload drops the runner outright.
 *
 * The catch this spec exists for: an unresolvable entry is a WARNING, not an
 * error. Vite prints `Failed to resolve dependency: X, present in client
 * 'optimizeDeps.include'` and carries on with X silently absent — so the list
 * can look complete while pre-bundling nothing, and the package inherits exactly
 * the duplicate-React failures the list was written to prevent. Worse, a dropped
 * entry never reaches the dep-cache hash, so `node_modules/.vite` from an
 * earlier run is happily reused and the failure turns intermittent: green on one
 * cache state, red on another, with nothing in the config to point at.
 *
 * Why the entries fail is pnpm's strict `node_modules`: a package resolves only
 * what it DECLARES, and `@base-ui/react`, the recipe runtime and the browser
 * render helper reach this package transitively, through
 * `@bc-solutions-coder/ui` and `@bc-solutions-coder/testing`. packages/ui and
 * both apps already answer this by declaring what their own list names; so must
 * this package.
 *
 * Pure-logic spec: runs in the vitest NODE project.
 */

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import formsVitestConfig from "../../vitest.config";

// This guard lives at src/core/, so TWO levels up reaches the package root.
const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..", "..");

/** The slice of a vitest project descriptor this spec reads. */
interface ProjectWithOptimizeDeps {
  readonly optimizeDeps?: { readonly include?: readonly string[] };
  readonly test?: { readonly name?: string };
}

function browserProjectIncludes(): readonly string[] {
  const projects = (formsVitestConfig.test?.projects ??
    []) as unknown as readonly ProjectWithOptimizeDeps[];
  const browser = projects.find((project) => project.test?.name === "browser");

  expect(browser, "browser project").toBeDefined();

  return browser?.optimizeDeps?.include ?? [];
}

/**
 * Resolve every id in a PRISTINE Node process anchored at `fromDir`.
 *
 * Three things have to be neutralised for the answer to match Vite's, and the
 * first two were observed making this check pass against a package that Vite
 * could not resolve a single Base UI subpath in:
 *
 *   - vitest's node project runs specs through its own module runner, which
 *     patches CJS resolution for mocking, so an in-process
 *     `createRequire(...).resolve("@base-ui/react/field")` succeeds regardless.
 *     Hence a child process.
 *   - `pnpm run` exports NODE_PATH pointing at the hoisted virtual store
 *     (`node_modules/.pnpm/node_modules`), where every transitive package in the
 *     workspace is reachable. Node's CJS resolver honours it; Vite, which walks
 *     `node_modules` directories itself, does not. Hence the empty NODE_PATH
 *     below — without it a CJS child inherits pnpm's and answers a different
 *     question.
 *   - the walk has to run under the IMPORT conditions, which is what Vite reads
 *     an `optimizeDeps.include` entry with. Every workspace package here (the
 *     `@bc-solutions-coder/query` facade, `ui`, `sdk`) publishes an `exports` map
 *     with an `import` condition and no `require` one, so `require.resolve`
 *     reports ERR_PACKAGE_PATH_NOT_EXPORTED for a package Vite pre-bundles
 *     happily — a false alarm on the facade, and previously invisible only
 *     because no workspace package was listed. Hence `import.meta.resolve` in an
 *     ESM child anchored by its cwd. That also puts NODE_PATH out of reach for
 *     good: ESM resolution ignores it entirely.
 *
 * What is left is a plain directory walk from `fromDir`: exactly what Vite does
 * when it reads `optimizeDeps.include`.
 */
function resolveInPristineNode(
  ids: readonly string[],
  fromDir: string = packageDir,
): Readonly<Record<string, string | null>> {
  const script = `
    const out = {};
    for (const id of ${JSON.stringify(ids)}) {
      try {
        out[id] = import.meta.resolve(id);
      } catch {
        out[id] = null;
      }
    }
    process.stdout.write(JSON.stringify(out));
  `;

  return JSON.parse(runPristineNode(script, fromDir)) as Record<string, string | null>;
}

/** Runs `script` as ESM in a child Node rooted at `fromDir`, NODE_PATH cleared. */
function runPristineNode(script: string, fromDir: string): string {
  return execFileSync(process.execPath, ["--input-type=module", "-e", script], {
    cwd: fromDir,
    encoding: "utf8",
    env: { ...process.env, NODE_PATH: "" },
  });
}

/** `@base-ui/react/field` -> `@base-ui/react`; `react/jsx-runtime` -> `react`. */
function packageNameOf(id: string): string {
  const segments = id.split("/");

  return id.startsWith("@") ? segments.slice(0, 2).join("/") : (segments[0] ?? id);
}

function declaredDependencyNames(): readonly string[] {
  const pkg = JSON.parse(readFileSync(join(packageDir, "package.json"), "utf8")) as {
    dependencies?: Record<string, string>;
    devDependencies?: Record<string, string>;
    peerDependencies?: Record<string, string>;
  };

  return [
    ...Object.keys(pkg.dependencies ?? {}),
    ...Object.keys(pkg.devDependencies ?? {}),
    ...Object.keys(pkg.peerDependencies ?? {}),
  ];
}

describe("packages/forms browser pre-bundle list", () => {
  it("names a browser project carrying an optimizeDeps.include list", () => {
    expect(browserProjectIncludes().length).toBeGreaterThan(0);
  });

  it("declares every package its optimizeDeps.include names", () => {
    const declared = declaredDependencyNames();
    const undeclared = [
      ...new Set(browserProjectIncludes().map((include) => packageNameOf(include))),
    ].filter((name) => !declared.includes(name));

    // Under pnpm an undeclared package is an unresolvable one, so this is the
    // cause and the assertion below is the symptom. Named here separately
    // because the fix is a package.json line, and the failure should say so.
    expect(undeclared).toEqual([]);
  });

  it("resolves every optimizeDeps.include entry from the package root", () => {
    const resolved = resolveInPristineNode(browserProjectIncludes());
    const unresolvable = Object.keys(resolved).filter((id) => resolved[id] === null);

    // A non-empty list here IS the "Failed to resolve dependency" warning Vite
    // prints and then ignores.
    expect(unresolvable).toEqual([]);
  });

  it("shares one copy of Base UI with @bc-solutions-coder/ui", () => {
    // Declaring `@base-ui/react` locally is only safe while pnpm resolves it to
    // the very directory packages/ui got. Two real directories would mean two
    // module identities: a `Field.Root` from one and an `Input` from the other
    // share no context, so label association and `data-invalid` silently stop
    // working — the exact class of bug the pre-bundle list guards against.
    const probe = "@base-ui/react/field";
    const fromForms = resolveInPristineNode([probe])[probe];
    const uiDir = join(packageDir, "node_modules", "@bc-solutions-coder", "ui");
    const fromUi = resolveInPristineNode([probe], uiDir)[probe];

    // Both sides report `null` when they resolve nothing, so equality alone would
    // pass on a total resolution failure.
    expect(fromForms).not.toBeNull();
    expect(fromForms).toBe(fromUi);
  });
});
