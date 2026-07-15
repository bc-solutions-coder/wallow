import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import openApiConfig from "../openapi-ts.config";
import { client as generatedClient } from "./generated/client.gen";
import { createClientConfig } from "./runtime-config";

describe("createClientConfig", () => {
  it("defaults the client to the same-origin BFF path with credentials included", () => {
    const config = createClientConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("overrides the generated baseUrl from the OpenAPI document", () => {
    const config = createClientConfig({ baseUrl: "http://localhost:5001/" });

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("preserves other options passed by the generated client", () => {
    const config = createClientConfig({
      baseUrl: "http://localhost:5001/",
      throwOnError: true,
    });

    expect(config.throwOnError).toBe(true);
  });
});

describe("openapi-ts.config", () => {
  it("wires runtimeConfigPath into the client-fetch plugin", async () => {
    const config = await openApiConfig;
    const plugins = config.plugins ?? [];

    const clientPlugin = plugins.find(
      (plugin) => typeof plugin === "object" && plugin.name === "@hey-api/client-fetch",
    );

    expect(clientPlugin).toEqual(
      expect.objectContaining({
        name: "@hey-api/client-fetch",
        runtimeConfigPath: "./src/runtime-config.ts",
      }),
    );
  });
});

describe("generated client", () => {
  it("is constructed through createClientConfig", () => {
    const source = readFileSync(
      fileURLToPath(new URL("./generated/client.gen.ts", import.meta.url)),
      "utf8",
    );

    expect(source).toMatch(/from ['"]\.\.\/runtime-config['"]/);
    expect(source).toMatch(/createClient\(\s*createClientConfig\(/);
  });

  it("starts out pointed at the BFF path with credentials included", () => {
    const config = generatedClient.getConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });
});
