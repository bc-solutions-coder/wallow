import { existsSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * wallow-auth reaches TanStack Query through ONE door: `@bc-solutions-coder/query`,
 * the workspace facade. This spec is the half of that door's lock a linter cannot
 * turn (Wallow-x4qn.9.1, narrowed by Wallow-l5x2).
 *
 * WHAT LINT ALREADY OWNS, and what this file therefore no longer sweeps. The root
 * `.oxlintrc.json` restricts `@tanstack/react-query` outright, and since the lint
 * split (`pnpm lint` + `pnpm lint:tests`) that ban reaches SPECS as well as source —
 * which is what a per-file import table was for, because a spec holding its own
 * `QueryClient` binding is the copy-identity bug in miniature. A regex sweep over
 * `src/` said the same thing once per `pnpm test`, through a hand-rolled comment
 * stripper, against a hand-kept list of fourteen modules that had to be edited every
 * time a screen moved.
 *
 * WHAT IS LEFT IS NOT ABOUT IMPORTS AT ALL. The failure this app actually fears is A
 * SECOND COPY of react-query in the module graph: two copies give two
 * `QueryClientProvider` React contexts, and a `useMutation` from copy B inside a
 * provider from copy A throws "No QueryClient set" the moment the user presses Sign
 * in. That is invisible to a diff and invisible to a grep — it lives in the manifest,
 * in what pnpm links, and in module identity. So this file asks three things no rule
 * can:
 *
 *   1. The manifest declares the facade and NOT react-query — declaring both is
 *      exactly how the second copy gets in, and it is also what would make a direct
 *      import resolvable again.
 *   2. pnpm therefore does not link react-query into this app at all, so a
 *      reintroduced direct import cannot even resolve. (Failing here after a manifest
 *      edit means the install has not been re-run.)
 *   3. The facade module this app resolves IS the one `@bc-solutions-coder/testing`
 *      mounts its provider from and the one `@bc-solutions-coder/forms` runs its
 *      submit mutation from — asserted on the objects, not on the text.
 *
 * Plus one config contract of the same family: a linked workspace package is not
 * pre-bundled by default, so the browser project has to name the facade.
 *
 * Node project — it reads manifests and dynamically imports built `dist/` output; it
 * mounts nothing.
 */

/** The one place react-query is allowed to enter this workspace. */
const FACADE = "@bc-solutions-coder/query";

/** The shared authn layer, which rides on that same facade. */
const AUTH = "@bc-solutions-coder/auth";

/** The package no consumer may import or declare for itself. */
const RAW = "@tanstack/react-query";

// This spec lives at src/, so one level up reaches the app root and three
// reaches the repo root (src -> wallow-auth -> apps -> repo).
const srcDir: string = dirname(fileURLToPath(import.meta.url));
const appDir: string = resolve(srcDir, "..");
const repoRoot: string = resolve(appDir, "..", "..");

interface PackageManifest {
  readonly name?: string;
  readonly dependencies?: Readonly<Record<string, string>>;
  readonly devDependencies?: Readonly<Record<string, string>>;
  readonly peerDependencies?: Readonly<Record<string, string>>;
  readonly exports?: Readonly<Record<string, Readonly<Record<string, string>>>>;
}

function readText(path: string): string {
  return readFileSync(path, "utf8");
}

function manifest(): PackageManifest {
  return JSON.parse(readText(join(appDir, "package.json"))) as PackageManifest;
}

function everyDeclaredDependency(): Readonly<Record<string, string>> {
  const parsed: PackageManifest = manifest();

  return { ...parsed.dependencies, ...parsed.devDependencies, ...parsed.peerDependencies };
}

/**
 * The browser project's `optimizeDeps.include`, read off the CONFIG OBJECT.
 *
 * This used to regex `vitest.config.ts` for a `const extraBrowserOptimizeDeps =
 * [...]` declaration, on the stated grounds that importing the config would boot
 * a second browser provider. It does not: `playwright()` returns a descriptor
 * and nothing launches until vitest runs the project — `src/browser-deps.test.ts`
 * has imported the same config from the same node project all along. Reading the
 * value also asserts what Vite actually receives rather than how the file happens
 * to be written, so inlining the list into the `createVitestProjects` call no
 * longer moves the goalposts.
 */
function browserPreBundleList(): readonly string[] {
  const projects = (vitestConfig.test?.projects ?? []) as readonly {
    optimizeDeps?: { include?: readonly string[] };
    test?: { name?: string };
  }[];

  return projects.find((project) => project.test?.name === "browser")?.optimizeDeps?.include ?? [];
}

/** Where pnpm links a package for a given importer. */
function linkDir(importerDir: string, packageName: string): string {
  return join(importerDir, "node_modules", packageName);
}

function linkedManifest(importerDir: string, packageName: string): PackageManifest {
  const path: string = join(linkDir(importerDir, packageName), "package.json");

  expect(
    existsSync(path),
    `${packageName} is not linked into ${relative(repoRoot, importerDir)}`,
  ).toBe(true);

  return JSON.parse(readText(path)) as PackageManifest;
}

/**
 * Import the facade THROUGH one importer's own link, via the entry that
 * importer's copy of the exports map names. A computed specifier, so this
 * resolves the package exactly the way each consumer's bundler does rather than
 * however TypeScript would resolve a literal at compile time — which is the
 * whole point when the question under test is "is it the same module".
 */
async function importFacadeAs(importerDir: string): Promise<Record<string, unknown>> {
  const entry: string | undefined = linkedManifest(importerDir, FACADE).exports?.["."]?.["import"];

  expect(entry, `${FACADE} declares no "." import entry`).toBeTruthy();

  const entryPath: string = join(linkDir(importerDir, FACADE), entry as string);

  expect(existsSync(entryPath), `${FACADE} is not built (${entry} missing)`).toBe(true);

  return (await import(pathToFileURL(entryPath).href)) as Record<string, unknown>;
}

describe("wallow-auth's dependency manifest", () => {
  it("declares the query facade as a workspace dependency", () => {
    expect(manifest().dependencies?.[FACADE]).toBe("workspace:*");
  });

  it("declares the shared auth package as a workspace dependency", () => {
    expect(manifest().dependencies?.[AUTH]).toBe("workspace:*");
  });

  it("declares react-query in no dependency field of its own", () => {
    // Not "also declares". Declaring both the facade and react-query is exactly
    // how the second `QueryClientProvider` context gets into the graph, and it
    // is also what keeps a direct import resolvable under pnpm.
    expect(Object.keys(everyDeclaredDependency())).not.toContain(RAW);
  });
});

describe("browser-mode pre-bundling survives the facade hop", () => {
  it("registers the facade with the browser project rather than leaving it to discovery", () => {
    // A linked workspace package is not pre-bundled by default, and a dependency
    // discovered mid-run triggers a Vite reload that DROPS the runner instead of
    // failing a test — the worst failure mode in this app, whose specs are the
    // auth-flow safety net.
    //
    // Only the facade's PRESENCE is asserted here. That the list is non-empty,
    // that every entry is declared, and that every entry resolves from the app
    // root under Vite's own conditions are the shared guard's three cases, run
    // for this app by `src/browser-deps.test.ts` — and the declaration case is
    // also what stops react-query being pre-bundled under its own name, since
    // the manifest above no longer declares it.
    expect(browserPreBundleList()).toContain(FACADE);
  });
});

describe("the facade as wallow-auth resolves it", () => {
  it("is linked into the app's own node_modules", () => {
    // pnpm links a package into an importer's `node_modules` only when that
    // importer declares it, so this is the manifest edit having taken effect
    // rather than merely being written down.
    expect(existsSync(linkDir(appDir, FACADE)), `${FACADE} is not linked into wallow-auth`).toBe(
      true,
    );
    expect(linkedManifest(appDir, FACADE).name).toBe(FACADE);
  });

  it("links the shared auth package too", () => {
    expect(linkedManifest(appDir, AUTH).name).toBe(AUTH);
  });

  it("can no longer resolve react-query directly at all", () => {
    // The structural half of the copy-identity guarantee, and the strongest one
    // available: with react-query out of the manifest, pnpm unlinks it from this
    // app's `node_modules`, so a re-introduced direct import cannot even
    // resolve. Failing here after a manifest edit means the install has not been
    // re-run (`pnpm install --filter @bc-solutions-coder/wallow-auth...`).
    expect(
      existsSync(linkDir(appDir, RAW)),
      `${RAW} is still linked into wallow-auth — a second copy remains reachable`,
    ).toBe(false);
  });

  it("hands the app a QueryClient factory and the react-query surface the screens use", async () => {
    const facade: Record<string, unknown> = await importFacadeAs(appDir);

    for (const name of [
      "createQueryClient",
      "QueryClient",
      "QueryClientProvider",
      "useQuery",
      "useMutation",
      "useQueryClient",
    ]) {
      expect(typeof facade[name], `${FACADE} exports no ${name}`).toBe("function");
    }
  });

  it("gives the router a retry-disabled client of the facade's own QueryClient type", async () => {
    const facade: Record<string, unknown> = await importFacadeAs(appDir);
    const create = facade["createQueryClient"] as () => {
      getDefaultOptions: () => { queries?: { retry?: unknown } };
    };

    const client = create();

    expect(client).toBeInstanceOf(facade["QueryClient"] as new () => unknown);
    expect(client.getDefaultOptions().queries?.retry).toBe(false);
  });
});

/**
 * The copy-identity pole. Everything above could pass on a graph carrying two
 * react-query copies; these cases are what rule it out, because the symptom is a
 * runtime "No QueryClient set" from a provider the hook does not recognise —
 * thrown the moment a user presses Sign in, and invisible to every grep.
 */
describe("one react-query copy across the app and the packages it renders through", () => {
  it("shares the facade module with @bc-solutions-coder/testing", async () => {
    // `renderWithWallow` mounts `QueryClientProvider` from ITS resolution of the
    // facade; every screen spec's `useQuery`/`useMutation` comes from the app's.
    // Different modules here means every one of those specs — the auth-flow
    // safety net — is exercising a context the screens would never see in the
    // browser.
    const asApp: Record<string, unknown> = await importFacadeAs(appDir);
    const asTesting: Record<string, unknown> = await importFacadeAs(
      join(repoRoot, "packages", "testing"),
    );

    expect(asApp["QueryClient"]).toBe(asTesting["QueryClient"]);
    expect(asApp["QueryClientProvider"]).toBe(asTesting["QueryClientProvider"]);
    expect(asApp["useMutation"]).toBe(asTesting["useMutation"]);
  });

  it("shares the facade module with @bc-solutions-coder/forms", async () => {
    // `useAppForm` runs the mutation for the migrated password-recovery screens,
    // inside a provider this app mounts.
    const asApp: Record<string, unknown> = await importFacadeAs(appDir);
    const asForms: Record<string, unknown> = await importFacadeAs(
      join(repoRoot, "packages", "forms"),
    );

    expect(asApp["QueryClient"]).toBe(asForms["QueryClient"]);
    expect(asApp["useMutation"]).toBe(asForms["useMutation"]);
  });

  it("builds a router client the render seam's provider would accept", async () => {
    // The two poles above, stated at the VALUE the app actually constructs:
    // `getRouter()`'s client must satisfy the `QueryClient` class that the
    // provider in `@bc-solutions-coder/testing` — and in production, this same
    // module — checks it against.
    const asApp: Record<string, unknown> = await importFacadeAs(appDir);
    const asTesting: Record<string, unknown> = await importFacadeAs(
      join(repoRoot, "packages", "testing"),
    );
    const create = asApp["createQueryClient"] as () => unknown;

    expect(create()).toBeInstanceOf(asTesting["QueryClient"] as new () => unknown);
  });
});
