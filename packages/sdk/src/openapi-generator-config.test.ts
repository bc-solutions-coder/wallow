import { describe, expect, it } from "vitest";

import generatorConfig from "../openapi-ts.config";

// Guards Wallow-pu6a.1.10. Two separate regressions are locked here:
//
//  1. The generator must never again shell out to a post-processor. The
//     `format: "prettier"` config (removed in 1b1ee705) spawned a Prettier
//     binary this workspace does not install -- the toolchain is oxc.
//  2. At the installed @hey-api/openapi-ts 0.99.0 pin, `output.postProcess`
//     is the live mechanism and `output.format` is deprecated: setting
//     `format` at all (even to null) routes through the legacy path that
//     back-fills `postProcess`. So the invariant is BOTH "postProcess is
//     explicitly []" AND "format is never set".
//
// `defineConfig` at this pin is a pass-through -- it resolves the literal the
// config file exports without applying defaults -- so reading the resolved
// object proves the file sets the value explicitly rather than inheriting it.

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
