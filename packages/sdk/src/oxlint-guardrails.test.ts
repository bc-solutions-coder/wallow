import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, describe, expect, it } from "vitest";

/**
 * Import guardrails for the collapsed SDK surface (bead Wallow-pu6a.5.8).
 *
 * Beads 5.2-5.5 deleted the module-global client, the hand-written query slices
 * and key registry, and the three per-app facade singletons. Deleting them stops
 * TODAY's call sites; it does not stop the pattern coming back, because the
 * shapes are all still writable — a fork can hand-roll a `queryKeys` object or
 * reach past the exports map into `dist/` and get a working build with none of
 * the per-request isolation the collapse bought. `no-restricted-imports` is what
 * makes that a build failure with a message naming the replacement.
 *
 * A config assertion alone would pass against a rule that silently matches
 * nothing, so the config surface is pinned AND the real binary is run over
 * snippets: each restricted form must produce a `no-restricted-imports`
 * diagnostic, and the surviving entry points must produce none.
 */

// packages/sdk/src -> packages/sdk
const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");
// packages/sdk -> repo root
const repoRoot: string = resolve(packageRoot, "..", "..");
const oxlintConfigPath: string = resolve(repoRoot, ".oxlintrc.json");
const oxlintBinPath: string = resolve(repoRoot, "node_modules", ".bin", "oxlint");

const RULE_CODE: string = "eslint(no-restricted-imports)";

interface RestrictedPath {
  name: string;
  importNames?: string[];
  message?: string;
}

interface RestrictedPattern {
  group: string[];
  message?: string;
}

interface RestrictedImportsOptions {
  paths?: RestrictedPath[];
  patterns?: RestrictedPattern[];
}

interface OxlintConfig {
  rules?: Record<string, unknown>;
}

function readRuleEntry(): [string, RestrictedImportsOptions] {
  const config: OxlintConfig = JSON.parse(readFileSync(oxlintConfigPath, "utf8")) as OxlintConfig;
  const entry: unknown = config.rules?.["no-restricted-imports"];

  expect(Array.isArray(entry)).toBe(true);
  const [severity, options] = entry as [string, RestrictedImportsOptions];
  return [severity, options];
}

function restrictedPathFor(moduleName: string): RestrictedPath {
  const [, options] = readRuleEntry();
  const match: RestrictedPath | undefined = (options.paths ?? []).find(
    (candidate: RestrictedPath): boolean => candidate.name === moduleName,
  );

  expect(match, `no-restricted-imports has no entry for ${moduleName}`).toBeDefined();
  return match ?? { name: moduleName };
}

function allPatternGroups(): string[] {
  const [, options] = readRuleEntry();
  return (options.patterns ?? []).flatMap((pattern: RestrictedPattern): string[] => pattern.group);
}

const scratchDir: string = mkdtempSync(join(tmpdir(), "wallow-oxlint-guardrails-"));

afterAll((): void => {
  rmSync(scratchDir, { force: true, recursive: true });
});

/**
 * Lints `source` with the repo's real config and returns only the
 * `no-restricted-imports` diagnostics, so an unrelated stylistic warning in a
 * snippet cannot be mistaken for the guardrail firing (or for it staying quiet).
 */
function restrictedImportDiagnostics(fileName: string, source: string): string[] {
  const filePath: string = join(scratchDir, fileName);
  writeFileSync(filePath, source, "utf8");

  let stdout: string = "";
  try {
    stdout = execFileSync(oxlintBinPath, ["-c", oxlintConfigPath, "-f", "json", filePath], {
      cwd: repoRoot,
      encoding: "utf8",
      stdio: "pipe",
    });
  } catch (error: unknown) {
    stdout = (error as { stdout?: string }).stdout ?? "";
  }

  const report: { diagnostics?: { code?: string; message?: string }[] } = JSON.parse(stdout) as {
    diagnostics?: { code?: string; message?: string }[];
  };

  return (report.diagnostics ?? [])
    .filter((diagnostic): boolean => diagnostic.code === RULE_CODE)
    .map((diagnostic): string => diagnostic.message ?? "");
}

describe("the root oxlint config restricts the deleted SDK surface", () => {
  it("registers no-restricted-imports as an error, not a warning", () => {
    const [severity] = readRuleEntry();
    expect(severity).toBe("error");
  });

  it.each([
    "client",
    "configureBffClient",
    "configureSsrClient",
    "configureWallowClient",
    "createAuthClient",
    "createConfiguredOnce",
    "createMfaClient",
    "getSsrRequestContext",
    "resolveSsrFetchOrigin",
    "setSsrRequestContextResolver",
    "unwrap",
    "wireSsrCookieInterceptor",
  ])("restricts the deleted browser-entry export %s", (exportName: string) => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk").importNames).toContain(exportName);
  });

  it.each([
    "appsQueries",
    "authQueries",
    "ensureQueryBootstrapped",
    "inquiriesQueries",
    "mfaQueries",
    "organizationsQueries",
    "queryKeys",
    "registerQueryBootstrap",
    "resetQueryBootstrapForTests",
    "settingsQueries",
    "userQueries",
  ])("restricts the retired query-entry export %s", (exportName: string) => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk/query").importNames).toContain(exportName);
  });

  it.each([
    "@bc-solutions-coder/sdk/dist/**",
    "@bc-solutions-coder/sdk/src/**",
    "@bc-solutions-coder/sdk/generated/**",
  ])("restricts deep imports matching %s", (group: string) => {
    expect(allPatternGroups()).toContain(group);
  });

  it.each(["**/lib/wallow-sdk", "**/lib/wallow-auth-sdk", "**/lib/sdk"])(
    "restricts the deleted app facade path %s",
    (group: string) => {
      expect(allPatternGroups()).toContain(group);
    },
  );

  it("points a reader at the per-request factory rather than only forbidding", () => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk").message).toContain("createWallowSdk");
  });

  it("points a reader at the generated query artifacts", () => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk/query").message).toMatch(/Options\(\)/u);
  });
});

describe("the guardrails fire on real source", () => {
  it.each([
    [
      "singleton-config.ts",
      'import { configureBffClient } from "@bc-solutions-coder/sdk";\nexport const use = configureBffClient;\n',
    ],
    [
      "ssr-resolver.ts",
      'import { setSsrRequestContextResolver } from "@bc-solutions-coder/sdk";\nexport const use = setSsrRequestContextResolver;\n',
    ],
    [
      "unwrap.ts",
      'import { unwrap } from "@bc-solutions-coder/sdk";\nexport const use = unwrap;\n',
    ],
    [
      "key-registry.ts",
      'import { queryKeys } from "@bc-solutions-coder/sdk/query";\nexport const use = queryKeys;\n',
    ],
    [
      "query-slice.ts",
      'import { organizationsQueries } from "@bc-solutions-coder/sdk/query";\nexport const use = organizationsQueries;\n',
    ],
    [
      "deep-dist.ts",
      'import { thing } from "@bc-solutions-coder/sdk/dist/index.js";\nexport const use = thing;\n',
    ],
    [
      "deep-src.ts",
      'import { thing } from "@bc-solutions-coder/sdk/src/generated/sdk.gen";\nexport const use = thing;\n',
    ],
    [
      "app-facade.ts",
      'import { getSdk } from "../../lib/wallow-auth-sdk";\nexport const use = getSdk;\n',
    ],
  ])("rejects %s", (fileName: string, source: string) => {
    expect(restrictedImportDiagnostics(fileName, source)).not.toHaveLength(0);
  });

  // A guardrail that also catches `export ... from` matters more than it looks:
  // the surviving `features/<name>/api.ts` seams are pure re-export files, so a
  // rule blind to that form would miss the most likely place a deleted symbol
  // creeps back in.
  it("rejects a re-export of a deleted symbol, not only a plain import", () => {
    const diagnostics: string[] = restrictedImportDiagnostics(
      "reexport-seam.ts",
      'export { configureBffClient } from "@bc-solutions-coder/sdk";\n',
    );

    expect(diagnostics).not.toHaveLength(0);
  });

  it("rejects a type-only deep import", () => {
    const diagnostics: string[] = restrictedImportDiagnostics(
      "type-only-deep.ts",
      'import type { Thing } from "@bc-solutions-coder/sdk/dist/index.js";\nexport type Alias = Thing;\n',
    );

    expect(diagnostics).not.toHaveLength(0);
  });

  it("leaves the surviving entry points alone", () => {
    const diagnostics: string[] = restrictedImportDiagnostics(
      "compliant.ts",
      [
        'import { createWallowSdk } from "@bc-solutions-coder/sdk";',
        'import { createApiPassthrough } from "@bc-solutions-coder/sdk/server/passthrough";',
        'import { createWallowBffServer } from "@bc-solutions-coder/sdk/server";',
        'import { organizationsGetAllOptions, queriesWithTag } from "@bc-solutions-coder/sdk/query";',
        "",
        "export const surface = [",
        "  createWallowSdk,",
        "  createApiPassthrough,",
        "  createWallowBffServer,",
        "  organizationsGetAllOptions,",
        "  queriesWithTag,",
        "];",
        "",
      ].join("\n"),
    );

    expect(diagnostics).toEqual([]);
  });
});
