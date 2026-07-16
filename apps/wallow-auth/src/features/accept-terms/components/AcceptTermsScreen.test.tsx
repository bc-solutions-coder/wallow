/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { Route as acceptTermsRoute } from "../../../routes/accept-terms";
import { AcceptTermsScreen } from "./AcceptTermsScreen";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the DOM-matcher convention wallow-web's RTL tests
// established and wallow-auth copies.
expect.extend(matchers);

/**
 * Component spec for the AcceptTerms screen (Wallow-vec7.3.10), ported from the
 * Blazor oracle `api/src/Wallow.Auth/Components/Pages/AcceptTerms.razor`.
 *
 * This is the ToS/Privacy GATE in the external-login (social sign-up) flow — not
 * the static terms document, which is the separate `/terms` route
 * (Wallow-vec7.3.3). Testids come verbatim from the oracle (scout inventory on
 * Wallow-vec7.3): `accept-terms-heading`, `accept-terms-error`,
 * `accept-terms-checkbox`, `accept-terms-privacy-checkbox`,
 * `accept-terms-submit`.
 *
 * ── THE FLOW THIS SCREEN SITS IN (read from the controller, not assumed) ──────
 *
 * `AccountController` (api/src/Modules/Identity/Wallow.Identity.Api/Controllers/
 * AccountController.cs) drives the whole round trip:
 *
 *   1. `external-login`          L241-265  refuses unless `IsAllowedAsync(returnUrl)`.
 *   2. `external-login-callback` L268-393  Path C (new user): data-protects
 *      `provider|key|email|first|last|verified` into the **ExternalLoginState**
 *      cookie (HttpOnly, Secure, SameSite=Lax, 10 min) and redirects to
 *      `{authUrl}/accept-terms?returnUrl=…&email=…&name=…`.
 *   3. THIS SCREEN navigates to `complete-external-registration`.
 *   4. `complete-external-registration` L395+ reads the cookie back, unprotects
 *      it, creates/links the user, signs them in, deletes the cookie, and
 *      redirects to the validated returnUrl.
 *
 * The user's identity for step 4 lives ENTIRELY in that cookie. This screen is a
 * consent gate and nothing else: it holds no state, makes no request, and its
 * only job is to hand the browser to step 4.
 *
 * ── ACCEPTANCE: THE ExternalLoginState COOKIE IS PURE PROXY PASSTHROUGH ───────
 *
 * The cookie is `HttpOnly`, so this screen *cannot* read it even if it tried,
 * and it must not try: `apps/wallow-auth/src/lib/auth-server.ts` is a passthrough
 * reverse proxy mounting `/v1/**` at the ROOT and forwarding `Cookie` inbound and
 * `Set-Cookie` outbound verbatim. The browser attaches the cookie itself on a
 * top-level same-origin navigation (SameSite=Lax permits exactly this: a
 * top-level GET). No relay, no session store, no token — the tests below pin
 * that the cookie is untouched, that no fetch happens, and that the `auth`
 * facade is never even reached for.
 *
 * ── DIVERGENCE 1: NO `isSafeReturnUrl` GUARD (the big one; disclosed on bead) ──
 *
 * The bead's DESIGN says "apply the isSafeReturnUrl open-redirect guard on any
 * navigation". Applied to THIS screen's `returnUrl` it would refuse 100% of real
 * traffic, so it is deliberately NOT applied. Verified, not assumed:
 *
 *   • `OpenIddictRedirectUriValidator.IsAllowedAsync` (Identity.Infrastructure/
 *     Services/OpenIddictRedirectUriValidator.cs:23-32) bails unless
 *     `Uri.TryCreate(uri, UriKind.**Absolute**, …)`, then allow-lists the ORIGIN
 *     against every registered OIDC redirect/post-logout URI plus `AuthUrl`.
 *     A relative path can never pass it.
 *   • `external-login` (L257-260) REFUSES to start the flow at all unless
 *     `IsAllowedAsync(returnUrl)` — so a relative returnUrl never reaches step 2.
 *   • `external-login-callback` (L273-277) re-validates and falls back to
 *     `authUrl` (also absolute) otherwise.
 *
 * => the `returnUrl` arriving here is ALWAYS an absolute, allow-listed URL.
 * `isSafeReturnUrl` (packages/sdk/src/auth-oidc.ts) returns true only for a
 * relative path starting with a single '/', so it is `false` for every value
 * this screen can legitimately receive. Guarding here would send every social
 * sign-up to `/error?reason=invalid_redirect_uri`.
 *
 * This is the `buildConnectLogoutUrl` precedent, not the `buildConsentSubmitUrl`
 * one — that builder documents the identical reasoning for
 * `post_logout_redirect_uri`: "deliberately NOT guarded by isSafeReturnUrl: it
 * is an absolute URI by definition, and OpenIddict validates it server-side …
 * Applying the relative-path guard would reject every legitimate caller."
 *
 * Nothing is given up by omitting it. This screen's navigation target is a
 * SAME-ORIGIN CONSTANT path; `returnUrl` is inert cargo in a query parameter,
 * and `complete-external-registration` re-validates it with the same allow-list
 * "early, before any user creation" (L403-407), falling back to `authUrl`. The
 * open-redirect decision is the API's and is made server-side. What this screen
 * DOES owe is that the cargo cannot break out of the query string — pinned below
 * by the percent-encoding test, which is the injection guard that actually
 * applies here.
 *
 * ── DIVERGENCE 2: THE ORACLE'S ToS/PRIVACY LINKS ARE BROKEN (fixed on port) ───
 *
 * The oracle links to `/terms-of-service` and `/privacy-policy` (AcceptTerms.
 * razor:48,56). Neither route exists: the pages are `@page "/terms"` (Terms.
 * razor:1) and `@page "/privacy"` (Privacy.razor:1). Both links 404 in Blazor
 * today. On a screen whose entire purpose is to obtain informed consent to those
 * two documents, shipping 404s is not parity worth keeping — the port links to
 * the real routes, which Wallow-vec7.3.16 has already registered.
 *
 * ── DIVERGENCE 3: `session_expired` IS NOT WIRE-REACHABLE HERE ───────────────
 *
 * The oracle's error switch handles `terms_required` and `session_expired`. Only
 * the first is reachable: `complete-external-registration` sends every
 * session-expired path to `{authUrl}/**login**?error=session_expired` (L416,
 * L437, L444), and the ONLY redirect back to this screen is
 * `/accept-terms?error=terms_required&returnUrl=…` (L412). The branch is kept
 * anyway — it is static copy, it costs nothing, and `?error=` is a query string
 * anyone can construct, so the mapping must handle it deliberately rather than
 * by accident. Pinned below alongside the unrecognised-code fallback.
 *
 * ── DIVERGENCE 4: TESTID PLACEMENT ON THE CHECKBOXES ─────────────────────────
 *
 * The oracle puts `accept-terms-checkbox` / `accept-terms-privacy-checkbox` on
 * the wrapping `<div>` (L44, L52), not the `<input>`. The names are preserved
 * verbatim; the placement moves to the input, which is the element a test or an
 * E2E `.check()` must actually reach. A testid on a div wrapping a checkbox
 * cannot be clicked to toggle it.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly. Per bd memory `vitest-resetmodules-breaks-
 * instanceof-across-graphs`, this file uses a plain `vi.mock` factory +
 * `vi.hoisted` spies and NEVER `vi.resetModules()`.
 */

const mocks = vi.hoisted(() => ({
  /** Every property read off the `auth` facade slice, for the no-relay test. */
  authAccess: [] as string[],
  /**
   * The REAL `isSafeReturnUrl` implementation (auth-oidc.ts), not a stub. It is
   * offered so that an implementation which wires the guard in — as the bead's
   * DESIGN literally instructs — fails the "threads the flow's real absolute
   * returnUrl" test BEHAVIOURALLY, showing the screen breaking, rather than
   * crashing on a missing mock. See divergence 1.
   */
  isSafeReturnUrl: vi.fn(
    (url: string | null | undefined): boolean =>
      url !== null &&
      url !== undefined &&
      url.trim() !== "" &&
      url.startsWith("/") &&
      !url.startsWith("//"),
  ),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    // A trap, not a stub: this screen makes no request, so ANY reach for the
    // auth client is a relay this port must not have.
    auth: new Proxy(
      {},
      {
        get: (_target: object, property: string | symbol) => {
          mocks.authAccess.push(String(property));
          return () => {
            throw new Error(`AcceptTerms must not call auth.${String(property)}`);
          };
        },
      },
    ),
    oidc: { isSafeReturnUrl: mocks.isSafeReturnUrl },
  }),
}));

/** The endpoint the gate hands the browser to — same-origin, via the h3 proxy. */
const ENDPOINT = "/v1/identity/auth/complete-external-registration";

/**
 * A real returnUrl for this flow: absolute and origin-allow-listed. Every value
 * this screen can legitimately receive looks like this (see divergence 1).
 */
const RETURN_URL = "https://app.example.com/callback";
const EMAIL = "ada@example.com";
const NAME = "Ada Lovelace";

/**
 * Replace `window.location` with a plain settable object so the screen's full
 * navigation is observable. jsdom refuses `vi.spyOn(window.location, "assign")`
 * ("Cannot redefine property"), but `location` itself is a configurable
 * accessor, so `vi.stubGlobal` swaps it wholesale — and `globalThis === window`
 * under jsdom, so the screen's `globalThis.location.href = …` writes here.
 * Established by Wallow-vec7.3.4 (Consent); bd memory `full-navigation-seam-for-
 * wallow-auth-screens-that`.
 */
function stubLocation(): { href: string } {
  const location = { href: "" };
  vi.stubGlobal("location", location);
  return location;
}

function renderScreen(props: Partial<Parameters<typeof AcceptTermsScreen>[0]> = {}) {
  return render(<AcceptTermsScreen returnUrl={RETURN_URL} {...props} />);
}

/** Tick both consent boxes — the only way to arm the submit button. */
async function acceptBoth(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByTestId("accept-terms-checkbox"));
  await user.click(screen.getByTestId("accept-terms-privacy-checkbox"));
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.authAccess.length = 0;
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("AcceptTermsScreen", () => {
  it("renders the gate: heading, both consent checkboxes, and submit", () => {
    renderScreen();

    expect(screen.getByTestId("accept-terms-heading")).toHaveTextContent(/almost there/iu);
    expect(screen.getByTestId("accept-terms-checkbox")).toBeInTheDocument();
    expect(screen.getByTestId("accept-terms-privacy-checkbox")).toBeInTheDocument();
    expect(screen.getByTestId("accept-terms-submit")).toBeInTheDocument();
    expect(screen.queryByTestId("accept-terms-error")).toBeNull();
  });

  it("exposes both testids on real checkbox inputs", () => {
    // Divergence 4: the oracle puts these testids on the wrapping div. A div
    // cannot be clicked to toggle the box it wraps, so the port moves them onto
    // the inputs — which is what every test and E2E `.check()` below relies on.
    renderScreen();

    expect(screen.getByTestId("accept-terms-checkbox")).toHaveAttribute("type", "checkbox");
    expect(screen.getByTestId("accept-terms-privacy-checkbox")).toHaveAttribute("type", "checkbox");
  });

  it("shows who is signing up when the link carries an email and name", () => {
    // Oracle: `@if (!string.IsNullOrEmpty(Email))` renders "Signing up as",
    // `@Name`, `@Email`. This is the user's only chance to notice the provider
    // handed over the wrong account before one gets created.
    renderScreen({ email: EMAIL, name: NAME });

    expect(screen.getByText(EMAIL)).toBeInTheDocument();
    expect(screen.getByText(NAME)).toBeInTheDocument();
  });

  it("omits the signing-up-as block when the link carries no email", () => {
    // Oracle: the whole block is gated on Email, so a nameless/emailless link
    // must not render an empty identity card. Anchored on the heading so the
    // negative cannot pass against a screen that renders nothing at all.
    renderScreen({ name: NAME });

    expect(screen.getByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(screen.queryByText(NAME)).toBeNull();
    expect(screen.queryByText(/signing up as/iu)).toBeNull();
  });

  it("links to the terms and privacy documents that actually exist", () => {
    // Divergence 2: the oracle's `/terms-of-service` and `/privacy-policy` are
    // 404s; the real routes are `/terms` and `/privacy`. Consent to a document
    // the user cannot open is not informed consent.
    renderScreen();

    const terms: HTMLElement = screen.getByRole("link", { name: /terms of service/iu });
    const privacy: HTMLElement = screen.getByRole("link", { name: /privacy policy/iu });

    expect(terms).toHaveAttribute("href", "/terms");
    expect(privacy).toHaveAttribute("href", "/privacy");
    // Oracle: `target="_blank"` — reading the terms must not abandon the sign-up.
    expect(terms).toHaveAttribute("target", "_blank");
    expect(privacy).toHaveAttribute("target", "_blank");
  });
});

describe("AcceptTermsScreen consent gating", () => {
  it("keeps submit disabled until BOTH terms and privacy are accepted", async () => {
    // Oracle: `Disabled="@(!_termsAccepted || !_privacyAccepted)"`. Accepting one
    // document is not accepting both, and this is the entire point of the screen.
    const user = userEvent.setup();
    renderScreen();

    expect(screen.getByTestId("accept-terms-submit")).toBeDisabled();

    await user.click(screen.getByTestId("accept-terms-checkbox"));
    expect(screen.getByTestId("accept-terms-submit")).toBeDisabled();

    await user.click(screen.getByTestId("accept-terms-privacy-checkbox"));
    expect(screen.getByTestId("accept-terms-submit")).toBeEnabled();
  });

  it("keeps submit disabled when only privacy is accepted", async () => {
    // The mirror of the above: `||`, not `&&`. A port that checked either box
    // would pass the previous test's final assertion and fail this one.
    const user = userEvent.setup();
    renderScreen();

    await user.click(screen.getByTestId("accept-terms-privacy-checkbox"));

    expect(screen.getByTestId("accept-terms-submit")).toBeDisabled();
  });

  it("re-disables submit when a consent box is un-ticked", async () => {
    // Consent is revocable right up to the click. The oracle's binding is
    // two-way; a port that only ever latched the flag forward would miss this.
    const user = userEvent.setup();
    renderScreen();

    await acceptBoth(user);
    expect(screen.getByTestId("accept-terms-submit")).toBeEnabled();

    await user.click(screen.getByTestId("accept-terms-checkbox"));

    expect(screen.getByTestId("accept-terms-submit")).toBeDisabled();
  });

  it("does not complete the registration while the boxes are unchecked", async () => {
    // THE DECLINE BRANCH, part 1: declining is simply not accepting. The oracle
    // ALSO re-checks inside the handler (`if (!_termsAccepted || !_privacyAccepted)
    // return;`) rather than trusting the disabled attribute — so the click must
    // be inert, not merely unclickable. Anchored on the disabled assertion.
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen();

    expect(screen.getByTestId("accept-terms-submit")).toBeDisabled();
    await user.click(screen.getByTestId("accept-terms-submit"));

    expect(location.href).toBe("");
  });

  it("never sends acceptedTerms=false", async () => {
    // THE DECLINE BRANCH, part 2. `complete-external-registration` DOES have an
    // `if (!acceptedTerms)` branch (L410-413) that bounces back here with
    // error=terms_required — but this screen must never drive it. Declining means
    // going nowhere; there is no "no thanks" round trip to the API.
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen();

    await user.click(screen.getByTestId("accept-terms-checkbox"));
    await user.click(screen.getByTestId("accept-terms-submit"));

    expect(screen.getByTestId("accept-terms-submit")).toBeDisabled();
    expect(location.href).toBe("");
  });

  it("offers a way out that does not create an account", async () => {
    // THE DECLINE BRANCH, part 3: the oracle's card footer, "Changed your mind?
    // Back to sign in" -> /login. Asserted by role + href because the oracle
    // gives it no testid and the scout's inventory forbids inventing one for an
    // element that shipped without one. Note this is an <a>: `toBeDisabled` can
    // never match it (bd memory `jest-dom-tobedisabled-cannot-match-an-anchor`).
    const location = stubLocation();
    renderScreen();

    expect(screen.getByRole("link", { name: /back to sign in/iu })).toHaveAttribute(
      "href",
      "/login",
    );
    expect(location.href).toBe("");
    // Walking away leaves the ExternalLoginState cookie to expire on its own
    // (10 min): no user was created, so there is nothing to clean up client-side.
    expect(mocks.authAccess).toEqual([]);
  });
});

describe("AcceptTermsScreen accept branch", () => {
  it("hands the browser to complete-external-registration once both are accepted", async () => {
    // Oracle: `Navigation.NavigateTo(completeUrl, forceLoad: true)`. A FULL
    // navigation, never `router.navigate`: `/v1/**` is served by the h3 reverse
    // proxy, not the client route tree, which would 404 in-app. It must also be a
    // real top-level navigation for the browser to attach the SameSite=Lax
    // ExternalLoginState cookie step 4 needs.
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen();

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    await waitFor(() => {
      expect(location.href).toBe(
        `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
      );
    });
  });

  it("keeps the handoff same-origin, never the oracle's ApiBaseUrl", async () => {
    // THE ORIGIN DECISION. The oracle builds `{ApiBaseUrl}/v1/…` from config,
    // defaulting to http://localhost:5001. That prepend is deliberately not
    // ported, for the reason ConsentScreen documents at length: this origin DOES
    // host /v1/** (auth-server.ts mounts the proxy at the root), so prepending an
    // API origin would send the browser CROSS-ORIGIN and drop the SameSite=Lax
    // ExternalLoginState cookie — which is the user's whole identity here, so the
    // endpoint would bounce them to /login?error=session_expired. It would also
    // reintroduce an ApiBaseUrl knob this app lacks: WALLOW_API_INTERNAL_URL is a
    // SERVER-side address the browser cannot resolve at all.
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen();

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    await waitFor(() => {
      expect(location.href).not.toBe("");
    });
    expect(location.href.startsWith(ENDPOINT)).toBe(true);
    expect(location.href).not.toMatch(/^https?:\/\//u);
    expect(location.href).not.toContain("localhost:5001");
  });

  it("threads the flow's real absolute returnUrl through untouched", async () => {
    // THE BINDING TEST for divergence 1. Every returnUrl this screen can receive
    // is absolute and origin-allow-listed, so `isSafeReturnUrl` is false for all
    // of them. An implementation that wires the guard in — as the bead's DESIGN
    // says to — refuses this, the ONLY shape real traffic has, and every social
    // sign-up dies at /error?reason=invalid_redirect_uri. The API re-validates
    // this value against its allow-list before honouring it (L403-407).
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen({ returnUrl: "https://app.example.com/connect/authorize?client_id=web" });

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    await waitFor(() => {
      expect(location.href).toBe(
        `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(
          "https://app.example.com/connect/authorize?client_id=web",
        )}`,
      );
    });
  });

  it("falls back to '/' when the link carries no returnUrl", async () => {
    // Oracle: `Uri.EscapeDataString(ReturnUrl ?? "/")` — only NULLISH falls back
    // (bd memory `returnurl-guard-refuse-dont-sanitize`). "/" fails the API's
    // absolute-URI check, so the endpoint substitutes authUrl (L403-407): the
    // fallback means "send me home", and the API decides where home is.
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen({ returnUrl: undefined });

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    await waitFor(() => {
      expect(location.href).toBe(`${ENDPOINT}?acceptedTerms=true&returnUrl=%2F`);
    });
  });

  it("percent-encodes returnUrl so it cannot inject extra query parameters", async () => {
    // The injection guard that DOES apply here (see divergence 1). `returnUrl` is
    // attacker-supplied cargo in a URL this screen builds by concatenation; the
    // oracle's `Uri.EscapeDataString` is `encodeURIComponent`, not form encoding.
    // Unencoded, this value would smuggle a second `acceptedTerms` in — and ASP.
    // NET binds `[FromQuery] bool acceptedTerms` from a duplicated key as
    // "true,false", which fails to parse and lands on the !acceptedTerms branch.
    const hostile = "https://app.example.com/cb&acceptedTerms=false";
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen({ returnUrl: hostile });

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    await waitFor(() => {
      expect(location.href).toBe(
        `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(hostile)}`,
      );
    });
    expect(location.href).not.toContain("acceptedTerms=false");
    expect(location.href.match(/acceptedTerms=/gu)).toHaveLength(1);
  });

  it("never reads, rewrites, or relays the ExternalLoginState cookie", async () => {
    // THE ACCEPTANCE CRITERION: pure proxy passthrough, no relay.
    //
    // The cookie is HttpOnly (AccountController L376-383), so document.cookie
    // cannot see it and the browser attaches it itself on this top-level
    // same-origin GET. The screen must therefore do NOTHING with it: not read it,
    // not copy it into the URL, not clear it, and not hand it to the API through
    // the SDK. `complete-external-registration` deletes it server-side once it
    // has been spent (L438, L445, L465).
    //
    // The non-HttpOnly decoy below stands in for what a misguided "relay" port
    // would find and forward; the assertions pin that it is left exactly as it
    // was and never leaves the browser.
    const decoy = "ExternalLoginState=CfDJ8-protected-blob";
    document.cookie = decoy;
    const fetchSpy = vi.fn();
    const user = userEvent.setup();
    const location = stubLocation();
    vi.stubGlobal("fetch", fetchSpy);
    renderScreen();

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    // Anchor: the flow really ran, so the negatives below are not vacuous.
    await waitFor(() => {
      expect(location.href).toBe(
        `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
      );
    });
    // Untampered: still there, unchanged, for the browser to send.
    expect(document.cookie).toContain(decoy);
    // Not relayed: no cookie material in the URL the screen built.
    expect(location.href).not.toContain("ExternalLoginState");
    expect(location.href).not.toContain("CfDJ8");
    // Not relayed: no request of any kind, and the auth client never touched.
    expect(fetchSpy).not.toHaveBeenCalled();
    expect(mocks.authAccess).toEqual([]);

    document.cookie = `${decoy}; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
  });
});

describe("AcceptTermsScreen error mapping", () => {
  it("explains a terms_required bounce-back", async () => {
    // The ONLY error the wire delivers here: `complete-external-registration`
    // L410-413 redirects to /accept-terms?error=terms_required&returnUrl=… .
    renderScreen({ error: "terms_required" });

    expect(await screen.findByTestId("accept-terms-error")).toHaveTextContent(
      /must accept the terms to continue/iu,
    );
  });

  it("explains a session_expired error", async () => {
    // Divergence 3: kept as deliberate handling of a query string anyone can
    // construct, though the endpoint routes real session expiry to
    // /login?error=session_expired instead.
    renderScreen({ error: "session_expired" });

    expect(await screen.findByTestId("accept-terms-error")).toHaveTextContent(
      /session has expired/iu,
    );
  });

  it("falls back to the generic message for an unrecognised error code", async () => {
    // The oracle's `_ =>` arm, and the test that BINDS the mapping: without it a
    // blanket "always show one message" implementation passes both tests above
    // (bd memory `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-
    // bind`).
    renderScreen({ error: "wat" });

    const error: HTMLElement = await screen.findByTestId("accept-terms-error");
    expect(error).toHaveTextContent(/an error occurred/iu);
    expect(error).not.toHaveTextContent(/must accept the terms/iu);
    expect(error).not.toHaveTextContent(/session has expired/iu);
  });

  it("does not resolve inherited Object keys as error copy", async () => {
    // bd memory `attacker-supplied-query-key-lookups-use-map-not-record`:
    // /accept-terms?error=toString is a URL anyone can send a victim. A Record +
    // bracket lookup resolves Object.prototype.toString — a FUNCTION handed to
    // the renderer. A ReadonlyMap + .get() only ever sees keys put in it. The
    // benign "wat" case above does NOT catch this; this test is the reason the
    // mapping must not be "simplified" back to an object literal.
    renderScreen({ error: "toString" });

    const error: HTMLElement = await screen.findByTestId("accept-terms-error");
    expect(error).toHaveTextContent(/an error occurred/iu);
    expect(error).not.toHaveTextContent(/function|native code|\[object/iu);
  });

  it("renders no error block when the link carries no error", () => {
    // Anchored on the heading: a screen rendering nothing must not pass this.
    renderScreen();

    expect(screen.getByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(screen.queryByTestId("accept-terms-error")).toBeNull();
  });

  it("still lets the user accept after a terms_required bounce-back", async () => {
    // The bounce-back is a second chance, not a dead end — the error block must
    // not replace the gate. The returnUrl the API validated and echoed back rides
    // through unchanged.
    const user = userEvent.setup();
    const location = stubLocation();
    renderScreen({ error: "terms_required" });

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    await waitFor(() => {
      expect(location.href).toBe(
        `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
      );
    });
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — the four
 * `[SupplyParameterFromQuery]` properties read out of the query string — only
 * exists once a URL is parsed by a router. The root here is a throwaway: the
 * app's real `__root.tsx` renders `<html>`, and `src/router.tsx` is off-limits to
 * this task (Wallow-vec7.3.16 pre-registered every screen route).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    acceptTermsRoute.update({
      id: "/accept-terms",
      path: "/accept-terms",
      getParentRoute: () => rootRoute,
    }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return render(<RouterProvider router={router} />);
}

/** The redirect `external-login-callback` L392 actually issues, verbatim. */
function callbackRedirectUrl(): string {
  return (
    `/accept-terms?returnUrl=${encodeURIComponent(RETURN_URL)}` +
    `&email=${encodeURIComponent(EMAIL)}&name=${encodeURIComponent(NAME)}`
  );
}

describe("/accept-terms route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component; this
    // task's job is to replace it. The path is the contract and is not this
    // task's to change.
    renderRouteAt(callbackRedirectUrl());

    expect(await screen.findByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(screen.queryByTestId("route-placeholder")).toBeNull();
  });

  it("threads returnUrl, email and name out of the real callback redirect", async () => {
    const user = userEvent.setup();
    const location = stubLocation();
    renderRouteAt(callbackRedirectUrl());

    await screen.findByTestId("accept-terms-heading");
    expect(screen.getByText(EMAIL)).toBeInTheDocument();
    expect(screen.getByText(NAME)).toBeInTheDocument();

    await acceptBoth(user);
    await user.click(screen.getByTestId("accept-terms-submit"));

    await waitFor(() => {
      expect(location.href).toBe(
        `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
      );
    });
  });

  it("threads the error code out of the terms_required bounce-back", async () => {
    // The other redirect that lands here: L412.
    renderRouteAt(`/accept-terms?error=terms_required&returnUrl=${encodeURIComponent(RETURN_URL)}`);

    expect(await screen.findByTestId("accept-terms-error")).toHaveTextContent(
      /must accept the terms to continue/iu,
    );
  });

  it("renders without throwing when the link carries no query at all", async () => {
    // A bare /accept-terms must still render its gate — `validateSearch` has to
    // treat all four params as optional rather than throw. (The user has no
    // ExternalLoginState cookie in that case, so the API will bounce them to
    // /login?error=session_expired; that is the API's call to make, not a reason
    // for this route to explode.)
    renderRouteAt("/accept-terms");

    expect(await screen.findByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(screen.queryByTestId("accept-terms-error")).toBeNull();
  });

  it("treats a non-string search param as absent", async () => {
    // TanStack Router JSON-parses scalar search values, so `?name=42` arrives as
    // the NUMBER 42, not "42". The route must narrow with a `typeof` check (the
    // convention /consent set) rather than trusting the type: handing a number to
    // a `string | undefined` prop is how a port ships `.trim is not a function`.
    renderRouteAt(`/accept-terms?email=${encodeURIComponent(EMAIL)}&name=42`);

    expect(await screen.findByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(screen.getByText(EMAIL)).toBeInTheDocument();
    expect(screen.queryByText("42")).toBeNull();
  });
});
