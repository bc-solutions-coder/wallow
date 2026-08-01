import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import viteConfig from "../vite.config";

/**
 * The charter: this package depends on nothing, reads no environment of its own,
 * and exports exactly the modules on disk.
 *
 * Every module here is imported by a Start app's `start.ts`, which Start aliases
 * into the CLIENT bundle as well as the server one. That is the whole reason
 * these helpers were copied per app instead of shared — so a module that could
 * reach `process` or `node:*` would put the duplication straight back.
 */

const packageDir: URL = new URL("../", import.meta.url);
const repoRoot: URL = new URL("../../", packageDir);

function readJsonc<T>(url: URL): T {
  // `tsconfig.json` and `.oxlintrc.json` carry comments. Strip line comments
  // outside strings, then parse.
  const text: string = readFileSync(fileURLToPath(url), "utf8");
  const stripped: string = text.replaceAll(/^\s*\/\/.*$/gmu, "");

  return JSON.parse(stripped) as T;
}

interface ExportCondition {
  readonly types: string;
  readonly import: string;
}

interface Manifest {
  readonly name: string;
  readonly exports: Readonly<Record<string, ExportCondition>>;
  readonly publishConfig: { readonly exports: Readonly<Record<string, ExportCondition>> };
  readonly dependencies?: Readonly<Record<string, string>>;
  readonly peerDependencies?: Readonly<Record<string, string>>;
}

interface TsConfig {
  readonly compilerOptions: {
    readonly types?: readonly string[];
  };
  readonly include?: readonly string[];
}

interface RestrictedImports {
  readonly paths?: readonly { readonly name: string }[];
  readonly patterns?: readonly { readonly group: readonly string[] }[];
}

interface OxlintOverride {
  readonly files: readonly string[];
  readonly rules: Readonly<Record<string, unknown>>;
}

interface OxlintConfig {
  readonly rules: Readonly<Record<string, unknown>>;
  readonly overrides: readonly OxlintOverride[];
}

const manifest: Manifest = readJsonc<Manifest>(new URL("package.json", packageDir));
const tsconfig: TsConfig = readJsonc<TsConfig>(new URL("tsconfig.json", packageDir));
const buildTsconfig: TsConfig = readJsonc<TsConfig>(new URL("tsconfig.build.json", packageDir));
const oxlint: OxlintConfig = readJsonc<OxlintConfig>(new URL(".oxlintrc.json", repoRoot));

/** The glob the charter's lint override has to be keyed on. */
const ENV_GLOB = "packages/env/src/**/*.ts";

/** Every shipped module: `src/*.ts` that is not a spec. */
const modules: readonly string[] = readdirSync(fileURLToPath(new URL("src", packageDir)))
  .filter((file: string) => file.endsWith(".ts") && !file.endsWith(".test.ts"))
  .map((file: string) => file.replace(/\.ts$/u, ""))
  .toSorted();

/**
 * A module's source with its block comments and whole-line `//` comments removed.
 *
 * The scans below are about what the module DOES, and every one of these modules
 * documents the hazard it exists to avoid — naming `process.env` in prose is the
 * explanation, not a violation. Trailing comments are deliberately left in: they
 * sit beside code, so stripping them is where a scan starts missing things.
 */
function codeOf(source: string): string {
  return source.replaceAll(/\/\*[\s\S]*?\*\//gu, "").replaceAll(/^[^\S\n]*\/\/.*$/gmu, "");
}

function readModule(name: string): string {
  return readFileSync(fileURLToPath(new URL(`src/${name}.ts`, packageDir)), "utf8");
}

const sources: readonly (readonly [string, string])[] = modules.map(
  (name: string) => [name, codeOf(readModule(name))] as const,
);

function subpathsOf(map: Readonly<Record<string, ExportCondition>>): readonly string[] {
  return Object.keys(map)
    .map((subpath: string) => subpath.replace(/^\.\//u, ""))
    .toSorted();
}

/** `no-restricted-imports`' options object, wherever it is declared. */
function restrictedImports(rules: Readonly<Record<string, unknown>>): RestrictedImports {
  const entry: unknown = rules["no-restricted-imports"];

  expect(Array.isArray(entry), "no-restricted-imports is configured").toBe(true);

  return (entry as [string, RestrictedImports])[1];
}

function bannedSpecifiers(rule: RestrictedImports): readonly string[] {
  return [
    ...(rule.paths ?? []).map((path) => path.name),
    ...(rule.patterns ?? []).flatMap((pattern) => pattern.group),
  ];
}

describe("the package depends on nothing", () => {
  it.each(["dependencies", "peerDependencies"] as const)("declares no %s", (field) => {
    expect(Object.keys(manifest[field] ?? {})).toEqual([]);
  });
});

describe("the package compiles against no host runtime", () => {
  it("admits no ambient @types package", () => {
    // An empty `types` is what keeps `process`, `Buffer` and `node:fs` from
    // resolving in the shipped source. Absent would mean "include every installed
    // @types", which is the opposite.
    expect(tsconfig.compilerOptions.types).toEqual([]);
  });

  it("covers the whole of src apart from the specs", () => {
    expect(tsconfig.include).toEqual(["src"]);
  });

  it.each(sources)("reads no environment of its own in %s", (_name: string, source: string) => {
    // The typecheck already refuses `process`, but only while `types` stays
    // empty. This says the same thing about the source itself, so the two have to
    // be broken together: an env-reading helper takes the record as a parameter
    // and the CALLER does the read, inside the server-only callback.
    expect(source).not.toMatch(/\bprocess\s*\.\s*env\b/u);
    expect(source).not.toMatch(/\bimport\.meta\.env\b/u);
  });

  it.each(sources)("imports nothing at all in %s", (_name: string, source: string) => {
    // Zero dependencies and zero cross-module imports: each subpath is a leaf, so
    // a consumer resolving one pulls in nothing else.
    expect(source).not.toMatch(/^\s*import\s/mu);
  });
});

describe("the lint charter is declared where oxlint reads it", () => {
  const override: OxlintOverride | undefined = oxlint.overrides.find((entry: OxlintOverride) =>
    entry.files.includes(ENV_GLOB),
  );

  it("keys an override on this package's source", () => {
    expect(override, `no override matches ${ENV_GLOB}`).toBeDefined();
  });

  it.each(["react", "react-dom", "zustand", "@bc-solutions-coder/*"])("bans %s", (specifier) => {
    expect(bannedSpecifiers(restrictedImports(override!.rules))).toContain(specifier);
  });

  it("restates every ban the root config makes", () => {
    // An oxlint `overrides` entry REPLACES the rule's options rather than merging
    // them, so an override that lists only the charter bans silently unbans
    // everything the root forbids.
    const rootBans: readonly string[] = bannedSpecifiers(restrictedImports(oxlint.rules));
    const envBans: readonly string[] = bannedSpecifiers(restrictedImports(override!.rules));

    expect(envBans).toEqual(expect.arrayContaining([...rootBans]));
  });
});

describe("every module on disk is reachable, and nothing else is", () => {
  it("has modules to export", () => {
    // Guards the diffs below: two empty lists are equal.
    expect(modules.length).toBeGreaterThan(0);
  });

  it("names one subpath per module", () => {
    expect(subpathsOf(manifest.exports)).toEqual(modules);
  });

  it("publishes the same subpaths it resolves from source", () => {
    expect(subpathsOf(manifest.publishConfig.exports)).toEqual(modules);
  });

  it("exposes no root barrel", () => {
    // Subpath-only, so a consumer's import names the module it depends on and a
    // bundler can drop the rest.
    expect(Object.keys(manifest.exports)).not.toContain(".");
  });

  it.each(["", "publish"] as const)("points %s conditions at the module's own file", (mode) => {
    const map = mode === "" ? manifest.exports : manifest.publishConfig.exports;
    const suffix: string = mode === "" ? "src/NAME.ts" : "dist/NAME.js";
    const types: string = mode === "" ? "src/NAME.ts" : "dist/NAME.d.ts";

    for (const name of modules) {
      expect(map[`./${name}`]).toEqual({
        types: `./${types.replace("NAME", name)}`,
        import: `./${suffix.replace("NAME", name)}`,
      });
    }
  });

  it("gives every subpath a lib entry to emit from", () => {
    // The shared Vite preset emits one file per entry. A subpath with no entry
    // resolves, at publish time, to a file that was never written.
    const build = (
      viteConfig as unknown as {
        build?: { lib?: { entry?: Record<string, string> } };
      }
    ).build;

    expect(Object.keys(build?.lib?.entry ?? {}).toSorted()).toEqual(modules);
  });

  it("declares every module in the declaration build", () => {
    // Nothing here imports anything else here, so an unlisted module emits no
    // .d.ts rather than being pulled in transitively.
    expect(buildTsconfig.include).toEqual(modules.map((name: string) => `src/${name}.ts`));
  });
});
