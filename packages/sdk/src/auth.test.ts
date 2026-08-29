import { afterEach, describe, expect, it, vi } from "vitest";

import { logout } from "./auth";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
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
    const { location, fetchMock } = stubBrowser(endSessionRedirect(), "wallow_bff-csrf=tok-abc");

    await logout();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/bff/logout");
    expect(sentInit(fetchMock).method).toBe("POST");
    expect(location.href).not.toBe("/bff/logout");
  });

  it("sends the session cookie by asking for credentials on the request", async () => {
    const { fetchMock } = stubBrowser(endSessionRedirect(), "wallow_bff-csrf=tok-abc");

    await logout();

    expect(sentInit(fetchMock).credentials).toBe("include");
  });

  it("echoes the BFF's double-submit cookie in the x-csrf-token header", async () => {
    // The non-HttpOnly companion cookie the login handler writes is the ONE
    // token source in the browser (Wallow-j7qk) — the module token store that
    // used to sit in front of it is deleted.
    const { fetchMock } = stubBrowser(
      endSessionRedirect(),
      "wallow_bff=sealed-session-blob; wallow_bff-csrf=tok-from-cookie",
    );

    await logout();

    expect(sentHeaders(fetchMock).get("x-csrf-token")).toBe("tok-from-cookie");
  });

  it("prefers an explicitly supplied csrfToken over the cookie", async () => {
    const { fetchMock } = stubBrowser(endSessionRedirect(), "wallow_bff-csrf=tok-from-cookie");

    await logout({ csrfToken: "tok-explicit" });

    expect(sentHeaders(fetchMock).get("x-csrf-token")).toBe("tok-explicit");
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

    await expect(logout()).resolves.toBeUndefined();
  });

  it("asks fetch not to follow the cross-origin redirect itself", async () => {
    // Following it would put the IdP's end-session endpoint behind CORS and
    // fail the whole logout with an opaque TypeError; the browser must make
    // that hop as a navigation instead.
    const { fetchMock } = stubBrowser(endSessionRedirect(), "wallow_bff-csrf=tok-abc");

    await logout();

    expect(sentInit(fetchMock).redirect).toBe("manual");
  });

  it("navigates to the IdP end-session URL from the response Location header", async () => {
    const { location } = stubBrowser(endSessionRedirect(), "wallow_bff-csrf=tok-abc");

    await logout();

    expect(location.href).toBe(END_SESSION_URL);
  });

  it("falls back to the app root when the redirect is opaque and its Location is unreadable", async () => {
    const { location } = stubBrowser(opaqueRedirect(), "wallow_bff-csrf=tok-abc");

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
      "wallow_bff-csrf=tok-abc",
    );

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

/**
 * SSR import-safety: `logout()` navigates via the `location` global, which does
 * not exist in Node. Merely importing the module must stay safe (it is
 * re-exported from the browser entry that SSR pulls in), and calling the
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
describe("logout under SSR (no browser globals)", () => {
  it("has no location global in this environment (guards the specs below)", () => {
    expect("location" in globalThis).toBe(false);
  });

  it("imports the module in Node without throwing at module load", async () => {
    vi.resetModules();

    await expect(import("./auth")).resolves.toBeDefined();
  });

  it("logout() throws a descriptive browser-context error rather than a bare ReferenceError", () => {
    expect(() => logout()).toThrowError(/logout\(\)/u);
    expect(() => logout()).toThrowError(/browser/iu);
    expect(() => logout()).not.toThrowError(/location is not defined/u);
  });

  it("throws a plain Error, not a ReferenceError, when called server-side", () => {
    let thrown: unknown;
    try {
      logout();
    } catch (error: unknown) {
      thrown = error;
    }

    expect(thrown).toBeInstanceOf(Error);
    expect(thrown).not.toBeInstanceOf(ReferenceError);
  });

  it("throws the same descriptive error when location is present but undefined", () => {
    vi.stubGlobal("location", undefined);

    expect(() => logout()).toThrowError(/browser/iu);
  });

  it("still navigates when a real location global is present", async () => {
    // logout() no longer navigates to /bff/logout — that GET is a 405 under the
    // hardened gate (Wallow-pu6a.3.9). It POSTs, then navigates on the redirect
    // the handler answers with. Only the SSR guard is under test here.
    const { location, fetchMock } = stubBrowser(endSessionRedirect(), "wallow_bff-csrf=tok-abc");

    await logout();
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/bff/logout");
    expect(location.href).toBe(END_SESSION_URL);
  });
});
