import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  client,
  configureBffClient,
  configureWallowClient,
  type BffClientOptions,
  type WallowClientOptions,
} from "./client";
import { client as generatedClient } from "./generated/client.gen";
import { getV1IdentityUsersMe } from "./generated/sdk.gen";

const clientSource = readFileSync(
  fileURLToPath(new URL("./client.ts", import.meta.url)),
  "utf8",
);

describe("client", () => {
  it("is the same instance the generated SDK operations call", () => {
    expect(client).toBe(generatedClient);
  });
});

describe("configureBffClient", () => {
  beforeEach(() => {
    client.setConfig({ baseUrl: undefined, credentials: undefined });
  });

  it("defaults to the same-origin BFF path with credentials included", () => {
    configureBffClient();

    const config = client.getConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("uses a custom baseUrl when provided", () => {
    configureBffClient({ baseUrl: "http://bff.internal/api" });

    const config = client.getConfig();

    expect(config.baseUrl).toBe("http://bff.internal/api");
    expect(config.credentials).toBe("include");
  });

  it("configures the client the generated SDK operations use", () => {
    configureBffClient();

    const config = generatedClient.getConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("accepts a BffClientOptions value", () => {
    const options: BffClientOptions = { baseUrl: "http://bff.typed/api" };

    configureBffClient(options);

    expect(client.getConfig().baseUrl).toBe("http://bff.typed/api");
  });
});

describe("configureWallowClient back-compat alias", () => {
  beforeEach(() => {
    client.setConfig({ baseUrl: undefined, credentials: undefined });
  });

  it("is the same function as configureBffClient, not a divergent copy", () => {
    expect(configureWallowClient).toBe(configureBffClient);
  });

  it("still defaults to the same-origin BFF path with credentials included", () => {
    configureWallowClient();

    const config = client.getConfig();

    expect(config.baseUrl).toBe("/api");
    expect(config.credentials).toBe("include");
  });

  it("still configures the same client instance the generated SDK uses", () => {
    configureWallowClient({ baseUrl: "http://bff.legacy/api" });

    const config = generatedClient.getConfig();

    expect(config.baseUrl).toBe("http://bff.legacy/api");
    expect(config.credentials).toBe("include");
  });

  it("accepts a WallowClientOptions value (type alias preserved)", () => {
    const options: WallowClientOptions = { baseUrl: "http://bff.legacy2/api" };

    configureWallowClient(options);

    expect(client.getConfig().baseUrl).toBe("http://bff.legacy2/api");
  });

  it("carries a @deprecated JSDoc tag naming configureBffClient", () => {
    const deprecatedAlias =
      /\/\*\*(?:[^*]|\*(?!\/))*@deprecated(?:[^*]|\*(?!\/))*\*\/\s*export const configureWallowClient\b/;

    expect(clientSource).toMatch(deprecatedAlias);
    expect(clientSource).toMatch(/@deprecated[^*]*configureBffClient/);
  });
});

describe("generated SDK requests after configureBffClient", () => {
  const fetchMock = vi.fn(
    async (_request: Request) =>
      new Response("{}", {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
  );

  beforeEach(() => {
    fetchMock.mockClear();
    vi.stubGlobal("fetch", fetchMock);
    client.setConfig({ baseUrl: undefined, credentials: undefined });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("go through the configured BFF baseUrl with credentials included", async () => {
    configureBffClient({ baseUrl: "http://bff.test/api" });

    await getV1IdentityUsersMe();

    expect(fetchMock).toHaveBeenCalledTimes(1);

    const request: Request = fetchMock.mock.calls[0]![0];

    expect(request.url).toBe("http://bff.test/api/v1/identity/users/me");
    expect(request.credentials).toBe("include");
  });

  it("go through the BFF baseUrl when configured via the deprecated alias", async () => {
    configureWallowClient({ baseUrl: "http://bff.alias/api" });

    await getV1IdentityUsersMe();

    expect(fetchMock).toHaveBeenCalledTimes(1);

    const request: Request = fetchMock.mock.calls[0]![0];

    expect(request.url).toBe("http://bff.alias/api/v1/identity/users/me");
    expect(request.credentials).toBe("include");
  });
});
