import { readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import generatorConfig from "../openapi-ts.config";

// Guards Wallow-pu6a.1.10. Two separate regressions are locked here:
//
//  1. The generator must never again shell out to a post-processor. The
//     `format: "prettier"` config (removed in 1b1ee705) spawned a Prettier
//     binary this workspace does not install -- the toolchain is oxc -- and
//     every failed run dropped an `openapi-ts-error-<epoch>.log` in the
//     package root.
//  2. At the installed @hey-api/openapi-ts 0.99.0 pin, `output.postProcess`
//     is the live mechanism and `output.format` is deprecated: setting
//     `format` at all (even to null) routes through the legacy path that
//     back-fills `postProcess`. So the invariant is BOTH "postProcess is
//     explicitly []" AND "format is never set".
//
// `defineConfig` at this pin is a pass-through -- it resolves the literal the
// config file exports without applying defaults -- so reading the resolved
// object proves the file sets the value explicitly rather than inheriting it.

// packages/sdk/src -> packages/sdk
const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");
// packages/sdk -> repo root
const repoRoot: string = resolve(packageRoot, "..", "..");

const STALE_LOG_PATTERN: RegExp = /^openapi-ts-error-\d+\.log$/;

// Directories that are build output, vendored code, or test artifacts: a log
// found there would not be a committed-source problem.
const SKIP_DIRECTORIES: ReadonlySet<string> = new Set([
  ".git",
  ".nuxt",
  ".output",
  ".vite",
  "TestResults",
  "bin",
  "coverage",
  "dist",
  "node_modules",
  "obj",
  "playwright-report",
  "test-results",
]);

function findStaleGeneratorLogs(directory: string, found: string[] = []): string[] {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!SKIP_DIRECTORIES.has(entry.name)) {
        findStaleGeneratorLogs(join(directory, entry.name), found);
      }
    } else if (STALE_LOG_PATTERN.test(entry.name)) {
      found.push(join(directory, entry.name));
    }
  }

  return found;
}

interface GeneratorOutput {
  path?: string;
  format?: unknown;
  postProcess?: readonly unknown[];
}

function isGeneratorOutput(value: unknown): value is GeneratorOutput {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

async function resolveGeneratorOutput(): Promise<GeneratorOutput> {
  const config = await generatorConfig;
  const output: unknown = config.output;

  // A single object output is the shape this package commits to; the string
  // and array forms would silently drop the postProcess pin.
  expect(isGeneratorOutput(output)).toBe(true);
  if (!isGeneratorOutput(output)) {
    throw new TypeError("openapi-ts.config.ts must declare a single object output");
  }

  return output;
}

describe("hey-api generator post-processing is pinned off", () => {
  it("explicitly sets output.postProcess to an empty list", async () => {
    const output: GeneratorOutput = await resolveGeneratorOutput();

    expect(output.postProcess).toEqual([]);
  });

  it("never sets the deprecated output.format, which back-fills postProcess", async () => {
    const output: GeneratorOutput = await resolveGeneratorOutput();

    // Deliberately not toBeUndefined via optional chaining: `format: null` is
    // just as forbidden as `format: "prettier"` because both enter the
    // legacy deprecation path.
    expect(Object.hasOwn(output, "format")).toBe(false);
  });

  it("still targets the committed generated-client directory", async () => {
    const output: GeneratorOutput = await resolveGeneratorOutput();

    expect(output.path).toBe("./src/generated");
  });
});

describe("no stale generator error logs remain in the working tree", () => {
  it("finds no openapi-ts-error-<epoch>.log anywhere in the repo", () => {
    expect(findStaleGeneratorLogs(repoRoot)).toEqual([]);
  });
});
