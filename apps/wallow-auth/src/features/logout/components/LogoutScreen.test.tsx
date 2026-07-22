import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as logoutRoute } from "../../../routes/logout";
import { LogoutScreen } from "./LogoutScreen";

/**
 * Component spec for the Logout screen (Wallow-vec7.3.5).
 *
 * ONE ROUTE, TWO PHASES. `/logout` is not two screens: the oracle drives both
 * off the `signed_out` query parameter on a single `@page "/logout"`. The
 * CONFIRM step asks "are you sure" and hands off to `/connect/logout`; the
 * SIGNED-OUT LANDING is where OpenIddict sends the browser BACK after the
 * end-session request completes, and it offers a way back to the relying party.
 * The two phases share `logout-confirm-heading` — same testid, different text —
 * which is the oracle's choice (scout inventory on Wallow-vec7.3) and is
 * preserved verbatim rather than "fixed" into two testids.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is the only permitted importer
 * of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-across-
 * graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies and
 * NEVER `vi.resetModules()`.
 *
 * ── THE ORIGIN TRAP (the load-bearing port decision on this screen) ───────────
 *
 * The oracle builds its logout URL against an absolute API origin:
 *
 *     private string ApiBaseUrl => Configuration["ApiBaseUrl"]
 *         ?? throw new InvalidOperationException("ApiBaseUrl must be configured");
 *     string url = $"{ApiBaseUrl}/connect/logout";
 *
 * **That prepend must NOT be ported**, for exactly the reasons established on
 * `/consent` (Wallow-vec7.3.4). apps/wallow-auth's h3 server
 * (`src/lib/auth-server.ts`) is a PASSTHROUGH REVERSE PROXY mounting
 * `/connect/**` and `/v1/**` at the ROOT — the same fact behind the facade's
 * `baseUrl: '/'` (bd memory `wallow-auth-same-origin-baseurl-apps-wallow-auth`).
 * This origin DOES host `/connect/logout`, so the origin argument is `""`.
 *
 * This one is worse than cosmetic HERE specifically, because `/connect/logout`
 * is a COOKIE-READING endpoint: it must see the auth cookie to know whose
 * session to end. Sending the browser cross-origin drops that `SameSite` cookie,
 * and the end-session request then either no-ops or bounces the user through an
 * unnecessary re-prompt — a sign-out button that does not sign you out. It would
 * also reintroduce an `ApiBaseUrl` knob this app deliberately lacks: its only API
 * URL, `WALLOW_API_INTERNAL_URL`, is a SERVER-side internal address
 * (`http://wallow-api` under Aspire) the browser cannot resolve at all — and the
 * oracle's getter THROWS when unset, so there is no silent-default escape.
 *
 * `buildConnectLogoutUrl` (Wallow-vec7.2.2) already ports the rest of the
 * `LogoutUrl` getter (the `IsNullOrEmpty` omission of the parameter and the
 * `Uri.EscapeDataString` encoding) under its own tests. These tests pin what the
 * SCREEN owes the builder — the `""` origin above all — rather than re-deriving
 * the builder's string algebra.
 *
 * ── WHY NO isSafeReturnUrl GUARD ON THIS SCREEN ──────────────────────────────
 *
 * Every other screen in this phase guards its returnUrl with `isSafeReturnUrl`.
 * This one must NOT, and the difference is not an oversight to correct:
 * `post_logout_redirect_uri` is an ABSOLUTE URI by definition — the relying
 * party's own origin, which is not this one — so the relative-path guard
 * (`starts with a single '/'`) would reject every legitimate caller. That is why
 * `buildConnectLogoutUrl` documents itself as deliberately unguarded.
 *
 * The open-redirect defence here is the SERVER's instead, and it is stronger: it
 * is `auth.validateRedirectUri`, an allow-list check against the client's
 * REGISTERED post-logout URIs. The tests below pin that the screen never renders
 * an unvalidated URI as a link — that call is the only thing standing between an
 * attacker-crafted `?signed_out=true&post_logout_redirect_uri=https://evil.test`
 * and a "Return to application" button pointing at it.
 *
 * ── `validateRedirectUri` RETURNS `Promise<unknown>`, NOT `Promise<boolean>` ──
 *
 * The C# client collapses the call to a bool (AuthApiClient.cs:93-108):
 *
 *     if (!response.IsSuccessStatusCode) { return false; }
 *     RedirectUriValidationResponse? body = await …ReadFromJsonAsync…;
 *     return body?.Allowed == true;
 *
 * The TS facade does NOT: `validateRedirectUri: (uri: string) => Promise<unknown>`
 * (auth-client.ts:149), because the OpenAPI spec declares the 200 with no schema
 * (openapi/v1.json:1005-1009) — the endpoint returns an anonymous
 * `Ok(new { allowed = … })` (AccountController.cs:601-607), so there is nothing
 * to generate a type from. Two consequences the screen owns, both pinned below:
 *
 *   1. The `{ allowed: boolean }` narrowing happens at THIS boundary. The scout's
 *      instruction for the untyped facade methods is exactly this: screens define
 *      their own local interface and narrow at their own edge. `body?.Allowed ==
 *      true` is a STRICT test — anything that is not literally `allowed: true`
 *      (missing key, `"true"` the string, a non-object body) is NOT allowed.
 *   2. The C# `!IsSuccessStatusCode → false` arm arrives here as a REJECTION,
 *      because the facade's `unwrap()` throws on non-2xx rather than returning
 *      null. A rejected validation is a DENIED validation, not an error to
 *      surface: the oracle has no error state on this screen at all (no
 *      `logout-error` testid exists), and it must not fail OPEN. The
 *      `WallowError` code-loss gotcha (bd memory `wallow-auth-auth-client-ts-
 *      wallowerror-code-loss`) costs this screen nothing — it never reads `.code`.
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  validateRedirectUri: vi.fn(),
  buildConnectLogoutUrl: vi.fn(),
  isSafeReturnUrl: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: { validateRedirectUri: mocks.validateRedirectUri },
    oidc: {
      buildConnectLogoutUrl: mocks.buildConnectLogoutUrl,
      isSafeReturnUrl: mocks.isSafeReturnUrl,
    },
  }),
}));

/** A registered post-logout URI: absolute, and another origin than this one. */
const REDIRECT_URI = "https://app.wallow.test/signed-out";

/**
 * The real `buildConnectLogoutUrl` behaviour (packages/sdk/src/auth-oidc.ts),
 * restated here because the screen reaches it through the mocked facade.
 * Restating the rule rather than stubbing a fixed string keeps these tests
 * pinning the screen's USE of the builder against the builder's actual
 * semantics — in particular that it is the ORIGIN ARGUMENT, not the builder,
 * that decides whether the sign-out handoff stays same-origin.
 *
 * The builder's own string algebra is pinned by its tests in
 * `packages/sdk/src/auth-oidc.test.ts`; it is reproduced only so this screen
 * meets a builder that behaves like the real one.
 */
function buildConnectLogoutUrlRule(origin: string, postLogoutRedirectUri?: string | null): string {
  const url: string = `${origin.replace(/\/+$/u, "")}/connect/logout`;

  if (
    postLogoutRedirectUri === null ||
    postLogoutRedirectUri === undefined ||
    postLogoutRedirectUri === ""
  ) {
    return url;
  }

  return `${url}?post_logout_redirect_uri=${encodeURIComponent(postLogoutRedirectUri)}`;
}

/** The `{ allowed }` body the API's anonymous `Ok(new { allowed = … })` sends. */
function allowedBody(allowed: boolean): unknown {
  return { allowed };
}

/** A WallowError-shaped rejection, as the real facade's `unwrap()` throws. */
function wallowErrorShaped(status: number): Error {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status,
    code: "UNKNOWN",
    title: "Unknown error",
  });
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.buildConnectLogoutUrl.mockImplementation(buildConnectLogoutUrlRule);
  mocks.validateRedirectUri.mockResolvedValue(allowedBody(true));
});

describe("LogoutScreen — the confirm step", () => {
  it("heads the card 'Sign out'", async () => {
    await renderWithClient(<LogoutScreen />);

    // The oracle's `else` arm of `@if (SignedOut == "true")`. Same testid as the
    // landing, different text — that shared testid is why the TEXT is the
    // assertion that tells the two phases apart.
    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
  });

  it("asks for confirmation before signing the user out", async () => {
    await renderWithClient(<LogoutScreen />);

    // The oracle's prompt. A sign-out that fires on navigation rather than on a
    // click is a CSRF sink: `<img src="/logout">` would end the session.
    await expect.element(page.getByText("Are you sure you want to sign out?")).toBeInTheDocument();
  });

  it("points the sign-out button at this origin's /connect/logout", async () => {
    await renderWithClient(<LogoutScreen />);

    // THE ORIGIN TRAP. See the header note: the h3 proxy serves /connect/** at
    // the root, so the handoff must stay same-origin or the SameSite auth cookie
    // never reaches the endpoint that needs it to know whose session to end.
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute("href", "/connect/logout");
  });

  it("builds that URL against the empty origin, not a configured API base URL", async () => {
    await renderWithClient(<LogoutScreen />);

    // Asserted on the ARGUMENT as well as the result, so that porting the
    // oracle's `$"{ApiBaseUrl}/connect/logout"` fails loudly here rather than
    // silently in production — the two are indistinguishable from the rendered
    // href alone if someone hardcodes a base that happens to be "".
    expect(mocks.buildConnectLogoutUrl.mock.calls[0]?.[0]).toBe("");
  });

  it("carries post_logout_redirect_uri through to the logout URL", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // The oracle's `if (!IsNullOrEmpty(PostLogoutRedirectUri))` arm. OpenIddict
    // needs this on the END-SESSION request to know where to send the browser
    // back to; dropping it here strands the user on the landing page.
    expect(mocks.buildConnectLogoutUrl.mock.calls[0]?.[1]).toBe(REDIRECT_URI);
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute(
        "href",
        `/connect/logout?post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`,
      );
  });

  it("omits the parameter entirely when no redirect URI was supplied", async () => {
    await renderWithClient(<LogoutScreen />);

    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute("href", "/connect/logout");
  });

  it("treats an empty post_logout_redirect_uri as absent", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri="" />);

    // `IsNullOrEmpty` parity: `?post_logout_redirect_uri=` is a malformed link,
    // not a request to return to the empty string.
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute("href", "/connect/logout");
  });

  it("does not validate the redirect URI on the confirm step", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // The oracle validates ONLY under `SignedOut == "true"`. Validating here
    // would be wasted — the API re-validates the parameter on the end-session
    // request itself — and would leak a probe on every render of the prompt.
    // Anchored on the prompt actually rendering, so this cannot pass by the
    // screen simply not being the confirm step.
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
    expect(mocks.validateRedirectUri).not.toHaveBeenCalled();
  });

  it("does not apply the relative-path returnUrl guard to an absolute URI", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // See the header note: `isSafeReturnUrl` demands a single leading '/', so
    // applying it to a post-logout URI would reject every legitimate relying
    // party. The server-side allow-list is the defence on this screen.
    expect(mocks.isSafeReturnUrl).not.toHaveBeenCalled();
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
  });

  it("does not render the signed-out copy", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    // Anchored on the confirm step being present: the two arms are mutually
    // exclusive in the oracle's `@if/else`, and telling a user who has NOT signed
    // out that they have is the failure this pins.
    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
    expect(page.getByText("You have been successfully signed out.").query()).toBeNull();
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("renders the sign-out control as a link, not a button", async () => {
    await renderWithClient(<LogoutScreen />);

    // The oracle's `<a href="@LogoutUrl">`. This has to be a real navigation:
    // /connect/logout is served by the h3 proxy and is not in the client-side
    // route tree, so a router-driven control would 404 in-app.
    expect(page.getByTestId("logout-confirm-button").element().tagName).toBe("A");
  });
});

describe("LogoutScreen — signed_out is an exact string match", () => {
  it.each([
    ["false", "the literal string false"],
    ["TRUE", "a differently-cased true"],
    ["True", "a pascal-cased true"],
    ["1", "a truthy-looking 1"],
    ["", "an empty value"],
    ["yes", "an unrelated value"],
  ])("shows the confirm step for signed_out=%s (%s)", async (signedOut: string) => {
    await renderWithClient(<LogoutScreen signedOut={signedOut} />);

    // The oracle compares `SignedOut == "true"` — an ordinal string equality, not
    // a boolean parse. This matters in the safe direction: anything else falls to
    // the CONFIRM step, so a mangled link asks again rather than telling a
    // still-signed-in user they are signed out.
    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
  });

  it("shows the confirm step when signed_out is absent", async () => {
    await renderWithClient(<LogoutScreen />);

    await expect.element(page.getByTestId("logout-confirm-heading")).toHaveTextContent("Sign out");
  });

  it("shows the landing only for exactly 'true'", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(page.getByTestId("logout-confirm-button").query()).toBeNull();
  });
});

describe("LogoutScreen — the signed-out landing", () => {
  it("heads the card 'Signed out'", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
  });

  it("confirms the sign-out succeeded", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    await expect
      .element(page.getByText("You have been successfully signed out."))
      .toBeInTheDocument();
  });

  it("does not offer to sign the user out again", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The session is already gone; the oracle's `@if/else` makes these two arms
    // mutually exclusive, and a second sign-out control here would be a dead end.
    // Anchored on the landing actually rendering.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(page.getByTestId("logout-confirm-button").query()).toBeNull();
    expect(page.getByText("Are you sure you want to sign out?").query()).toBeNull();
  });

  it("validates the post-logout redirect URI against the server", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The oracle's `await AuthApiClient.ValidateRedirectUriAsync(PostLogoutRedirectUri)`.
    // This is the acceptance criterion's "redirect-uri validation branch".
    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalledWith(REDIRECT_URI);
    });
  });

  it("offers a link back to the application once the URI is allowed", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The oracle's `@if (_isRedirectUriValid)` block. Note the href is the RAW
    // post-logout URI, not the /connect/logout URL — this is the return trip.
    await expect
      .element(page.getByTestId("logout-return-link"))
      .toHaveAttribute("href", REDIRECT_URI);
    await expect
      .element(page.getByTestId("logout-return-link"))
      .toHaveTextContent("Return to application");
  });

  it("does not link to a URI the server refused", async () => {
    mocks.validateRedirectUri.mockResolvedValue(allowedBody(false));

    await renderWithClient(
      <LogoutScreen signedOut="true" postLogoutRedirectUri="https://evil.test/collect" />,
    );

    // THE OPEN-REDIRECT DEFENCE. `signed_out` and `post_logout_redirect_uri` are
    // both attacker-suppliable — this landing renders for anyone who types the
    // URL, with no proof a sign-out ever happened. `_isRedirectUriValid` starting
    // FALSE and only the server being able to flip it is what keeps this page
    // from laundering a Wallow-branded link to an arbitrary origin.
    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalled();
    });
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("never puts an unvalidated URI in the DOM, even briefly", async () => {
    mocks.validateRedirectUri.mockReturnValue(new Promise(() => {}));

    const { container } = await renderWithClient(
      <LogoutScreen signedOut="true" postLogoutRedirectUri="https://evil.test/collect" />,
    );

    // In flight, `_isRedirectUriValid` is still false. A link rendered
    // optimistically and retracted on the answer is a link a fast user can click
    // — the whole point of the check is that it gates FIRST. Anchored on the
    // landing having rendered, so an empty screen cannot satisfy this.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
    expect(container.innerHTML).not.toContain("evil.test");
  });

  it("still confirms the sign-out while the validation is in flight", async () => {
    mocks.validateRedirectUri.mockReturnValue(new Promise(() => {}));

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The oracle renders the heading and copy unconditionally and only the LINK
    // behind `_isRedirectUriValid` — the user is told the sign-out worked without
    // waiting on a check that has nothing to do with it.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    await expect
      .element(page.getByText("You have been successfully signed out."))
      .toBeInTheDocument();
  });

  it("skips validation entirely when no redirect URI was supplied", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" />);

    // The oracle's `&& !string.IsNullOrEmpty(PostLogoutRedirectUri)`.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(mocks.validateRedirectUri).not.toHaveBeenCalled();
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("skips validation when the redirect URI is empty", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri="" />);

    // `IsNullOrEmpty` parity again — an empty URI is a malformed link, not a
    // destination to ask the server about.
    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    expect(mocks.validateRedirectUri).not.toHaveBeenCalled();
  });
});

describe("LogoutScreen — the validation response is untyped and must fail closed", () => {
  it("links when the body is exactly { allowed: true }", async () => {
    mocks.validateRedirectUri.mockResolvedValue(allowedBody(true));

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    await expect.element(page.getByTestId("logout-return-link")).toBeInTheDocument();
  });

  it.each<[unknown, string]>([
    [{ allowed: "true" }, "the STRING 'true' rather than the boolean"],
    [{ allowed: 1 }, "a truthy non-boolean"],
    [{ allowed: null }, "an explicit null"],
    [{}, "a body missing the key"],
    [null, "a null body"],
    [undefined, "an undefined body"],
    ["allowed", "a bare string body"],
    [true, "a bare boolean body"],
    [{ Allowed: true }, "the C# PascalCase key the wire does not use"],
  ])("refuses to link when the body is %j (%s)", async (body: unknown) => {
    mocks.validateRedirectUri.mockResolvedValue(body);

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // `body?.Allowed == true` is a STRICT comparison in the oracle, and the
    // facade hands this screen an `unknown` (the spec declares the 200 with no
    // schema), so the narrowing is the screen's own. Every shape that is not
    // literally `allowed: true` is NOT allowed — a screen that leaned on JS
    // truthiness would link on `allowed: "false"`, which is a string and truthy.
    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalled();
    });
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it.each([
    [400, "a bad request"],
    [401, "an unauthenticated call"],
    [404, "an unregistered client"],
    [500, "a server fault"],
  ])("refuses to link when validation rejects with %i (%s)", async (status: number) => {
    mocks.validateRedirectUri.mockRejectedValue(wallowErrorShaped(status));

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The C# `!IsSuccessStatusCode → false` arm, which arrives through this seam
    // as a REJECTION because `unwrap()` throws on non-2xx. FAILING CLOSED is the
    // whole point: an unreachable validator must not become a reason to trust the
    // attacker's URI.
    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalled();
    });
    expect(page.getByTestId("logout-return-link").query()).toBeNull();
  });

  it("surfaces no error state when validation fails", async () => {
    mocks.validateRedirectUri.mockRejectedValue(wallowErrorShaped(500));

    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    // The oracle has NO error state on this screen — the scout's inventory has no
    // `logout-error` testid because there is no element to give one to. A failed
    // validation is not the user's problem: they ARE signed out, which is what
    // they came for. Only the convenience link is lost.
    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalled();
    });
    expect(page.getByTestId("logout-error").query()).toBeNull();
    await expect
      .element(page.getByText("You have been successfully signed out."))
      .toBeInTheDocument();
  });
});

describe("LogoutScreen — the footer", () => {
  it.each([
    ["the confirm step", undefined],
    ["the signed-out landing", "true"],
  ])("links back to sign in from %s", async (_phase: string, signedOut: string | undefined) => {
    await renderWithClient(<LogoutScreen signedOut={signedOut} />);

    // The oracle's `BbCardFooter` renders on BOTH arms, outside the @if.
    await expect.element(page.getByTestId("logout-back-link")).toHaveAttribute("href", "/login");
    await expect.element(page.getByTestId("logout-back-link")).toHaveTextContent("Back to sign in");
  });
});

/**
 * ── TESTID INVENTORY ─────────────────────────────────────────────────────────
 *
 * The oracle gives only TWO testids (scout inventory on Wallow-vec7.3):
 * `logout-confirm-heading` and `logout-confirm-button`. Both are reused verbatim,
 * including the heading's reuse across both phases.
 *
 * Two elements in the oracle have NO testid, and both are invented here under the
 * `{page}-{element}` kebab convention, exactly as the scout directed for the
 * Login external-provider gap:
 *
 *   • `logout-return-link` — the "Return to application" anchor. This is the one
 *     element on the screen gated by a security check, so leaving it unaddressable
 *     would make the open-redirect defence untestable from E2E, which is where it
 *     most needs proving.
 *   • `logout-back-link` — the footer's "Back to sign in", named to match the
 *     `error-back-link` the Error page already established.
 */
describe("LogoutScreen — testids", () => {
  it("exposes the oracle's testids on the confirm step", async () => {
    await renderWithClient(<LogoutScreen postLogoutRedirectUri={REDIRECT_URI} />);

    await expect.element(page.getByTestId("logout-confirm-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-confirm-button")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-back-link")).toBeInTheDocument();
  });

  it("exposes the oracle's testids on the signed-out landing", async () => {
    await renderWithClient(<LogoutScreen signedOut="true" postLogoutRedirectUri={REDIRECT_URI} />);

    await expect.element(page.getByTestId("logout-confirm-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-return-link")).toBeInTheDocument();
    await expect.element(page.getByTestId("logout-back-link")).toBeInTheDocument();
  });
});

/**
 * Mounts the REAL route through a memory router. Bare-rendering a route
 * component that reads search params throws (`useSearch` needs a router in
 * context) — the harness `ResetPasswordForm.test.tsx` established and
 * `ConsentScreen.test.tsx` followed.
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    logoutRoute.update({ id: "/logout", path: "/logout", getParentRoute: () => rootRoute } as any),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/logout route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path itself is the contract and is
    // NOT this task's to change (router.tsx is off-limits).
    await renderRouteAt("/logout");

    await expect.element(page.getByTestId("logout-confirm-heading")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("threads post_logout_redirect_uri from the query string into the logout URL", async () => {
    await renderRouteAt(`/logout?post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`);

    // Must actually REACH the screen, not merely parse: a route that dropped it
    // would build a bare /connect/logout and strand the user after sign-out.
    await expect
      .element(page.getByTestId("logout-confirm-button"))
      .toHaveAttribute(
        "href",
        `/connect/logout?post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`,
      );
  });

  it("threads signed_out from the query string into the phase choice", async () => {
    await renderRouteAt(
      `/logout?signed_out=true&post_logout_redirect_uri=${encodeURIComponent(REDIRECT_URI)}`,
    );

    await expect
      .element(page.getByTestId("logout-confirm-heading"))
      .toHaveTextContent("Signed out");
    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalledWith(REDIRECT_URI);
    });
  });

  it("reads both parameters off the query string under their wire names", () => {
    // The oracle's two `[SupplyParameterFromQuery]` properties. Both wire names
    // are snake_case — `post_logout_redirect_uri` is OpenIddict's own parameter
    // name and is not this screen's to rename, even though the prop it feeds is
    // `postLogoutRedirectUri`.
    const validateSearch = logoutRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch).toBeDefined();
    expect(
      validateSearch?.({ post_logout_redirect_uri: REDIRECT_URI, signed_out: "true" }),
    ).toEqual({
      post_logout_redirect_uri: REDIRECT_URI,
      signed_out: "true",
    });
  });

  it("tolerates a query string with neither of them", () => {
    const validateSearch = logoutRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({})).toEqual({
      post_logout_redirect_uri: undefined,
      signed_out: undefined,
    });
  });

  it("treats a non-string signed_out as absent rather than crashing", () => {
    const validateSearch = logoutRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    // `?signed_out[]=true` parses to an array. The oracle's string comparison
    // would simply be false; the port must not throw on the way to the same
    // answer, because that would turn a junk link into a blank page.
    expect(validateSearch?.({ signed_out: ["true"], post_logout_redirect_uri: 42 })).toEqual({
      post_logout_redirect_uri: undefined,
      signed_out: undefined,
    });
  });
});
