import { beforeEach, describe, expect, it, vi } from "vitest";
import type { QueryClient } from "@tanstack/react-query";

/**
 * Apps feature query layer (Wallow-8w1h.5.1) — copies the CANONICAL
 * Organizations `api.test.ts` spec. The `getWallowSdk()` facade is mocked: these
 * tests assert the query/mutation layer's KEY STABILITY and its DELEGATION to
 * the facade `apps` slice, not the wire.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  register: vi.fn(),
}));

// Route/component files import only from this feature's api.ts; api.ts in turn
// imports getWallowSdk. We mock the facade module so the slice methods are spies.
vi.mock("../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    apps: {
      list: mocks.list,
      get: mocks.get,
      register: mocks.register,
    },
  }),
}));

import { appsQueries, registerAppMutation } from "./api";

/** Invoke a queryOptions `queryFn` while ignoring its QueryFunctionContext arg. */
async function callQueryFn(queryFn: unknown): Promise<unknown> {
  return (queryFn as () => Promise<unknown>)();
}

describe("appsQueries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("list", () => {
    it("keys the list query as ['apps']", () => {
      expect(appsQueries.list().queryKey).toEqual(["apps"]);
    });

    it("keeps the list queryKey stable across calls", () => {
      expect(appsQueries.list().queryKey).toEqual(appsQueries.list().queryKey);
    });

    it("queryFn delegates to the facade apps.list and returns its data", async () => {
      const apps = [{ clientId: "c1", displayName: "My App" }];
      mocks.list.mockResolvedValue(apps);

      const result = await callQueryFn(appsQueries.list().queryFn);

      expect(mocks.list).toHaveBeenCalledTimes(1);
      expect(result).toBe(apps);
    });
  });

  describe("detail", () => {
    it("keys the detail query as ['apps', clientId]", () => {
      expect(appsQueries.detail("c1").queryKey).toEqual(["apps", "c1"]);
    });

    it("queryFn delegates to the facade apps.get with the clientId", async () => {
      const app = { clientId: "c1", displayName: "My App" };
      mocks.get.mockResolvedValue(app);

      const result = await callQueryFn(appsQueries.detail("c1").queryFn);

      expect(mocks.get).toHaveBeenCalledWith("c1");
      expect(result).toBe(app);
    });
  });
});

describe("registerAppMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  function fakeQueryClient(): QueryClient {
    return { invalidateQueries: vi.fn() } as unknown as QueryClient;
  }

  it("mutationFn delegates to the facade apps.register with the body", async () => {
    const registered = { clientId: "c2", clientSecret: "s3cr3t" };
    mocks.register.mockResolvedValue(registered);
    const body = {
      clientName: "New App",
      requestedScopes: ["inquiries.read"],
      clientType: "public",
      redirectUris: null,
    };

    const mutation = registerAppMutation(fakeQueryClient());
    const result = await mutation.mutationFn(body);

    expect(mocks.register).toHaveBeenCalledWith(body);
    expect(result).toBe(registered);
  });

  it("invalidates the ['apps'] list query on success", () => {
    const queryClient = fakeQueryClient();

    const mutation = registerAppMutation(queryClient);
    mutation.onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["apps"] });
  });
});
