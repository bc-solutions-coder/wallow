/*
 * The shared pre-bundle guard: `describeBrowserPreBundleList` verifies that a
 * package's browser Vitest project can actually resolve every entry in its
 * `optimizeDeps.include`.
 *
 * That list is not an optimisation. Left to on-the-fly discovery, Vite
 * pre-bundles a Base UI subpath into a chunk carrying its OWN copy of React and
 * the first spec that renders the part dies on `Cannot read properties of null
 * (reading 'useRef')`, or the mid-run "dependencies optimized: ..." reload drops
 * the runner outright.
 *
 * The catch this guard exists for: an unresolvable entry is a WARNING, not an
 * error. Vite prints `Failed to resolve dependency: X, present in client
 * 'optimizeDeps.include'` and carries on with X silently absent — so the list can
 * look complete while pre-bundling nothing, and the package inherits exactly the
 * duplicate-React failures the list was written to prevent. Worse, a dropped
 * entry never reaches the dep-cache hash, so `node_modules/.vite` from an earlier
 * run is happily reused and the failure turns intermittent: green on one cache
 * state, red on another, with nothing in the config to point at.
 *
 * Why an entry fails is pnpm's strict `node_modules`: a package resolves only
 * what it DECLARES, and `@base-ui/react`, the recipe runtime and the browser
 * render helper reach most consumers transitively, through
 * `@bc-solutions-coder/ui` and `@bc-solutions-coder/testing`. The fix is a
 * `package.json` line, which is why the declaration check below is stated
 * separately from the resolution one.
 *
 * This lives here rather than in one package because the failure it catches is
 * silent everywhere, not only where it was first found (packages/forms). Every
 * consumer with a browser project calls it from a one-import spec.
 *
 * NODE-ONLY: it spawns child processes and reads the filesystem, so the calling
 * spec must be a `*.test.ts` that lands in the node project. It is deliberately
 * NOT re-exported from the package barrel, which is loaded at Vitest
 * config-load time.
 */

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

/** The slice of a Vitest project descriptor this guard reads. */
interface ProjectWithOptimizeDeps {
  readonly optimizeDeps?: { readonly include?: readonly string[] };
  readonly test?: { readonly name?: string };
}

/** The slice of a Vitest root config this guard reads. */
export interface ConfigWithProjects {
  readonly test?: { readonly projects?: unknown };
}

export interface BrowserPreBundleGuardOptions {
  /**
   * The package root — the directory holding `package.json` and the directory
   * Vite resolves an `optimizeDeps.include` entry from. Pass
   * `fileURLToPath(new URL("..", import.meta.url))` walked up to it.
   */
  readonly packageDir: string;

  /** The package's own `vitest.config.ts` default export. */
  readonly config: unknown;

  /**
   * Name of the project to check, for packages whose browser project is not
   * called `browser` (packages/ui's `storybook` project carries its own list).
   * Defaults to `"browser"`.
   */
  readonly projectName?: string;
}

/** `@base-ui/react/field` -> `@base-ui/react`; `react/jsx-runtime` -> `react`. */
function packageNameOf(id: string): string {
  const [first, second] = id.split("/");

  if (first === undefined) {
    return id;
  }

  // A scoped name is `@scope/name` — the first TWO segments, not the first.
  return id.startsWith("@") && second !== undefined ? `${first}/${second}` : first;
}

/**
 * What Vite will actually try to resolve for an entry.
 *
 * A glob entry (`@base-ui/react/*`) is never resolved as written: Vite's
 * `expandGlobIds` calls `resolvePackageData(pkgName, config.root)` and derives
 * the subpaths from that package's own `exports` keys. So resolving the PACKAGE
 * is both necessary and sufficient — and resolving the literal glob would fail
 * on a perfectly good list.
 */
function resolvableForm(id: string): string {
  return id.includes("*") ? packageNameOf(id) : id;
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
export function resolveInPristineNode(
  ids: readonly string[],
  fromDir: string,
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

  const stdout = execFileSync(process.execPath, ["--input-type=module", "-e", script], {
    cwd: fromDir,
    encoding: "utf8",
    env: { ...process.env, NODE_PATH: "" },
  });

  return JSON.parse(stdout) as Record<string, string | null>;
}

function declaredDependencyNames(packageDir: string): readonly string[] {
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

/**
 * Register the pre-bundle guard for one package. Call it at the top level of a
 * `*.test.ts` in the package whose config is passed.
 */
export function describeBrowserPreBundleList(options: BrowserPreBundleGuardOptions): void {
  const { packageDir, config, projectName = "browser" } = options;

  function includes(): readonly string[] {
    const projects = ((config as ConfigWithProjects).test?.projects ??
      []) as readonly ProjectWithOptimizeDeps[];
    const project = projects.find((entry) => entry.test?.name === projectName);

    expect(project, `${projectName} project`).toBeDefined();

    return project?.optimizeDeps?.include ?? [];
  }

  describe(`${projectName} pre-bundle list`, () => {
    it("names a project carrying an optimizeDeps.include list", () => {
      expect(includes()).not.toEqual([]);
    });

    it("declares every package its optimizeDeps.include names", () => {
      const declared = declaredDependencyNames(packageDir);
      const undeclared = [...new Set(includes().map((include) => packageNameOf(include)))].filter(
        (name) => !declared.includes(name),
      );

      // Under pnpm an undeclared package is an unresolvable one, so this is the
      // cause and the assertion below is the symptom. Named here separately
      // because the fix is a package.json line, and the failure should say so.
      expect(undeclared).toEqual([]);
    });

    it("resolves every optimizeDeps.include entry from the package root", () => {
      const resolved = resolveInPristineNode(
        includes().map((id) => resolvableForm(id)),
        packageDir,
      );
      const unresolvable = Object.keys(resolved).filter((id) => resolved[id] === null);

      // A non-empty list here IS the "Failed to resolve dependency" warning Vite
      // prints and then ignores.
      expect(unresolvable).toEqual([]);
    });
  });
}

/**
 * A consumer's browser-project `optimizeDeps.include`, read off the CONFIG OBJECT.
 *
 * Importing the config does not boot a browser provider: `playwright()` returns a
 * descriptor and nothing launches until vitest runs the project. Reading the value
 * asserts what Vite actually receives rather than how the file happens to be written.
 *
 * `projects` is typed `unknown` for the same reason `describeBrowserPreBundleList`
 * types its whole config that way: vitest's own `TestProjectConfiguration` is a
 * union whose members include a bare glob `string`, so a narrower parameter type
 * rejects every real `defineConfig` result at the call site.
 */
export function browserPreBundleList(config: ConfigWithProjects): readonly string[] {
  const projects = (config.test?.projects ?? []) as readonly ProjectWithOptimizeDeps[];

  return projects.find((project) => project.test?.name === "browser")?.optimizeDeps?.include ?? [];
}

/**
 * The companion identity check, for a package that reaches Base UI through
 * `@bc-solutions-coder/ui` and also names it in its own pre-bundle list.
 */
export function describeSharedBaseUi(packageDir: string): void {
  it("shares one copy of Base UI with @bc-solutions-coder/ui", () => {
    // Declaring `@base-ui/react` locally is only safe while pnpm resolves it to
    // the very directory packages/ui got. Two real directories would mean two
    // module identities: a `Field.Root` from one and an `Input` from the other
    // share no context, so label association and `data-invalid` silently stop
    // working — the exact class of bug the pre-bundle list guards against.
    const probe = "@base-ui/react/field";
    const fromHere = resolveInPristineNode([probe], packageDir)[probe];
    const uiDir = join(packageDir, "node_modules", "@bc-solutions-coder", "ui");
    const fromUi = resolveInPristineNode([probe], uiDir)[probe];

    // Both sides report `null` when they resolve nothing, so equality alone would
    // pass on a total resolution failure.
    expect(fromHere).not.toBeNull();
    expect(fromHere).toBe(fromUi);
  });
}
