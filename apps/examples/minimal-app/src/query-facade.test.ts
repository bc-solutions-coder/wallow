import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The minimal reference app reaches TanStack Query through ONE door:
 * `@bc-solutions-coder/query`, the workspace facade. This spec is that door's
 * lock (Wallow-x4qn.10).
 *
 * Two kinds of assertion, because a facade is half manifest and half runtime:
 *
 *  1. STRUCTURAL — the manifest declares the facade and not
 *     `@tanstack/react-query`, and no file in the app imports react-query
 *     directly. The README table and `HelloCard`'s user-visible copy advertise
 *     the shared packages a fork gets, so this app is documentation as much as it
 *     is code.
 *  2. RUNTIME — the facade actually resolves *from this app's own
 *     `node_modules`* and hands back a working `createQueryClient` plus the
 *     react-query surface. Grep alone would pass on a manifest edit that pnpm
 *     never linked, and the whole point of the facade is that the app gets its
 *     react-query symbols and its `QueryClient` from a single module instance.
 *
 * Structural assertions read source as TEXT rather than importing it: the files
 * under test are a Vite config, a Start router factory and a route module, none
 * of which can be imported in a plain node context.
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

describe("minimal-app's imports", () => {
  it("takes createQueryClient and the QueryClient type from the facade", () => {
    const names: readonly string[] = importedNamesFrom(read("src/router.tsx"), FACADE);

    expect(names).toContain("createQueryClient");
    expect(names).toContain("QueryClient");
  });

  it("types the root route's context with the facade's QueryClient", () => {
    // `RouterContext.queryClient` and the client `src/router.tsx` constructs must
    // name the SAME type, or the router factory stops type-checking against its
    // own route tree.
    expect(importedNamesFrom(read("src/routes/__root.tsx"), FACADE)).toContain("QueryClient");
  });

  it("imports react-query nowhere in the app", () => {
    for (const [entry, source] of appSources()) {
      expect(importSpecifiers(source), entry).not.toContain(REACT_QUERY);
    }
  });

  it("teaches a fork the facade import in the README's first-query example", () => {
    // The README is this app's deliverable as much as its source is: it is the
    // copy-from recipe, so an example importing the banned door teaches the
    // banned door.
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

/**
 * Every hand-written file of this app — the root-level configs, manifest and
 * README, plus every source module under `src`. Specs are excluded (they must
 * name the banned packages in order to forbid them) and so is
 * `src/routeTree.gen.ts`, which is codegen.
 */
function appFiles(
  extensions: RegExp = /\.(?:tsx?|json|md)$/u,
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
