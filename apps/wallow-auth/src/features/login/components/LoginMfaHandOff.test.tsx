import { isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as loginRoute } from "@app/routes/login";

/**
 * ROUTE-LEVEL spec for the MFA hand-off (Wallow-vec7.3.15 / 2.8e).
 *
 * NO NEW PRODUCTION CODE WAS NEEDED for this bead: `.3.11` built the hand-off
 * into `authDispositionOf` (auth-result.ts:251-271) while porting the password
 * tab, and this file's job is to PIN that behaviour so a later edit cannot
 * quietly take it away. Its tests were written to fail against a mutated
 * implementation before being accepted (see the mutation log on the bead).
 *
 * ── WHY A SECOND FILE, AND WHAT IS DIFFERENT ABOUT IT ────────────────────────
 *
 * `LoginScreen.test.tsx` already pins the hand-off at the COMPONENT level, with
 * `returnUrl` handed to `<LoginScreen>` as a prop. That leaves the seam this
 * bead's acceptance actually names untested: the query string. `.3.15` asks that
 * "returnUrl is preserved ACROSS the hand-off", and the value starts life as
 * `?returnUrl=…` on the /login link, not as a prop. So every test here drives
 * the REAL route — `validateSearch` → `Route.useSearch()` → `<LoginScreen>` →
 * `navigate()` — through `renderRouteAt`. A route that dropped `returnUrl` out
 * of its search schema would leave every component test green and every user
 * stranded; only these tests see it.
 *
 * That seam is not hypothetical. TanStack's default search parser JSON-parses
 * every query value (bd memory `tanstack-router-default-search-parser-json-
 * parses-values`), which is why `validateSearch` type-guards each param — and
 * why a returnUrl surviving the round trip INTACT is worth pinning.
 *
 * ── THE .3.17 OUTAGE IS THE POINT OF THIS FILE ───────────────────────────────
 *
 * A returnUrl guard is not a thing you either "have" or "lack" — it has TWO
 * failure directions, and the expensive one is the guard that refuses real
 * traffic. `.3.17` was exactly that: `/mfa/challenge` was handed an ABSOLUTE
 * returnUrl and `isSafeReturnUrl` accepts only relative ones, so 100% of
 * external-login MFA users hit /error. A total outage LOOKS like a security
 * feature from a test suite that only ever feeds the guard hostile inputs.
 *
 * So `refusesNothingOnALegitimateAbsoluteReturnUrl` below feeds the LEGITIMATE
 * absolute value, and it is the load-bearing test in this file: if the hand-off
 * ever grows an `isSafeReturnUrl` check, that test goes red rather than green.
 *
 * Real traffic, verified in the controller rather than assumed:
 *
 *   • RELATIVE — `AuthorizationController.Authorize` builds returnUrl as
 *     `PathBase + Path + QueryString` (:53), refuses it unless `Url.IsLocalUrl`
 *     (:62), then redirects to `{authUrl}/login?returnUrl=…` (:67).
 *   • ABSOLUTE — `AccountController.ExternalLoginCallback` admits returnUrl only
 *     if `redirectUriValidator.IsAllowedAsync` passes (:274), then hands it to
 *     `{authUrl}/mfa/challenge?returnUrl={Uri.EscapeDataString(returnUrl)}`
 *     (:313, :335). Absolute, allow-listed, and `isSafeReturnUrl` says FALSE.
 *
 * Both reach this screen. Both must survive the hand-off.
 *
 * ── GUARD WHERE THE CLIENT PICKS THE DESTINATION, DEFER WHERE THE SERVER DOES ─
 *
 * (bd memory `guard-where-the-client-picks-the-destination-defer-where-the-
 * server-does`.) The asymmetry inside `authDispositionOf` is deliberate:
 *
 *   TICKET path GUARDS — the returnUrl IS the destination the browser is sent
 *     to, so `isSafeReturnUrl` decides, and an unsafe value is REFUSED (not
 *     sanitised). Pinned in `LoginScreen.test.tsx`, not re-pinned here.
 *   MFA path DEFERS — the destination is the CONSTANT in-app path
 *     `/mfa/challenge`. The returnUrl is inert query CARGO that this screen only
 *     carries; `/mfa/challenge` re-reads and re-guards it on arrival.
 *
 * The deferred guard's premise was CHECKED, not taken on faith: `MfaChallenge-
 * Form.tsx:373-407` computes `isSafeReturnUrl` for the relative case and, for
 * the absolute case, spends a request on `auth.validateRedirectUri` — the server
 * allow-list, the only thing that can tell an allow-listed absolute returnUrl
 * from an attack. The value is re-guarded on arrival, so carrying it here is
 * safe. What a deferred guard still owes is INJECTION, which
 * `encodesTheReturnUrlAsASingleQueryValue` pins.
 *
 * ── NO cookieRelay ───────────────────────────────────────────────────────────
 *
 * A cross-origin hand-off would need a `cookieRelay` param. Ours does
 * not: the partial-auth cookie is first-party through the proxy, so the hand-off
 * is a client-router `navigate()`. `carriesNoRelaySpecificQueryParam` pins the
 * absence by enumerating the query KEYS rather than grepping for the string
 * "cookieRelay" — an assertion that only says `not.toContain("cookieRelay")`
 * would pass for a hand-off that grew any OTHER stray param.
 *
 * ── TEST SEAM: THE REAL SDK OVER A FAKE TRANSPORT (Wallow-pu6a.5.1) ──────────
 *
 * The SDK is no longer replaced by a hand-written object (forbidden by
 * `src/sdk-test-seam.test.ts`). `@bc-solutions-coder/testing/sdk-harness` fakes
 * `fetch` and nothing else, so the login POST is serialized, sent, parsed and
 * fed to `authDispositionOf` by the code that ships. What used to be
 * `mocks.login.mockResolvedValue(body)` is now `respondWithLogin(body)`: the
 * SAME body, delivered as a real 200 response on the wire.
 *
 * Two of the old spies are gone entirely, because the harness leaves the REAL
 * `auth-oidc` builders running (they are pure functions):
 *
 *   • `isSafeReturnUrl` is IMPORTED and called directly where a test needs to
 *     state the guard's verdict on a value — no local mirror of its rule that
 *     could drift from the implementation it stands in for.
 *   • `buildExchangeTicketUrl` really builds a URL, so the exchange-ticket
 *     hand-off is observed at the NAVIGATION it performs (see below) rather than
 *     at a spy's arguments. That is strictly stronger: it pins the URL the
 *     browser is actually sent to, not the arguments of a builder that a
 *     regression could stop honouring.
 *
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface (Wallow-pu6a.5.5).
 * The `@tanstack/react-router` `useNavigate` mock STAYS — the client-router
 * hand-off is the subject of this file, and the router is not the SDK.
 *
 * ── NAVIGATION SEAM (Wallow-xzha.3.1) ────────────────────────────────────────
 *
 * The exchange-ticket hand-off assigns `globalThis.location.href = …`. In real
 * Chromium `location` is `[Unforgeable]`, so `vi.stubGlobal("location", …)`
 * cannot shadow it and the assignment would navigate the runner iframe and tear
 * the test down. Instead we listen on the Navigation API `navigate` event the
 * assignment fires, record `destination.url`, and `preventDefault()` so the
 * navigation is cancelled. The recorded array stands in for the old settable
 * `location.href`: a full-page hand-off appends exactly one absolute URL, and a
 * client-router hand-off appends nothing at all. bd memory
 * `full-navigation-seam-for-wallow-auth-screens-that`.
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

/**
 * The returnUrl `/connect/authorize` really sends: relative, already past
 * `Url.IsLocalUrl`. The real-traffic pole of the guard.
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/**
 * The returnUrl the EXTERNAL-login flow really sends: absolute, and admitted by
 * the server's `redirectUriValidator` allow-list. `isSafeReturnUrl` returns
 * FALSE for this — it is relative-only — which is precisely why the MFA path
 * must not consult it. This constant is the `.3.17` outage in one value.
 */
const ALLOW_LISTED_ABSOLUTE_RETURN_URL = "http://localhost:5003/dashboard";

/** `AccountController.Login` — the one endpoint every test here drives. */
const LOGIN_ENDPOINT = "/v1/identity/auth/login";

/** The provider list the login screen also renders; answered empty throughout. */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

/** `AccountController.ExchangeTicket` — the full-page continue-to-sign-in hand-off. */
const EXCHANGE_TICKET_PATH = "/v1/identity/auth/exchange-ticket";

const NOT_FOUND_STATUS = 404;

let harness: SdkHarness;

/** The body the login endpoint answers with; set per describe/test. */
let loginBody: unknown;

/** Program the 200 body the login POST resolves with — the old `mockResolvedValue`. */
function respondWithLogin(body: unknown): void {
  loginBody = body;
}

interface NavigateEvent extends Event {
  readonly destination: { readonly url: string };
}
interface NavigationLike {
  addEventListener: (type: "navigate", handler: (event: NavigateEvent) => void) => void;
  removeEventListener: (type: "navigate", handler: (event: NavigateEvent) => void) => void;
}
const navigationApi: NavigationLike = (globalThis as unknown as { navigation: NavigationLike })
  .navigation;

/** Listeners registered by `captureHandoff`, torn down in `afterEach`. */
const navDisposers: Array<() => void> = [];

/** Arm the navigation seam and return the array a full-page hand-off lands in. */
function captureHandoff(): { urls: string[] } {
  const urls: string[] = [];
  const handler = (event: NavigateEvent): void => {
    urls.push(event.destination.url);
    // Cancel the navigation so assigning `location.href` does not tear the
    // Chromium runner down; the recorded URL is what we assert on.
    event.preventDefault();
  };
  navigationApi.addEventListener("navigate", handler);
  navDisposers.push(() => {
    navigationApi.removeEventListener("navigate", handler);
  });
  return { urls };
}

/**
 * Drive the REAL `/login` route at `url`, so `validateSearch` and
 * `Route.useSearch()` are part of every test rather than being stubbed past.
 * `router.tsx` is off-limits to screen tasks (bd memory `apps-wallow-auth-src-
 * router-tsx-is-closed`), so the route is re-parented onto a throwaway root —
 * the same harness `LoginScreen.test.tsx` uses.
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

/** Fill in the password tab and submit it — the oracle's `HandleLogin`. */
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
  harness = createAuthHarness();
  respondWithLogin({ succeeded: true });
  harness.respond((call) => {
    if (call.path === LOGIN_ENDPOINT) {
      return Response.json(loginBody);
    }

    if (call.path === PROVIDERS_ENDPOINT) {
      return Response.json([]);
    }

    // `client_id=web` on the login link makes the route ask for that client's
    // branding overlay. A bare 404 is the API's "no branding configured", which
    // leaves the fork's chrome in place — nothing this file looks at.
    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

afterEach(() => {
  for (const dispose of navDisposers.splice(0)) {
    dispose();
  }
});

// ─────────────────────────────────────────────────────────────────────────────
// mfaRequired — 200 { succeeded: false, mfaRequired: true }
// ─────────────────────────────────────────────────────────────────────────────

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
    // A direct (non-OIDC) sign-in. There is no returnUrl to preserve, and the
    // hand-off must not invent a dangling `?returnUrl=` — `/mfa/challenge` reads
    // that back as a PRESENT-but-empty value and fails it (`IsNullOrEmpty`
    // parity, MfaChallengeForm.tsx:158).
    const user = userEvent.setup();
    renderRouteAt("/login");

    await submitCredentials(user);

    expect(await handOffHref()).toBe("/mfa/challenge");
  });

  it("does NOT refuse a legitimate absolute returnUrl on the hand-off", async () => {
    // ── THE .3.17 REGRESSION TEST ────────────────────────────────────────────
    // This is the external-login user: allow-listed ABSOLUTE returnUrl, which
    // `isSafeReturnUrl` calls unsafe. If the MFA path ever guards on it, that
    // user is redirected to /error and MFA-over-external-login is 100% dead.
    // A guard here would be an OUTAGE wearing a security feature's clothes, so
    // this test must fail — loudly — the moment one appears.
    //
    // The verdict comes from the REAL guard (imported, not mirrored), so the
    // premise cannot drift from the function this test is defending against.
    const user = userEvent.setup();
    expect(isSafeReturnUrl(ALLOW_LISTED_ABSOLUTE_RETURN_URL)).toBe(false);
    renderRouteAt(loginUrlWithReturnUrl(ALLOW_LISTED_ABSOLUTE_RETURN_URL));

    await submitCredentials(user);

    const { path, params } = partsOf(await handOffHref());
    expect(path).toBe("/mfa/challenge");
    expect(params.get("returnUrl")).toBe(ALLOW_LISTED_ABSOLUTE_RETURN_URL);
  });

  it("carries no relay-specific query param on the hand-off", async () => {
    // The oracle threads `cookieRelay` (Login.razor:509) because its hand-off
    // crossed an origin; ours is same-origin, so the param must be GONE.
    // Enumerating the keys rather than asserting `not.toContain("cookieRelay")`
    // is deliberate: the acceptance says "no relay-specific query params", and
    // only an exhaustive check can see a DIFFERENTLY-named relay param.
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    const { params } = partsOf(await handOffHref());
    expect([...params.keys()]).toEqual(["returnUrl"]);
  });

  it("encodes the returnUrl as a single query value", async () => {
    // What a DEFERRED guard still owes. A returnUrl carrying `&cookieRelay=…`
    // must land as ONE value, not split into a second key: ASP.NET binds a
    // duplicated [FromQuery] as "a,b", a parse failure that silently takes the
    // wrong branch. Pinned at the route level because the value makes a full
    // round trip through the query string before being re-encoded here.
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
    // The partial-auth cookie is first-party through the proxy, so there is
    // nothing to relay across an origin and no reason to drop the SPA. A
    // regression to `location.href = …` would still "work" in a browser, which
    // is exactly why it needs pinning here. The ONLY seam this component uses to
    // drop the SPA is the exchange-ticket URL feeding `location.href`; an MFA
    // hand-off that stayed a client-router `navigate()` never triggers one, so
    // the armed navigation seam must record NOTHING.
    const handoff = captureHandoff();
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    await handOffHref();
    expect(handoff.urls).toEqual([]);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// mfaEnrollmentRequired — the /mfa/enroll arm of the same hand-off
// ─────────────────────────────────────────────────────────────────────────────

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
    // The enroll arm defers for the same reason the challenge arm does; the
    // .3.17 outage would be identical, just one screen over.
    respondWithLogin({ succeeded: false, mfaEnrollmentRequired: true });
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(ALLOW_LISTED_ABSOLUTE_RETURN_URL));

    await submitCredentials(user);

    const { path, params } = partsOf(await handOffHref());
    expect(path).toBe("/mfa/enroll");
    expect(params.get("returnUrl")).toBe(ALLOW_LISTED_ABSOLUTE_RETURN_URL);
  });

  it("does not hand off to /mfa/enroll while the user is inside the grace period", async () => {
    // The hand-off's boundary: grace means "enroll LATER", so the sign-in
    // continues to the ticket exchange instead of being diverted. Pinned here
    // because an over-eager enrollment hand-off would strand grace-period users
    // on a screen they were explicitly excused from.
    const deadline = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
    respondWithLogin({
      succeeded: true,
      mfaEnrollmentRequired: true,
      mfaGraceDeadline: deadline,
      signInTicket: "sign-in-ticket-xyz",
    });
    const handoff = captureHandoff();
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    // The exchange-ticket seam is the URL fed to `location.href`. With the REAL
    // builder in the graph it is asserted at the navigation itself — the exact
    // same-origin endpoint, ticket and returnUrl the browser is sent to — which
    // pins the continue-to-sign-in path more tightly than the builder's
    // arguments ever did, and still never captures `[Unforgeable]` `location`.
    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    const target = new URL(handoff.urls[0] ?? "");
    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_TICKET_PATH);
    expect(target.searchParams.get("ticket")).toBe("sign-in-ticket-xyz");
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});
