/**
 * Facade-routing guard (Wallow-x4qn.4): `@bc-solutions-coder/testing` must reach
 * TanStack Query through `@bc-solutions-coder/query`, never through
 * `@tanstack/react-query` directly.
 *
 * Why this is worth a spec rather than left to the repo-root oxlint
 * `no-restricted-imports` rule: the import statement is only half of it. The
 * other half is the MANIFEST — a package that keeps react-query in its own
 * `dependencies`/`peerDependencies` can still resolve a second copy through its
 * own `node_modules`, and two copies of react-query in one graph give two
 * `QueryClientProvider` React contexts (a `useQuery` from copy B inside a
 * provider from copy A throws "No QueryClient set" at runtime). Lint sees the
 * import; only this spec sees the resolution path that makes the import safe.
 *
 * Vitest browser-mode pre-bundling is pinned here too, and deliberately does NOT
 * treat the facade as a rename of react-query: `@tanstack/react-query` is still
 * the module Vite pre-bundles (the facade re-exports it, it does not replace it),
 * so dropping it from `optimizeDeps.include` would let Vite discover it mid-run
 * and reload — which drops the runner rather than failing a test.
 *
 * Pure-logic spec: it reads this package's own manifest and sources, so it runs
 * in the node project.
 */
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/** The one place react-query is allowed to enter this workspace. */
const FACADE = "@bc-solutions-coder/query";
/** The package no consumer may import or declare for itself. */
const RAW = "@tanstack/react-query";

// This spec lives at src/, so ONE level up reaches the package root.
const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..");

function readText(relativePath: string): string {
  return readFileSync(join(packageDir, relativePath), "utf8");
}

function readPackageJson(): Record<string, Record<string, string> | undefined> {
  return JSON.parse(readText("package.json")) as Record<string, Record<string, string> | undefined>;
}

/** Every TypeScript source in `src/`, specs included. */
function sourceFileNames(): string[] {
  return readdirSync(join(packageDir, "src")).filter(
    (name: string): boolean => name.endsWith(".ts") || name.endsWith(".tsx"),
  );
}

/** Files whose import statements name `specifier` as their module source. */
function filesImportingFrom(specifier: string): string[] {
  const quoted: string = specifier.replaceAll("/", String.raw`\/`);
  const pattern = new RegExp(String.raw`from\s+"${quoted}"`, "u");
  return sourceFileNames().filter((name: string): boolean =>
    pattern.test(readText(join("src", name))),
  );
}

/**
 * The `extraBrowserOptimizeDeps` entries this package's own `vitest.config.ts`
 * layers onto the shared baseline, read as text: that config is a module the
 * Vitest config loader consumes, and importing it here would boot a second
 * browser provider just to read a list of strings.
 */
function extraBrowserOptimizeDeps(): string[] {
  const block: string =
    readText("vitest.config.ts").match(/extraBrowserOptimizeDeps:\s*\[([^\]]*)\]/su)?.[1] ?? "";
  return [...block.matchAll(/"([^"]+)"/gu)].map((entry: RegExpExecArray): string => entry[1]);
}

describe("react-query imports route through the facade", () => {
  it("takes the render seam's QueryClient and QueryClientProvider from the facade", () => {
    const source = readText("src/render-with-wallow.tsx");

    expect(source).toMatch(
      /import\s+\{[^}]*\bQueryClient\b[^}]*\}\s+from\s+"@bc-solutions-coder\/query"/su,
    );
    expect(source).toMatch(
      /import\s+\{[^}]*\bQueryClientProvider\b[^}]*\}\s+from\s+"@bc-solutions-coder\/query"/su,
    );
  });

  it("takes the render seam spec's QueryClient and useQuery from the facade", () => {
    // The spec exercises the seam through the same module the seam itself uses;
    // if it kept importing react-query directly, its `toBeInstanceOf(QueryClient)`
    // assertion would be comparing against a class from a different copy.
    const source = readText("src/render-with-wallow.test.tsx");

    expect(source).toMatch(
      /import\s+\{[^}]*\bQueryClient\b[^}]*\}\s+from\s+"@bc-solutions-coder\/query"/su,
    );
    expect(source).toMatch(
      /import\s+\{[^}]*\buseQuery\b[^}]*\}\s+from\s+"@bc-solutions-coder\/query"/su,
    );
  });

  it("leaves no direct @tanstack/react-query import anywhere in src/", () => {
    // Named as a list so a failure reports WHICH file regressed. Occurrences of
    // the bare string are fine and expected (browser-optimize-deps.test.ts and
    // vitest-projects.test.ts both feed it to `mergeOptimizeDeps` as sample
    // input); only an import of it is a facade breach.
    expect(filesImportingFrom(RAW)).toEqual([]);
  });

  it("has at least one file importing the facade, so the guard is not vacuous", () => {
    expect(filesImportingFrom(FACADE).length).toBeGreaterThan(0);
  });
});

describe("the manifest can only resolve react-query through the facade", () => {
  it("declares the facade as a workspace runtime dependency", () => {
    // A dependency, not a peer: the render seam constructs a `QueryClient`
    // itself, so this package needs the module present at runtime rather than
    // supplied by whoever installs it.
    expect(readPackageJson().dependencies?.[FACADE]).toBe("workspace:*");
  });

  it("declares @tanstack/react-query in no dependency field of its own", () => {
    const pkg = readPackageJson();

    for (const field of ["dependencies", "devDependencies", "peerDependencies"]) {
      expect(pkg[field] ?? {}, `${field} must not name ${RAW}`).not.toHaveProperty(RAW);
    }
  });

  it("still declares the render peers the seam mounts against", () => {
    // Dropping react-query must not take the genuinely-peer render deps with it.
    const peers = readPackageJson().peerDependencies ?? {};

    expect(peers).toHaveProperty("react");
    expect(peers).toHaveProperty("react-dom");
    expect(peers).toHaveProperty("@tanstack/react-router");
  });
});

describe("browser-mode pre-bundling survives the facade hop", () => {
  it("keeps @tanstack/react-query pre-bundled", () => {
    // NOT a rename: react-query is still the module Vite pre-bundles, because
    // the facade re-exports it. Removing this entry shows up as a mid-run Vite
    // reload that silently drops the runner, not as a clean failure.
    expect(extraBrowserOptimizeDeps()).toContain(RAW);
  });

  it("registers the facade with the browser project as well", () => {
    // A linked workspace package is not pre-bundled by default, so the config
    // must name it one of the two ways that work — inlined for SSR, or added to
    // the pre-bundle list. Which of the two is the green phase's empirical call
    // (see the bead design); this only pins that the choice was made explicitly
    // rather than leaving the facade to mid-run discovery.
    const config = readText("vitest.config.ts");
    const inlinedForSsr: boolean = /noExternal:\s*\[[^\]]*"@bc-solutions-coder\/query"/su.test(
      config,
    );

    expect(inlinedForSsr || extraBrowserOptimizeDeps().includes(FACADE)).toBe(true);
  });

  it("keeps the router and SDK extras it already pre-bundled", () => {
    const extras = extraBrowserOptimizeDeps();

    expect(extras).toContain("@tanstack/react-router");
    expect(extras).toContain("@bc-solutions-coder/sdk");
  });
});
