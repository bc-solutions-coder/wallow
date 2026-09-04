import { describe, expect, it } from "vitest";

import generatorConfig from "../openapi-ts.config";

/**
 * The generator config as a contract: one input (the SDK's snapshot), one
 * plugin (types with runtime enums), and a filter that keeps `ErrorCode` alone.
 * The SDK's guard on `output.postProcess` applies here for the same reason.
 */

interface ResolvedConfig {
  input?: unknown;
  output?: unknown;
  parser?: unknown;
  plugins?: unknown;
}

async function resolveConfig(): Promise<ResolvedConfig> {
  return (await generatorConfig) as ResolvedConfig;
}

describe("the api-errors generator config", () => {
  it("reads the SDK's committed snapshot", async () => {
    const config: ResolvedConfig = await resolveConfig();

    expect(config.input).toBe("../sdk/openapi/v1.json");
  });

  it("emits into the committed generated directory with post-processing pinned off", async () => {
    const config: ResolvedConfig = await resolveConfig();

    expect(config.output).toEqual({ path: "./src/generated", postProcess: [] });
  });

  it("runs the typescript plugin alone, with JavaScript enums", async () => {
    const config: ResolvedConfig = await resolveConfig();

    expect(config.plugins).toEqual([{ name: "@hey-api/typescript", enums: "javascript" }]);
  });

  it("keeps ErrorCode and drops every operation", async () => {
    const config: ResolvedConfig = await resolveConfig();

    expect(config.parser).toEqual({
      filters: {
        operations: { exclude: ["/.*/"] },
        schemas: { include: ["ErrorCode"] },
      },
    });
  });
});
