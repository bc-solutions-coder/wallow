import { readFileSync } from "node:fs";
import { createServer, type Server } from "node:http";
import { type AddressInfo } from "node:net";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { getUser, login, logout, type WallowUser } from "./auth";
import { setCsrfToken } from "./csrf";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  setCsrfToken(null);
});

/** A writable stand-in for the `location` global, so a test can read the target. */
interface FakeLocation {
  href: string;
}

/** The end-session URL the BFF logout handler 302s to on success. */
const END_SESSION_URL: string =
  "https://idp.example.com/connect/logout?post_logout_redirect_uri=https%3A%2F%2Fapp.example.com%2F";

/**
 * Stub the browser globals `logout()` depends on: a writable `location`, a
 * `document.cookie` string, and a `fetch` resolving to `response`.
 */
function stubBrowser(
  response: unknown,
  cookie: string = "",
): { location: FakeLocation; fetchMock: ReturnType<typeof vi.fn> } {
  const location: FakeLocation = { href: "" };
  const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue(response);
  vi.stubGlobal("location", location);
  vi.stubGlobal("document", { cookie });
  vi.stubGlobal("fetch", fetchMock);
  return { location, fetchMock };
}

/** The 302 the logout handler answers on success, with a readable `Location`. */
function endSessionRedirect(location: string = END_SESSION_URL): Response {
  return new Response(null, { status: 302, headers: { location } });
}

/**
 * What `fetch(..., { redirect: "manual" })` really hands back in a browser when
 * the 302 points at another origin: status 0, no readable headers, empty URL.
 * The `Set-Cookie` headers were still applied, so the session IS cleared — only
 * the redirect target is invisible.
 */
function opaqueRedirect(): Response {
  return {
    type: "opaqueredirect",
    status: 0,
    ok: false,
    url: "",
    headers: new Headers(),
  } as unknown as Response;
}

/** An RFC 7807 rejection from the BFF, e.g. the CSRF gate's 403. */
function problemResponse(status: number, code: string): Response {
  return new Response(JSON.stringify({ title: "Rejected", extensions: { code } }), {
    status,
    headers: { "content-type": "application/problem+json" },
  });
}

/** The request init `logout()` passed to `fetch`, as a normalized `Headers`. */
function sentHeaders(fetchMock: ReturnType<typeof vi.fn>): Headers {
  const init: RequestInit = (fetchMock.mock.calls[0]?.[1] ?? {}) as RequestInit;
  return new Headers(init.headers);
}

/** The request init `logout()` passed to `fetch`. */
function sentInit(fetchMock: ReturnType<typeof vi.fn>): RequestInit {
  return (fetchMock.mock.calls[0]?.[1] ?? {}) as RequestInit;
}

describe("login", () => {
  it("redirects to the BFF login endpoint with an encoded returnTo", () => {
    const location: { href: string } = { href: "" };
    vi.stubGlobal("location", location);

    login("/dashboard");

    expect(location.href).toBe("/bff/login?returnTo=%2Fdashboard");
  });
});

/**
 * `logout()` under the hardened `/bff/logout` gate (Wallow-pu6a.3.9).
 *
 * The ported handler answers `405 Method Not Allowed` (with `Allow: POST`) to
 * every non-`POST` request and `403` to a `POST` without a matching
 * `x-csrf-token` — a bare `GET /bff/logout` made an `<img src="/bff/logout">`
 * enough to revoke any visitor's session. The old browser helper navigated with
 * exactly that GET, so it now leaves the user staring at a raw 405 with the
 * session still live. These specs pin the POST-based replacement.
 */
describe("logout (POST + CSRF gate)", () => {
  it("POSTs to /bff/logout instead of navigating the browser there with a GET", async () => {
    const { location, fetchMock } = stubBrowser(endSessionRedirect());
    setCsrfToken("tok-abc");

    await logout();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/bff/logout");
    expect(sentInit(fetchMock).method).toBe("POST");
    expect(location.href).not.toBe("/bff/logout");
  });

  it("sends the session cookie by asking for credentials on the request", async () => {
    const { fetchMock } = stubBrowser(endSessionRedirect());
    setCsrfToken("tok-abc");

    await logout();

    expect(sentInit(fetchMock).credentials).toBe("include");
  });

  it("echoes the CSRF token learned from /bff/user in the x-csrf-token header", async () => {
    const { fetchMock } = stubBrowser(endSessionRedirect());
    setCsrfToken("tok-from-user-endpoint");

    await logout();

    expect(sentHeaders(fetchMock).get("x-csrf-token")).toBe("tok-from-user-endpoint");
  });

  it("prefers an explicitly supplied csrfToken over the module token", async () => {
    const { fetchMock } = stubBrowser(endSessionRedirect());
    setCsrfToken("tok-from-module");

    await logout({ csrfToken: "tok-explicit" });

    expect(sentHeaders(fetchMock).get("x-csrf-token")).toBe("tok-explicit");
  });

  it("falls back to the BFF's readable double-submit cookie when no token was set", async () => {
    // The dashboard never calls setCsrfToken; the non-HttpOnly companion cookie
    // the login handler writes is the only token the browser holds.
    const { fetchMock } = stubBrowser(
      endSessionRedirect(),
      "wallow_bff=sealed-session-blob; wallow_bff-csrf=tok-from-cookie",
    );

    await logout();

    expect(sentHeaders(fetchMock).get("x-csrf-token")).toBe("tok-from-cookie");
  });

  it("reads the double-submit cookie under a __Host- prefixed session cookie name", async () => {
    // F10 defaults the session cookie to `__Host-wallow_bff` whenever it is
    // Secure, so the companion is `__Host-wallow_bff-csrf`.
    const { fetchMock } = stubBrowser(
      endSessionRedirect(),
      "__Host-wallow_bff=sealed; __Host-wallow_bff-csrf=tok-host-prefixed",
    );

    await logout();

    expect(sentHeaders(fetchMock).get("x-csrf-token")).toBe("tok-host-prefixed");
  });

  it("omits the header entirely when no token can be found anywhere", async () => {
    const { fetchMock } = stubBrowser(endSessionRedirect(), "unrelated=value");

    await logout();

    expect(sentHeaders(fetchMock).has("x-csrf-token")).toBe(false);
  });

  it("does not require a document global to build the request", async () => {
    const location: FakeLocation = { href: "" };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue(endSessionRedirect());
    vi.stubGlobal("location", location);
    vi.stubGlobal("fetch", fetchMock);
    setCsrfToken("tok-abc");

    await expect(logout()).resolves.toBeUndefined();
  });

  it("asks fetch not to follow the cross-origin redirect itself", async () => {
    // Following it would put the IdP's end-session endpoint behind CORS and
    // fail the whole logout with an opaque TypeError; the browser must make
    // that hop as a navigation instead.
    const { fetchMock } = stubBrowser(endSessionRedirect());
    setCsrfToken("tok-abc");

    await logout();

    expect(sentInit(fetchMock).redirect).toBe("manual");
  });

  it("navigates to the IdP end-session URL from the response Location header", async () => {
    const { location } = stubBrowser(endSessionRedirect());
    setCsrfToken("tok-abc");

    await logout();

    expect(location.href).toBe(END_SESSION_URL);
  });

  it("falls back to the app root when the redirect is opaque and its Location is unreadable", async () => {
    const { location } = stubBrowser(opaqueRedirect());
    setCsrfToken("tok-abc");

    await logout();

    expect(location.href).toBe("/");
  });

  it("rejects with a descriptive error and stays put when the BFF answers 403", async () => {
    const { location } = stubBrowser(problemResponse(403, "CSRF_INVALID"));

    await expect(logout()).rejects.toThrowError(/403/u);
    expect(location.href).toBe("");
  });

  it("rejects rather than silently leaving the user signed in when the BFF answers 405", async () => {
    const { location } = stubBrowser(
      new Response(null, { status: 405, headers: { allow: "POST" } }),
    );
    setCsrfToken("tok-abc");

    await expect(logout()).rejects.toThrowError(/405/u);
    expect(location.href).toBe("");
  });

  it("still refuses to run server-side, throwing synchronously before any fetch", () => {
    // Wallow-pu6a.3.6 guard: the SSR check must stay a synchronous throw, not a
    // rejected promise, so `location` is never touched during SSR.
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    expect(() => logout()).toThrowError(/browser/iu);
    expect(fetchMock).not.toHaveBeenCalled();
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

/**
 * SSR import-safety: `login()`/`logout()` navigate via the `location` global,
 * which does not exist in Node. Merely importing the module must stay safe (it
 * is re-exported from the browser entry that SSR pulls in), and calling either
 * function server-side must fail with a descriptive, actionable error instead of
 * the raw `ReferenceError: location is not defined`. The SDK stays
 * framework-neutral while doing so — no `@tanstack/react-start` isomorphic-fn
 * dependency.
 *
 * These specs run in the vitest node project, where `location` is genuinely
 * absent from `globalThis` — the exact production condition — so they deliberately
 * stub nothing. The final case additionally pins an explicitly `undefined`
 * `location`, the shape a partially-polyfilled SSR runtime can present.
 */
describe("login/logout under SSR (no browser globals)", () => {
  it("has no location global in this environment (guards the specs below)", () => {
    expect("location" in globalThis).toBe(false);
  });

  it("imports the module in Node without throwing at module load", async () => {
    vi.resetModules();

    await expect(import("./auth")).resolves.toBeDefined();
  });

  it("login() throws a descriptive browser-context error rather than a bare ReferenceError", () => {
    expect(() => login("/dashboard")).toThrowError(/login\(\)/u);
    expect(() => login("/dashboard")).toThrowError(/browser/iu);
    expect(() => login("/dashboard")).not.toThrowError(/location is not defined/u);
  });

  it("logout() throws a descriptive browser-context error rather than a bare ReferenceError", () => {
    expect(() => logout()).toThrowError(/logout\(\)/u);
    expect(() => logout()).toThrowError(/browser/iu);
    expect(() => logout()).not.toThrowError(/location is not defined/u);
  });

  it("throws a plain Error, not a ReferenceError, when called server-side", () => {
    let thrown: unknown;
    try {
      login();
    } catch (error: unknown) {
      thrown = error;
    }

    expect(thrown).toBeInstanceOf(Error);
    expect(thrown).not.toBeInstanceOf(ReferenceError);
  });

  it("throws the same descriptive error when location is present but undefined", () => {
    vi.stubGlobal("location", undefined);

    expect(() => login()).toThrowError(/browser/iu);
    expect(() => logout()).toThrowError(/browser/iu);
  });

  it("still navigates when a real location global is present", async () => {
    // logout() no longer navigates to /bff/logout — that GET is a 405 under the
    // hardened gate (Wallow-pu6a.3.9). It POSTs, then navigates on the redirect
    // the handler answers with. Only the SSR guard is under test here.
    const { location, fetchMock } = stubBrowser(endSessionRedirect());
    setCsrfToken("tok-abc");

    login("/dashboard");
    expect(location.href).toBe("/bff/login?returnTo=%2Fdashboard");

    await logout();
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/bff/logout");
    expect(location.href).toBe(END_SESSION_URL);
  });
});

/**
 * Framework neutrality guard: the SSR fix must be a plain `typeof location`
 * check, never TanStack Start's `createIsomorphicFn`. The SDK must not gain a
 * dependency on `@tanstack/react-start` in any dependency bucket, and `auth.ts`
 * must not import it.
 */
describe("SDK framework neutrality", () => {
  // packages/sdk/src -> packages/sdk
  const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

  interface PackageJson {
    dependencies?: Record<string, string>;
    devDependencies?: Record<string, string>;
    peerDependencies?: Record<string, string>;
  }

  it("declares no dependency on @tanstack/react-start", () => {
    const pkg: PackageJson = JSON.parse(
      readFileSync(resolve(packageRoot, "package.json"), "utf8"),
    ) as PackageJson;

    const declared: string[] = [
      ...Object.keys(pkg.dependencies ?? {}),
      ...Object.keys(pkg.devDependencies ?? {}),
      ...Object.keys(pkg.peerDependencies ?? {}),
    ];

    expect(declared).not.toContain("@tanstack/react-start");
  });

  it("does not import @tanstack/react-start or createIsomorphicFn in auth.ts", () => {
    const source: string = readFileSync(resolve(packageRoot, "src/auth.ts"), "utf8");

    expect(source).not.toContain("@tanstack/react-start");
    expect(source).not.toContain("createIsomorphicFn");
  });
});
