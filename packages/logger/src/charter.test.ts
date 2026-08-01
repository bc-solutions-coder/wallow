import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import viteConfig from "../vite.config";

/**
 * The charter: this package depends on nothing, compiles against no host
 * runtime, and exports exactly the two entries it builds.
 *
 * The zero-dependency rule is what keeps a logging package from dragging an OIDC
 * client into every consumer — which is why the two header constants it shares
 * with the SDK are declared locally and pinned by an app-side spec instead.
 */

const packageDir: URL = new URL("../", import.meta.url);
const repoRoot: URL = new URL("../../", packageDir);

function readJsonc<T>(url: URL): T {
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
  readonly compilerOptions: { readonly types?: readonly string[] };
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
const oxlint: OxlintConfig = readJsonc<OxlintConfig>(new URL(".oxlintrc.json", repoRoot));

/** The glob the charter's lint override has to be keyed on. */
const LOGGER_GLOB = "packages/logger/src/**/*.ts";

/** Every module that ships, with comments stripped so the scans read code only. */
const MODULES: readonly string[] = ["index", "log-event", "otlp", "rate-limit", "server"];

function codeOf(source: string): string {
  return source.replaceAll(/\/\*[\s\S]*?\*\//gu, "").replaceAll(/^[^\S\n]*\/\/.*$/gmu, "");
}

function readModule(name: string): string {
  const path: string = fileURLToPath(new URL(`src/${name}.ts`, packageDir));

  return readFileSync(path, "utf8");
}

const sources: readonly (readonly [string, string])[] = MODULES.map(
  (name: string) => [name, codeOf(readModule(name))] as const,
);

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

  it.each(sources)("imports only its own modules in %s", (_name: string, source: string) => {
    const specifiers: string[] = [...source.matchAll(/from\s+"([^"]+)"/gu)].map(
      (match) => match[1]!,
    );

    for (const specifier of specifiers) {
      expect(specifier.startsWith("./"), `${specifier} is not a sibling module`).toBe(true);
    }
  });
});

describe("the package compiles against no host runtime", () => {
  it("admits no ambient @types package", () => {
    // Both entries are web-standard: the ingest handler takes a `Request` and
    // answers a `Response`, and every host detail — collector URL, allowed
    // origins, the CSRF verifier — arrives as an argument. An empty `types` is
    // what keeps `process`, `Buffer` and `node:*` from resolving here.
    expect(tsconfig.compilerOptions.types).toEqual([]);
  });

  it.each(sources)("reads no environment of its own in %s", (_name: string, source: string) => {
    expect(source).not.toMatch(/\bprocess\s*\.\s*env\b/u);
    expect(source).not.toMatch(/\bimport\.meta\.env\b/u);
  });

  it.each(sources)("names no node builtin in %s", (_name: string, source: string) => {
    expect(source).not.toMatch(/"node:/u);
  });
});

describe("the lint charter is declared where oxlint reads it", () => {
  const override: OxlintOverride | undefined = oxlint.overrides.find((entry: OxlintOverride) =>
    entry.files.includes(LOGGER_GLOB),
  );

  it("keys an override on this package's source", () => {
    expect(override, `no override matches ${LOGGER_GLOB}`).toBeDefined();
  });

  it("bans every other workspace package", () => {
    expect(bannedSpecifiers(restrictedImports(override!.rules))).toContain("@bc-solutions-coder/*");
  });

  it("restates every ban the root config makes", () => {
    // An oxlint `overrides` entry REPLACES the rule's options rather than merging
    // them, so an override listing only the charter bans silently unbans
    // everything the root forbids.
    const rootBans: readonly string[] = bannedSpecifiers(restrictedImports(oxlint.rules));
    const loggerBans: readonly string[] = bannedSpecifiers(restrictedImports(override!.rules));

    expect(loggerBans).toEqual(expect.arrayContaining([...rootBans]));
  });
});

describe("the two entries agree across manifest, build and tsconfig", () => {
  const entries: Record<string, string> =
    (viteConfig as unknown as { build?: { lib?: { entry?: Record<string, string> } } }).build?.lib
      ?.entry ?? {};

  it("declares a browser entry and a server entry", () => {
    expect(Object.keys(manifest.exports).toSorted()).toEqual([".", "./server"]);
  });

  it("publishes the same subpaths it resolves from source", () => {
    expect(Object.keys(manifest.publishConfig.exports).toSorted()).toEqual([".", "./server"]);
  });

  it("gives every subpath a lib entry to emit from", () => {
    // A subpath with no entry resolves, at publish time, to a file the build
    // never wrote.
    expect(Object.keys(entries).toSorted()).toEqual(["index", "server"]);
  });

  it("points the published conditions at the built files", () => {
    expect(manifest.publishConfig.exports).toEqual({
      ".": { types: "./dist/index.d.ts", import: "./dist/index.js" },
      "./server": { types: "./dist/server.d.ts", import: "./dist/server.js" },
    });
  });
});

describe("the browser entry stays out of the server's way", () => {
  const index: string = sources.find(([name]) => name === "index")![1];

  it("holds no ingest handler", () => {
    // The browser bundle must not carry the guards, the limiter or the OTLP
    // encoder: they are dead weight in a page and a map of the server's checks.
    expect(index).not.toMatch(/createLogIngestHandler|createRateLimiter|toOtlpLogsPayload/u);
  });

  it("shares the wire contract with the server rather than restating it", () => {
    // Sender and receiver drift into a type error instead of a silently
    // discarded field.
    expect(index).toMatch(/from "\.\/log-event"/u);
  });
});
