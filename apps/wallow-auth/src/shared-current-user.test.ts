import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * "Who is signed in" has exactly ONE definition in this workspace, and it is not
 * in this app: it is `@bc-solutions-coder/auth` (Wallow-x4qn.9.2).
 *
 * wallow-auth carried a third variant of that query, hand-rolled inline inside
 * `routes/invitation.tsx`'s `InvitationRoute()`: the same generated key, its own
 * `queryFn` over the SDK's `getCurrentUser`, a local `retry: false`, and NEITHER
 * of the two things the canonical query adds — the 30-second `staleTime` and the
 * `sub` rename that satisfies the SDK's claim helpers. wallow-web's copy was
 * deleted by Wallow-x4qn.8 (`src/shared-auth.test.ts`, which this file mirrors);
 * this is the last one.
 *
 * A copy of an auth read is the failure that does not announce itself: both
 * copies resolve the SAME cache key, so nothing breaks until one drifts — and
 * this one had already drifted, in the direction of a probe that re-fetches on
 * every mount and stores a user the shared guards cannot read.
 *
 * So this spec pins the DELETION, and it pins it at the two levels a grep
 * cannot:
 *
 *  1. the route no longer names the hand-rolled probe's parts at all — the
 *     bead's own criterion, asserted against RAW source (prose included) so a
 *     doc comment left describing a `queryFn` this route no longer has cannot
 *     satisfy it;
 *  2. nothing ELSE under `src/` picked the probe up. A "deletion" that moved the
 *     same three lines into a local helper is not this task.
 *
 * The behaviour of the SURVIVING probe — what the route does with a 200, a 401,
 * a 500, and with a user the shared query already primed — is pinned in
 * `src/routes/invitation.test.tsx`, in a real browser against the real SDK.
 *
 * Node project: reads source as text and imports the linked package's built
 * `dist/`; it mounts nothing. Reading rather than importing is deliberate — the
 * module under test is a `.tsx` route that cannot be imported in a plain node
 * context.
 */

const srcDir: string = dirname(fileURLToPath(import.meta.url));
const appDir: string = resolve(srcDir, "..");

/** The shared authn layer, and the only door this app's auth reads come through. */
const AUTH = "@bc-solutions-coder/auth";

/** The query facade — named here only to assert the route stopped importing from it. */
const FACADE = "@bc-solutions-coder/query";

/** The route that hand-rolled the probe. */
const PROBE_ROUTE = "routes/invitation.tsx";

/** The two SDK symbols the hand-rolled probe was built out of. */
const PROBE_PARTS: readonly string[] = ["getCurrentUser", "usersGetCurrentUserQueryKey"];

/** The canonical query's staleTime — what makes a re-mounted probe a cache read. */
const CURRENT_USER_STALE_TIME_MS = 30_000;

/** This file, excluded from the source scans: it must name the deleted symbols. */
const SELF: string = relative(srcDir, fileURLToPath(import.meta.url));

function read(relativePath: string): string {
  return readFileSync(join(srcDir, relativePath), "utf8");
}

/** Source with comments removed, so prose about a symbol is not read as a use of it. */
function codeOf(relativePath: string): string {
  return read(relativePath)
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/**
 * Every hand-written module under `src/` except the specs and codegen.
 *
 * Specs are out because several of them legitimately NAME the retired symbols in
 * order to forbid them, or describe the probe in prose. `withFileTypes` matters:
 * Vitest browser mode writes failure screenshots into `__screenshots__/<spec>/`
 * directories, and a name-only filter would hand `readFileSync` a directory.
 */
function appSources(): readonly string[] {
  return readdirSync(srcDir, { recursive: true, withFileTypes: true })
    .filter(
      (entry): boolean =>
        entry.isFile() && (entry.name.endsWith(".ts") || entry.name.endsWith(".tsx")),
    )
    .map((entry): string => relative(srcDir, join(entry.parentPath, entry.name)))
    .filter((path): boolean => path !== SELF && path !== "routeTree.gen.ts")
    .filter((path): boolean => !/\.test\.tsx?$/u.test(path))
    .toSorted();
}

/**
 * The names a file imports from one module — value imports, `import type` lines
 * and inline `type` members alike, alias targets normalised away — so the green
 * phase stays free to arrange its import statements however it likes.
 */
function importedNamesFrom(relativePath: string, moduleSpecifier: string): readonly string[] {
  const escaped: string = moduleSpecifier.replaceAll(/[.*+?^${}()|[\]\\]/gu, String.raw`\$&`);
  const pattern = new RegExp(
    String.raw`import\s+(?:type\s+)?\{([^}]*)\}\s*from\s+"${escaped}"`,
    "gu",
  );

  return [...codeOf(relativePath).matchAll(pattern)].flatMap((match): readonly string[] =>
    (match[1] as string)
      .split(",")
      .map((name): string =>
        (
          name
            .trim()
            .replace(/^type\s+/u, "")
            .split(/\s+as\s+/u)[0] as string
        ).trim(),
      )
      .filter((name): boolean => name.length > 0),
  );
}

/** Where pnpm links a workspace package for this importer. */
function packageDir(name: string): string {
  return join(appDir, "node_modules", name);
}

interface PackageManifest {
  readonly name?: string;
  readonly exports?: Readonly<Record<string, Readonly<Record<string, string>>>>;
}

function linkedManifest(name: string): PackageManifest {
  const manifestPath: string = join(packageDir(name), "package.json");

  expect(existsSync(manifestPath), `${name} is not linked into wallow-auth`).toBe(true);

  return JSON.parse(readFileSync(manifestPath, "utf8")) as PackageManifest;
}

/**
 * Import a package THROUGH this app's own link, via the entry its exports map
 * names — a computed specifier, so the package resolves the way this app's
 * bundler resolves it rather than the way TypeScript would resolve a literal.
 */
async function importLinked(name: string): Promise<Record<string, unknown>> {
  const entry: string | undefined = linkedManifest(name).exports?.["."]?.["import"];

  expect(entry, `${name} declares no "." import entry`).toBeTruthy();

  const entryPath: string = join(packageDir(name), entry as string);

  expect(existsSync(entryPath), `${name} is not built (${entry} missing)`).toBe(true);

  return (await import(pathToFileURL(entryPath).href)) as Record<string, unknown>;
}

/**
 * `extraBrowserOptimizeDeps` as `vitest.config.ts` spells it, read as text: that
 * config is a module the Vitest config loader consumes, and importing it here
 * would boot a second browser provider just to read a list of strings.
 */
function extraBrowserOptimizeDeps(): readonly string[] {
  const block: string =
    readFileSync(join(appDir, "vitest.config.ts"), "utf8").match(
      /extraBrowserOptimizeDeps[^=]*=\s*\[([^\]]*)\]/su,
    )?.[1] ?? "";

  return [...block.matchAll(/"([^"]+)"/gu)].map((entry): string => entry[1] as string);
}

describe("the invitation route's hand-rolled current-user probe", () => {
  it("is scanning a source tree that still has the route in it", () => {
    // A guard on the guard: if the route moves, every case below would pass
    // vacuously on a file that is no longer there.
    expect(appSources()).toContain(PROBE_ROUTE);
  });

  it.each(PROBE_PARTS)("names %s nowhere, prose included", (symbol: string) => {
    // RAW source, not comment-stripped, which is the bead's criterion verbatim
    // (`grep -n <symbol> routes/invitation.tsx` finds nothing). The route's
    // auth-probe rationale survives this task; the sentences describing HOW the
    // probe was hand-wired do not, because they document an implementation that
    // now lives in `@bc-solutions-coder/auth`.
    expect(read(PROBE_ROUTE)).not.toContain(symbol);
  });

  it("declares no queryFn of its own", () => {
    // The probe's body. A route that still spells out a `queryFn` for `users/me`
    // has kept the copy and merely renamed its imports.
    expect(codeOf(PROBE_ROUTE)).not.toMatch(/queryFn/u);
  });

  it("takes no react-query hook from the facade any more", () => {
    // `useQuery` was in this route ONLY for the probe (Wallow-x4qn.9.1 swapped its
    // specifier and left the entry in `query-facade.test.ts` for this task to
    // retire). Subscribing is the shared hook's job now.
    expect(importedNamesFrom(PROBE_ROUTE, FACADE)).toEqual([]);
  });
});

describe("the invitation route's surviving current-user read", () => {
  it("comes from the shared auth package", () => {
    expect(importedNamesFrom(PROBE_ROUTE, AUTH)).toContain("useCurrentUser");
  });

  it("subscribes rather than gating: the hook, not the beforeLoad primer", () => {
    // `ensureCurrentUser` is the `beforeLoad` half of the contract, and it would
    // be the wrong half here: this route must RENDER for an anonymous visitor
    // (that is the whole point of the probe — a sign-in affordance), not resolve
    // the user before it renders. It is also the shape that would let a future
    // `requireAuth`-style gate creep onto a public invitation link.
    expect(importedNamesFrom(PROBE_ROUTE, AUTH)).not.toContain("ensureCurrentUser");
  });

  it("passes the request-scoped client off the router context", () => {
    // A module-global client would hand one request's session to the next under
    // SSR. `packages/auth`'s hook takes the client precisely so this stays the
    // caller's decision — see `packages/auth/src/use-current-user.ts`.
    expect(codeOf(PROBE_ROUTE)).toMatch(/sdk\.client/u);
  });
});

describe("no second current-user probe survives anywhere under src/", () => {
  it.each(PROBE_PARTS)("does not reach for %s from any module", (symbol: string) => {
    // Comment-stripped, because `features/invitation/components/InvitationScreen.tsx`
    // legitimately DESCRIBES the route's probe in prose and is not this task's to
    // edit. A code hit is a probe.
    for (const path of appSources()) {
      expect(codeOf(path), `${path} still reaches for ${symbol}`).not.toContain(symbol);
    }
  });

  it("re-declares neither the query nor its user type", () => {
    // The migration is a deletion, not a move under a new name.
    for (const path of appSources()) {
      expect(codeOf(path), `${path} redeclares currentUserQuery`).not.toMatch(
        /function\s+currentUserQuery/u,
      );
      expect(codeOf(path), `${path} redeclares the CurrentUser type`).not.toMatch(
        /(?:type|interface)\s+CurrentUser\b/u,
      );
    }
  });
});

describe("the auth package as wallow-auth resolves it", () => {
  it("hands this app the current-user hook", async () => {
    const auth: Record<string, unknown> = await importLinked(AUTH);

    for (const symbol of ["useCurrentUser", "currentUserQuery"]) {
      expect(typeof auth[symbol], `${AUTH} does not export ${symbol}`).toBe("function");
    }
  });

  it("holds a resolved user long enough that a re-mounted probe is a cache read", async () => {
    // The behavioural delta this task buys the invitation route, at its source:
    // the inline probe had no `staleTime`, so every mount re-asked `users/me`.
    const auth: Record<string, unknown> = await importLinked(AUTH);
    const currentUserQuery = auth["currentUserQuery"] as (client: unknown) => {
      staleTime?: number;
    };

    expect(currentUserQuery(fakeClient()).staleTime).toBe(CURRENT_USER_STALE_TIME_MS);
  });
});

describe("browser-mode pre-bundling covers the auth package", () => {
  it("registers it with the browser project rather than leaving it to discovery", () => {
    // A linked workspace package is not pre-bundled by default, and a dependency
    // discovered mid-run triggers a Vite reload that DROPS the runner instead of
    // failing a test. wallow-web already names this package for the same reason
    // (its `vitest.config.ts`), and the route specs in this app are the auth
    // flow's safety net — a silent reload there is the worst failure mode.
    const config: string = readFileSync(join(appDir, "vitest.config.ts"), "utf8");
    const inlinedForSsr: boolean = new RegExp(
      String.raw`noExternal:\s*\[[^\]]*"${AUTH}"`,
      "su",
    ).test(config);

    expect(inlinedForSsr || extraBrowserOptimizeDeps().includes(AUTH)).toBe(true);
  });
});

/**
 * The only thing a query KEY needs off a client: the base URL it is scoped to.
 * Building the options never issues a request, so a real transport would add
 * nothing here.
 */
function fakeClient(): { getConfig: () => { baseUrl: string } } {
  return { getConfig: () => ({ baseUrl: "http://wallow.test" }) };
}
