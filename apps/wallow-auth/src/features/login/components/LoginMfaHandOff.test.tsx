import { isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import {
  expectNavigationEscape,
  navigationEscapes,
} from "@bc-solutions-coder/testing/navigation-escape";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as loginRoute } from "@app/routes/login";

/**
 * The MFA hand-off, driven through the real `/login` route so `returnUrl` makes
 * the round trip through the query string.
 *
 * Runs the real SDK over a faked fetch (sdk-harness); only `useNavigate` is
 * mocked, because the client-router hand-off is the subject.
 *
 * The hand-off must NOT consult `isSafeReturnUrl`: `/mfa/challenge` re-guards the
 * cargo on arrival, and external login sends ABSOLUTE returnUrls the
 * relative-only guard rejects.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

// `importOriginal` MUST be spread: the route harness needs the real
// `createRouter`/`RouterProvider`/`Outlet`/`createRootRoute`.
vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const EMAIL = "user@example.com";
const PASSWORD = "Sup3rSecret!";

/** The returnUrl a DIRECT sign-in carries: relative by construction. */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/**
 * The returnUrl the EXTERNAL-login flow really sends: absolute, and admitted by
 * the server's allow-list. `isSafeReturnUrl` returns FALSE for it — it is
 * relative-only — which is precisely why the MFA path must not consult it.
 */
const ALLOW_LISTED_ABSOLUTE_RETURN_URL = "http://localhost:5003/dashboard";

/** The one endpoint every test here drives. */
const LOGIN_ENDPOINT = "/v1/identity/auth/login";

/** The provider list the login screen also renders; answered empty throughout. */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

/** The full-page continue-to-sign-in hand-off. */
const EXCHANGE_TICKET_PATH = "/v1/identity/auth/exchange-ticket";

const NOT_FOUND_STATUS = 404;

let harness: SdkHarness;

/** The body the login endpoint answers with; set per describe/test. */
let loginBody: unknown;

/** Program the 200 body the login POST resolves with. */
function respondWithLogin(body: unknown): void {
  loginBody = body;
}

/**
 * Drive the REAL `/login` route at `url`, so `validateSearch` and
 * `Route.useSearch()` are part of every test rather than stubbed past. The route
 * is re-parented onto a throwaway root; the app's own `router.tsx` is untouched.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
  });
}

/** The /login link the OIDC authorize endpoint really builds. */
function loginUrlWithReturnUrl(returnUrl: string): string {
  return `/login?returnUrl=${encodeURIComponent(returnUrl)}&client_id=web`;
}

/** Fill in the password tab and submit it. */
async function submitCredentials(user: ReturnType<typeof userEvent.setup>) {
  await expect.element(page.getByTestId("login-email")).toBeInTheDocument();
  await user.type(page.getByTestId("login-email"), EMAIL);
  await user.type(page.getByTestId("login-password"), PASSWORD);
  await user.click(page.getByTestId("login-submit"));
}

/** The single `navigate({ href })` the hand-off performs. */
async function handOffHref(): Promise<string> {
  await vi.waitFor(() => {
    expect(mocks.navigate).toHaveBeenCalledTimes(1);
  });

  const [[arg]] = mocks.navigate.mock.calls as [[{ href: string }]];

  return arg.href;
}

/** Split a hand-off href into its constant path and its query params. */
function partsOf(href: string): { path: string; params: URLSearchParams } {
  const [path, query = ""] = href.split("?");

  return { path: path ?? "", params: new URLSearchParams(query) };
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createPassthroughHarness();
  respondWithLogin({ succeeded: true });
  harness.respond((call) => {
    if (call.path === LOGIN_ENDPOINT) {
      return Response.json(loginBody);
    }

    if (call.path === PROVIDERS_ENDPOINT) {
      return Response.json([]);
    }

    // `client_id=web` on the login link makes the route ask for that client's
    // branding overlay. A bare 404 is "no branding configured".
    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

describe("/login MFA hand-off — mfaRequired", () => {
  beforeEach(() => {
    respondWithLogin({ succeeded: false, mfaRequired: true });
  });

  it("threads returnUrl out of the query string into the /mfa/challenge hand-off", async () => {
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    const { path, params } = partsOf(await handOffHref());
    expect(path).toBe("/mfa/challenge");
    // Decoded, so this asserts the value ARRIVES intact — not merely that some
    // encoded blob was copied through.
    expect(params.get("returnUrl")).toBe(RETURN_URL);
  });

  it("hands off to a bare /mfa/challenge when the login link carries no returnUrl", async () => {
    // A direct (non-OIDC) sign-in. There is no returnUrl to preserve, and a
    // dangling `?returnUrl=` reads back at `/mfa/challenge` as PRESENT-but-empty
    // and fails there.
    const user = userEvent.setup();
    renderRouteAt("/login");

    await submitCredentials(user);

    expect(await handOffHref()).toBe("/mfa/challenge");
  });

  it("does NOT refuse a legitimate absolute returnUrl on the hand-off", async () => {
    // The external-login user: allow-listed ABSOLUTE returnUrl, which
    // `isSafeReturnUrl` calls unsafe. A guard on this path sends that user to
    // /error and kills MFA-over-external-login outright. The verdict comes from
    // the REAL guard, imported rather than mirrored, so the premise cannot drift.
    const user = userEvent.setup();
    expect(isSafeReturnUrl(ALLOW_LISTED_ABSOLUTE_RETURN_URL)).toBe(false);
    renderRouteAt(loginUrlWithReturnUrl(ALLOW_LISTED_ABSOLUTE_RETURN_URL));

    await submitCredentials(user);

    const { path, params } = partsOf(await handOffHref());
    expect(path).toBe("/mfa/challenge");
    expect(params.get("returnUrl")).toBe(ALLOW_LISTED_ABSOLUTE_RETURN_URL);
  });

  it("carries no relay-specific query param on the hand-off", async () => {
    // A cross-origin hand-off would need a relay param; this one is same-origin.
    // The KEYS are enumerated rather than asserting `not.toContain("cookieRelay")`
    // because only an exhaustive check sees a DIFFERENTLY-named relay param.
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    const { params } = partsOf(await handOffHref());
    expect([...params.keys()]).toEqual(["returnUrl"]);
  });

  it("encodes the returnUrl as a single query value", async () => {
    // What a DEFERRED guard still owes. A returnUrl carrying `&cookieRelay=…`
    // must land as ONE value: ASP.NET binds a duplicated [FromQuery] as "a,b", a
    // parse failure that silently takes the wrong branch.
    const smuggler = "/connect/authorize?client_id=web&cookieRelay=attacker";
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(smuggler));

    await submitCredentials(user);

    const { params } = partsOf(await handOffHref());
    expect([...params.keys()]).toEqual(["returnUrl"]);
    expect(params.get("returnUrl")).toBe(smuggler);
    expect(params.get("cookieRelay")).toBeNull();
  });

  it("hands off through the client router, not a full page load", async () => {
    // The partial-auth cookie is first-party through the proxy, so there is no
    // reason to drop the SPA. A regression to `location.href = …` would still
    // "work" in a browser, which is why it is pinned: the only seam that drops
    // the SPA is the exchange-ticket URL, so the guard records NOTHING.
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    await handOffHref();
    expect(navigationEscapes()).toEqual([]);
  });
});

describe("/login MFA hand-off — mfaEnrollmentRequired", () => {
  it("threads returnUrl out of the query string into the /mfa/enroll hand-off", async () => {
    respondWithLogin({ succeeded: false, mfaEnrollmentRequired: true });
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    const { path, params } = partsOf(await handOffHref());
    expect(path).toBe("/mfa/enroll");
    expect(params.get("returnUrl")).toBe(RETURN_URL);
    expect([...params.keys()]).toEqual(["returnUrl"]);
  });

  it("does NOT refuse a legitimate absolute returnUrl on the enrollment hand-off", async () => {
    // The enroll arm defers for the same reason the challenge arm does.
    respondWithLogin({ succeeded: false, mfaEnrollmentRequired: true });
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(ALLOW_LISTED_ABSOLUTE_RETURN_URL));

    await submitCredentials(user);

    const { path, params } = partsOf(await handOffHref());
    expect(path).toBe("/mfa/enroll");
    expect(params.get("returnUrl")).toBe(ALLOW_LISTED_ABSOLUTE_RETURN_URL);
  });

  it("does not hand off to /mfa/enroll while the user is inside the grace period", async () => {
    // Grace means "enroll LATER", so the sign-in continues to the ticket exchange
    // instead of being diverted to a screen the user was excused from.
    const deadline = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
    respondWithLogin({
      succeeded: true,
      mfaEnrollmentRequired: true,
      mfaGraceDeadline: deadline,
      signInTicket: "sign-in-ticket-xyz",
    });
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    // The real builder runs, so the exchange is asserted at the navigation
    // itself — the exact same-origin endpoint, ticket and returnUrl the browser
    // is sent to.
    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_TICKET_PATH);
    expect(target.searchParams.get("ticket")).toBe("sign-in-ticket-xyz");
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});
