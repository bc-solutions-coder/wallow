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
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { Route as invitationRoute } from "../../../routes/invitation";
import { InvitationScreen } from "./InvitationScreen";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the DOM-matcher convention wallow-web's RTL tests
// established and wallow-auth copies.
expect.extend(matchers);

/**
 * Component spec for the InvitationLanding screen (Wallow-vec7.3.9), ported from
 * the Blazor oracle `api/src/Wallow.Auth/Components/Pages/InvitationLanding.razor`.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `invitation-loading`, `invitation-error`, `invitation-info`,
 * `invitation-expired`, `invitation-accept-error`, `invitation-accept`,
 * `invitation-decline`, `invitation-create-account`, `invitation-sign-in`.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is the only permitted importer
 * of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-across-
 * graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies and
 * NEVER `vi.resetModules()`.
 *
 * ── THE AUTH-STATE SEAM (the gap this file's red phase left open) ────────────
 *
 * This is the ONLY oracle in the phase that injects `AuthenticationStateProvider`
 * (InvitationLanding.razor:7,130) — Blazor Server holds the auth cookie's
 * principal server-side and hands it to the component for free. apps/wallow-auth
 * has no equivalent: its h3 server is a PASSTHROUGH REVERSE PROXY with no
 * session store (`src/lib/auth-server.ts`) and the auth cookie is HttpOnly.
 *
 * `isAuthenticated` is therefore a PROP, and the tests below pin both branches
 * against it. The ROUTE answers it with `auth.getCurrentUser()`
 * (Wallow-vec7.2.4): a same-origin `GET /v1/identity/users/me` whose 200-vs-401
 * IS the answer — `null` for anonymous, and it never throws on a 401. The route
 * tests at the bottom pin all three of its outcomes.
 *
 * And the authenticated branch is a BUG FIX, not a port: `Wallow.Auth` registers
 * no authentication at all, so the oracle's `_isAuthenticated` is always false
 * and its accept/decline branch is dead code — while the API has always
 * supported it (`{token}/accept` is `[Authorize]`d, InvitationsController.cs:
 * 82-84).
 *
 * ── FOUR ORACLE BRANCHES COLLAPSE INTO REJECTIONS AT THIS SEAM ───────────────
 *
 * The oracle reads its API through `AuthApiClient`, which SWALLOWS non-2xx into
 * a sentinel — `VerifyInvitationAsync` returns `null` on any failure
 * (AuthApiClient.cs:297-312), `AcceptInvitationAsync` returns
 * `IsSuccessStatusCode` (AuthApiClient.cs:314-322) — so the oracle distinguishes
 * "the server said no" from "the call blew up" by sentinel-vs-`catch`, and gives
 * each its own copy:
 *
 *   verify:  null  -> "This invitation is not valid or has already been used."
 *            catch -> "Unable to verify this invitation. Please try again later."
 *   accept:  false -> "Unable to accept this invitation. It may have expired or
 *                      already been used."
 *            catch -> "An error occurred while accepting the invitation. Please
 *                      try again."
 *
 * The TS facade's `unwrap()` THROWS on every non-2xx, so both of each pair
 * arrive as one rejected promise and the sentinel-vs-catch fork is gone. What
 * survives is the STATUS: `toWallowError` always populates `.status`, falling
 * back to the raw `Response.status` and then to 500
 * (auth-client.ts:238-270). That is enough to keep all four messages, because
 * the fork maps cleanly onto 4xx-vs-not:
 *
 *   - `GET /v1/identity/invitations/verify/{token}` returns exactly ONE failure,
 *     `NotFound()` (InvitationsController.cs:71-80) — i.e. the oracle's `null`
 *     case IS the 404. Anything else (500, proxy/network) is the `catch` case.
 *   - `POST /v1/identity/invitations/{token}/accept` (InvitationsController.cs:
 *     82-91) throws `EntityNotFoundException` for an unknown/spent token and
 *     rejects an expired one from the aggregate — the "expired or already been
 *     used" copy verbatim — all 4xx. A 5xx is the `catch` case.
 *
 * Keyed on STATUS, not on `code`: unlike the `/v1/identity/auth/*` endpoints
 * (bd memory `mfa-endpoints-mfacontroller-return-business-failures-as-a`), these
 * two send no machine-readable code at all — `NotFound()` is a bare status with
 * no body, so `readCode()` finds nothing and every one of these rejections is
 * `code: "UNKNOWN"` (bd memory `wallow-auth-auth-client-ts-wallowerror-code-
 * loss`). A code-keyed mapping here would collapse all four messages into the
 * generic one. Verified by reading the controller, not assumed.
 *
 * ── THE INERT `email=` PARAMETER (an oracle wart, ported deliberately) ────────
 *
 * `GetRegisterUrl()` (InvitationLanding.razor:196-201) builds
 * `/register?email=…&returnUrl=…`, but `Register.razor` declares only
 * `client_id` and `returnUrl` as `[SupplyParameterFromQuery]` (Register.razor:
 * 179-183) — it never reads `email`. The parameter is dead in the Blazor
 * original, and the ported `/register` route (Wallow-vec7.3.8) likewise reads
 * only `client_id`/`returnUrl`. It is ported anyway — it is the oracle's link
 * contract, it costs nothing, and a Register that prefills the invited address
 * is a plausible follow-up — but it is pinned below as INERT so no one reads
 * these tests as proof the address prefills. It does not.
 *
 * ── WHY NO `isSafeReturnUrl` GUARD ON THIS SCREEN ────────────────────────────
 *
 * Every screen that ACCEPTS a returnUrl guards it (bd memory `returnurl-guard-
 * refuse-dont-sanitize`). This one accepts none: the oracle's only
 * `[SupplyParameterFromQuery]` is `Token`, and the two returnUrls here are BUILT
 * by the screen (`/invitation?token=…`), so they are safe by construction —
 * there is no open-redirect surface to guard. The attacker-controlled part is
 * the token, and the test below pins that it is percent-encoded INTO the
 * returnUrl (so a token like `x&returnUrl=//evil.example` cannot smuggle a
 * second parameter out) and that the result still satisfies the real
 * `isSafeReturnUrl` rule.
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  verifyInvitation: vi.fn(),
  acceptInvitation: vi.fn(),
  getCurrentUser: vi.fn(),
  isSafeReturnUrl: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      verifyInvitation: mocks.verifyInvitation,
      acceptInvitation: mocks.acceptInvitation,
      getCurrentUser: mocks.getCurrentUser,
    },
    oidc: { isSafeReturnUrl: mocks.isSafeReturnUrl },
  }),
}));

const TOKEN = "inv-tok-123";
const EMAIL = "invitee@example.com";
const HOME_HREF = "/";

/** The self-referential returnUrl both unauthenticated links carry back here. */
const SELF_RETURN_URL = `/invitation?token=${TOKEN}`;

/**
 * The real `isSafeReturnUrl` rule (packages/sdk/src/auth-oidc.ts), restated so
 * the returnUrl this screen BUILDS can be checked against the guard's actual
 * semantics rather than against a stub that says "safe".
 */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

/** An `InvitationResponse`, as `InvitationsController.MapToResponse` shapes it. */
function invitation(overrides: Record<string, unknown> = {}) {
  return {
    id: "8f1d4c9e-0000-4000-8000-000000000001",
    email: EMAIL,
    status: "Pending",
    expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
    createdAt: new Date(Date.now() - 86_400_000).toISOString(),
    acceptedByUserId: null,
    ...overrides,
  };
}

/** A `WallowError`-shaped rejection, as the real facade's `unwrap()` throws. */
function wallowErrorShaped(status: number): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status,
    // `UNKNOWN` is not a convenience here — it is what these two endpoints
    // actually produce (see "FOUR ORACLE BRANCHES" above), and pinning it stops
    // an implementer from keying the copy on a code that never arrives.
    code: "UNKNOWN",
    title: "Unknown error",
  });
}

/** A promise this test resolves/rejects by hand, to observe an in-flight state. */
function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void } {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((r) => {
    resolve = r;
  });
  return { promise, resolve };
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
 * `token` is read with `in` rather than `??`: an EXPLICIT `token: undefined` is
 * the tokenless link under test, and `props.token ?? TOKEN` would silently hand
 * it the default instead — testing the opposite of what the caller asked for.
 */
function renderScreen(props: { token?: string; isAuthenticated?: boolean } = {}) {
  return renderWithClient(
    <InvitationScreen
      isAuthenticated={props.isAuthenticated ?? false}
      token={"token" in props ? props.token : TOKEN}
    />,
  );
}

/**
 * Replace `window.location` with a plain settable object so the screen's full
 * navigation is observable. jsdom refuses `vi.spyOn(window.location, "assign")`
 * ("Cannot redefine property"), but `location` itself is a configurable
 * accessor, so `vi.stubGlobal` swaps it wholesale — and `globalThis === window`
 * under jsdom, so the screen's `globalThis.location.href = …` writes here.
 */
/**
 * The `href` of one of the screen's links, PARSED — so the assertions below read
 * the query string the way a browser does (one decode, parameters by name)
 * rather than string-matching an encoded blob, which would pass on
 * `?returnUrl=/x&returnUrl=//evil.example` too. The base is a throwaway: these
 * hrefs are relative, and `URL` needs an origin to resolve one.
 */
async function linkUrl(testId: string): Promise<URL> {
  const link: HTMLElement = await screen.findByTestId(testId);

  return new URL(link.getAttribute("href") ?? "", "https://auth.example");
}

function stubLocation(): { href: string } {
  const location = { href: "" };
  vi.stubGlobal("location", location);
  return location;
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  mocks.verifyInvitation.mockResolvedValue(invitation());
  mocks.acceptInvitation.mockResolvedValue(null);
  // The seam's anonymous answer: `null`, NOT a rejection (Wallow-vec7.2.4). Only
  // the route calls it; the component takes `isAuthenticated` as a prop.
  mocks.getCurrentUser.mockResolvedValue(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("InvitationScreen — missing token", () => {
  it("refuses a link with no token, without calling the API", async () => {
    renderScreen({ token: undefined });

    // The oracle's `IsNullOrWhiteSpace(Token)` guard (InvitationLanding.razor:
    // 118-124): its own message, and it returns BEFORE the verify call.
    expect(await screen.findByTestId("invitation-error")).toHaveTextContent(
      /no invitation token provided/iu,
    );
    expect(mocks.verifyInvitation).not.toHaveBeenCalled();
  });

  it("treats a whitespace-only token as no token", async () => {
    renderScreen({ token: "   " });

    expect(await screen.findByTestId("invitation-error")).toHaveTextContent(
      /no invitation token provided/iu,
    );
    expect(mocks.verifyInvitation).not.toHaveBeenCalled();
  });

  it("offers a way back to sign-in from the error state", async () => {
    renderScreen({ token: undefined });

    // InvitationLanding.razor:32-34 — the error branch is a dead end without it.
    await screen.findByTestId("invitation-error");
    expect(screen.getByRole("link", { name: /back to sign in/iu })).toHaveAttribute(
      "href",
      "/login",
    );
  });
});

describe("InvitationScreen — verifying", () => {
  it("verifies the token from the link", async () => {
    renderScreen({ token: TOKEN });

    await waitFor(() => {
      expect(mocks.verifyInvitation).toHaveBeenCalledWith(TOKEN);
    });
  });

  it("shows the loading state while the verify is in flight, then the invitation", async () => {
    const pending = deferred<unknown>();
    mocks.verifyInvitation.mockReturnValue(pending.promise);

    renderScreen();

    // Anchored: the loading state must be REPLACED, not merely present — a
    // screen that renders the spinner forever would pass the first assertion.
    expect(await screen.findByTestId("invitation-loading")).toBeInTheDocument();
    expect(screen.queryByTestId("invitation-info")).toBeNull();

    pending.resolve(invitation());

    expect(await screen.findByTestId("invitation-info")).toBeInTheDocument();
    expect(screen.queryByTestId("invitation-loading")).toBeNull();
  });

  it("names the invited address once verified", async () => {
    renderScreen();

    // InvitationLanding.razor:41 — the address is the whole point of the info
    // block: it tells the user WHICH identity the invitation is for.
    expect(await screen.findByTestId("invitation-info")).toHaveTextContent(EMAIL);
  });

  it("reports a 404 as an invalid or already-used invitation", async () => {
    mocks.verifyInvitation.mockRejectedValue(wallowErrorShaped(404));

    renderScreen();

    expect(await screen.findByTestId("invitation-error")).toHaveTextContent(
      /not valid or has already been used/iu,
    );
    expect(screen.queryByTestId("invitation-info")).toBeNull();
  });

  it("reports a server failure as a transient problem, not a bad invitation", async () => {
    mocks.verifyInvitation.mockRejectedValue(wallowErrorShaped(500));

    renderScreen();

    // The oracle's `catch` branch. Distinct copy from the 404: telling a user
    // their invitation is spent when the server merely fell over sends them to
    // an administrator for a replacement they do not need.
    expect(await screen.findByTestId("invitation-error")).toHaveTextContent(
      /unable to verify this invitation/iu,
    );
  });
});

describe("InvitationScreen — expired invitation", () => {
  it("shows the expired notice when the server says the status is Expired", async () => {
    mocks.verifyInvitation.mockResolvedValue(invitation({ status: "Expired" }));

    renderScreen({ isAuthenticated: true });

    expect(await screen.findByTestId("invitation-expired")).toHaveTextContent(/has expired/iu);
    expect(screen.getByTestId("invitation-info")).toBeInTheDocument();
  });

  it("shows the expired notice when expiresAt has passed, whatever the status says", async () => {
    // InvitationLanding.razor:147 is an OR: a `Pending` row whose `ExpiresAt` is
    // past is expired too. The status only flips to `Expired` when the
    // `CleanupExpiredAsync` sweep gets to it (InvitationService.cs:71-89), so
    // between expiry and the sweep this is the ONLY branch that catches it.
    const oneSecondAgo: string = new Date(Date.now() - 1_000).toISOString();
    mocks.verifyInvitation.mockResolvedValue(
      invitation({ status: "Pending", expiresAt: oneSecondAgo }),
    );

    renderScreen({ isAuthenticated: true });

    expect(await screen.findByTestId("invitation-expired")).toBeInTheDocument();
  });

  it("offers no way to accept an expired invitation, even when signed in", async () => {
    mocks.verifyInvitation.mockResolvedValue(invitation({ status: "Expired" }));

    renderScreen({ isAuthenticated: true });

    // Anchored on the expired notice: without it, `queryByTestId(...).toBeNull()`
    // would pass against a screen that rendered nothing at all.
    expect(await screen.findByTestId("invitation-expired")).toBeInTheDocument();
    expect(screen.queryByTestId("invitation-accept")).toBeNull();
    expect(screen.queryByTestId("invitation-decline")).toBeNull();
  });

  it("offers no sign-in path for an expired invitation either", async () => {
    mocks.verifyInvitation.mockResolvedValue(invitation({ status: "Expired" }));

    renderScreen({ isAuthenticated: false });

    // The expiry check precedes the auth branch (InvitationLanding.razor:46-54):
    // signing in to accept a dead invitation is a wasted round trip.
    expect(await screen.findByTestId("invitation-expired")).toBeInTheDocument();
    expect(screen.queryByTestId("invitation-create-account")).toBeNull();
    expect(screen.queryByTestId("invitation-sign-in")).toBeNull();
  });
});

describe("InvitationScreen — authenticated branch", () => {
  it("asks the signed-in user to accept or decline", async () => {
    renderScreen({ isAuthenticated: true });

    expect(await screen.findByTestId("invitation-accept")).toBeInTheDocument();
    expect(screen.getByTestId("invitation-decline")).toBeInTheDocument();
    // The account links belong to the OTHER branch; both showing would offer a
    // signed-in user a "create account" they do not need.
    expect(screen.queryByTestId("invitation-create-account")).toBeNull();
    expect(screen.queryByTestId("invitation-sign-in")).toBeNull();
  });

  it("declining just leaves, without touching the invitation", async () => {
    renderScreen({ isAuthenticated: true });

    // InvitationLanding.razor:75-81 — `Href="/"`, no call. "No thanks" does NOT
    // revoke the invitation; it stays open for a later visit.
    expect(await screen.findByTestId("invitation-decline")).toHaveAttribute("href", HOME_HREF);
    expect(mocks.acceptInvitation).not.toHaveBeenCalled();
  });

  it("accepts the invitation with the link's token and lands the user home", async () => {
    const user = userEvent.setup();
    const location = stubLocation();

    renderScreen({ isAuthenticated: true });
    await user.click(await screen.findByTestId("invitation-accept"));

    await waitFor(() => {
      expect(mocks.acceptInvitation).toHaveBeenCalledWith(TOKEN);
    });

    // A FULL navigation, not `navigate()` — the oracle's
    // `NavigateTo("/", forceLoad: true)` (InvitationLanding.razor:179). The
    // reload is load-bearing: accepting the invitation changes the user's tenant
    // membership, and a client-side transition would carry the pre-acceptance
    // session state into the destination.
    await waitFor(() => {
      expect(location.href).toBe(HOME_HREF);
    });
  });

  it("disables accept and makes decline inert while an accept is in flight", async () => {
    const user = userEvent.setup();
    const pending = deferred<unknown>();
    mocks.acceptInvitation.mockReturnValue(pending.promise);
    stubLocation();

    renderScreen({ isAuthenticated: true });
    await user.click(await screen.findByTestId("invitation-accept"));

    // The oracle's `_isSubmitting` guard (InvitationLanding.razor:164,169-171):
    // a second accept is a second POST against a one-shot token.
    await waitFor(() => {
      expect(screen.getByTestId("invitation-accept")).toBeDisabled();
    });

    // Decline is a LINK, and a link cannot carry `disabled`: jest-dom's
    // `toBeDisabled` only recognises form and custom elements, and `aria-disabled`
    // alone still navigates on click. Losing the `href` is what actually makes it
    // inert — and leaving mid-POST would hide the outcome of a request that is
    // changing the user's tenant membership.
    const decline: HTMLElement = screen.getByTestId("invitation-decline");
    expect(decline).not.toHaveAttribute("href");
    expect(decline).toHaveAttribute("aria-disabled", "true");

    pending.resolve(null);
  });

  it("reports a rejected accept as expired or already used, keeping the buttons alive", async () => {
    const user = userEvent.setup();
    mocks.acceptInvitation.mockRejectedValue(wallowErrorShaped(404));

    renderScreen({ isAuthenticated: true });
    await user.click(await screen.findByTestId("invitation-accept"));

    expect(await screen.findByTestId("invitation-accept-error")).toHaveTextContent(
      /expired or already been used/iu,
    );
    // `invitation-accept-error`, NOT `invitation-error`: the invitation verified
    // fine, so the info block and the buttons stay (InvitationLanding.razor:
    // 58-65 renders inside the authenticated branch, above the buttons).
    expect(screen.getByTestId("invitation-info")).toBeInTheDocument();
    expect(screen.getByTestId("invitation-accept")).toBeEnabled();
  });

  it("reports a server failure on accept as a retryable error", async () => {
    const user = userEvent.setup();
    mocks.acceptInvitation.mockRejectedValue(wallowErrorShaped(500));

    renderScreen({ isAuthenticated: true });
    await user.click(await screen.findByTestId("invitation-accept"));

    expect(await screen.findByTestId("invitation-accept-error")).toHaveTextContent(
      /an error occurred while accepting the invitation/iu,
    );
  });

  it("clears a previous accept error when the user tries again", async () => {
    const user = userEvent.setup();
    mocks.acceptInvitation.mockRejectedValueOnce(wallowErrorShaped(500));
    stubLocation();

    renderScreen({ isAuthenticated: true });
    await user.click(await screen.findByTestId("invitation-accept"));
    await screen.findByTestId("invitation-accept-error");

    // The oracle's `_acceptError = null` on re-entry (InvitationLanding.razor:
    // 170): a stale failure banner above a succeeded accept is a lie.
    await user.click(screen.getByTestId("invitation-accept"));

    await waitFor(() => {
      expect(screen.queryByTestId("invitation-accept-error")).toBeNull();
    });
    expect(mocks.acceptInvitation).toHaveBeenCalledTimes(2);
  });
});

describe("InvitationScreen — unauthenticated branch", () => {
  it("offers the anonymous visitor an account to create or a session to sign into", async () => {
    renderScreen({ isAuthenticated: false });

    expect(await screen.findByTestId("invitation-create-account")).toBeInTheDocument();
    expect(screen.getByTestId("invitation-sign-in")).toBeInTheDocument();
    // Accepting needs a `[Authorize]`d POST (InvitationsController.cs:82-83);
    // offering it to an anonymous visitor buys them a 401.
    expect(screen.queryByTestId("invitation-accept")).toBeNull();
  });

  it("sends the visitor to register with the invited address and a way back", async () => {
    renderScreen({ isAuthenticated: false });

    const url: URL = await linkUrl("invitation-create-account");

    expect(url.pathname).toBe("/register");
    // INERT — `/register` does not read `email` (see the header note). Pinned so
    // the oracle's link shape survives verbatim, not as a claim that it prefills.
    expect(url.searchParams.get("email")).toBe(EMAIL);
    expect(url.searchParams.get("returnUrl")).toBe(SELF_RETURN_URL);
  });

  it("sends the visitor to sign in with a way back", async () => {
    renderScreen({ isAuthenticated: false });

    const url: URL = await linkUrl("invitation-sign-in");

    expect(url.pathname).toBe("/login");
    // The round trip is the point: sign in, come back HERE, and the screen then
    // renders the authenticated branch with the accept button.
    expect(url.searchParams.get("returnUrl")).toBe(SELF_RETURN_URL);
  });

  it("encodes a hostile token into the returnUrl instead of letting it smuggle parameters", async () => {
    const hostileToken = "x&returnUrl=//evil.example";
    mocks.verifyInvitation.mockResolvedValue(invitation());

    renderScreen({ isAuthenticated: false, token: hostileToken });

    const url: URL = await linkUrl("invitation-sign-in");

    // ONE returnUrl, and it is ours: the token is a VALUE inside it, not a
    // second parameter appended to the query string.
    expect(url.searchParams.getAll("returnUrl")).toHaveLength(1);
    expect(url.searchParams.get("returnUrl")).toBe(`/invitation?token=${hostileToken}`);
    // And what we built still satisfies the real guard — this screen accepts no
    // returnUrl of its own, so safety is by construction, not by check.
    expect(isSafeReturnUrlRule(url.searchParams.get("returnUrl"))).toBe(true);
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — "the token is
 * read from the query string" — only exists once a URL is parsed by a router.
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`,
 * and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    invitationRoute.update({
      id: "/invitation",
      path: "/invitation",
      getParentRoute: () => rootRoute,
    }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/invitation route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path is the contract — `/invitation`,
    // singular — and is not this task's to change.
    renderRouteAt(`/invitation?token=${TOKEN}`);

    expect(await screen.findByTestId("invitation-info")).toBeInTheDocument();
    expect(screen.queryByTestId("route-placeholder")).toBeNull();
  });

  it("verifies the token it read from the query string", async () => {
    renderRouteAt(`/invitation?token=${TOKEN}`);

    await waitFor(() => {
      expect(mocks.verifyInvitation).toHaveBeenCalledWith(TOKEN);
    });
  });

  it("percent-decodes the token before verifying it", async () => {
    const rawToken = "a b+c/d";
    renderRouteAt(`/invitation?token=${encodeURIComponent(rawToken)}`);

    await waitFor(() => {
      expect(mocks.verifyInvitation).toHaveBeenCalledWith(rawToken);
    });
  });

  it("treats a link with no token as no token", async () => {
    renderRouteAt("/invitation");

    expect(await screen.findByTestId("invitation-error")).toHaveTextContent(
      /no invitation token provided/iu,
    );
    expect(mocks.verifyInvitation).not.toHaveBeenCalled();
  });

  it("asks the API who the visitor is, and shows the accept button to a signed-in one", async () => {
    // The route's answer to the oracle's `AuthStateProvider` (see the header):
    // a resolved user IS the session. The auth cookie is HttpOnly, so the 200 is
    // the only thing the browser can observe about it.
    mocks.getCurrentUser.mockResolvedValue({ id: "u-1", email: EMAIL });

    renderRouteAt(`/invitation?token=${TOKEN}`);

    expect(await screen.findByTestId("invitation-accept")).toBeInTheDocument();
    expect(screen.queryByTestId("invitation-sign-in")).toBeNull();
  });

  it("treats the seam's anonymous answer as anonymous", async () => {
    // `getCurrentUser` maps 401 to `null` WITHOUT throwing (Wallow-vec7.2.4) —
    // anonymous is an expected answer, not a failure.
    mocks.getCurrentUser.mockResolvedValue(null);

    renderRouteAt(`/invitation?token=${TOKEN}`);

    expect(await screen.findByTestId("invitation-sign-in")).toBeInTheDocument();
    expect(screen.queryByTestId("invitation-accept")).toBeNull();
  });

  it("treats a failed auth probe as anonymous rather than crashing the invitation", async () => {
    // The oracle's `catch { _isAuthenticated = false; }` (InvitationLanding.razor:
    // 133-136). A 500 from `/users/me` is not evidence of a session, and the
    // less-privileged branch is the safe read: it offers a sign-in link, where the
    // other offers an accept button whose `[Authorize]`d POST would 401.
    mocks.getCurrentUser.mockRejectedValue(wallowErrorShaped(500));

    renderRouteAt(`/invitation?token=${TOKEN}`);

    expect(await screen.findByTestId("invitation-sign-in")).toBeInTheDocument();
    expect(screen.queryByTestId("invitation-accept")).toBeNull();
    // And the invitation itself still verifies — the probe is an affordance, not
    // a gate on the page's content.
    expect(screen.getByTestId("invitation-info")).toBeInTheDocument();
  });

  it("treats a non-string token as absent rather than verifying a boolean", async () => {
    // TanStack's search parsing JSON-parses scalars, so `?token=true` arrives as
    // the BOOLEAN `true`, not the string "true" (bd memory on validateSearch).
    // Handing that to `verifyInvitation(token: string)` would put `true` in a URL
    // path segment.
    renderRouteAt("/invitation?token=true");

    expect(await screen.findByTestId("invitation-error")).toHaveTextContent(
      /no invitation token provided/iu,
    );
    expect(mocks.verifyInvitation).not.toHaveBeenCalled();
  });
});
