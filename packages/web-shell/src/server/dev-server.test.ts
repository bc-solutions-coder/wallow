import { type IncomingMessage, type Server, type ServerResponse } from "node:http";
import { type AddressInfo } from "node:net";
import { join } from "node:path";

import { tanstackRouter } from "@tanstack/router-plugin/vite";
import { type InlineConfig, type ViteDevServer } from "vite";
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { createDevServer, type DevServerConfig } from "./dev-server";

// The dev seam builds its own inline plugin list because `configFile: false`
// means the app's vite.config.ts (and any tanstackRouter() in it) is never read,
// so the codegen plugin MUST be re-declared here too. Stub it to observe the
// wiring (presence, order-before-react, appDir-derived options) without booting
// the real generator; the derived options are only visible via the call args.
vi.mock("@tanstack/router-plugin/vite", () => ({
  tanstackRouter: vi.fn((options: unknown) => ({
    name: "tanstack:router-generator",
    __options: options,
  })),
}));

const tanstackRouterMock = vi.mocked(tanstackRouter);

/**
 * Drives `createDevServer` end-to-end over a real ephemeral port (Wallow-0q2s.8.4).
 * The factory's job is Node<->WHATWG bridging + a two-way dispatch (proxy vs
 * Vite-middlewares-then-SSR), so the suite makes real HTTP requests against the
 * listening server. The heavy dependency — booting a live Vite dev server — is
 * injected as a fake so the tests pin the DISPATCH + CONFIG contract, not Vite's
 * internals. The per-app differences the extraction must preserve are asserted
 * directly: `reactPluginInDev` gates whether an `@vitejs/plugin-react` plugin is
 * registered, and `loadProxyHandler` is invoked per request (never memoized by
 * the factory) so wallow-web's fresh-load laziness and wallow-auth's own caching
 * both remain the app's choice.
 */
const started: Server[] = [];
let originalPort: string | undefined;

/** A minimal SSR entry: echoes the path so the SSR fallback is observable. */
function render(request: Request): Promise<Response> {
  const { pathname } = new URL(request.url);
  const status: number = pathname === "/missing" ? 404 : 200;
  return Promise.resolve(
    new Response(`SSR:${pathname}`, {
      status,
      headers: { "content-type": "text/html; charset=utf-8" },
    }),
  );
}

/** Recursively collect the `.name` of every resolved Vite plugin. */
async function collectPluginNames(plugins: unknown): Promise<string[]> {
  const resolved: unknown = await plugins;
  if (resolved === null || resolved === undefined || resolved === false) {
    return [];
  }
  if (Array.isArray(resolved)) {
    const nested: string[][] = await Promise.all(
      resolved.map((entry) => collectPluginNames(entry)),
    );
    return nested.flat();
  }
  if (typeof resolved === "object" && "name" in resolved) {
    return [String((resolved as { name: unknown }).name)];
  }
  return [];
}

/** Index of the first plugin whose resolved name matches, or -1. */
function firstIndex(names: string[], pattern: RegExp): number {
  return names.findIndex((name: string): boolean => pattern.test(name));
}

/** The single `options` object the (stubbed) tanstackRouter plugin was last called with. */
function lastRouterOptions(): Record<string, unknown> {
  expect(tanstackRouterMock).toHaveBeenCalled();
  return tanstackRouterMock.mock.calls.at(-1)?.[0] as Record<string, unknown>;
}

interface FakeVite {
  vite: ViteDevServer;
  /** IDs passed to `ssrLoadModule`, in call order. */
  ssrCalls: string[];
}

/** A fake Vite dev server whose middlewares always fall through to the SSR handler. */
function makeFakeVite(): FakeVite {
  const ssrCalls: string[] = [];
  const middlewares = (_req: IncomingMessage, _res: ServerResponse, next: () => void): void => {
    next();
  };
  const vite = {
    middlewares,
    ssrLoadModule: (id: string): Promise<unknown> => {
      ssrCalls.push(id);
      if (id === "/src/ssr.tsx") {
        return Promise.resolve({ render });
      }
      return Promise.resolve({});
    },
    ssrFixStacktrace: (): void => {},
    close: (): Promise<void> => Promise.resolve(),
  } as unknown as ViteDevServer;
  return { vite, ssrCalls };
}

interface Harness {
  baseUrl: string;
  /** How many times `config.loadProxyHandler` was invoked. */
  loadCalls: () => number;
  /** The `InlineConfig` handed to the injected `createViteServer`. */
  inlineConfig: () => InlineConfig;
  ssrCalls: () => string[];
}

interface StartOptions {
  reactPluginInDev?: boolean;
  proxyHandler?: (request: Request) => Promise<Response>;
  appDir?: string;
  clientIpHeader?: string;
}

async function start(options: StartOptions = {}): Promise<Harness> {
  const { vite, ssrCalls } = makeFakeVite();
  let capturedConfig: InlineConfig | undefined;
  let loadCalls = 0;

  const proxyHandler: (request: Request) => Promise<Response> =
    options.proxyHandler ?? ((): Promise<Response> => Promise.resolve(new Response("ready")));

  const config: DevServerConfig = {
    appName: "wallow-test",
    defaultPort: "3002",
    appDir: options.appDir ?? "/tmp/wallow-dev-fixture",
    isProxyPath: (pathname: string): boolean => pathname === "/health",
    loadProxyHandler: (): Promise<(request: Request) => Promise<Response>> => {
      loadCalls += 1;
      return Promise.resolve(proxyHandler);
    },
    ...(options.reactPluginInDev === undefined
      ? {}
      : { reactPluginInDev: options.reactPluginInDev }),
    ...(options.clientIpHeader === undefined ? {} : { clientIpHeader: options.clientIpHeader }),
  };

  const server: Server = await createDevServer(config, {
    createViteServer: (inlineConfig: InlineConfig): Promise<ViteDevServer> => {
      capturedConfig = inlineConfig;
      return Promise.resolve(vite);
    },
  });
  started.push(server);
  const { port } = server.address() as AddressInfo;
  return {
    baseUrl: `http://127.0.0.1:${port}`,
    loadCalls: (): number => loadCalls,
    inlineConfig: (): InlineConfig => {
      if (capturedConfig === undefined) {
        throw new Error("createViteServer was never called");
      }
      return capturedConfig;
    },
    ssrCalls: (): string[] => ssrCalls,
  };
}

beforeAll((): void => {
  originalPort = process.env.PORT;
  // ephemeral: every dev server binds a free port, no collisions
  process.env.PORT = "0";
});

beforeEach((): void => {
  tanstackRouterMock.mockClear();
});

afterEach((): void => {
  while (started.length > 0) {
    started.pop()?.close();
  }
});

afterAll((): void => {
  if (originalPort === undefined) {
    delete process.env.PORT;
  } else {
    process.env.PORT = originalPort;
  }
});

describe("createDevServer", () => {
  it("resolves to a listening node http server", async () => {
    const harness: Harness = await start();

    expect((harness.inlineConfig as unknown) !== undefined).toBe(true);
    const res: Response = await fetch(`${harness.baseUrl}/`);
    expect(res.status).toBe(200);
  });

  it("boots a Vite dev server in custom middlewareMode rooted at the app dir with no config file", async () => {
    const harness: Harness = await start({ appDir: "/tmp/some-app-root" });

    const cfg: InlineConfig = harness.inlineConfig();
    expect(cfg.root).toBe("/tmp/some-app-root");
    expect(cfg.appType).toBe("custom");
    expect(cfg.configFile).toBe(false);
    expect(cfg.server?.middlewareMode).toBe(true);
  });

  it("always wires the shared wallowStyles() brand-assets plugin", async () => {
    const harness: Harness = await start();

    const names: string[] = await collectPluginNames(harness.inlineConfig().plugins);
    expect(names).toContain("wallow:brand-assets");
  });

  it("does NOT register @vitejs/plugin-react in dev by default (whole-document hydration)", async () => {
    const harness: Harness = await start();

    const names: string[] = await collectPluginNames(harness.inlineConfig().plugins);
    expect(names.some((name: string): boolean => /react/i.test(name))).toBe(false);
  });

  it("does NOT register @vitejs/plugin-react when reactPluginInDev is false", async () => {
    const harness: Harness = await start({ reactPluginInDev: false });

    const names: string[] = await collectPluginNames(harness.inlineConfig().plugins);
    expect(names.some((name: string): boolean => /react/i.test(name))).toBe(false);
  });

  it("registers @vitejs/plugin-react when reactPluginInDev is true", async () => {
    const harness: Harness = await start({ reactPluginInDev: true });

    const names: string[] = await collectPluginNames(harness.inlineConfig().plugins);
    expect(names.some((name: string): boolean => /react/i.test(name))).toBe(true);
  });

  it("does not load the proxy bridge at boot (proxy laziness)", async () => {
    const harness: Harness = await start();

    expect(harness.loadCalls()).toBe(0);
  });

  it("dispatches proxy paths through loadProxyHandler and pipes the response", async () => {
    const harness: Harness = await start({
      proxyHandler: (): Promise<Response> =>
        Promise.resolve(new Response("upstream", { status: 200 })),
    });

    const res: Response = await fetch(`${harness.baseUrl}/health`);

    expect(res.status).toBe(200);
    expect(await res.text()).toBe("upstream");
    expect(harness.loadCalls()).toBe(1);
  });

  it("invokes loadProxyHandler per proxy request rather than memoizing it in the factory", async () => {
    const harness: Harness = await start();

    await fetch(`${harness.baseUrl}/health`);
    await fetch(`${harness.baseUrl}/health`);

    expect(harness.loadCalls()).toBe(2);
  });

  it("preserves the proxy response status and multiple Set-Cookie headers", async () => {
    const harness: Harness = await start({
      proxyHandler: (): Promise<Response> => {
        const headers: Headers = new Headers();
        headers.append("set-cookie", "a=1; Path=/");
        headers.append("set-cookie", "b=2; Path=/");
        return Promise.resolve(new Response("teapot", { status: 418, headers }));
      },
    });

    const res: Response = await fetch(`${harness.baseUrl}/health`);

    expect(res.status).toBe(418);
    expect(res.headers.getSetCookie()).toHaveLength(2);
  });

  it("stamps the client IP header from req.socket.remoteAddress on proxied requests when clientIpHeader is set (auth topology)", async () => {
    const header = "x-wallow-client-ip";
    const harness: Harness = await start({
      clientIpHeader: header,
      proxyHandler: (request: Request): Promise<Response> =>
        Promise.resolve(new Response(request.headers.get(header) ?? "<absent>")),
    });

    const res: Response = await fetch(`${harness.baseUrl}/health`);
    const body: string = await res.text();

    // The socket peer over loopback is a real, non-empty address (127.0.0.1 or an
    // IPv6 form). Assert the seam header carried it through rather than being absent.
    expect(body).not.toBe("<absent>");
    expect(body.length).toBeGreaterThan(0);
    expect(body).toMatch(/(\d{1,3}\.){3}\d{1,3}|::1|::ffff:/);
  });

  it("does NOT stamp a client IP header when clientIpHeader is omitted (web topology)", async () => {
    const header = "x-wallow-client-ip";
    const harness: Harness = await start({
      proxyHandler: (request: Request): Promise<Response> =>
        Promise.resolve(new Response(request.headers.get(header) ?? "<absent>")),
    });

    const res: Response = await fetch(`${harness.baseUrl}/health`);
    const body: string = await res.text();

    expect(body).toBe("<absent>");
  });

  it("server-renders non-proxy paths through Vite middlewares then the SSR entry", async () => {
    const harness: Harness = await start();

    const res: Response = await fetch(`${harness.baseUrl}/login`);

    expect(res.status).toBe(200);
    expect(await res.text()).toBe("SSR:/login");
    expect(harness.ssrCalls()).toContain("/src/ssr.tsx");
    expect(harness.loadCalls()).toBe(0);
  });

  it("propagates the SSR entry's own status (e.g. a 404 route)", async () => {
    const harness: Harness = await start();

    const res: Response = await fetch(`${harness.baseUrl}/missing`);

    expect(res.status).toBe(404);
  });

  it("wires the TanStack Router codegen plugin into the dev inline plugin list", async () => {
    const harness: Harness = await start();

    const names: string[] = await collectPluginNames(harness.inlineConfig().plugins);
    expect(names.some((name: string): boolean => /tanstack.?router/i.test(name))).toBe(true);
  });

  it("wires the router plugin even when reactPluginInDev is false (codegen is independent of Fast Refresh)", async () => {
    const harness: Harness = await start({ reactPluginInDev: false });

    const names: string[] = await collectPluginNames(harness.inlineConfig().plugins);
    expect(names.some((name: string): boolean => /tanstack.?router/i.test(name))).toBe(true);
  });

  it("orders the router plugin BEFORE react when react is registered in dev", async () => {
    const harness: Harness = await start({ reactPluginInDev: true });

    const names: string[] = await collectPluginNames(harness.inlineConfig().plugins);
    const routerIndex: number = firstIndex(names, /tanstack.?router/i);
    const reactIndex: number = firstIndex(names, /react/i);

    expect(routerIndex).toBeGreaterThanOrEqual(0);
    expect(reactIndex).toBeGreaterThanOrEqual(0);
    expect(routerIndex).toBeLessThan(reactIndex);
  });

  it("derives the router plugin's route paths from config.appDir and targets react", async () => {
    await start({ appDir: "/tmp/some-app-root" });

    const options: Record<string, unknown> = lastRouterOptions();
    expect(options.target).toBe("react");
    expect(options.routesDirectory).toBe(join("/tmp/some-app-root", "src", "routes"));
    expect(options.generatedRouteTree).toBe(join("/tmp/some-app-root", "src", "routeTree.gen.ts"));
  });

  it("keeps automatic route-based code splitting OFF in the dev seam too", async () => {
    await start({ appDir: "/tmp/some-app-root" });

    expect(lastRouterOptions().autoCodeSplitting).toBe(false);
  });
});
