/*
 * Facade discipline for Wallow-x4qn.5: TanStack Query reaches this package only
 * through @bc-solutions-coder/query.
 *
 * The facade is a private workspace package that re-exports @tanstack/react-query
 * verbatim and is the ONE place the dependency is declared. Routing through it is
 * not stylistic. `useAppForm`'s `useMutation` and a host app's
 * `QueryClientProvider` have to come from the same module instance or the
 * mutation resolves no client at all ("No QueryClient set"), and the surest way
 * to end up with two instances is two manifests naming their own react-query
 * range. One declarer makes that impossible to express.
 *
 * The manifest half of the same guard lives in `package-scaffold.test.ts` (no
 * bucket may name react-query, and the browser pre-bundle list names the facade).
 * This spec is the import half, and it covers the specs as well as the shipped
 * modules: a spec that wraps a component in a `QueryClientProvider` built from
 * the raw package would be testing a second context that no app has.
 *
 * Repo-wide enforcement arrives later as an oxlint `no-restricted-imports` rule.
 * That rule cannot see the pre-bundle list, the CLAUDE.md prose or the manifest,
 * so this spec is not made redundant by it — it fails on the same commit that
 * would silently reintroduce the second copy, and it says why.
 *
 * Pure-logic spec: runs in the vitest NODE project.
 */

import { readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/** The facade every TanStack Query symbol in this package must come from. */
const FACADE = "@bc-solutions-coder/query";

/** The package the facade re-exports, which nothing here may name directly. */
const RAW_QUERY = "@tanstack/react-query";

// This guard lives at src/core/, so TWO levels up reaches the package root.
const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const sourceDir = join(packageDir, "src");

/**
 * This spec names the raw specifier in its own constants, so scanning itself
 * would be a permanent false positive.
 */
const SELF = fileURLToPath(import.meta.url);

/** Installed packages and build output are not authored modules. */
const SKIPPED_DIRECTORIES: ReadonlySet<string> = new Set(["node_modules", "dist", "coverage"]);

/** `import("x")` — the code-split form, still a module edge for this rule. */
const DYNAMIC_IMPORT_PATTERN = /\bimport\s*\(\s*["']([^"']+)["']\s*\)/gu;

/** `… from "x"`, a bare side-effect `import "x"`, or `require("x")`. */
const STATIC_IMPORT_PATTERN = /(?:\bfrom|^\s*import|\brequire\s*\()\s*["']([^"']+)["']/gmu;

/** Every authored `.ts`/`.tsx` module under `src/`, minus this file. */
function sourceFiles(dir: string = sourceDir): string[] {
  const found: string[] = [];
  const visible = readdirSync(dir).filter(
    (entry) => !entry.startsWith(".") && !SKIPPED_DIRECTORIES.has(entry),
  );

  for (const entry of visible) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...sourceFiles(full));
    } else if ((full.endsWith(".ts") || full.endsWith(".tsx")) && full !== SELF) {
      found.push(full);
    }
  }
  return found;
}

/** `import { A, type B } from "x"` — the form the bindings check below reads. */
const NAMED_IMPORT_PATTERN = /import\s+(?:type\s+)?\{([^}]*)\}\s*from\s*["']([^"']+)["']/gu;

/**
 * Comments, gone — a spec or guide DISCUSSING an import must not read as one.
 * Every assertion here quotes the specifier it forbids in its own rationale
 * (this file most of all), and the whole-line form is the same one
 * `package-scaffold.test.ts` strips before parsing a tsconfig.
 */
function stripComments(source: string): string {
  return source.replaceAll(/\/\*[\s\S]*?\*\//gu, "").replaceAll(/^\s*\/\/.*$/gmu, "");
}

/** Every module specifier `source` imports, static and dynamic forms alike. */
function importedSpecifiers(source: string): string[] {
  const code = stripComments(source);

  return [
    ...[...code.matchAll(STATIC_IMPORT_PATTERN)].map((match) => match[1]),
    ...[...code.matchAll(DYNAMIC_IMPORT_PATTERN)].map((match) => match[1]),
  ];
}

/** `@tanstack/react-query/build/x` counts too: the subpath is the same package. */
function isRawQuery(specifier: string): boolean {
  return specifier === RAW_QUERY || specifier.startsWith(`${RAW_QUERY}/`);
}

function read(relativePath: string): string {
  return readFileSync(join(packageDir, relativePath), "utf8");
}

interface NamedImport {
  readonly specifier: string;
  readonly bindings: readonly string[];
}

/** Each named-import statement in `source`, with its `type` modifiers dropped. */
function namedImports(source: string): NamedImport[] {
  return [...stripComments(source).matchAll(NAMED_IMPORT_PATTERN)].map((match) => ({
    specifier: match[2],
    bindings: match[1]
      .split(",")
      .map((binding) => binding.replace(/\btype\b/u, "").trim())
      .filter((binding) => binding.length > 0),
  }));
}

/** The names a module imports from the facade, across however many statements. */
function facadeBindings(source: string): string[] {
  return namedImports(source)
    .filter((statement) => statement.specifier === FACADE)
    .flatMap((statement) => statement.bindings);
}

describe("packages/forms query facade", () => {
  it("finds source modules to sweep", () => {
    // Non-vacuity guard: a broken walker would make every sweep below pass by
    // scanning nothing.
    expect(sourceFiles().length).toBeGreaterThan(10);
  });

  it("imports react-query in no module, spec or otherwise", () => {
    const offenders = sourceFiles()
      .filter((file) =>
        importedSpecifiers(readFileSync(file, "utf8")).some((specifier) => isRawQuery(specifier)),
      )
      .map((file) => relative(packageDir, file));

    expect(offenders).toEqual([]);
  });

  it("takes the submit mutation from the facade in use-app-form.ts", () => {
    // The package's only runtime react-query consumer, pinned by name because it
    // is the file the whole rule exists for: `useMutation` here and the host's
    // provider must share one module instance.
    const bindings = facadeBindings(read(join("src", "form", "use-app-form.ts")));

    expect(bindings).toContain("useMutation");
    // The mutation-options type travels with it: `UseAppFormOptions` embeds
    // `UseMutationOptions`, so a split import would leave half the surface on the
    // raw package.
    expect(bindings).toContain("UseMutationOptions");
  });

  it("builds every spec's QueryClient from the facade", () => {
    // Read as import BINDINGS rather than as a text search for the symbol, so
    // that a file merely naming `QueryClient` in prose is not an offender and a
    // future third source of the class is.
    const offenders = sourceFiles()
      .flatMap((file) =>
        namedImports(readFileSync(file, "utf8"))
          .filter(
            (statement) =>
              statement.specifier !== FACADE &&
              statement.bindings.some((binding) => binding.startsWith("QueryClient")),
          )
          .map((statement) => `${relative(packageDir, file)} -> ${statement.specifier}`),
      )
      // A provider from a different instance than `useAppForm`'s `useMutation`
      // makes a spec green against a wiring no app has.
      .toSorted();

    expect(offenders).toEqual([]);
  });
});

describe("packages/forms agent guide", () => {
  it("documents the facade as where react-query comes from", () => {
    // Line-filtered rather than a whole-file `toContain`, which prints the entire
    // guide as its diff.
    const mentions = read("CLAUDE.md")
      .split("\n")
      .filter((line) => line.includes(FACADE));

    expect(mentions.length, `${FACADE} is unmentioned in CLAUDE.md`).toBeGreaterThan(0);
  });

  it("no longer calls react-query a peer dependency", () => {
    // The guide's layering section listed it beside react and react-dom as the
    // host's to supply. It is now this package's own `workspace:*` dependency on
    // the facade, and a guide that still says otherwise is what the next
    // contributor copies.
    const peerLines = read("CLAUDE.md")
      .split("\n")
      .filter((line) => /peer/iu.test(line) && line.includes(RAW_QUERY));

    expect(peerLines).toEqual([]);
  });
});
