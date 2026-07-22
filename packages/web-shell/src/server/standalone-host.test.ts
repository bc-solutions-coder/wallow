import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { type Server } from "node:http";
import { type AddressInfo } from "node:net";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";

import { createStandaloneHost, type ShellConfig } from "./standalone-host";

/**
 * Drives `createStandaloneHost` end-to-end over a real ephemeral port and a
 * throwaway `dist/` tree standing in for a built app (Wallow-0q2s.8.3). The
 * host's whole job is Node<->WHATWG bridging + a three-way dispatch (proxy,
 * built asset, SSR fallback), so the suite makes real HTTP requests rather than
 * mocking the request/response bridge. The one per-app difference — auth stamps
 * `clientIpHeader`, web does not — is asserted by echoing the header back out of
 * a fake proxy handler.
 */
let appDir: string;
let emptyDir: string;
const started: Server[] = [];
let originalPort: string | undefined;

/** A minimal built SSR entry: echoes the path so the SSR fallback is observable. */
const SSR_ENTRY = [
  "export async function render(request) {",
  "  const { pathname } = new URL(request.url);",
  '  const status = pathname === "/missing" ? 404 : 200;',
  '  return new Response("SSR:" + pathname, {',
  "    status,",
  '    headers: { "content-type": "text/html; charset=utf-8" },',
  "  });",
  "}",
  "",
].join("\n");

beforeAll(async (): Promise<void> => {
  originalPort = process.env.PORT;
  // ephemeral: every host binds a free port, no collisions
  process.env.PORT = "0";

  appDir = await mkdtemp(join(tmpdir(), "wallow-web-shell-host-"));
  // A tmpdir carries no package.json, so mark it ESM for the dynamic ssr.js import.
  await writeFile(join(appDir, "package.json"), JSON.stringify({ type: "module" }));
  await mkdir(join(appDir, "dist", "client"), { recursive: true });
  await mkdir(join(appDir, "dist", "server"), { recursive: true });
  await writeFile(join(appDir, "dist", "client", "client.js"), "console.log('hydrate');");
  await writeFile(join(appDir, "dist", "server", "ssr.js"), SSR_ENTRY);

  // No dist/server/ssr.js behind it — exercises the build-first guard.
  emptyDir = await mkdtemp(join(tmpdir(), "wallow-web-shell-host-empty-"));
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

async function start(
  overrides: Partial<ShellConfig> = {},
): Promise<{ server: Server; baseUrl: string }> {
  const config: ShellConfig = {
    appName: "wallow-test",
    defaultPort: "3002",
    appDir,
    isProxyPath: (pathname: string): boolean => pathname === "/health",
    handleProxy: (): Promise<Response> => Promise.resolve(new Response("ready", { status: 200 })),
    ...overrides,
  };
  const server: Server = await createStandaloneHost(config);
  started.push(server);
  const { port } = server.address() as AddressInfo;
  return { server, baseUrl: `http://127.0.0.1:${port}` };
}

describe("createStandaloneHost", () => {
  it("resolves to a listening node http server", async () => {
    const { server } = await start();

    expect(server.address()).not.toBeNull();
    expect((server.address() as AddressInfo).port).toBeGreaterThan(0);
    expect(typeof server.close).toBe("function");
  });

  it("dispatches proxy paths to handleProxy (e.g. /health)", async () => {
    const { baseUrl } = await start();

    const res: Response = await fetch(`${baseUrl}/health`);

    expect(res.status).toBe(200);
    expect(await res.text()).toBe("ready");
  });

  it("serves built client assets out of dist/client", async () => {
    const { baseUrl } = await start();

    const res: Response = await fetch(`${baseUrl}/client.js`);

    expect(res.status).toBe(200);
    expect(await res.text()).toBe("console.log('hydrate');");
    expect(res.headers.get("content-type")).toBe("text/javascript; charset=utf-8");
  });

  it("server-renders every non-proxy, non-asset path via the eager SSR entry", async () => {
    const { baseUrl } = await start();

    const res: Response = await fetch(`${baseUrl}/login`);

    expect(res.status).toBe(200);
    expect(await res.text()).toBe("SSR:/login");
  });

  it("propagates the SSR entry's own status (e.g. a 404 route)", async () => {
    const { baseUrl } = await start();

    const res: Response = await fetch(`${baseUrl}/missing`);

    expect(res.status).toBe(404);
  });

  it("falls through to SSR for the root path rather than reading it as a file", async () => {
    const { baseUrl } = await start();

    const res: Response = await fetch(`${baseUrl}/`);

    expect(await res.text()).toBe("SSR:/");
  });

  it("stamps the client IP header on proxied requests when clientIpHeader is set (auth topology)", async () => {
    const header = "x-wallow-client-ip";
    const { baseUrl } = await start({
      clientIpHeader: header,
      handleProxy: (request: Request): Promise<Response> =>
        Promise.resolve(new Response(request.headers.get(header) ?? "<absent>")),
    });

    const res: Response = await fetch(`${baseUrl}/health`);
    const body: string = await res.text();

    expect(body).not.toBe("<absent>");
    expect(body.length).toBeGreaterThan(0);
  });

  it("does NOT stamp a client IP header when clientIpHeader is omitted (web topology)", async () => {
    const header = "x-wallow-client-ip";
    const { baseUrl } = await start({
      handleProxy: (request: Request): Promise<Response> =>
        Promise.resolve(new Response(request.headers.get(header) ?? "<absent>")),
    });

    const res: Response = await fetch(`${baseUrl}/health`);
    const body: string = await res.text();

    expect(body).toBe("<absent>");
  });

  it("preserves the proxy response status and multiple Set-Cookie headers", async () => {
    const { baseUrl } = await start({
      handleProxy: (): Promise<Response> => {
        const headers: Headers = new Headers();
        headers.append("set-cookie", "a=1; Path=/");
        headers.append("set-cookie", "b=2; Path=/");
        return Promise.resolve(new Response("teapot", { status: 418, headers }));
      },
    });

    const res: Response = await fetch(`${baseUrl}/health`);

    expect(res.status).toBe(418);
    expect(res.headers.getSetCookie()).toHaveLength(2);
  });

  it("rejects at startup when the SSR entry is missing, naming the app and pnpm build", async () => {
    await expect(
      createStandaloneHost({
        appName: "wallow-auth",
        defaultPort: "3002",
        appDir: emptyDir,
        isProxyPath: (): boolean => false,
        handleProxy: (): Promise<Response> => Promise.resolve(new Response(null)),
      }),
    ).rejects.toThrow(/wallow-auth/);

    await expect(
      createStandaloneHost({
        appName: "wallow-auth",
        defaultPort: "3002",
        appDir: emptyDir,
        isProxyPath: (): boolean => false,
        handleProxy: (): Promise<Response> => Promise.resolve(new Response(null)),
      }),
    ).rejects.toThrow(/pnpm build/);
  });
});
