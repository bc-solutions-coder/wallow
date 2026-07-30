/**
 * Devtools never reach a production bundle (Wallow-pu6a.5.7).
 *
 * No app in this repo mounts the TanStack Router or Query devtools today —
 * verified by sweep, not assumed — so the thing being shipped here is the
 * ABSENCE of a dependency edge, and the spec reads the dependency graph
 * (manifests + source imports) rather than exercising behaviour. That is the
 * same assertion shape as `packages/sdk/src/server/h3-free.test.ts`.
 *
 * Pinning an absence matters more than usual for this one because Wallow is a
 * fork-first base platform and every TanStack Start template ships the devtools
 * mounted unconditionally in the root route. A fork that copies that pattern
 * ships a multi-hundred-kB debug panel to end users, which is exactly what this
 * task exists to prevent. So the rule is encoded rather than merely observed:
 *
 *   1. A devtools package may never sit in an app's `dependencies` — devtools
 *      are build/dev tooling, so `devDependencies` is their only home.
 *   2. No app module may STATICALLY import a devtools module. A static import
 *      is an unconditional graph edge: `import.meta.env.DEV` around the JSX
 *      does not remove it, it only stops it rendering, and the code is still in
 *      the bundle.
 *   3. The one permitted form is a DYNAMIC `import("…devtools…")` in a file
 *      that also carries a dev-mode guard, which Vite splits into its own chunk
 *      that a production build never requests.
 *
 * The detectors are proven against a fixture tree containing each violation
 * before they are pointed at the real `apps/` tree, so a green repo sweep is
 * evidence of compliance rather than of a scanner that finds nothing.
 *
 * This spec lives in the query package, the shared TanStack Query facade — it
 * already owns the `QueryClient` factory every app boots from, and the devtools
 * in question are the panels for that client and its router. No single app owns
 * a rule that binds all three.
 */
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readdirSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, describe, expect, it } from "vitest";

// packages/query/src -> repo root (src -> query -> packages -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const appsDir: string = resolve(repoRoot, "apps");

/**
 * This spec names devtools specifiers in its own regexes and fixtures, so
 * scanning itself would be a permanent false positive. It sits outside `apps/`
 * and the repo sweep never reaches it, but the skip is explicit so that
 * re-pointing the sweep at `packages/` later cannot resurrect the problem.
 */
const SELF: string = fileURLToPath(import.meta.url);

/**
 * Directories a sweep never descends into: installed packages and build output
 * are not authored modules, and a stale `dist/` would report an import the
 * current source no longer has.
 */
const SKIPPED_DIRECTORIES: ReadonlySet<string> = new Set([
  "node_modules",
  "dist",
  "build",
  ".output",
  ".nitro",
  ".vite",
  "coverage",
  "test-results",
  "playwright-report",
]);

/** The four dependency groups npm/pnpm install from. */
const DEPENDENCY_GROUPS: readonly string[] = [
  "dependencies",
  "devDependencies",
  "peerDependencies",
  "optionalDependencies",
];

type ManifestGroups = Record<string, Record<string, string> | undefined>;

function readManifest(path: string): ManifestGroups {
  return JSON.parse(readFileSync(path, "utf8")) as ManifestGroups;
}

/** Directory entries a sweep descends into: authored source, not tooling output. */
function scannableEntries(dir: string): string[] {
  return readdirSync(dir).filter(
    (entry: string): boolean => !entry.startsWith(".") && !SKIPPED_DIRECTORIES.has(entry),
  );
}

/** Every `.ts`/`.tsx` file under the given directory, recursively. */
function sourceFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of scannableEntries(dir)) {
    const full: string = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...sourceFiles(full));
    } else if (full.endsWith(".ts") || full.endsWith(".tsx")) {
      found.push(full);
    }
  }
  return found;
}

/**
 * Every workspace app under `apps/`, as a repo-relative path — an app being any
 * directory with its own `package.json`, so the nesting of `apps/examples/*` is
 * discovered rather than spelled out. Derived, not listed, so that adding or
 * renaming an app cannot leave this sweep reading a path that no longer exists.
 */
function workspaceApps(dir: string): string[] {
  if (existsSync(join(dir, "package.json"))) {
    return [relative(repoRoot, dir)];
  }
  const found: string[] = [];
  for (const entry of scannableEntries(dir)) {
    const full: string = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...workspaceApps(full));
    }
  }
  return found;
}

/**
 * A package name is a devtools package when one of its `/`-separated segments
 * IS `devtools` or ENDS `-devtools`: `@tanstack/react-query-devtools`,
 * `@tanstack/react-router-devtools`, `@vitejs/devtools`.
 *
 * Deliberately not a bare `includes("devtools")` substring test, which would
 * also flag `@jsdevtools/ono` — a JSON-schema helper already in this repo's
 * tree that has nothing to do with a debug panel.
 */
function isDevtoolsPackage(name: string): boolean {
  return name
    .replace(/^@/u, "")
    .split("/")
    .some((segment: string): boolean => segment === "devtools" || segment.endsWith("-devtools"));
}

/**
 * The package a module specifier resolves to, with any subpath dropped:
 * `@tanstack/react-query-devtools/production` -> `@tanstack/react-query-devtools`.
 * Relative specifiers have no package and answer `undefined`.
 */
function packageOfSpecifier(specifier: string): string | undefined {
  if (specifier.startsWith(".") || specifier.startsWith("/")) {
    return undefined;
  }
  const segments: string[] = specifier.split("/");
  return specifier.startsWith("@") ? segments.slice(0, 2).join("/") : segments[0];
}

/** `import("x")` — the code-split form, the only one allowed to name devtools. */
const DYNAMIC_IMPORT_PATTERN: RegExp = /\bimport\s*\(\s*["']([^"']+)["']\s*\)/gu;

/**
 * An unconditional module edge: `… from "x"`, a bare side-effect `import "x"`,
 * or `require("x")`. Applied only AFTER the dynamic imports have been stripped
 * out, so the two forms cannot both claim the same occurrence.
 */
const STATIC_IMPORT_PATTERN: RegExp = /(?:\bfrom|^\s*import|\brequire\s*\()\s*["']([^"']+)["']/gmu;

/**
 * A dev-only build guard. Vite substitutes `import.meta.env.DEV` at build time,
 * so a production build folds the branch away and never emits the dynamic
 * chunk's request; the `NODE_ENV` spelling is accepted for the same reason.
 */
const DEV_GUARD_PATTERN: RegExp =
  /import\.meta\.env\.DEV|process\.env\.NODE_ENV\s*!==\s*["']production["']/u;

interface ModuleSpecifiers {
  readonly static: readonly string[];
  readonly dynamic: readonly string[];
}

function moduleSpecifiers(source: string): ModuleSpecifiers {
  const dynamic: string[] = [...source.matchAll(DYNAMIC_IMPORT_PATTERN)].map(
    (match: RegExpMatchArray): string => match[1],
  );
  const withoutDynamic: string = source.replace(DYNAMIC_IMPORT_PATTERN, "");
  const staticSpecifiers: string[] = [...withoutDynamic.matchAll(STATIC_IMPORT_PATTERN)].map(
    (match: RegExpMatchArray): string => match[1],
  );
  return { static: staticSpecifiers, dynamic };
}

/** A devtools reference a production bundle would carry, with the file that made it. */
interface DevtoolsOffence {
  readonly file: string;
  readonly specifier: string;
  readonly reason: "static-import" | "ungated-dynamic-import";
}

/**
 * Every way the given tree pulls devtools into a production bundle. A gated
 * dynamic import is not an offence — that is the supported way to mount them.
 */
function devtoolsOffences(root: string): DevtoolsOffence[] {
  const offences: DevtoolsOffence[] = [];
  for (const file of sourceFiles(root).filter((path: string): boolean => path !== SELF)) {
    const source: string = readFileSync(file, "utf8");
    const specifiers: ModuleSpecifiers = moduleSpecifiers(source);
    const gated: boolean = DEV_GUARD_PATTERN.test(source);

    for (const specifier of specifiers.static) {
      const pkg: string | undefined = packageOfSpecifier(specifier);
      if (pkg !== undefined && isDevtoolsPackage(pkg)) {
        offences.push({ file: relative(repoRoot, file), specifier, reason: "static-import" });
      }
    }
    for (const specifier of specifiers.dynamic) {
      const pkg: string | undefined = packageOfSpecifier(specifier);
      if (pkg !== undefined && isDevtoolsPackage(pkg) && !gated) {
        offences.push({
          file: relative(repoRoot, file),
          specifier,
          reason: "ungated-dynamic-import",
        });
      }
    }
  }
  return offences;
}

/** Devtools packages declared as production runtime deps of the given manifest. */
function devtoolsRuntimeDependencies(manifestPath: string): string[] {
  const manifest: ManifestGroups = readManifest(manifestPath);
  return Object.keys(manifest.dependencies ?? {}).filter((name: string): boolean =>
    isDevtoolsPackage(name),
  );
}

describe("the devtools guard recognises a devtools package", () => {
  it.each([
    "@tanstack/react-query-devtools",
    "@tanstack/react-router-devtools",
    "@tanstack/react-devtools",
    "@vitejs/devtools",
    "devtools",
  ])("flags %s", (name: string) => {
    expect(isDevtoolsPackage(name)).toBe(true);
  });

  it.each([
    "@tanstack/react-query",
    "@tanstack/react-router",
    // Already in this repo's tree via the OpenAPI toolchain: a substring match
    // on "devtools" would flag it, and it is not a debug panel.
    "@jsdevtools/ono",
    "react",
  ])("does not flag %s", (name: string) => {
    expect(isDevtoolsPackage(name)).toBe(false);
  });

  it("reads through a subpath to the package", () => {
    expect(packageOfSpecifier("@tanstack/react-query-devtools/production")).toBe(
      "@tanstack/react-query-devtools",
    );
    expect(packageOfSpecifier("./local-module")).toBeUndefined();
  });
});

describe("the devtools guard catches the ways devtools reach a bundle", () => {
  // A fixture app carrying one of each violation plus the compliant form, so
  // the detectors are shown to fail on real offending source before they are
  // pointed at `apps/` — where they currently find nothing.
  const fixtureRoot: string = mkdtempSync(join(tmpdir(), "wallow-devtools-guard-"));

  afterAll(() => {
    rmSync(fixtureRoot, { recursive: true, force: true });
  });

  function writeFixture(relativePath: string, contents: string): void {
    const full: string = join(fixtureRoot, relativePath);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, contents, "utf8");
  }

  writeFixture(
    "package.json",
    JSON.stringify({
      name: "fixture-app",
      dependencies: { "@tanstack/react-query-devtools": "^5.0.0", react: "^19.0.0" },
      devDependencies: { "@tanstack/react-router-devtools": "^1.0.0" },
    }),
  );
  writeFixture(
    "src/static-offender.tsx",
    [
      'import { ReactQueryDevtools } from "@tanstack/react-query-devtools";',
      "",
      "export function Panel() {",
      // The trap this whole spec exists to catch: the render is gated, but the
      // import is not, so the panel is still in the production bundle.
      "  return import.meta.env.DEV ? <ReactQueryDevtools /> : null;",
      "}",
    ].join("\n"),
  );
  writeFixture(
    "src/ungated-dynamic-offender.ts",
    'export const load = () => import("@tanstack/react-router-devtools");',
  );
  writeFixture(
    "src/compliant.ts",
    [
      "export const load = () =>",
      '  import.meta.env.DEV ? import("@tanstack/react-router-devtools") : undefined;',
    ].join("\n"),
  );

  it("flags a devtools package sitting in production dependencies", () => {
    expect(devtoolsRuntimeDependencies(join(fixtureRoot, "package.json"))).toEqual([
      "@tanstack/react-query-devtools",
    ]);
  });

  it("flags a static devtools import even when the RENDER is dev-gated", () => {
    const offences: DevtoolsOffence[] = devtoolsOffences(fixtureRoot);

    expect(offences).toContainEqual({
      file: relative(repoRoot, join(fixtureRoot, "src/static-offender.tsx")),
      specifier: "@tanstack/react-query-devtools",
      reason: "static-import",
    });
  });

  it("flags a dynamic devtools import with no dev guard around it", () => {
    const offences: DevtoolsOffence[] = devtoolsOffences(fixtureRoot);

    expect(offences).toContainEqual({
      file: relative(repoRoot, join(fixtureRoot, "src/ungated-dynamic-offender.ts")),
      specifier: "@tanstack/react-router-devtools",
      reason: "ungated-dynamic-import",
    });
  });

  it("accepts a dev-gated dynamic import — the supported way to mount devtools", () => {
    const offences: DevtoolsOffence[] = devtoolsOffences(fixtureRoot);
    const compliant: string = relative(repoRoot, join(fixtureRoot, "src/compliant.ts"));

    expect(offences.map((offence: DevtoolsOffence): string => offence.file)).not.toContain(
      compliant,
    );
  });
});

describe("no app ships devtools to production", () => {
  const apps: readonly string[] = workspaceApps(appsDir);

  it("finds the apps to sweep", () => {
    // Without this the derived cases below could go vacuously green if `apps/`
    // is ever restructured: an empty list runs no cases and fails nothing.
    expect(apps.length).toBeGreaterThan(0);
  });

  it.each(apps)("%s declares no devtools package in production dependencies", (app: string) => {
    expect(devtoolsRuntimeDependencies(resolve(repoRoot, app, "package.json"))).toEqual([]);
  });

  it.each(apps)("%s pulls devtools into no production module graph", (app: string) => {
    expect(devtoolsOffences(resolve(repoRoot, app))).toEqual([]);
  });

  it.each(apps)("%s declares the devtools it does use as dev-only", (app: string) => {
    // Nothing declares devtools anywhere today. If a fork adds them, this is
    // the group they belong in, and the case above is what keeps them there.
    const manifest: ManifestGroups = readManifest(resolve(repoRoot, app, "package.json"));
    const declared: string[] = DEPENDENCY_GROUPS.flatMap((group: string): string[] =>
      Object.keys(manifest[group] ?? {})
        .filter((name: string): boolean => isDevtoolsPackage(name))
        .map((): string => group),
    );

    expect(declared.filter((group: string): boolean => group !== "devDependencies")).toEqual([]);
  });
});
