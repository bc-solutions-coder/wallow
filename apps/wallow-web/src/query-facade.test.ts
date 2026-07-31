import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * wallow-web reaches TanStack Query through ONE door: `@bc-solutions-coder/query`,
 * the workspace facade. This spec is that door's lock (Wallow-x4qn.8).
 *
 * This app matters more than most for the lock: it is the REFERENCE dashboard a
 * fork copies from, so every import site here is a pattern that gets duplicated
 * downstream. A single surviving `@tanstack/react-query` import teaches the door
 * the facade exists to close — and, worse, is how a second copy of the library
 * (a second `QueryClientProvider` context, and a `useQuery` that throws
 * "No QueryClient set" against a provider it does not recognise) gets into the
 * graph.
 *
 * THE IMPORT HALF OF THAT LOCK IS NOW LINT'S (Wallow-l5x2). The root
 * `.oxlintrc.json` restricts `@tanstack/react-query` outright, and since the lint
 * split (`pnpm lint` + `pnpm lint:tests`) the ban reaches specs as well as source.
 * This file used to re-state it as a regex sweep over every `.tsx` on disk plus a
 * hand-kept table of twelve screen paths — a table that had to be edited whenever a
 * component moved, and that reported nothing a linter does not report faster and at
 * the offending line.
 *
 * WHAT IS LEFT IS WHAT A RULE CANNOT READ: the manifest, what pnpm actually linked,
 * and what the vitest harness hands Vite.
 *
 *  1. MANIFEST — the app declares the facade (and the auth package that rides on
 *     it) and not react-query. Declaring both is exactly how a second library copy,
 *     and with it a second `QueryClientProvider` context, gets into the graph.
 *  2. HARNESS — the browser project pre-bundles the linked facade and the node
 *     project inlines it, neither of which happens by default for a workspace link.
 *  3. RUNTIME — the facade actually resolves *from this app's own `node_modules`*
 *     and hands back a working `createQueryClient` plus the react-query surface.
 *     Lint and grep alike would pass on a manifest edit pnpm never linked.
 *
 * Node project: reads files, mounts nothing.
 */

const here: string = dirname(fileURLToPath(import.meta.url));
const appRoot: string = resolve(here, "..");

/** The two facade-era packages this app must consume. */
const FACADE = "@bc-solutions-coder/query";
const AUTH = "@bc-solutions-coder/auth";
/** The door it must not use. */
const REACT_QUERY = "@tanstack/react-query";

const read = (relativePath: string): string => readFileSync(resolve(appRoot, relativePath), "utf8");

interface PackageManifest {
  readonly name?: string;
  readonly dependencies?: Readonly<Record<string, string>>;
  readonly devDependencies?: Readonly<Record<string, string>>;
  readonly exports?: Readonly<Record<string, Readonly<Record<string, string>>>>;
}

const manifest = (): PackageManifest => JSON.parse(read("package.json")) as PackageManifest;

const declaredDeps = (): Readonly<Record<string, string>> => {
  const parsed: PackageManifest = manifest();

  return { ...parsed.dependencies, ...parsed.devDependencies };
};

describe("wallow-web's dependency manifest", () => {
  it("declares the query facade as a workspace dependency", () => {
    expect(manifest().dependencies?.[FACADE]).toBe("workspace:*");
  });

  it("declares the shared auth package as a workspace dependency", () => {
    // The facade's first consumer inside this app: the current-user query the
    // route gates read now comes from here (see `shared-auth.test.ts`).
    expect(manifest().dependencies?.[AUTH]).toBe("workspace:*");
  });

  it("does not declare react-query directly", () => {
    // Not "also declares" — declaring both the facade and react-query is exactly
    // how a second library copy (and a second `QueryClient` identity) gets in.
    expect(Object.keys(declaredDeps())).not.toContain(REACT_QUERY);
  });

  it("keeps the workspace packages the app still consumes", () => {
    // Dropping an entry must not take the rest of the workspace with it.
    for (const pkg of ["forms", "sdk", "styles", "testing", "ui"]) {
      expect(manifest().dependencies).toHaveProperty(`@bc-solutions-coder/${pkg}`);
    }
  });
});

describe("the vitest harness resolves the facade explicitly", () => {
  // There is deliberately no spec pinning a `@tanstack/react-query` entry in the
  // pre-bundle list. One was here, on the theory that react-query is still the
  // module Vite pre-bundles, one facade hop away. It is not: this app does not
  // declare react-query, so under pnpm's strict `node_modules` the entry resolved
  // to nothing and Vite logged `Failed to resolve dependency` once per run and
  // pre-bundled nothing at all. The facade entry below is what does the work, and
  // `src/browser-deps.test.ts` now asserts the general invariant the dead entry
  // hid behind — every entry in the list must actually resolve.

  it("pre-bundles the facade and the auth package, which pnpm merely LINKS", () => {
    // A linked workspace package is not pre-bundled by default, and both of these
    // are imported from browser-project specs (a component reading a query; the
    // home-gate spec reading the current-user query). Unnamed, Vite discovers
    // them mid-run and reloads.
    const extras: readonly string[] = browserPreBundleList();

    expect(extras).toContain(FACADE);
    expect(extras).toContain(AUTH);
  });

  it("inlines the facade for the node project instead of externalizing it", () => {
    // The node project runs the SSR-side route specs; without `ssr.noExternal`
    // the linked facade is externalized to a bare Node import instead of being
    // transformed. Same knob `packages/testing`'s own config carries.
    expect(read("vitest.config.ts")).toMatch(
      /noExternal:\s*\[[^\]]*"@bc-solutions-coder\/query"/su,
    );
  });
});

describe("the facade as this app resolves it", () => {
  it("is linked into the app's own node_modules", () => {
    // pnpm links a package into an importer's node_modules only when that
    // importer declares it, so this is the manifest edit having actually taken
    // effect rather than merely being written down.
    expect(existsSync(packageDir(FACADE)), `${FACADE} is not linked into wallow-web`).toBe(true);
    expect(linkedManifest(FACADE).name).toBe(FACADE);
  });

  it("hands the app a QueryClient factory and the whole react-query surface", async () => {
    const facade: Record<string, unknown> = await importLinked(FACADE);

    expect(typeof facade["createQueryClient"]).toBe("function");
    expect(typeof facade["useQuery"]).toBe("function");
    expect(typeof facade["useMutation"]).toBe("function");
    expect(typeof facade["useQueryClient"]).toBe("function");
    expect(typeof facade["queryOptions"]).toBe("function");
    expect(typeof facade["QueryClientProvider"]).toBe("function");
    expect(typeof facade["QueryClient"]).toBe("function");
  });

  it("gives the router a retry-disabled client of the facade's own QueryClient type", async () => {
    const facade: Record<string, unknown> = await importLinked(FACADE);
    const create = facade["createQueryClient"] as () => {
      getDefaultOptions: () => { queries?: { retry?: unknown } };
    };

    const client = create();

    // One module instance, so `instanceof` holds — the symptom of two copies is
    // a runtime "No QueryClient set" from a provider the hook does not recognise.
    expect(client).toBeInstanceOf(facade["QueryClient"] as new () => unknown);
    expect(client.getDefaultOptions().queries?.retry).toBe(false);
  });
});

/** Where pnpm links a workspace package for this importer. */
function packageDir(name: string): string {
  return resolve(appRoot, "node_modules", name);
}

function linkedManifest(name: string): PackageManifest {
  const manifestPath: string = resolve(packageDir(name), "package.json");

  expect(existsSync(manifestPath), `${name} is not linked into wallow-web`).toBe(true);

  return JSON.parse(readFileSync(manifestPath, "utf8")) as PackageManifest;
}

/**
 * Import a package THROUGH the app's link, via the entry its own exports map
 * names — a computed specifier, so this spec resolves the package exactly the
 * way the app's bundler does instead of TypeScript resolving it at compile time.
 */
async function importLinked(name: string): Promise<Record<string, unknown>> {
  const entry: string | undefined = linkedManifest(name).exports?.["."]?.["import"];

  expect(entry, `${name} declares no "." import entry`).toBeTruthy();

  const entryPath: string = resolve(packageDir(name), entry as string);

  expect(existsSync(entryPath), `${name} is not built (${entry} missing)`).toBe(true);

  return (await import(pathToFileURL(entryPath).href)) as Record<string, unknown>;
}

/**
 * The browser project's `optimizeDeps.include`, read off the CONFIG OBJECT.
 *
 * This used to regex `vitest.config.ts` for a `const extraBrowserOptimizeDeps =
 * [...]` declaration, on the stated grounds that importing the config would boot
 * a second browser provider. It does not: `playwright()` returns a descriptor and
 * nothing launches until vitest runs the project — `src/browser-deps.test.ts` has
 * imported the same config from the same node project all along. Reading the value
 * asserts what Vite actually receives rather than how the file happens to be
 * written, so inlining the list into the `createVitestProjects` call no longer
 * moves the goalposts.
 */
function browserPreBundleList(): readonly string[] {
  const projects = (vitestConfig.test?.projects ?? []) as readonly {
    optimizeDeps?: { include?: readonly string[] };
    test?: { name?: string };
  }[];

  return projects.find((project) => project.test?.name === "browser")?.optimizeDeps?.include ?? [];
}
