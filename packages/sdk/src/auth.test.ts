import { createServer, type Server } from "node:http";
import { type AddressInfo } from "node:net";

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { getUser, login, logout, type WallowUser } from "./auth";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("login", () => {
  it("redirects to the BFF login endpoint with an encoded returnTo", () => {
    const location: { href: string } = { href: "" };
    vi.stubGlobal("location", location);

    login("/dashboard");

    expect(location.href).toBe("/bff/login?returnTo=%2Fdashboard");
  });
});

describe("logout", () => {
  it("redirects to the BFF logout endpoint", () => {
    const location: { href: string } = { href: "" };
    vi.stubGlobal("location", location);

    logout();

    expect(location.href).toBe("/bff/logout");
  });
});

describe("getUser", () => {
  it("returns the parsed JSON body on a 200 response", async () => {
    const user: WallowUser = { sub: "user-123", email: "user@example.com" };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<WallowUser> => user,
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: WallowUser | null = await getUser();

    expect(result).toEqual(user);
    expect(fetchMock).toHaveBeenCalledWith("/bff/user", {
      credentials: "include",
    });
  });

  it("returns null on a 401 response", async () => {
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async (): Promise<unknown> => ({}),
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: WallowUser | null = await getUser();

    expect(result).toBeNull();
  });

  it("prepends a provided base URL so the request target is an absolute URL", async () => {
    const user: WallowUser = { sub: "user-ssr" };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<WallowUser> => user,
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: WallowUser | null = await getUser({ baseUrl: "http://localhost:3000" });

    expect(result).toEqual(user);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:3000/bff/user", {
      credentials: "include",
    });
  });
});

/**
 * SSR reproduction: during a full-page load wallow-web's `beforeLoad` calls
 * `getUser()` server-side, where the global `fetch` is the real Node (undici)
 * fetch. A relative URL such as `/bff/user` has no origin to resolve against in
 * Node and throws `TypeError: Failed to parse URL from /bff/user`, which surfaces
 * as the dashboard error boundary. Passing an absolute base URL must let the SSR
 * path resolve the user without that crash. This exercises the REAL global fetch
 * against a throwaway loopback server rather than a stub, so it fails today for
 * exactly the production reason.
 */
describe("getUser during SSR (real Node fetch)", () => {
  let server: Server;
  let baseUrl: string;

  beforeEach(async () => {
    server = createServer((req, res) => {
      if (req.url === "/bff/user") {
        res.writeHead(200, { "content-type": "application/json" });
        res.end(JSON.stringify({ sub: "ssr-user" }));
        return;
      }

      res.writeHead(404);
      res.end();
    });

    await new Promise<void>((resolve) => {
      server.listen(0, "127.0.0.1", resolve);
    });
    const address: AddressInfo = server.address() as AddressInfo;
    baseUrl = `http://127.0.0.1:${address.port}`;
  });

  afterEach(async () => {
    await new Promise<void>((resolve, reject) => {
      server.close((error) => (error ? reject(error) : resolve()));
    });
  });

  it("resolves the user against an absolute base URL instead of throwing on a relative one", async () => {
    const result: WallowUser | null = await getUser({ baseUrl });

    expect(result).toEqual({ sub: "ssr-user" });
  });
});
