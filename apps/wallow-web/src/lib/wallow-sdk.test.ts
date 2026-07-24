import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * getWallowSdk() facade (Wallow-8w1h.3.3, slimmed in Wallow-evd5.2.2). The
 * guarded-singleton entry that configures the BFF client exactly once and, since
 * the per-feature data slices moved into the SDK query layer, exposes only the
 * current-user slice. On first use it configures the SSR/BFF client and (in the
 * browser) wires the CSRF request interceptor; `user.me()` delegates to the SDK's
 * `getUser()`.
 *
 * The generated ops are mocked here because this facade is the ONLY module
 * permitted to import them; the tests assert delegation, not the wire.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  configureBffClient: vi.fn(),
  configureSsrClient: vi.fn(),
  getSsrRequestContext: vi.fn(() => undefined),
  getUser: vi.fn(),
  client: { interceptors: { request: { use: vi.fn() } } },
}));

vi.mock("@bc-solutions-coder/sdk", () => ({
  // Real (passthrough) facade helpers: the collapsed facade imports
  // createConfiguredOnce from the SDK, so the mock must supply a working
  // implementation. createConfiguredOnce returns a fresh lazy singleton per
  // module graph, which is exactly what freshFacade()'s vi.resetModules wants.
  createConfiguredOnce: <TFacade>(configure: () => void, build: () => TFacade): (() => TFacade) => {
    let facade: TFacade | undefined;
    let ready = false;
    return (): TFacade => {
      if (!ready) {
        configure();
        facade = build();
        ready = true;
      }
      return facade as TFacade;
    };
  },
  configureBffClient: mocks.configureBffClient,
  configureSsrClient: mocks.configureSsrClient,
  getSsrRequestContext: mocks.getSsrRequestContext,
  wireCsrfInterceptor: vi.fn(),
  client: mocks.client,
  getUser: mocks.getUser,
}));

vi.mock("@bc-solutions-coder/sdk/query", () => ({
  registerQueryBootstrap: vi.fn(),
}));

/**
 * Re-evaluate the facade module so its `configured` singleton flag starts
 * fresh, then hand back `getWallowSdk`. Each test drives a clean singleton.
 */
async function freshFacade(): Promise<typeof import("./wallow-sdk").getWallowSdk> {
  vi.resetModules();
  const mod = await import("./wallow-sdk");
  return mod.getWallowSdk;
}

describe("getWallowSdk", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * Singleton configuration (Wallow-0q2s.7.2). This vitest node project runs with
   * `import.meta.env.SSR === true`, so `ensureConfigured()` takes the SSR branch:
   * it reads the request context from the SDK's relocated
   * `getSsrRequestContext()` seam and delegates all client wiring (absolute-origin
   * baseUrl + the live cookie-forwarding interceptor) to the SDK's
   * `configureSsrClient()`. The SSR wiring no longer lives in this app — it moved
   * into `@bc-solutions-coder/sdk`, so the facade never hand-rolls the interceptor
   * (`configureBffClient` + the CSRF path apply only in the browser branch).
   */
  describe("singleton configuration", () => {
    it("configures the SSR client exactly once across multiple calls", async () => {
      const getWallowSdk = await freshFacade();

      getWallowSdk();
      getWallowSdk();
      getWallowSdk();

      expect(mocks.configureSsrClient).toHaveBeenCalledTimes(1);
    });

    it("reads the SSR request context and delegates client wiring to configureSsrClient", async () => {
      const getWallowSdk = await freshFacade();

      getWallowSdk();

      expect(mocks.getSsrRequestContext).toHaveBeenCalled();
      expect(mocks.configureSsrClient).toHaveBeenCalledTimes(1);
      // The SSR branch delegates entirely to the SDK helper; it does not fall
      // through to the browser-only configureBffClient path.
      expect(mocks.configureBffClient).not.toHaveBeenCalled();
    });
  });

  describe("user slice", () => {
    it("me() delegates to the SDK getUser()", async () => {
      const user = { id: "u1", email: "a@b.c" };
      mocks.getUser.mockResolvedValue(user);
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().user.me();

      expect(mocks.getUser).toHaveBeenCalledTimes(1);
      expect(result).toBe(user);
    });
  });
});
