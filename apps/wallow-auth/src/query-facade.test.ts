import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * wallow-auth reaches TanStack Query through ONE door: `@bc-solutions-coder/query`,
 * the workspace facade. This spec is that door's lock (Wallow-x4qn.9.1).
 *
 * This app is the login / signup / MFA surface, which is why the swap gets a spec
 * of its own rather than being trusted to the 47 component specs beside it. A
 * facade swap is "mechanical" only in the sense that every edit is small; the two
 * ways it goes wrong are both silent:
 *
 *  1. A DROPPED NAME. Fourteen modules import `useQuery`, `useMutation` or both.
 *     Rewriting `import { useMutation, useQuery } from …` and losing one half
 *     leaves a file that still compiles (TypeScript resolves the survivor) but
 *     whose screen no longer submits or no longer loads. {@link REACT_QUERY_USERS}
 *     is therefore a per-file table, not a global "somebody imports the facade"
 *     check: a name that disappears is named in the failure.
 *  2. A SECOND COPY of react-query. Two copies in one module graph give two
 *     `QueryClientProvider` React contexts, and a `useMutation` from copy B
 *     inside a provider from copy A throws "No QueryClient set" the moment the
 *     user presses Sign in. Nothing about that is visible in a diff, and it is
 *     not visible to a grep either — it lives in the resolution graph. So the
 *     last two describes leave the source behind and assert against the modules
 *     as this app, `@bc-solutions-coder/testing` (which mounts the provider every
 *     component spec renders under) and `@bc-solutions-coder/forms` (which owns
 *     the migrated password-recovery screens) actually resolve them.
 *
 * Structural assertions read source as TEXT rather than importing it: the files
 * under test are a Start router factory, route modules and `.tsx` screens, none
 * of which can be imported in a plain node context. Comments are stripped first,
 * so a doc comment that legitimately discusses the old import (this file does)
 * is not mistaken for one.
 *
 * Node project — it reads files and dynamically imports built `dist/` output; it
 * mounts nothing.
 */

/** The one place react-query is allowed to enter this workspace. */
const FACADE = "@bc-solutions-coder/query";

/**
 * The shared authn layer this app must declare here. Declared by Wallow-x4qn.9.1
 * so the install, the lockfile and the Dockerfile COPY lines moved exactly once;
 * first USED by Wallow-x4qn.9.2, which swapped `routes/invitation.tsx`'s
 * hand-rolled current-user probe onto `useCurrentUser`.
 */
const AUTH = "@bc-solutions-coder/auth";

/** The package no consumer may import or declare for itself. */
const RAW = "@tanstack/react-query";

// This spec lives at src/, so one level up reaches the app root and three
// reaches the repo root (src -> wallow-auth -> apps -> repo).
const srcDir: string = dirname(fileURLToPath(import.meta.url));
const appDir: string = resolve(srcDir, "..");
const repoRoot: string = resolve(appDir, "..", "..");

/** This file, excluded from the source scans below: it must name the banned doors. */
const SELF: string = relative(srcDir, fileURLToPath(import.meta.url));

/**
 * Every react-query name each module imports, keyed by its path under `src/`.
 *
 * The KEYS are the swap's checklist — every module that reached react-query
 * before must reach the facade after, and no module may quietly drop out. The
 * VALUES are the anti-drop guard from the header: the exact names that must
 * survive the rewrite, whether they ride as values or as `import type`.
 *
 * `routes/invitation.tsx` is NOT here: Wallow-x4qn.9.1 swapped its specifier like
 * every other module's, and Wallow-x4qn.9.2 then deleted the hand-rolled
 * current-user probe that `useQuery` existed for — the route reads the visitor
 * through `@bc-solutions-coder/auth`'s `useCurrentUser` now and takes no
 * react-query name of its own. `src/shared-current-user.test.ts` owns that route.
 */
const REACT_QUERY_USERS: Readonly<Record<string, readonly string[]>> = {
  "app/router.tsx": ["QueryClient"],
  "app/routes/__root.tsx": ["QueryClient"],
  "app/routes/login.tsx": ["useQuery"],
  "features/consent/components/ConsentScreen.tsx": ["useQuery"],
  "features/invitation/components/InvitationScreen.tsx": ["useMutation", "useQuery"],
  "features/login/components/ExternalProviders.test.tsx": ["QueryClient"],
  "features/login/components/ExternalProviders.tsx": ["useQuery"],
  "features/login/components/MagicLinkLoginForm.tsx": ["useMutation"],
  "features/login/components/OtpLoginForm.tsx": ["useMutation"],
  "features/login/components/PasswordLoginForm.tsx": ["useMutation"],
  "features/logout/components/LogoutScreen.tsx": ["useQuery"],
  "features/mfa-challenge/components/MfaChallengeForm.tsx": ["useMutation", "useQuery"],
  "features/mfa-enroll/components/MfaEnrollForm.tsx": ["useMutation"],
  "features/register/components/RegisterForm.tsx": ["useMutation", "useQuery"],
  "features/verify-email/components/VerifyEmailConfirm.tsx": ["useQuery"],
};

/**
 * Dependencies the swap must NOT take with it. Two of them are the reason the
 * app renders at all (`react`, `@tanstack/react-router`); the other three are
 * the packages whose own react-query now arrives through the same facade, so an
 * over-eager "drop the query deps" edit is a live risk.
 */
const SURVIVING_DEPENDENCIES: readonly string[] = [
  "@bc-solutions-coder/forms",
  "@bc-solutions-coder/sdk",
  "@bc-solutions-coder/testing",
  "@bc-solutions-coder/ui",
  "@tanstack/react-router",
  "@tanstack/react-router-ssr-query",
  "react",
];

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
 * Every hand-written TypeScript module under `src/`, specs included — a spec
 * that keeps its own `QueryClient` binding is the copy-identity bug in
 * miniature, so they are in scope rather than exempt.
 *
 * `withFileTypes` + `isFile()` matters: Vitest browser mode writes failure
 * screenshots into `src/**\/__screenshots__/<spec>.test.tsx/` directories, and a
 * name-only filter would hand `readFileSync` a directory. `routeTree.gen.ts` is
 * codegen and this file is the guard itself.
 */
function appSources(): readonly string[] {
  return readdirSync(srcDir, { recursive: true, withFileTypes: true })
    .filter(
      (entry): boolean =>
        entry.isFile() && (entry.name.endsWith(".ts") || entry.name.endsWith(".tsx")),
    )
    .map((entry): string => relative(srcDir, join(entry.parentPath, entry.name)))
    .filter((path): boolean => path !== SELF && !path.endsWith("routeTree.gen.ts"))
    .toSorted();
}

/** Source with comments removed, so prose about an import is not read as one. */
function codeOf(relativePath: string): string {
  return readText(join(srcDir, relativePath))
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/**
 * Every module specifier a file pulls from: `import … from`, `export … from` and
 * bare side-effect imports alike. Read off comment-stripped code.
 */
function moduleSpecifiers(relativePath: string): readonly string[] {
  const code: string = codeOf(relativePath);

  return [
    ...[...code.matchAll(/\bfrom\s+"([^"]+)"/gu)].map((match): string => match[1] as string),
    ...[...code.matchAll(/^\s*import\s+"([^"]+)"/gmu)].map((match): string => match[1] as string),
  ];
}

/**
 * The names a file imports from one module — value imports, `import type` lines
 * and inline `type` members alike, alias targets normalised away — so the swap
 * stays free to merge `createQueryClient` and `QueryClient` into a single
 * statement or keep them apart.
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

/** Files under `src/` whose imports name `specifier`. */
function filesImportingFrom(specifier: string): readonly string[] {
  return appSources().filter((path): boolean => moduleSpecifiers(path).includes(specifier));
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

  it.each(SURVIVING_DEPENDENCIES)("still declares %s", (dependency: string) => {
    expect(Object.keys(everyDeclaredDependency())).toContain(dependency);
  });
});

describe("wallow-auth's react-query imports", () => {
  it("scans a source tree that actually has the modules under test in it", () => {
    // A guard on the guard: a broken scan would make every case below pass
    // vacuously, and this is a table-driven spec over paths that can move.
    const scanned: readonly string[] = appSources();

    for (const path of Object.keys(REACT_QUERY_USERS)) {
      expect(scanned, `${path} is not in the scanned source tree`).toContain(path);
    }
  });

  it.each(Object.entries(REACT_QUERY_USERS))(
    "%s takes its react-query names from the facade",
    (path: string, names: readonly string[]) => {
      // Names, not just the specifier: collapsing `{ useMutation, useQuery }` to
      // one of the two still type-checks and still passes a specifier-only
      // check, while leaving a screen that cannot submit or cannot load.
      expect(importedNamesFrom(path, FACADE)).toEqual(expect.arrayContaining([...names]));
    },
  );

  it("leaves no direct react-query import anywhere under src/", () => {
    // Named as a list, so a failure reports WHICH module regressed.
    expect(filesImportingFrom(RAW)).toEqual([]);
  });

  it("takes createQueryClient from the facade in the router factory", () => {
    // `getRouter()` is called once per SSR request and once in the browser; the
    // client it builds is the one every screen's hooks resolve against, so this
    // is the single import that decides which copy the whole app runs on.
    expect(importedNamesFrom("app/router.tsx", FACADE)).toContain("createQueryClient");
  });

  it("has at least as many facade importers as it had react-query importers", () => {
    expect(filesImportingFrom(FACADE).length).toBeGreaterThanOrEqual(
      Object.keys(REACT_QUERY_USERS).length,
    );
  });
});

describe("browser-mode pre-bundling survives the facade hop", () => {
  it("registers the facade with the browser project rather than leaving it to discovery", () => {
    // A linked workspace package is not pre-bundled by default, and a dependency
    // discovered mid-run triggers a Vite reload that DROPS the runner instead of
    // failing a test — the worst failure mode in this app, whose specs are the
    // auth-flow safety net. Which of the two fixes to use (pre-bundle the facade
    // or inline it for SSR) is the green phase's empirical call; this only pins
    // that the choice was made explicitly.
    const config: string = readText(join(appDir, "vitest.config.ts"));
    const inlinedForSsr: boolean = /noExternal:\s*\[[^\]]*"@bc-solutions-coder\/query"/su.test(
      config,
    );

    expect(inlinedForSsr || browserPreBundleList().includes(FACADE)).toBe(true);
  });

  it("does not pre-bundle react-query under its own name", () => {
    // Under pnpm's strict `node_modules` an app that no longer declares
    // react-query cannot resolve it, and an unresolvable `optimizeDeps` entry is
    // a WARNING after which Vite pre-bundles nothing — the same silent reload,
    // now with a config that looks correct. packages/forms hit this first.
    expect(browserPreBundleList()).not.toContain(RAW);
  });

  it("keeps the extras this app already needed pre-bundled", () => {
    expect(browserPreBundleList()).toContain("zod");
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
