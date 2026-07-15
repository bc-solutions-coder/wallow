import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// packages/sdk/src -> packages/sdk
const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");
// packages/sdk -> repo root
const repoRoot: string = resolve(packageRoot, "..", "..");
const oxfmtConfigPath: string = resolve(repoRoot, ".oxfmtrc.json");
const oxfmtSchemaPath: string = resolve(
  repoRoot,
  "node_modules",
  "oxfmt",
  "configuration_schema.json",
);
const oxfmtBinPath: string = resolve(repoRoot, "node_modules", ".bin", "oxfmt");

// The formatter is invoked over exactly the same target list the root
// `format:check` script uses, so the test exercises the real acceptance surface.
const formatTargets: string[] = [
  "apps",
  "packages",
  "package.json",
  "pnpm-workspace.yaml",
  "tsconfig.base.json",
  ".oxlintrc.json",
  ".oxfmtrc.json",
];

interface OxfmtConfig {
  tabWidth?: number;
  printWidth?: number;
  singleQuote?: boolean;
  semi?: boolean;
  trailingComma?: string;
  ignorePatterns?: string[];
  [key: string]: unknown;
}

function readOxfmtConfig(): OxfmtConfig {
  return JSON.parse(readFileSync(oxfmtConfigPath, "utf8")) as OxfmtConfig;
}

interface JsonSchema {
  properties?: Record<string, unknown>;
}

function readSchemaKeys(): string[] {
  const schema: JsonSchema = JSON.parse(readFileSync(oxfmtSchemaPath, "utf8")) as JsonSchema;
  return Object.keys(schema.properties ?? {});
}

// These assertions lock the acceptance surface of the oxfmt config
// standardization (bead Wallow-ve7q.2.2): the `.oxfmtrc.json` must become an
// explicit style config (not the minimal `{ "ignorePatterns": [] }`) using the
// REAL Prettier-style oxfmt schema key names (tabWidth / printWidth /
// singleQuote / semi / trailingComma), not the invalid names from the original
// design doc (indentWidth / lineWidth / quoteStyle / semicolons). The pattern
// mirrors the existing build-config.test.ts / sdk-publish-workflow.test.ts
// static config-surface checks.
describe("oxfmt config (explicit style standardization)", () => {
  it("has an .oxfmtrc.json at the repo root", () => {
    expect(existsSync(oxfmtConfigPath)).toBe(true);
  });

  it("is explicit, not the minimal ignorePatterns-only stub", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    const keys: string[] = Object.keys(config).filter((key: string): boolean => key !== "$schema");
    // The minimal stub is exactly { "ignorePatterns": [] }; an explicit config
    // must declare real style options in addition to ignorePatterns.
    expect(keys).not.toEqual(["ignorePatterns"]);
    expect(keys.length).toBeGreaterThan(1);
  });

  it("sets a 2-space indent via tabWidth", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    expect(config.tabWidth).toBe(2);
  });

  it("sets a 100-column printWidth", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    expect(config.printWidth).toBe(100);
  });

  it("uses double quotes via singleQuote:false", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    expect(config).toHaveProperty("singleQuote");
    expect(config.singleQuote).toBe(false);
  });

  it("requires semicolons via semi:true", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    expect(config).toHaveProperty("semi");
    expect(config.semi).toBe(true);
  });

  it("emits trailing commas everywhere via trailingComma:all", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    expect(config.trailingComma).toBe("all");
  });

  it("excludes dist and generated output from formatting", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    const patterns: string[] = config.ignorePatterns ?? [];
    const joined: string = patterns.join("\n");
    expect(joined).toMatch(/dist/);
    expect(joined).toMatch(/generated/);
  });

  it("uses only key names that exist in the real oxfmt schema", () => {
    const config: OxfmtConfig = readOxfmtConfig();
    const schemaKeys: string[] = readSchemaKeys();
    const unknownKeys: string[] = Object.keys(config).filter(
      (key: string): boolean => key !== "$schema" && !schemaKeys.includes(key),
    );
    // Guards against the invalid design-doc names indentWidth / lineWidth /
    // quoteStyle / semicolons that oxfmt does not recognize.
    expect(unknownKeys).toEqual([]);
  });

  it("passes `oxfmt --check` clean across the whole workspace", () => {
    let exitCode: number = 0;
    try {
      execFileSync(oxfmtBinPath, ["--check", ...formatTargets], {
        cwd: repoRoot,
        encoding: "utf8",
        stdio: "pipe",
      });
    } catch (error: unknown) {
      const status: number | undefined = (error as { status?: number }).status;
      exitCode = status ?? 1;
    }
    expect(exitCode).toBe(0);
  });
});
