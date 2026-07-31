import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
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
 * Two kinds of assertion, because a facade is half manifest and half runtime:
 *
 *  1. STRUCTURAL — the manifest declares the facade (and the auth package that
 *     rides on it) and not `@tanstack/react-query`, and no source file imports
 *     react-query directly. The README's directory table and `vite.config.ts`'s
 *     prose both describe the workspace a fork gets, so this app is documentation
 *     as much as it is code.
 *  2. RUNTIME — the facade actually resolves *from this app's own
 *     `node_modules`* and hands back a working `createQueryClient` plus the
 *     react-query surface. Grep alone would pass on a manifest edit pnpm never
 *     linked.
 *
 * Structural assertions read source as TEXT rather than importing it: the files
 * under test are Vite/Vitest configs, a Start router factory and route modules,
 * none of which can be imported in a plain node context.
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

describe("wallow-web's imports", () => {
  it("takes createQueryClient and the QueryClient type from the facade", () => {
    const names: readonly string[] = importedNamesFrom(read("src/app/router.tsx"), FACADE);

    expect(names).toContain("createQueryClient");
    expect(names).toContain("QueryClient");
  });

  it("types the root route's context with the facade's QueryClient", () => {
    // `RouterContext.queryClient` and the client `src/router.tsx` constructs must
    // name the SAME type, or the router factory stops type-checking against its
    // own route tree.
    expect(importedNamesFrom(read("src/app/routes/__root.tsx"), FACADE)).toContain("QueryClient");
  });

  it("takes the invalidation-assertion types from the facade", () => {
    // `src/shared/testing/invalidation.ts` runs the REAL `queriesWithTag` /
    // `queriesForOperation` predicates against real generated keys, so its
    // `Query`/`QueryFilters` types have to be the same ones the app's queries are
    // built from.
    const names: readonly string[] = importedNamesFrom(
      read("src/shared/testing/invalidation.ts"),
      FACADE,
    );

    expect(names).toContain("Query");
    expect(names).toContain("QueryFilters");
  });

  it.each([
    "src/features/organizations/components/OrganizationDetail.tsx",
    "src/features/organizations/components/OrganizationList.tsx",
    "src/features/organizations/components/MemberList.tsx",
    "src/features/organizations/components/CreateOrganizationForm.tsx",
    "src/features/inquiries/components/InquiryDetail.tsx",
    "src/features/inquiries/components/InquiryList.tsx",
    "src/features/inquiries/components/CreateInquiryForm.tsx",
    "src/features/apps/components/AppList.tsx",
    "src/features/apps/components/RegisterAppForm.tsx",
    "src/features/mfa/components/MfaSettingsSection.tsx",
    "src/features/mfa/components/MfaEnrollFlow.tsx",
    "src/features/settings/components/ProfileSection.tsx",
  ])("routes %s's react-query hooks through the facade", (relativePath: string) => {
    // Every screen that reads or writes backend data names the facade, and names
    // it for the SAME hooks it used to import from react-query — a swap of the
    // specifier only, never a drop of the hook.
    const source: string = read(relativePath);
    const viaFacade: readonly string[] = importedNamesFrom(source, FACADE);

    expect(viaFacade.length, `${relativePath} imports nothing from ${FACADE}`).toBeGreaterThan(0);
    expect(importSpecifiers(source), relativePath).not.toContain(REACT_QUERY);
  });

  it("imports react-query nowhere in the app", () => {
    for (const [entry, source] of appSources()) {
      expect(importSpecifiers(source), entry).not.toContain(REACT_QUERY);
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

/**
 * Every hand-written file of this app — the root-level configs, manifest,
 * Dockerfile and README, plus every source module under `src`. Specs are excluded
 * (they must name the banned packages in order to forbid them) and so is
 * `src/routeTree.gen.ts`, which is codegen.
 */
function appFiles(
  extensions: RegExp = /(?:\.(?:tsx?|json|md)|^Dockerfile)$/u,
): readonly (readonly [string, string])[] {
  const rootEntries: string[] = readdirSync(appRoot)
    .filter((entry: string) => extensions.test(entry))
    .filter((entry: string) => statSync(resolve(appRoot, entry)).isFile());

  const srcEntries: string[] = readdirSync(resolve(appRoot, "src"), { recursive: true })
    .map(String)
    .filter((entry: string) => extensions.test(entry))
    .map((entry: string) => `src/${entry}`);

  return [...rootEntries, ...srcEntries]
    .filter((entry: string) => !/\.test\.tsx?$/u.test(entry) && !entry.endsWith("routeTree.gen.ts"))
    .map((entry: string) => [entry, read(entry)] as readonly [string, string]);
}

/**
 * The subset of the above that is executable TypeScript. The import guards use
 * this rather than `appFiles()` so a fenced example in the README is judged by
 * the README's own assertions, not reported as an app import.
 */
function appSources(): readonly (readonly [string, string])[] {
  return appFiles(/\.tsx?$/u);
}

/** Every module specifier the file imports from, `import type` included. */
function importSpecifiers(source: string): readonly string[] {
  return [...source.matchAll(/^\s*import\s[^;]*?from\s+"([^"]+)"/gmu)].map(
    (match: RegExpMatchArray) => match[1] as string,
  );
}

/**
 * The names a file imports from one module — value and type imports alike, alias
 * targets normalised away — so a rename can move `QueryClient` to the facade
 * without this spec dictating whether it rides in its own `import type` line.
 */
function importedNamesFrom(source: string, moduleSpecifier: string): readonly string[] {
  const escaped: string = moduleSpecifier.replaceAll(/[.*+?^${}()|[\]\\]/gu, String.raw`\$&`);
  const pattern: RegExp = new RegExp(
    String.raw`import\s+(?:type\s+)?\{([^}]*)\}\s+from\s+"${escaped}"`,
    "gu",
  );

  return [...source.matchAll(pattern)].flatMap((match: RegExpMatchArray) =>
    (match[1] as string)
      .split(",")
      .map(
        (name: string) =>
          name
            .trim()
            .replace(/^type\s+/u, "")
            .split(/\s+as\s+/u)[0] as string,
      )
      .filter((name: string) => name.length > 0),
  );
}
