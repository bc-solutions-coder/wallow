import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";
import { describe, expect, it } from "vitest";

/**
 * "Who is signed in" has exactly ONE definition in this workspace, and it is not
 * in this app: it is `@bc-solutions-coder/auth` (Wallow-x4qn.8).
 *
 * wallow-web carried a byte-for-byte copy of that query in
 * `src/lib/current-user.ts` — the same generated operation, the same generated
 * key, the same `sub` rename, the same 30-second `staleTime` — and wallow-auth
 * carried a third variant inline. Copies of an auth read are the failure that
 * does not announce itself: they resolve the SAME cache key, so nothing breaks
 * until one copy drifts (a different `staleTime`, a 401 no longer softened to
 * `null`) and the gate on one route starts disagreeing with the gate on another.
 *
 * So this spec pins the deletion, not just the addition:
 *
 *  1. `src/lib/current-user.ts` is GONE and nothing imports it any more — an app
 *     that keeps both the package and its own copy has not migrated.
 *  2. The two route gates (`/` and `/dashboard`) read the user through the
 *     package, and still PRIME it into the request's query cache
 *     (`ensureQueryData`, directly or via the package's `ensureCurrentUser`)
 *     rather than fetching per navigation.
 *  3. The package as this app resolves it exposes the auth surface the app needs,
 *     and its query key is the SAME generated key the profile screen's read uses
 *     (`src/features/settings/api.ts`) — one cache entry for one resource, which
 *     is the property the deleted local copy existed to provide.
 *
 * Node project: reads files and imports the linked package, mounts nothing.
 */

const here: string = dirname(fileURLToPath(import.meta.url));
const appRoot: string = resolve(here, "..");

const AUTH = "@bc-solutions-coder/auth";

/** The current-user layer's staleTime, which keeps a per-navigation gate cheap. */
const CURRENT_USER_STALE_TIME_MS = 30_000;

/** The route gates that read the signed-in user. */
const GATE_ROUTES: readonly string[] = [
  "src/app/routes/index.tsx",
  "src/app/routes/dashboard/route.tsx",
];

const read = (relativePath: string): string => readFileSync(resolve(appRoot, relativePath), "utf8");

describe("the app's own current-user copy", () => {
  it("looks in a directory that is really there", () => {
    // The two cases below assert a path does NOT exist, which a stale directory
    // name satisfies for free — `src/lib/` became `src/shared/lib/` in the zone
    // restructure, and neither case would have noticed. Prove the directory this
    // spec scans exists and holds modules before trusting an absence in it.
    const sharedLib: string = resolve(appRoot, "src/shared/lib");

    expect(existsSync(sharedLib)).toBe(true);
    expect(readdirSync(sharedLib).length).toBeGreaterThan(0);
  });

  it("no longer exists", () => {
    expect(existsSync(resolve(appRoot, "src/shared/lib/current-user.ts"))).toBe(false);
  });

  it("leaves no co-located spec behind", () => {
    expect(existsSync(resolve(appRoot, "src/shared/lib/current-user.test.ts"))).toBe(false);
  });

  it("is imported from nowhere", () => {
    // A relative `current-user` specifier surviving anywhere is either a dangling
    // import or a second copy under a new name.
    for (const [entry, source] of appSources()) {
      for (const specifier of importSpecifiers(source)) {
        expect(specifier, `${entry} still imports ${specifier}`).not.toMatch(/current-user/u);
      }
    }
  });

  it("is not re-declared under another name", () => {
    // The migration is a deletion, not a move: no module in this app defines the
    // query or the type again.
    for (const [entry, source] of appSources()) {
      expect(source, `${entry} redeclares currentUserQuery`).not.toMatch(
        /export\s+(?:async\s+)?function\s+currentUserQuery/u,
      );
      expect(source, `${entry} redeclares the CurrentUser type`).not.toMatch(
        /export\s+(?:type|interface)\s+CurrentUser\b/u,
      );
    }
  });
});

describe.each(GATE_ROUTES)("%s reads the shared current-user layer", (relativePath: string) => {
  it("imports its current-user read from the auth package", () => {
    // Either shape is idiomatic: the query plus `ensureQueryData`, or the
    // package's `ensureCurrentUser` primer that composes exactly that pair.
    const names: readonly string[] = importedNamesFrom(read(relativePath), AUTH);

    expect(
      names.includes("currentUserQuery") || names.includes("ensureCurrentUser"),
      `${relativePath} imports neither currentUserQuery nor ensureCurrentUser from ${AUTH}`,
    ).toBe(true);
  });

  it("still primes the user into the request's query cache", () => {
    // `ensureQueryData` (not `fetchQuery`) is what makes the 30s staleTime turn a
    // gate on every navigation into a cache read. `ensureCurrentUser` is that
    // call, so either spelling satisfies this.
    const source: string = read(relativePath);

    expect(source).toMatch(/ensureQueryData|ensureCurrentUser/u);
  });

  it("reads the request's own client, never a module-global one", () => {
    // A module-global client would hand one request's session to the next under
    // SSR. The client comes off the router context.
    expect(read(relativePath)).toMatch(/context\.sdk\.client/u);
  });
});

describe("the auth package as this app resolves it", () => {
  it("is linked into the app's own node_modules", () => {
    // pnpm links a package into an importer's node_modules only when that
    // importer declares it, so this is the manifest edit having taken effect
    // rather than merely being written down.
    expect(existsSync(packageDir(AUTH)), `${AUTH} is not linked into wallow-web`).toBe(true);
    expect(linkedManifest(AUTH).name).toBe(AUTH);
  });

  it("exposes the current-user layer and the route guards from one barrel", async () => {
    // The point of the package: an app's auth imports come from ONE place instead
    // of being split between a local module and the SDK.
    const auth: Record<string, unknown> = await importLinked(AUTH);

    for (const symbol of [
      "currentUserQuery",
      "ensureCurrentUser",
      "useCurrentUser",
      "hasRole",
      "hasPermission",
      "isAdmin",
      "requireAuth",
      "loginRedirect",
    ]) {
      expect(typeof auth[symbol], `${AUTH} does not export ${symbol}`).toBe("function");
    }
  });

  it("keys the current-user query with the generated key the profile read uses", async () => {
    // `src/features/settings/api.ts` re-exports `usersGetCurrentUserQueryKey`, so
    // the profile screen and the route gates share ONE cache entry — the property
    // the deleted local copy existed to provide, and the one a hand-rolled key
    // ('user','current') silently loses.
    const auth: Record<string, unknown> = await importLinked(AUTH);
    const currentUserQuery = auth["currentUserQuery"] as (client: unknown) => {
      queryKey: readonly unknown[];
      staleTime?: number;
    };

    expect(currentUserQuery(fakeClient()).queryKey).toEqual(
      usersGetCurrentUserQueryKey({ client: fakeClient() as never }),
    );
  });

  it("holds a resolved user long enough that a per-navigation gate stays a cache read", async () => {
    const auth: Record<string, unknown> = await importLinked(AUTH);
    const currentUserQuery = auth["currentUserQuery"] as (client: unknown) => {
      staleTime?: number;
    };

    expect(currentUserQuery(fakeClient()).staleTime).toBe(CURRENT_USER_STALE_TIME_MS);
  });
});

/**
 * The only thing a query KEY needs off a client: the base URL it is scoped to.
 * Building the key never issues a request, so a real transport would add nothing.
 */
function fakeClient(): { getConfig: () => { baseUrl: string } } {
  return { getConfig: () => ({ baseUrl: "https://wallow.test/api" }) };
}

/** Where pnpm links a workspace package for this importer. */
function packageDir(name: string): string {
  return resolve(appRoot, "node_modules", name);
}

interface PackageManifest {
  readonly name?: string;
  readonly exports?: Readonly<Record<string, Readonly<Record<string, string>>>>;
}

function linkedManifest(name: string): PackageManifest {
  const manifestPath: string = resolve(packageDir(name), "package.json");

  expect(existsSync(manifestPath), `${name} is not linked into wallow-web`).toBe(true);

  return JSON.parse(readFileSync(manifestPath, "utf8")) as PackageManifest;
}

/**
 * Import a package THROUGH the app's link, via the entry its own exports map
 * names — a computed specifier, so this spec resolves the package exactly the way
 * the app's bundler does instead of TypeScript resolving it at compile time.
 */
async function importLinked(name: string): Promise<Record<string, unknown>> {
  const entry: string | undefined = linkedManifest(name).exports?.["."]?.["import"];

  expect(entry, `${name} declares no "." import entry`).toBeTruthy();

  const entryPath: string = resolve(packageDir(name), entry as string);

  expect(existsSync(entryPath), `${name} is not built (${entry} missing)`).toBe(true);

  return (await import(pathToFileURL(entryPath).href)) as Record<string, unknown>;
}

/**
 * Every hand-written TypeScript module of this app — the root-level configs plus
 * everything under `src`. Specs are excluded (they name the retired module in
 * order to forbid it) and so is `src/routeTree.gen.ts`, which is codegen.
 */
function appSources(): readonly (readonly [string, string])[] {
  const extensions: RegExp = /\.tsx?$/u;

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

/** Every module specifier the file imports from, `import type` included. */
function importSpecifiers(source: string): readonly string[] {
  return [...source.matchAll(/^\s*import\s[^;]*?from\s+"([^"]+)"/gmu)].map(
    (match: RegExpMatchArray) => match[1] as string,
  );
}

/**
 * The names a file imports from one module — value and type imports alike, alias
 * targets normalised away — so a rename can move a symbol to the auth package
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
