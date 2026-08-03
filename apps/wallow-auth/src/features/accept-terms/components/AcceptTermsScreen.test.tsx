import {
  expectNavigationEscape,
  navigationEscapes,
} from "@bc-solutions-coder/testing/navigation-escape";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { Route as acceptTermsRoute } from "@app/routes/accept-terms";
import { AcceptTermsScreen } from "./AcceptTermsScreen";

/**
 * AcceptTerms: the ToS/Privacy consent gate (not the /terms document), plus its route.
 *
 * Real SDK over a faked fetch (sdk-harness): `harness.calls` staying empty is the
 * assertion that this screen issues no request at all. The gate finishes by
 * assigning `globalThis.location.href`, which the project's navigation guard
 * vetoes — `expectNavigationEscape()` reads that hand-off back.
 *
 * `isSafeReturnUrl` is deliberately not applied — it passes only relative paths,
 * and every returnUrl arriving here is absolute.
 */

/** The endpoint the gate hands the browser to — same-origin, via the passthrough proxy. */
const ENDPOINT = "/v1/identity/auth/complete-external-registration";

/** A real returnUrl for this flow: absolute and origin-allow-listed. */
const RETURN_URL = "https://app.example.com/callback";
const EMAIL = "ada@example.com";
const NAME = "Ada Lovelace";

/** The client that started the flow: arrives as `client_id`, leaves as `clientId`. */
const CLIENT_ID = "client-a";

function renderScreen(props: Partial<Parameters<typeof AcceptTermsScreen>[0]> = {}) {
  return renderWithWallow(<AcceptTermsScreen returnUrl={RETURN_URL} {...props} />, { harness });
}

/** Toggle a checkbox by CLICKING it, the way a pointer user does. */
async function toggleCheckbox(
  user: ReturnType<typeof userEvent.setup>,
  testId: string,
): Promise<void> {
  const box = page.getByTestId(testId);

  await expect.element(box).toBeInTheDocument();
  await user.click(box);
}

/** Tick both consent boxes — the only way to arm the submit button. */
async function acceptBoth(user: ReturnType<typeof userEvent.setup>) {
  await toggleCheckbox(user, "accept-terms-checkbox");
  await toggleCheckbox(user, "accept-terms-privacy-checkbox");
}

let harness: SdkHarness;

beforeEach(() => {
  vi.clearAllMocks();
  harness = createPassthroughHarness();
  harness.resolveJson({});
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("AcceptTermsScreen", () => {
  it("renders the gate: heading, both consent checkboxes, and submit", async () => {
    renderScreen();

    await expect
      .element(page.getByTestId("accept-terms-heading"))
      .toHaveTextContent(/almost there/iu);
    await expect.element(page.getByTestId("accept-terms-checkbox")).toBeInTheDocument();
    await expect.element(page.getByTestId("accept-terms-privacy-checkbox")).toBeInTheDocument();
    await expect.element(page.getByTestId("accept-terms-submit")).toBeInTheDocument();
    expect(page.getByTestId("accept-terms-error").query()).toBeNull();
  });

  it("exposes both testids on the checkboxes themselves", async () => {
    // A testid on a wrapping div cannot be clicked to toggle the box it wraps, so
    // both ids sit on the element carrying `role="checkbox"` — the one a user and
    // an E2E `.check()` act on. Asserted by ROLE, not `type="checkbox"`: the
    // catalog renders a `<span role="checkbox">` beside a hidden input.
    renderScreen();

    await expect
      .element(page.getByTestId("accept-terms-checkbox"))
      .toHaveAttribute("role", "checkbox");
    await expect
      .element(page.getByTestId("accept-terms-privacy-checkbox"))
      .toHaveAttribute("role", "checkbox");
  });

  it("shows who is signing up when the link carries an email and name", async () => {
    // The user's only chance to notice the provider handed over the wrong
    // account before one gets created.
    renderScreen({ email: EMAIL, name: NAME });

    await expect.element(page.getByText(EMAIL)).toBeInTheDocument();
    await expect.element(page.getByText(NAME)).toBeInTheDocument();
  });

  it("omits the signing-up-as block when the link carries no email", async () => {
    // Anchored on the heading so the negatives cannot pass against a screen that
    // renders nothing at all.
    renderScreen({ name: NAME });

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(page.getByText(NAME).query()).toBeNull();
    expect(page.getByText(/signing up as/iu).query()).toBeNull();
  });

  it("links to the terms and privacy documents that actually exist", async () => {
    // Consent to a document the user cannot open is not informed consent, so the
    // hrefs are the routes that really exist.
    renderScreen();

    const terms = page.getByRole("link", { name: /terms of service/iu });
    const privacy = page.getByRole("link", { name: /privacy policy/iu });

    await expect.element(terms).toHaveAttribute("href", "/terms");
    await expect.element(privacy).toHaveAttribute("href", "/privacy");
    // `target="_blank"`: reading the terms must not abandon the sign-up.
    await expect.element(terms).toHaveAttribute("target", "_blank");
    await expect.element(privacy).toHaveAttribute("target", "_blank");
  });
});

describe("AcceptTermsScreen consent gating", () => {
  it("keeps submit disabled until BOTH terms and privacy are accepted", async () => {
    const user = userEvent.setup();
    renderScreen();

    await expect.element(page.getByTestId("accept-terms-submit")).toBeDisabled();

    await toggleCheckbox(user, "accept-terms-checkbox");
    await expect.element(page.getByTestId("accept-terms-submit")).toBeDisabled();

    await toggleCheckbox(user, "accept-terms-privacy-checkbox");
    await expect.element(page.getByTestId("accept-terms-submit")).toBeEnabled();
  });

  it("keeps submit disabled when only privacy is accepted", async () => {
    // The mirror of the case above: `||`, not `&&`. A screen that armed on
    // either box passes that one and fails this.
    const user = userEvent.setup();
    renderScreen();

    await toggleCheckbox(user, "accept-terms-privacy-checkbox");

    await expect.element(page.getByTestId("accept-terms-submit")).toBeDisabled();
  });

  it("re-disables submit when a consent box is un-ticked", async () => {
    const user = userEvent.setup();
    renderScreen();

    await acceptBoth(user);
    await expect.element(page.getByTestId("accept-terms-submit")).toBeEnabled();

    await toggleCheckbox(user, "accept-terms-checkbox");

    await expect.element(page.getByTestId("accept-terms-submit")).toBeDisabled();
  });

  it("does not complete the registration while the boxes are unchecked", async () => {
    // The submit handler must re-check consent rather than trust the disabled
    // attribute, so the forced click has to be inert, not merely unclickable.
    const user = userEvent.setup();
    renderScreen();

    await expect.element(page.getByTestId("accept-terms-submit")).toBeDisabled();
    await user.click(page.getByTestId("accept-terms-submit"), { force: true });

    expect(navigationEscapes()).toEqual([]);
  });

  it("never sends acceptedTerms=false", async () => {
    // The endpoint does have an `acceptedTerms=false` branch, but this screen
    // must never drive it: declining means going nowhere, not a round trip.
    const user = userEvent.setup();
    renderScreen();

    await toggleCheckbox(user, "accept-terms-checkbox");
    await user.click(page.getByTestId("accept-terms-submit"), { force: true });

    await expect.element(page.getByTestId("accept-terms-submit")).toBeDisabled();
    expect(navigationEscapes()).toEqual([]);
  });

  it("offers a way out that does not create an account", async () => {
    // No testid on this footer link, so it is asserted by role + href.
    renderScreen();

    await expect
      .element(page.getByRole("link", { name: /back to sign in/iu }))
      .toHaveAttribute("href", "/login");
    expect(navigationEscapes()).toEqual([]);
    // Nothing to clean up client-side: no user was created, and the
    // ExternalLoginState cookie expires on its own.
    expect(harness.calls).toEqual([]);
  });
});

describe("AcceptTermsScreen consent boxes: accessible state", () => {
  it("publishes each box's checked state as aria-checked", async () => {
    // The catalog's Checkbox is a `<span role="checkbox">`: its state lives in
    // `aria-checked`, not in an input's `checked` property. On a consent gate
    // that state is the whole record of what the user agreed to.
    const user = userEvent.setup();
    renderScreen();

    await expect
      .element(page.getByTestId("accept-terms-checkbox"))
      .toHaveAttribute("aria-checked", "false");
    await expect
      .element(page.getByTestId("accept-terms-privacy-checkbox"))
      .toHaveAttribute("aria-checked", "false");

    await toggleCheckbox(user, "accept-terms-checkbox");

    await expect
      .element(page.getByTestId("accept-terms-checkbox"))
      .toHaveAttribute("aria-checked", "true");
    await expect
      .element(page.getByTestId("accept-terms-privacy-checkbox"))
      .toHaveAttribute("aria-checked", "false");
  });

  it("keeps each box named by its own label", async () => {
    // The label pairing asserted through what it buys: two boxes a user can tell
    // apart. The ids come from `useId()`, so the label has to point at whatever
    // element carries the role, or both boxes end up unnamed.
    renderScreen();

    await expect
      .element(page.getByRole("checkbox", { name: "I agree to the Terms of Service" }))
      .toBeInTheDocument();
    await expect
      .element(page.getByRole("checkbox", { name: "I agree to the Privacy Policy" }))
      .toBeInTheDocument();
  });
});

describe("AcceptTermsScreen accept branch", () => {
  it("hands the browser to complete-external-registration once both are accepted", async () => {
    // A FULL navigation, never `router.navigate`: `/v1/**` is served by the
    // passthrough reverse proxy, not the client route tree, and only a real
    // top-level navigation makes the browser attach the SameSite=Lax
    // ExternalLoginState cookie that carries the user's identity.
    const user = userEvent.setup();
    renderScreen();

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.pathname + target.search).toBe(
      `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
    );
  });

  it("keeps the handoff same-origin, never the oracle's ApiBaseUrl", async () => {
    // THE ORIGIN DECISION. This origin hosts /v1/** itself through the
    // passthrough proxy, so prepending an API origin would send the browser
    // cross-origin and drop the SameSite=Lax cookie the endpoint needs. There is
    // no browser-resolvable API origin to prepend anyway: WALLOW_API_INTERNAL_URL
    // is a server-side address.
    const user = userEvent.setup();
    renderScreen();

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.pathname).toBe(ENDPOINT);
    expect(target.origin).toBe(globalThis.location.origin);
    expect(escape.url).not.toContain("localhost:5001");
  });

  it("threads the flow's real absolute returnUrl through untouched", async () => {
    // Every returnUrl this screen can receive is absolute and origin-allow-listed,
    // so a screen wiring `isSafeReturnUrl` in would refuse the only shape real
    // traffic has. The API re-validates the value before honouring it.
    const user = userEvent.setup();
    renderScreen({ returnUrl: "https://app.example.com/connect/authorize?client_id=web" });

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.pathname + target.search).toBe(
      `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(
        "https://app.example.com/connect/authorize?client_id=web",
      )}`,
    );
  });

  it("falls back to '/' when the link carries no returnUrl", async () => {
    // Only a NULLISH returnUrl falls back. "/" fails the API's absolute-URI
    // check, so the fallback means "send me home" and the API decides where
    // home is.
    const user = userEvent.setup();
    renderScreen({ returnUrl: undefined });

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.pathname + target.search).toBe(`${ENDPOINT}?acceptedTerms=true&returnUrl=%2F`);
  });

  it("percent-encodes returnUrl so it cannot inject extra query parameters", async () => {
    // `returnUrl` is attacker-supplied cargo in a URL this screen builds by
    // concatenation. Unencoded, this value smuggles a second `acceptedTerms` in,
    // and ASP.NET binds a duplicated `[FromQuery] bool` key as "true,false",
    // which fails to parse and lands on the !acceptedTerms branch.
    const hostile = "https://app.example.com/cb&acceptedTerms=false";
    const user = userEvent.setup();
    renderScreen({ returnUrl: hostile });

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    const href: string = target.pathname + target.search;
    expect(href).toBe(`${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(hostile)}`);
    expect(href).not.toContain("acceptedTerms=false");
    expect(href.match(/acceptedTerms=/gu)).toHaveLength(1);
  });

  it("never reads, rewrites, or relays the ExternalLoginState cookie", async () => {
    // The real cookie is HttpOnly, so `document.cookie` cannot see it and the
    // browser attaches it itself on this top-level same-origin GET; the endpoint
    // deletes it server-side once spent. The non-HttpOnly decoy below stands in
    // for what a misguided "relay" implementation would find and forward.
    const decoy = "ExternalLoginState=CfDJ8-protected-blob";
    document.cookie = decoy;
    const fetchSpy = vi.fn();
    const user = userEvent.setup();
    vi.stubGlobal("fetch", fetchSpy);
    renderScreen();

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    // Anchor: the flow really ran, so the negatives below are not vacuous.
    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    const href: string = target.pathname + target.search;
    expect(href).toBe(`${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`);
    expect(document.cookie).toContain(decoy);
    expect(href).not.toContain("ExternalLoginState");
    expect(href).not.toContain("CfDJ8");
    // Both stubs are needed: the global `fetch` spy catches an ad-hoc request,
    // `harness.calls` one made through the real SDK, whose transport is the
    // harness's and not the stubbed global.
    expect(fetchSpy).not.toHaveBeenCalled();
    expect(harness.calls).toEqual([]);

    document.cookie = `${decoy}; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
  });
});

describe("AcceptTermsScreen error mapping", () => {
  it("explains a terms_required bounce-back", async () => {
    // The only error the wire delivers here.
    renderScreen({ error: "terms_required" });

    await expect
      .element(page.getByTestId("accept-terms-error"))
      .toHaveTextContent(/must accept the terms to continue/iu);
  });

  it("explains a session_expired error", async () => {
    // Not wire-reachable — the endpoint routes real session expiry to
    // /login?error=session_expired — but `?error=` is a query string anyone can
    // construct, so the mapping handles it deliberately rather than by accident.
    renderScreen({ error: "session_expired" });

    await expect
      .element(page.getByTestId("accept-terms-error"))
      .toHaveTextContent(/session has expired/iu);
  });

  it("falls back to the generic message for an unrecognised error code", async () => {
    // Binds the mapping: without this case, a blanket "always show one message"
    // screen passes both cases above.
    renderScreen({ error: "wat" });

    const error = page.getByTestId("accept-terms-error");
    await expect.element(error).toHaveTextContent(/an error occurred/iu);
    await expect.element(error).not.toHaveTextContent(/must accept the terms/iu);
    await expect.element(error).not.toHaveTextContent(/session has expired/iu);
  });

  it("does not resolve inherited Object keys as error copy", async () => {
    // /accept-terms?error=toString is a URL anyone can send a victim. A Record +
    // bracket lookup resolves Object.prototype.toString — a FUNCTION handed to
    // the renderer — where a ReadonlyMap only ever sees keys put in it. The
    // benign "wat" case above does not catch this, so the mapping must not be
    // "simplified" back to an object literal.
    renderScreen({ error: "toString" });

    const error = page.getByTestId("accept-terms-error");
    await expect.element(error).toHaveTextContent(/an error occurred/iu);
    await expect.element(error).not.toHaveTextContent(/function|native code|\[object/iu);
  });

  it("renders no error block when the link carries no error", async () => {
    // Anchored on the heading: a screen rendering nothing must not pass this.
    renderScreen();

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(page.getByTestId("accept-terms-error").query()).toBeNull();
  });

  it("still lets the user accept after a terms_required bounce-back", async () => {
    // The bounce-back is a second chance: the error block must not replace the
    // gate, and the echoed returnUrl rides through unchanged.
    const user = userEvent.setup();
    renderScreen({ error: "terms_required" });

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.pathname + target.search).toBe(
      `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
    );
  });
});

/**
 * THE client_id RELAY. The endpoint scopes its redirect allow-list to a client,
 * and this screen is the only link in that chain running in the browser.
 *
 * The spellings differ across the hop deliberately: the screen RECEIVES
 * `client_id` (the OIDC spelling on the redirect in) and SENDS `clientId` (the
 * `[FromQuery] string? clientId` the endpoint binds). The id is inert cargo
 * exactly as `returnUrl` is, so it gets the same treatment: relayed untouched
 * and percent-encoded.
 */
describe("AcceptTermsScreen client_id relay", () => {
  it("echoes the flow's client id back to complete-external-registration", async () => {
    const user = userEvent.setup();
    renderScreen({ clientId: CLIENT_ID });

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    // Asserted by name rather than by pinning the whole query string, so
    // parameter order stays the implementation's business.
    expect(target.searchParams.get("clientId")).toBe(CLIENT_ID);
    // The relay must not cost the flow its returnUrl.
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
  });

  it("omits clientId entirely when the flow carries none", async () => {
    // An EMPTY `clientId=` is worse than none: the endpoint reads a blank id as
    // an unknown client, whose allow list is the AuthUrl origin alone, and would
    // then refuse the very returnUrl the user is mid-journey to.
    const user = userEvent.setup();
    renderScreen({ clientId: undefined });

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.searchParams.has("clientId")).toBe(false);
    expect(target.pathname + target.search).toBe(
      `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
    );
  });

  it("percent-encodes clientId so it cannot inject extra query parameters", async () => {
    // The same injection guard `returnUrl` gets, for the same reason: unencoded,
    // this value smuggles a second `acceptedTerms` in and turns a completed
    // consent into a terms_required bounce.
    const hostile = "client-a&acceptedTerms=false";
    const user = userEvent.setup();
    renderScreen({ clientId: hostile });

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.searchParams.get("clientId")).toBe(hostile);
    const href: string = target.pathname + target.search;
    expect(href).not.toContain("acceptedTerms=false");
    expect(href.match(/acceptedTerms=/gu)).toHaveLength(1);
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — the params
 * read out of the query string — only exists once a URL is parsed by a router.
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/accept-terms", route: acceptTermsRoute }],
  });
}

/** The redirect the external-login callback actually issues, verbatim. */
function callbackRedirectUrl(): string {
  return (
    `/accept-terms?returnUrl=${encodeURIComponent(RETURN_URL)}` +
    `&email=${encodeURIComponent(EMAIL)}&name=${encodeURIComponent(NAME)}`
  );
}

describe("/accept-terms route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    renderRouteAt(callbackRedirectUrl());

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("threads returnUrl, email and name out of the real callback redirect", async () => {
    const user = userEvent.setup();
    renderRouteAt(callbackRedirectUrl());

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();
    await expect.element(page.getByText(EMAIL)).toBeInTheDocument();
    await expect.element(page.getByText(NAME)).toBeInTheDocument();

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.pathname + target.search).toBe(
      `${ENDPOINT}?acceptedTerms=true&returnUrl=${encodeURIComponent(RETURN_URL)}`,
    );
  });

  it("threads the error code out of the terms_required bounce-back", async () => {
    // The other redirect that lands here.
    renderRouteAt(`/accept-terms?error=terms_required&returnUrl=${encodeURIComponent(RETURN_URL)}`);

    await expect
      .element(page.getByTestId("accept-terms-error"))
      .toHaveTextContent(/must accept the terms to continue/iu);
  });

  it("renders without throwing when the link carries no query at all", async () => {
    // `validateSearch` has to treat every param as optional rather than throw.
    // The user has no ExternalLoginState cookie in that case, but bouncing them
    // is the API's call to make, not a reason for this route to explode.
    renderRouteAt("/accept-terms");

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();
    expect(page.getByTestId("accept-terms-error").query()).toBeNull();
  });

  it("threads client_id out of the callback redirect into the handoff", async () => {
    const user = userEvent.setup();
    renderRouteAt(`${callbackRedirectUrl()}&client_id=${encodeURIComponent(CLIENT_ID)}`);

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.searchParams.get("clientId")).toBe(CLIENT_ID);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
  });

  it("treats a non-string client_id as absent", async () => {
    // TanStack Router JSON-parses scalar search values, so `?client_id=42`
    // arrives as the NUMBER 42. Relaying it would name a client that cannot
    // exist, and the endpoint fails an unknown client closed.
    const user = userEvent.setup();
    renderRouteAt(`${callbackRedirectUrl()}&client_id=42`);

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();

    await acceptBoth(user);
    await user.click(page.getByTestId("accept-terms-submit"));

    const escape = await expectNavigationEscape();
    const target = new URL(escape.url);
    expect(target.searchParams.has("clientId")).toBe(false);
  });

  it("treats a non-string search param as absent", async () => {
    // Same JSON-parsing: handing that number to a `string | undefined` prop is
    // how a screen ships `.trim is not a function`, so the route narrows with a
    // `typeof` check rather than trusting the declared type.
    renderRouteAt(`/accept-terms?email=${encodeURIComponent(EMAIL)}&name=42`);

    await expect.element(page.getByTestId("accept-terms-heading")).toBeInTheDocument();
    await expect.element(page.getByText(EMAIL)).toBeInTheDocument();
    expect(page.getByText("42").query()).toBeNull();
  });
});
