import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * Slimmed `wallow-sdk.ts` (Wallow-evd5.2.3). The per-feature data slices left in
 * Wallow-evd5.2.2; this task retires the LAST slice — the current-user
 * (`user.me()` / `getUser`) plumbing and the `getWallowSdk` facade it hung off —
 * because the current user is now a cached TanStack Query
 * (`userQueries.currentUser()`, SDK query layer) read in the route `beforeLoad`s.
 *
 * What remains is the client configurator: `configureClient()` (the SSR/browser
 * config authority) plus its module-scope registration with the SDK query
 * bootstrap. The generated ops and client-config helpers are mocked because
 * loading the module runs that registration; the tests assert the slimmed surface
 * and the removal of the user/`getUser` plumbing, not the wire.
 */

// Spies for the SDK query bootstrap subpath, separate from the main barrel.
const queryMocks = vi.hoisted(() => ({
  registerQueryBootstrap: vi.fn(),
}));

vi.mock("@bc-solutions-coder/sdk/query", () => ({
  registerQueryBootstrap: queryMocks.registerQueryBootstrap,
}));

// Hoisted so the vi.mock factory and the test bodies share the same spies. These
// cover every helper the module could import at load in BOTH the pre-refactor
// (facade) and post-refactor (slimmed) shapes, so module import never throws and
// the assertions — not an import error — drive the result.
const mocks = vi.hoisted(() => ({
  configureBffClient: vi.fn(),
  configureSsrClient: vi.fn(),
  getSsrRequestContext: vi.fn(() => undefined),
  wireCsrfInterceptor: vi.fn(),
  getUser: vi.fn(),
  client: { interceptors: { request: { use: vi.fn() } } },
}));

vi.mock("@bc-solutions-coder/sdk", () => ({
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
  wireCsrfInterceptor: mocks.wireCsrfInterceptor,
  client: mocks.client,
  getUser: mocks.getUser,
}));

/** Re-evaluate `wallow-sdk.ts` so its module-scope registration runs afresh. */
async function importFacadeModule(): Promise<Record<string, unknown>> {
  vi.resetModules();
  return (await import("./wallow-sdk")) as unknown as Record<string, unknown>;
}

describe("wallow-sdk (slimmed to the client configurator, Wallow-evd5.2.3)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("still exports configureClient", async () => {
    const mod = await importFacadeModule();

    expect(typeof mod.configureClient).toBe("function");
  });

  it("no longer exports the retired getWallowSdk facade", async () => {
    const mod = await importFacadeModule();

    expect(mod.getWallowSdk).toBeUndefined();
  });

  it("configureClient takes the SSR branch in the node project", async () => {
    const mod = await importFacadeModule();

    (mod.configureClient as () => void)();

    expect(mocks.getSsrRequestContext).toHaveBeenCalled();
    expect(mocks.configureSsrClient).toHaveBeenCalledTimes(1);
    expect(mocks.configureBffClient).not.toHaveBeenCalled();
  });

  it("drops the user-slice / getUser / getWallowSdk plumbing from the source", () => {
    const source: string = readFileSync(
      fileURLToPath(new URL("./wallow-sdk.ts", import.meta.url)),
      "utf8",
    );

    expect(source).not.toMatch(/getWallowSdk/u);
    expect(source).not.toMatch(/getUser/u);
    expect(source).not.toMatch(/UserSlice/u);
    expect(source).not.toMatch(/createConfiguredOnce/u);
  });
});
