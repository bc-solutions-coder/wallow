import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * SDK query-layer bootstrap registration for wallow-auth (Wallow-evd5.3.1).
 *
 * The SDK's `./query` factories configure the shared `@hey-api` client lazily
 * through a registered configurator (`registerQueryBootstrap` /
 * `ensureQueryBootstrapped`, `packages/sdk/src/query/bootstrap.ts`). wallow-auth
 * has no configurator registered today — its client config lives as an anonymous
 * closure inside `createConfiguredOnce`, reachable only by calling
 * `getWallowAuthSdk()`. This task lifts that closure into an exported
 * `configureClient()` and registers it, mirroring wallow-web's
 * `src/lib/wallow-sdk.ts`.
 *
 * Three guarantees:
 *   1. `wallow-auth-sdk.ts` EXPORTS `configureClient` and registers it with the
 *      SDK query bootstrap at module scope (side-effect free — nothing touches
 *      the client until a query actually runs).
 *   2. The app configures the client ONCE no matter which side goes first. The
 *      facade keeps its own `createConfiguredOnce` guard and the bootstrap keeps
 *      its own, so handing the same `configureClient` to both would run it twice
 *      in whichever order the app happens to hit them — and the second pass would
 *      register a SECOND CSRF request interceptor on the shared client, stamping
 *      the header twice on every mutating request. Both orders are asserted.
 *   3. `router.tsx` side-effect-imports `./lib/wallow-auth-sdk` so the
 *      registration is loaded in BOTH the client and SSR module graphs before any
 *      route fires a query.
 *
 * THE BASE URL IS `/`, NOT `/api`: unlike wallow-web's BFF token tunnel,
 * wallow-auth's h3 server is a passthrough reverse proxy that forwards `/v1/**`
 * and `/connect/**` verbatim at the root (`src/lib/auth-server.ts`), so the
 * configurator has NO SSR branch to take — there is no per-request origin to
 * resolve. That absence is asserted, not assumed: an accidentally copied
 * `configureSsrClient` call would point wallow-auth at the wrong origin.
 *
 * The SDK barrel is mocked because loading this module builds the whole facade;
 * these tests assert registration and the registered configurator's wiring, not
 * the wire itself.
 */

// Spies for the SDK query bootstrap subpath (`@bc-solutions-coder/sdk/query`),
// separate from the main barrel so mocking one never touches the other. The pair
// reproduces the real bootstrap's semantics (register arms, `ensure` runs the
// configurator at most once) because the double-configure tests below turn on
// them: a stub that never ran the configurator could not tell one pass from two.
const queryMocks = vi.hoisted(() => {
  let configurator: (() => void) | undefined;
  let bootstrapped = false;

  const registerQueryBootstrap = vi.fn((configure: () => void) => {
    configurator = configure;
    bootstrapped = false;
  });

  const ensureQueryBootstrapped = vi.fn(() => {
    if (!bootstrapped) {
      configurator?.();
      bootstrapped = true;
    }
  });

  return { registerQueryBootstrap, ensureQueryBootstrapped };
});

vi.mock("@bc-solutions-coder/sdk/query", () => ({
  registerQueryBootstrap: queryMocks.registerQueryBootstrap,
  ensureQueryBootstrapped: queryMocks.ensureQueryBootstrapped,
  resetQueryBootstrapForTests: vi.fn(),
}));

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  configureBffClient: vi.fn(),
  configureSsrClient: vi.fn(),
  wireCsrfInterceptor: vi.fn(),
  client: { interceptors: { request: { use: vi.fn() } } },
}));

// `createConfiguredOnce` is reproduced faithfully rather than stubbed: it is the
// facade's half of the once-only guarantee the double-configure tests assert. The
// OIDC builders and `createAuthClient` are referenced inside the lazy build step,
// never at load, so plain stubs suffice.
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
  createAuthClient: () => ({}),
  configureBffClient: mocks.configureBffClient,
  configureSsrClient: mocks.configureSsrClient,
  wireCsrfInterceptor: mocks.wireCsrfInterceptor,
  client: mocks.client,
  isSafeReturnUrl: vi.fn(),
  buildConnectAuthorizeUrl: vi.fn(),
  buildConsentSubmitUrl: vi.fn(),
  buildExchangeTicketUrl: vi.fn(),
  buildConnectLogoutUrl: vi.fn(),
}));

/** Re-evaluate `wallow-auth-sdk.ts` so its module-scope registration runs afresh. */
async function importFacadeModule(): Promise<Record<string, unknown>> {
  vi.resetModules();
  return (await import("./wallow-auth-sdk")) as unknown as Record<string, unknown>;
}

describe("wallow-auth SDK query bootstrap registration (Wallow-evd5.3.1)", () => {
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

  it("configures the client once when a query bootstraps before the facade is used", async () => {
    const mod = await importFacadeModule();

    // First query fires -> the query layer bootstraps; then a mutation/OIDC call
    // site reaches for the facade.
    queryMocks.ensureQueryBootstrapped();
    (mod.getWallowAuthSdk as () => unknown)();

    expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
    expect(mocks.wireCsrfInterceptor).toHaveBeenCalledTimes(1);
  });

  it("configures the client once when the facade is used before any query", async () => {
    const mod = await importFacadeModule();

    (mod.getWallowAuthSdk as () => unknown)();
    queryMocks.ensureQueryBootstrapped();

    expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
    expect(mocks.wireCsrfInterceptor).toHaveBeenCalledTimes(1);
  });

  it("registers a configurator that points the client at the same-origin root and wires CSRF", async () => {
    await importFacadeModule();

    const configurator = queryMocks.registerQueryBootstrap.mock.calls[0]?.[0] as
      | (() => void)
      | undefined;
    expect(configurator).toBeTypeOf("function");

    configurator?.();

    expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
    expect(mocks.configureBffClient).toHaveBeenCalledWith({ baseUrl: "/" });
    expect(mocks.wireCsrfInterceptor).toHaveBeenCalledWith(mocks.client);
  });

  it("takes no SSR branch — wallow-auth's proxy is same-origin in both passes", async () => {
    await importFacadeModule();

    const configurator = queryMocks.registerQueryBootstrap.mock.calls[0]?.[0] as
      | (() => void)
      | undefined;
    configurator?.();

    expect(mocks.configureSsrClient).not.toHaveBeenCalled();
  });

  it("does not touch the client at import time (registration is side-effect free)", async () => {
    await importFacadeModule();

    expect(mocks.configureBffClient).not.toHaveBeenCalled();
    expect(mocks.wireCsrfInterceptor).not.toHaveBeenCalled();
  });

  it("router.tsx side-effect-imports ./lib/wallow-auth-sdk so registration loads before any query", () => {
    const routerSource: string = readFileSync(
      fileURLToPath(new URL("../router.tsx", import.meta.url)),
      "utf8",
    );

    expect(routerSource).toMatch(/import\s+["']\.\/lib\/wallow-auth-sdk["'];/u);
  });
});
