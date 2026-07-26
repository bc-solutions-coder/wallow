import { readdirSync, readFileSync, existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it, beforeEach, vi } from "vitest";

/**
 * SDK query-layer bootstrap registration for the minimal reference app
 * (Wallow-evd5.4.4).
 *
 * minimal-app deliberately renders NO live data — `HelloCard` is static. What it
 * owes a fork is the *wiring*: `src/lib/sdk.ts` is the copy-from skeleton for the
 * same-origin BFF client, so the SDK query layer's lazy configurator hook
 * (`registerQueryBootstrap` / `ensureQueryBootstrapped`,
 * `packages/sdk/src/query/bootstrap.ts`) has to be registered here too. Otherwise
 * a fork's FIRST `useQuery(xQueries.y())` fires against an unconfigured
 * `@hey-api` client and the failure surfaces far from its cause.
 *
 * This spec pins the same four guarantees wallow-auth's
 * `wallow-auth-sdk.bootstrap.test.ts` pins, plus the two that are specific to
 * this app's role as documentation:
 *
 *   1. `sdk.ts` exports `configureClient` and registers it with the SDK query
 *      bootstrap at module scope, side-effect free — nothing touches the shared
 *      client until a query (or the facade) actually runs.
 *   2. The client is configured exactly ONCE regardless of which side goes first.
 *      The facade's `createConfiguredOnce` guard and the bootstrap's guard are
 *      independent, so handing the same `configureClient` to both would run it
 *      twice and register a SECOND CSRF interceptor on the shared client,
 *      stamping the header twice on every mutating request. Both orders assert.
 *   3. `router.tsx` side-effect-imports `./lib/sdk`, so the registration is armed
 *      in BOTH the client and SSR module graphs. Without this the registration is
 *      dead code — nothing else in this app imports `sdk.ts`.
 *   4. The app still adds no live queries: the wiring is the deliverable, not a
 *      demo fetch.
 *
 * THE BASE URL IS `/`, NOT `/api`: this app's h3 host is a passthrough reverse
 * proxy forwarding `/v1/**` and `/connect/**` verbatim at the root (see
 * `src/lib/proxy-server.ts`), so — as in wallow-auth, and unlike wallow-web's BFF
 * token tunnel — the configurator has NO SSR branch to take. That absence is
 * asserted rather than assumed: a copied `configureSsrClient` call would point
 * this app at the wrong origin.
 *
 * The SDK barrel is mocked because loading the module builds the whole facade;
 * these tests assert registration and the registered configurator's wiring, not
 * the wire itself.
 */

const here: string = dirname(fileURLToPath(import.meta.url));
const appRoot: string = resolve(here, "../..");

// Spies for the query subpath (`@bc-solutions-coder/sdk/query`), kept separate
// from the barrel mock so mocking one never touches the other. The pair
// reproduces the real bootstrap's semantics (register arms; `ensure` runs the
// configurator at most once) because the double-configure tests below turn on
// them — a stub that never ran the configurator could not tell one pass from two.
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
// facade's half of the once-only guarantee the double-configure tests assert.
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
}));

/** Re-evaluate `sdk.ts` so its module-scope registration runs afresh. */
async function importSdkModule(): Promise<Record<string, unknown>> {
  vi.resetModules();
  return (await import("./sdk")) as unknown as Record<string, unknown>;
}

describe("minimal-app SDK query bootstrap registration (Wallow-evd5.4.4)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("registers a configurator with the SDK query bootstrap on import", async () => {
    await importSdkModule();

    expect(queryMocks.registerQueryBootstrap).toHaveBeenCalledTimes(1);
    expect(queryMocks.registerQueryBootstrap).toHaveBeenCalledWith(expect.any(Function));
  });

  it("exports configureClient and registers exactly that function", async () => {
    const mod = await importSdkModule();

    expect(typeof mod.configureClient).toBe("function");
    expect(queryMocks.registerQueryBootstrap).toHaveBeenCalledWith(mod.configureClient);
  });

  it("configures the client once when a query bootstraps before the facade is used", async () => {
    const mod = await importSdkModule();

    queryMocks.ensureQueryBootstrapped();
    (mod.getSdk as () => unknown)();

    expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
    expect(mocks.wireCsrfInterceptor).toHaveBeenCalledTimes(1);
  });

  it("configures the client once when the facade is used before any query", async () => {
    const mod = await importSdkModule();

    (mod.getSdk as () => unknown)();
    queryMocks.ensureQueryBootstrapped();

    expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
    expect(mocks.wireCsrfInterceptor).toHaveBeenCalledTimes(1);
  });

  it("registers a configurator that points the client at the same-origin root and wires CSRF", async () => {
    await importSdkModule();

    const configurator = queryMocks.registerQueryBootstrap.mock.calls[0]?.[0] as
      | (() => void)
      | undefined;
    expect(configurator).toBeTypeOf("function");

    configurator?.();

    expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
    expect(mocks.configureBffClient).toHaveBeenCalledWith({ baseUrl: "/" });
    expect(mocks.wireCsrfInterceptor).toHaveBeenCalledWith(mocks.client);
  });

  it("takes no SSR branch — this app's proxy is same-origin in both passes", async () => {
    await importSdkModule();

    const configurator = queryMocks.registerQueryBootstrap.mock.calls[0]?.[0] as
      | (() => void)
      | undefined;
    configurator?.();

    expect(mocks.configureSsrClient).not.toHaveBeenCalled();
  });

  it("does not touch the client at import time (registration is side-effect free)", async () => {
    await importSdkModule();

    expect(mocks.configureBffClient).not.toHaveBeenCalled();
    expect(mocks.wireCsrfInterceptor).not.toHaveBeenCalled();
  });

  it("router.tsx side-effect-imports ./lib/sdk so the registration is armed in both graphs", () => {
    const routerSource: string = readFileSync(resolve(appRoot, "src/router.tsx"), "utf8");

    expect(routerSource).toMatch(/import\s+["']\.\/lib\/sdk["'];/u);
  });
});

/**
 * Drop block and line comments so the query-free guard below reads EXECUTABLE
 * code only. The wiring is documented by prose that names the very hooks the
 * guard forbids ("a fork's first `useQuery(...)`"), and a mention in a comment is
 * not a live query.
 */
function stripComments(source: string): string {
  return source.replaceAll(/\/\*[\s\S]*?\*\//gu, "").replaceAll(/\/\/[^\n]*/gu, "");
}

describe("minimal-app stays query-free while documenting the query layer", () => {
  it("adds no live queries — the wiring is the deliverable, not a demo fetch", () => {
    const sources: string[] = readdirSync(resolve(appRoot, "src"), { recursive: true })
      .map(String)
      .filter((entry: string) => /\.tsx?$/u.test(entry) && !/\.test\.tsx?$/u.test(entry))
      .map((entry: string) => stripComments(readFileSync(resolve(appRoot, "src", entry), "utf8")));

    for (const source of sources) {
      expect(source).not.toMatch(/\buseSuspenseQuery\s*\(/u);
      expect(source).not.toMatch(/\buseQuery\s*\(/u);
      expect(source).not.toMatch(/\buseMutation\s*\(/u);
    }
  });

  it("README documents the SDK's ./query layer and links the frontend-state guide", () => {
    const readme: string = readFileSync(resolve(appRoot, "README.md"), "utf8");

    expect(readme).toMatch(/`?\.\/query`?/u);
    expect(readme).toMatch(/frontend-state\.md/u);
  });

  it("every relative doc link in the README resolves to a real file", () => {
    const readme: string = readFileSync(resolve(appRoot, "README.md"), "utf8");
    const targets: string[] = [...readme.matchAll(/\]\((\.\.?\/[^)\s#]+)/gu)].map(
      (match: RegExpMatchArray) => match[1] as string,
    );

    expect(targets.length).toBeGreaterThan(0);
    for (const target of targets) {
      expect(existsSync(resolve(appRoot, target)), `broken README link: ${target}`).toBe(true);
    }
  });
});
