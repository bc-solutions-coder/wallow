/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as loginRoute } from "../../../routes/login";

expect.extend(matchers);

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
 * The Blazor oracle's `BuildMfaRedirectUrl` (Login.razor:509) threads a
 * `cookieRelay` param because Blazor's hand-off crossed an origin. Ours does
 * not: the partial-auth cookie is first-party through the proxy, so the hand-off
 * is a client-router `navigate()`. `carriesNoRelaySpecificQueryParam` pins the
 * absence by enumerating the query KEYS rather than grepping for the string
 * "cookieRelay" — an assertion that only says `not.toContain("cookieRelay")`
 * would pass for a hand-off that grew any OTHER stray param.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk`, the app's own facade — never
 * `@bc-solutions-coder/sdk` directly. Plain `vi.mock` factory + `vi.hoisted`
 * spies, never `vi.resetModules()` (bd memory `vitest-resetmodules-breaks-
 * instanceof-across-graphs`).
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  login: vi.fn(),
  isSafeReturnUrl: vi.fn(),
  buildExchangeTicketUrl: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      login: mocks.login,
    },
    oidc: {
      isSafeReturnUrl: mocks.isSafeReturnUrl,
      buildExchangeTicketUrl: mocks.buildExchangeTicketUrl,
    },
  }),
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

/**
 * The real `isSafeReturnUrl` rule (packages/sdk/src/auth-oidc.ts), mirrored
 * rather than imported: screens may not import the SDK, so the seam is mocked,
 * and a mock returning a constant would let these tests pass for the wrong
 * reason. Note it says FALSE for the allow-listed absolute value above.
 */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

/** The real `buildExchangeTicketUrl` shape, mirrored for the same reason. */
function buildExchangeTicketUrlRule(origin: string, ticket: string, returnUrl: string): string {
  return (
    `${origin.replace(/\/+$/u, "")}/v1/identity/auth/exchange-ticket` +
    `?ticket=${encodeURIComponent(ticket)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}`
  );
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/**
 * Drive the REAL `/login` route at `url`, so `validateSearch` and
 * `Route.useSearch()` are part of every test rather than being stubbed past.
 * `router.tsx` is off-limits to screen tasks (bd memory `apps-wallow-auth-src-
 * router-tsx-is-closed`), so the route is re-parented onto a throwaway root —
 * the same harness `LoginScreen.test.tsx` uses.
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    loginRoute.update({
      id: "/login",
      path: "/login",
      getParentRoute: () => rootRoute,
    }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

/** The /login link the OIDC authorize endpoint really builds. */
function loginUrlWithReturnUrl(returnUrl: string): string {
  return `/login?returnUrl=${encodeURIComponent(returnUrl)}&client_id=web`;
}

/** Fill in the password tab and submit it — the oracle's `HandleLogin`. */
async function submitCredentials(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByTestId("login-email");
  await user.type(screen.getByTestId("login-email"), EMAIL);
  await user.type(screen.getByTestId("login-password"), PASSWORD);
  await user.click(screen.getByTestId("login-submit"));
}

/** The single `navigate({ href })` the hand-off performs. */
async function handOffHref(): Promise<string> {
  await waitFor(() => {
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

/**
 * Replace `window.location` with a plain settable object, so a hand-off that
 * performed a FULL navigation instead of a client-router one is observable.
 */
function stubLocation(): { href: string } {
  const location = { href: "" };
  vi.stubGlobal("location", location);
  return location;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  mocks.buildExchangeTicketUrl.mockImplementation(buildExchangeTicketUrlRule);
});

// ─────────────────────────────────────────────────────────────────────────────
// mfaRequired — 200 { succeeded: false, mfaRequired: true }
// ─────────────────────────────────────────────────────────────────────────────

describe("/login MFA hand-off — mfaRequired", () => {
  beforeEach(() => {
    mocks.login.mockResolvedValue({ succeeded: false, mfaRequired: true });
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
    const user = userEvent.setup();
    expect(isSafeReturnUrlRule(ALLOW_LISTED_ABSOLUTE_RETURN_URL)).toBe(false);
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
    // is exactly why it needs pinning here.
    const location = stubLocation();
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    await handOffHref();
    expect(location.href).toBe("");
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// mfaEnrollmentRequired — the /mfa/enroll arm of the same hand-off
// ─────────────────────────────────────────────────────────────────────────────

describe("/login MFA hand-off — mfaEnrollmentRequired", () => {
  it("threads returnUrl out of the query string into the /mfa/enroll hand-off", async () => {
    mocks.login.mockResolvedValue({ succeeded: false, mfaEnrollmentRequired: true });
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
    mocks.login.mockResolvedValue({ succeeded: false, mfaEnrollmentRequired: true });
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
    mocks.login.mockResolvedValue({
      succeeded: true,
      mfaEnrollmentRequired: true,
      mfaGraceDeadline: deadline,
      signInTicket: "sign-in-ticket-xyz",
    });
    const location = stubLocation();
    const user = userEvent.setup();
    renderRouteAt(loginUrlWithReturnUrl(RETURN_URL));

    await submitCredentials(user);

    await waitFor(() => {
      expect(location.href).toContain("/v1/identity/auth/exchange-ticket");
    });
    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});
