import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The minimal reference app reaches TanStack Query through ONE door:
 * `@bc-solutions-coder/query`, the workspace facade. This spec is that door's
 * lock (Wallow-x4qn.10).
 *
 * THE IMPORT HALF OF THAT LOCK IS NOW LINT'S (Wallow-l5x2). The root
 * `.oxlintrc.json` restricts `@tanstack/react-query` outright, and the two lint
 * passes between them reach every source file and every spec — which is all the
 * regex sweep this file used to run over `src` could say, and it said it later and
 * without a line number.
 *
 * Three things survive, none of them readable by a rule:
 *
 *  1. MANIFEST — the app declares the facade and not react-query. Declaring both
 *     is how a second copy of the library, and with it a second `QueryClient`
 *     identity, gets in.
 *  2. RUNTIME — the facade actually resolves *from this app's own `node_modules`*
 *     and hands back a working `createQueryClient` plus the react-query surface.
 *     Lint would pass on a manifest edit that pnpm never linked.
 *  3. THE README — this app is a copy-from recipe, and its prose is markdown, which
 *     no linter in this repo reads. An example importing the banned door teaches
 *     the banned door.
 */

const here: string = dirname(fileURLToPath(import.meta.url));
const appRoot: string = resolve(here, "..");

/** The facade package this app must consume, and the door it must not use. */
const FACADE = "@bc-solutions-coder/query";
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

describe("minimal-app's dependency manifest", () => {
  it("declares the query facade as a workspace dependency", () => {
    expect(manifest().dependencies?.[FACADE]).toBe("workspace:*");
  });

  it("does not declare react-query directly", () => {
    // Not "also declares" — declaring both the facade and react-query is how a
    // second copy of the library (and a second `QueryClient` identity) gets in.
    expect(Object.keys(declaredDeps())).not.toContain(REACT_QUERY);
  });
});

describe("the README a fork copies from", () => {
  it("teaches the facade import in its first-query example", () => {
    // The README is this app's deliverable as much as its source is: it is the
    // copy-from recipe, so an example importing the banned door teaches the
    // banned door. Markdown reaches no linter here, so this stays a spec.
    const readme: string = read("README.md");

    expect(readme).toContain(`from "${FACADE}"`);
    expect(readme).not.toContain(`from "${REACT_QUERY}"`);
  });
});

describe("the facade as this app resolves it", () => {
  it("is linked into the app's own node_modules", () => {
    // pnpm links a package into an importer's node_modules only when that
    // importer declares it, so this is the manifest edit having actually taken
    // effect rather than merely being written down.
    expect(existsSync(facadeDir()), `${FACADE} is not linked into minimal-app`).toBe(true);
    expect(linkedFacadeManifest().name).toBe(FACADE);
  });

  it("hands the app a QueryClient factory and the whole react-query surface", async () => {
    const facade: Record<string, unknown> = await importLinkedFacade();

    expect(typeof facade["createQueryClient"]).toBe("function");
    expect(typeof facade["useQuery"]).toBe("function");
    expect(typeof facade["QueryClientProvider"]).toBe("function");
    expect(typeof facade["QueryClient"]).toBe("function");
  });

  it("gives the router a retry-disabled client of the facade's own QueryClient type", async () => {
    const facade: Record<string, unknown> = await importLinkedFacade();
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

/** Where pnpm links the facade for this importer. */
function facadeDir(): string {
  return resolve(appRoot, "node_modules", FACADE);
}

function linkedFacadeManifest(): PackageManifest {
  const manifestPath: string = resolve(facadeDir(), "package.json");

  expect(existsSync(manifestPath), `${FACADE} is not linked into minimal-app`).toBe(true);

  return JSON.parse(readFileSync(manifestPath, "utf8")) as PackageManifest;
}

/**
 * Import the facade THROUGH the app's link, via the entry its own exports map
 * names — a computed specifier, so this spec resolves the package exactly the
 * way the app's bundler does instead of TypeScript resolving it at compile time.
 */
async function importLinkedFacade(): Promise<Record<string, unknown>> {
  const entry: string | undefined = linkedFacadeManifest().exports?.["."]?.["import"];

  expect(entry, `${FACADE} declares no "." import entry`).toBeTruthy();

  const entryPath: string = resolve(facadeDir(), entry as string);

  expect(existsSync(entryPath), `${FACADE} is not built (${entry} missing)`).toBe(true);

  return (await import(pathToFileURL(entryPath).href)) as Record<string, unknown>;
}
