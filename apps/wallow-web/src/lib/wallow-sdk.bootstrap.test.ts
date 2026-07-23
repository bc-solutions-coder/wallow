import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * SDK query-layer bootstrap registration (Wallow-evd5.2.1).
 *
 * The SDK's `./query` factories configure the shared `@hey-api` client lazily
 * through a registered configurator (`registerQueryBootstrap` /
 * `ensureQueryBootstrapped`, `packages/sdk/src/query/bootstrap.ts`). wallow-web's
 * app-specific configurator is `configureClient()` in `wallow-sdk.ts` (it stays
 * the SSR/browser config authority — `import.meta.env.SSR` is fine in APP code).
 *
 * This task makes two guarantees:
 *   1. `wallow-sdk.ts` EXPORTS `configureClient` and registers it with the SDK
 *      query bootstrap at module scope (side-effect free — nothing touches the
 *      client until a query actually runs).
 *   2. `router.tsx` side-effect-imports `./lib/wallow-sdk` so the registration is
 *      loaded in BOTH the client and SSR module graphs before any route fires a
 *      query.
 *
 * The generated ops and client-config helpers are mocked because loading
 * `wallow-sdk.ts` builds the whole facade; these tests assert registration and
 * the registered configurator's wiring, not the wire itself. This runs in the
 * vitest NODE project, so `import.meta.env.SSR === true` and `configureClient()`
 * takes the SSR branch (`configureSsrClient`), never the browser branch
 * (`configureBffClient`).
 */

// Spies for the SDK query bootstrap subpath (`@bc-solutions-coder/sdk/query`),
// separate from the main barrel so mocking one never touches the other.
const queryMocks = vi.hoisted(() => ({
  registerQueryBootstrap: vi.fn(),
}));

vi.mock("@bc-solutions-coder/sdk/query", () => ({
  registerQueryBootstrap: queryMocks.registerQueryBootstrap,
  ensureQueryBootstrapped: vi.fn(),
  resetQueryBootstrapForTests: vi.fn(),
}));

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  configureBffClient: vi.fn(),
  configureSsrClient: vi.fn(),
  getSsrRequestContext: vi.fn(() => undefined),
  wireCsrfInterceptor: vi.fn(),
  client: { interceptors: { request: { use: vi.fn() } } },
}));

// Only the helpers the facade executes at MODULE LOAD need real behavior:
// `createConfiguredOnce` (wraps getWallowSdk), `createMfaClient` (called with
// `unwrap` to build the mfa slice), and `unwrap` (passed into it). The generated
// ops are referenced inside lazy slice methods, never at load, so they may be
// absent from the mock.
vi.mock("@bc-solutions-coder/sdk", () => ({
  unwrap: async <T>(pending: Promise<{ data?: T; error?: unknown }>): Promise<T> => {
    const { data, error } = await pending;
    if (error !== undefined) {
      throw error;
    }
    return data as T;
  },
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
  createMfaClient: () => ({}),
  configureBffClient: mocks.configureBffClient,
  configureSsrClient: mocks.configureSsrClient,
  getSsrRequestContext: mocks.getSsrRequestContext,
  wireCsrfInterceptor: mocks.wireCsrfInterceptor,
  client: mocks.client,
  getUser: vi.fn(),
}));

/** Re-evaluate `wallow-sdk.ts` so its module-scope registration runs afresh. */
async function importFacadeModule(): Promise<Record<string, unknown>> {
  vi.resetModules();
  return (await import("./wallow-sdk")) as unknown as Record<string, unknown>;
}

describe("wallow-web SDK query bootstrap registration (Wallow-evd5.2.1)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("registers a configurator with the SDK query bootstrap on import", async () => {
    await importFacadeModule();

    expect(queryMocks.registerQueryBootstrap).toHaveBeenCalledTimes(1);
    expect(queryMocks.registerQueryBootstrap).toHaveBeenCalledWith(expect.any(Function));
  });

  it("exports configureClient and registers exactly that function", async () => {
    const mod = await importFacadeModule();

    expect(typeof mod.configureClient).toBe("function");
    expect(queryMocks.registerQueryBootstrap).toHaveBeenCalledWith(mod.configureClient);
  });

  it("registers a configurator that wires the SSR client (SSR branch) and not the browser client", async () => {
    await importFacadeModule();

    const configurator = queryMocks.registerQueryBootstrap.mock.calls[0]?.[0] as
      | (() => void)
      | undefined;
    expect(configurator).toBeTypeOf("function");

    configurator?.();

    // NODE project => import.meta.env.SSR === true => SSR branch only.
    expect(mocks.getSsrRequestContext).toHaveBeenCalledTimes(1);
    expect(mocks.configureSsrClient).toHaveBeenCalledTimes(1);
    expect(mocks.configureBffClient).not.toHaveBeenCalled();
  });

  it("does not touch the client at import time (registration is side-effect free)", async () => {
    await importFacadeModule();

    expect(mocks.configureSsrClient).not.toHaveBeenCalled();
    expect(mocks.configureBffClient).not.toHaveBeenCalled();
  });

  it("router.tsx side-effect-imports ./lib/wallow-sdk so registration loads before any query", () => {
    const routerSource: string = readFileSync(
      fileURLToPath(new URL("../router.tsx", import.meta.url)),
      "utf8",
    );

    expect(routerSource).toMatch(/import\s+["']\.\/lib\/wallow-sdk["'];/u);
  });
});
