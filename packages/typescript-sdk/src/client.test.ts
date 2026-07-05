import { beforeEach, describe, expect, it } from "vitest";

import { client, configureWallowClient } from "./client";

describe("configureWallowClient", () => {
  beforeEach(() => {
    client.setConfig({ baseUrl: undefined, credentials: undefined });
  });

  it("defaults to the same-origin BFF path with credentials included", () => {
    configureWallowClient();

    const config = client.getConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("uses a custom baseUrl when provided", () => {
    configureWallowClient({ baseUrl: "http://bff.internal/api" });

    const config = client.getConfig();

    expect(config.baseUrl).toBe("http://bff.internal/api");
    expect(config.credentials).toBe("include");
  });
});
