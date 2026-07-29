import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { type SdkCall, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
import { Route as consentRoute } from "../../../routes/consent";
import { ConsentScreen } from "./ConsentScreen";

/**
 * Component spec for the Consent screen (Wallow-vec7.3.4).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `consent-error`, `consent-heading`, `consent-scopes`, `consent-approve`,
 * `consent-deny`. The oracle has no loading testid — see "THE MISSING LOADING
 * STATE" below for why that is a finding, not an omission to paper over.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). The
 * SDK is the REAL one and only its `fetch` is faked, so nothing here stubs the
 * SDK barrel or its generated query surface. The consent-info lookup runs the
 * pipeline the app ships — `appsGetConsentInfoOptions()` -> request-scoped SDK ->
 * generated operation -> query serialization -> React Query — and the assertions
 * below read the outgoing REQUEST (`harness.calls`) rather than a spy on a
 * stand-in double. `renderWithWallow` supplies the router context the screen
 * reads its SDK off, and `createAuthHarness()` pins the harness origin to this
 * app's root-mounted API surface (Wallow-pu6a.5.5).
 *
 * The `oidc` slice needs no stand-in either. `isSafeReturnUrl` and
 * `buildConsentSubmitUrl` are PURE functions (packages/sdk/src/auth-oidc.ts) with
 * 67 tests of their own, so the real ones run here — which is strictly stronger
 * than the restated-rule fakes this file used to carry, since a screen that
 * merely satisfied the restatement proved nothing about the shipped guard.
 *
 * NAVIGATION SEAM (Wallow-xzha.3.1): the screen hands off with
 * `globalThis.location.href = buildConsentSubmitUrl(…)`. `window.location` is
 * `[Unforgeable]` in the real Chromium these specs now run in, so the jsdom-only
 * `vi.stubGlobal("location", …)` hack is gone, and with the REAL builder in play
 * the assigned value is a genuinely navigating URL that would tear the runner's
 * iframe down. Instead we listen on the Navigation API `navigate` event the
 * assignment fires, record `destination.url`, and `preventDefault()` so the
 * navigation is cancelled and the runner stays put. The recorded array stands in
 * for the old settable `location.href`: a hand-off appends exactly one absolute
 * URL and the tests assert the exact path + query it carries; a refused submit
 * appends nothing at all. bd memory
 * `full-navigation-seam-for-wallow-auth-screens-that`.
 *
 * ── THE ORIGIN DIVERGENCE (the load-bearing port decision in this screen) ─────
 *
 * The oracle ends `AppendToReturnUrl` (Consent.razor:70-80) by prepending an
 * absolute API origin:
 *
 *     return withParam.StartsWith('/') ? $"{ApiBaseUrl}{withParam}" : withParam;
 *
 * with `ApiBaseUrl = Configuration["ApiBaseUrl"] ?? "http://localhost:5001"`.
 * Its own comment states the reason: the returnUrl is a relative path issued by
 * the API's `/connect/authorize`, and "NavigationManager.NavigateTo resolves
 * relative URLs against the Auth app origin, which does not host
 * /connect/authorize".
 *
 * **That premise does not hold in this app, so the prepend must not be ported.**
 * apps/wallow-auth's API surface (`src/lib/api-passthrough.ts`) is a PASSTHROUGH
 * REVERSE PROXY that mounts `/connect/**` (and `/v1/**`) at the ROOT and
 * forwards them verbatim to the API — that is the same fact that makes the SDK
 * facade configure `baseUrl: '/'` rather than the SDK's `/api` default (bd
 * memory `wallow-auth-same-origin-baseurl-apps-wallow-auth`). This origin DOES
 * host `/connect/authorize`. So the consent submit URL is same-origin, and the
 * screen passes `""` as `buildConsentSubmitUrl`'s `origin` argument.
 *
 * This is not cosmetic. Hardcoding an API origin here would (a) send the browser
 * cross-origin for a request the proxy exists to keep same-origin, dropping the
 * `SameSite` auth cookie the authorize endpoint needs, and (b) reintroduce an
 * `ApiBaseUrl` config knob this app deliberately does not have (the only API
 * URL it knows, `WALLOW_API_INTERNAL_URL`, is a SERVER-side internal address —
 * `http://wallow-api` under Aspire — and is not resolvable from the browser at
 * all). The tests below pin the same-origin call explicitly rather than letting
 * an implementer copy the oracle's line and quietly recreate that knob.
 *
 * `buildConsentSubmitUrl` (Wallow-vec7.2.2) already ports the rest of
 * `AppendToReturnUrl` — the `ReturnUrl ?? "/"` nullish fallback, the
 * `Contains('?')` separator, and the `consent_granted=true` /
 * `consent_denied=true` parameter — and has 67 tests of its own. These tests
 * therefore pin the URL the SCREEN actually hands off to — same-origin, with the
 * right consent parameter appended to the right base — rather than re-deriving
 * the builder's string algebra here.
 *
 * ── THE OPEN-REDIRECT GUARD (acceptance criterion; NOT in the oracle) ─────────
 *
 * The oracle applies NO guard to `ReturnUrl` on this screen: it appends and
 * navigates. That is the gap this bead's acceptance criterion closes ("the
 * open-redirect guard on the returnUrl"), and `buildConsentSubmitUrl` enforces
 * it by THROWING a `TypeError` on a present-but-unsafe returnUrl rather than
 * silently sanitizing (bd memory `returnurl-guard-refuse-dont-sanitize`).
 *
 * The screen refuses EARLY — on mount, before rendering a prompt or fetching
 * anything — rather than waiting for the throw at click time. That is
 * `Login.razor` L533-540's pattern, the one call site in the oracle that does
 * check `IsSafe` before building a navigation URL: it bails to
 * `/error?reason=invalid_redirect_uri`. Deferring the refusal to the click would
 * mean rendering an Approve button whose destination we already know we will
 * refuse to build — i.e. asking the user to authorize a request we have already
 * decided is malformed, and telling them so only after they consent.
 *
 * The bail routes via the ROUTER (`/error` is an in-app registered route), using
 * `href` rather than `to`+`search` — bd memory `tanstack-router-redirect-to-an-
 * unregistered-route-use-href-not-to`, and here also because `/error`'s
 * `validateSearch` is being written concurrently by Wallow-vec7.3.3; `href`
 * keeps this screen from coupling to that in-flight shape.
 *
 * ── THE MISSING LOADING STATE (an oracle wart, deliberately not ported) ───────
 *
 * The oracle renders on `_consentInfo is null` alone, and `_consentInfo` is null
 * *while the request is still in flight* — which would flash "Unable to load
 * consent information" at every user before its own fetch resolves. The
 * scout's testid inventory has no loading testid because the oracle has no
 * loading state to give one to.
 *
 * The port renders NOTHING while the request is in flight: no error, no prompt.
 * That fixes the flash without inventing a testid for an element the oracle does
 * not have, and it keeps `consent-error` meaning "this failed" rather than "this
 * failed or has not happened yet". Pinned by the tests in "loading" below.
 *
 * ── ERROR STATE: `null` BECOMES A REJECTION AT THIS SEAM ─────────────────────
 *
 * The oracle's `_consentInfo` is null in two cases, and both must land on
 * `consent-error`:
 *
 *   1. No `client_id` — `OnInitializedAsync` skips the call entirely and logs a
 *      warning, so `_consentInfo` is never assigned.
 *   2. The request failed — `AuthApiClient.GetConsentInfoAsync`
 *      (api/src/Wallow.Auth/Services/AuthApiClient.cs:397-416) returns `null` on
 *      ANY non-2xx.
 *
 * Case 2 arrives differently through this seam but means the same thing: the
 * facade's `unwrap()` THROWS on non-2xx instead of returning null, so a failure
 * is a rejected promise. Either way the user sees `consent-error`. No status
 * narrowing is needed or wanted here — unlike VerifyEmailConfirm/ResetPassword,
 * this screen's oracle has exactly ONE error message for every failure, so the
 * `WallowError` code-loss gotcha (bd memory `wallow-auth-auth-client-ts-
 * wallowerror-code-loss`) costs this screen nothing.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy. Only
// the ROUTER is stubbed now — `useNavigate` is how the screen reports an unsafe
// returnUrl, and it is not part of the SDK seam.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const CLIENT_ID = "wallow-web";
const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * The `scope` query parameter as the authorize endpoint sends it: ONE
 * space-delimited string (OAuth's own delimiter, and what
 * `AuthorizationController` builds its consent redirect with), which this screen
 * splits into the list it asks the consent-info endpoint about.
 */
const SCOPE = "openid profile email";
const SCOPE_LIST = ["openid", "profile", "email"];

/**
 * THE READ SEAM MOVED (Wallow-evd5.3.1, Wallow-pu6a.5.5). The consent prompt is
 * `useQuery(appsGetConsentInfoOptions(...))` — the GENERATED factory — rather
 * than a facade call inside inline `useQuery` options. Against the harness that
 * distinction needs no mock at all: the real factory runs, so both the generated
 * `queryKey` and the real `queryFn` execute, and what the screen asks for is
 * read off the REQUEST the factory produced rather than off a spy's arguments.
 *
 * The operation issues `GET /v1/identity/apps/consent-info/{clientId}` with the
 * scope list as ONE space-joined `scopes` query parameter — and with the key
 * OMITTED entirely when the list is empty, which is exactly the distinction
 * "asks with no scopes" below pins. This app's SDK is rooted at the origin, so
 * the recorded `path` is the bare endpoint path.
 */
const CONSENT_INFO_ROOT = "/v1/identity/apps/consent-info";
const CONSENT_INFO_PATH = `${CONSENT_INFO_ROOT}/${CLIENT_ID}`;

/** The two failure statuses the error-state tests answer with. */
const NOT_FOUND = 404;
const SERVER_ERROR = 500;

/**
 * The URLs the two submit paths hand off to, as the REAL `buildConsentSubmitUrl`
 * composes them: `RETURN_URL` already carries a `?`, so the separator is `&`.
 */
const GRANTED_TARGET = `${RETURN_URL}&consent_granted=true`;
const DENIED_TARGET = `${RETURN_URL}&consent_denied=true`;

/**
 * Every recorded request to the consent-info endpoint, whatever the client id —
 * so a screen that looked up the WRONG client is still counted here and fails
 * the path assertion rather than silently reading as "no request".
 */
function consentInfoCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path.startsWith(CONSENT_INFO_ROOT));
}

/**
 * The `scopes` query parameter of the first consent-info request, decoded.
 *
 * `null` means the key was omitted. Read through `URL.searchParams` rather than
 * off the raw query string so the encoding of the space delimiter (`%20` vs `+`)
 * — a serializer detail, not this screen's contract — cannot break the test.
 *
 * THROWS when no request was made: without that, an absent request would make
 * every scope assertion pass vacuously, which is precisely the failure mode the
 * old `toHaveBeenCalled` precondition existed to rule out.
 */
function requestedScopesParameter(): string | null {
  const [call] = consentInfoCalls();
  if (call === undefined) {
    throw new Error("expected a consent-info request, but the screen made none");
  }

  return new URL(call.url).searchParams.get("scopes");
}

/**
 * NAVIGATION SEAM — see this file's header. The `navigate` event the screen's
 * `location.href` assignment fires is recorded and CANCELLED, so the real
 * builder's real (navigating) URL is observable without tearing the Chromium
 * runner down.
 */
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

/** Arm the navigation seam and return the array the submit URL lands in. */
function captureHandoff(): { urls: string[] } {
  const urls: string[] = [];
  const handler = (event: NavigateEvent): void => {
    urls.push(event.destination.url);
    event.preventDefault();
  };
  navigationApi.addEventListener("navigate", handler);
  navDisposers.push(() => {
    navigationApi.removeEventListener("navigate", handler);
  });
  return { urls };
}

/** The absolute URL the screen handed off to, recovered from the cancelled navigation. */
function handoffUrl(urls: readonly string[]): URL {
  const [recorded] = urls;
  if (recorded === undefined) {
    throw new Error("expected the screen to hand off exactly one navigation, but it made none");
  }

  return new URL(recorded);
}

/** That hand-off's path + query — the part `buildConsentSubmitUrl` composes. */
function submitTarget(urls: readonly string[]): string {
  const url: URL = handoffUrl(urls);

  return `${url.pathname}${url.search}`;
}

/** A `ConsentInfoResponse`, as the generated type shapes it. */
function consentInfo(overrides: Record<string, unknown> = {}) {
  return {
    clientId: CLIENT_ID,
    displayName: "Wallow Web",
    logoUrl: null,
    requestedScopes: [
      { name: "openid", description: "Sign you in" },
      { name: "profile", description: "See your profile" },
    ],
    ...overrides,
  };
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

let harness: SdkHarness;

/**
 * Every request that reached the network in the current test.
 *
 * A function rather than a direct `harness.calls` read so the table-driven
 * blocks below can assert on it: `harness` is reassigned per test, and a closure
 * created inside a `for` loop that captures a reassigned binding is exactly what
 * `no-loop-func` forbids. This binding never changes, so the closures stay safe.
 */
function recordedCalls(): readonly SdkCall[] {
  return harness.calls;
}

beforeEach(() => {
  vi.clearAllMocks();
  // The real SDK over a recording transport; the default answer is a loaded
  // consent prompt, which most tests below take as their starting point.
  harness = createAuthHarness();
  harness.resolveJson(consentInfo());
});

afterEach(() => {
  navDisposers.forEach((dispose) => {
    dispose();
  });
  navDisposers.length = 0;
});

describe("ConsentScreen — loading", () => {
  it("requests the consent info for the client in the query string", async () => {
    harness.pending();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await vi.waitFor(() => {
      expect(consentInfoCalls()).toHaveLength(1);
    });

    // The client id is a path segment; the scopes are a query parameter and are
    // pinned separately below.
    expect(consentInfoCalls()[0]?.path).toBe(CONSENT_INFO_PATH);
    expect(consentInfoCalls()[0]?.method).toBe("GET");
  });

  /**
   * ── THE SCOPE LIST IS AN INPUT, NOT ONLY AN OUTPUT (Wallow-dzt4) ────────────
   *
   * The oracle called `GetConsentInfoAsync(ClientId, Array.Empty<string>())` and
   * this port copied it, on the reasoning that "the scopes being consented to
   * come back FROM this call". That reasoning was wrong, and it is the bug this
   * bead fixes: the consent-info endpoint answers "describe THESE scopes for
   * this client", so an empty list means an empty answer — users were asked to
   * approve a request whose scope list rendered blank. The oracle's Blazor
   * consent screen had the same defect; it is not a port regression, but it is
   * not a contract to preserve either.
   *
   * The authorize endpoint now carries the requested scopes to `/consent` as a
   * space-delimited `scope` parameter; the route parses it and hands it here.
   */
  it("forwards the requested scopes to the consent-info lookup", async () => {
    harness.pending();

    await renderWithClient(
      <ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} scope={SCOPE} />,
    );

    // The wait is load-bearing: it is what makes `requestedScopesParameter()`
    // read a real request rather than throw, and it rules out the vacuous pass a
    // screen that made NO call at all would otherwise get.
    await vi.waitFor(() => {
      expect(consentInfoCalls()).toHaveLength(1);
    });

    // The API takes ONE space-joined `scopes` value (`AppsController` splits on
    // ' '), not repeated parameters — a comma-joined value arrives there as a
    // single unknown scope name.
    expect(requestedScopesParameter()).toBe(SCOPE_LIST.join(" "));
  });

  it("splits the scope parameter on whitespace rather than passing it whole", async () => {
    // A single mangled scope name is exactly what the delimiter bug produced:
    // the endpoint would fail to resolve it and render one garbled row.
    harness.pending();

    await renderWithClient(
      <ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} scope="openid  profile " />,
    );

    await vi.waitFor(() => {
      expect(consentInfoCalls()).toHaveLength(1);
    });

    // Empty segments from repeated or trailing spaces are dropped, not sent as
    // empty scope names — the wire value is re-joined with a SINGLE space.
    expect(requestedScopesParameter()).toBe("openid profile");
  });

  it("asks with no scopes when the link carries none", async () => {
    // A link without `scope` is malformed, not an invitation to invent a list.
    // The screen must not fabricate scopes the relying party never asked for.
    harness.pending();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await vi.waitFor(() => {
      expect(consentInfoCalls()).toHaveLength(1);
    });

    // The facade omits the key entirely for an empty list; an empty value would
    // be equally acceptable to the controller, so both count as "asked with
    // none" — inventing scope names would not.
    const scopes: string | null = requestedScopesParameter();

    expect(scopes === null || scopes === "").toBe(true);
  });

  it("shows no error while the request is still in flight", async () => {
    // The oracle's wart, deliberately not ported: it renders on `_consentInfo is
    // null`, which is also true before the fetch resolves, so it flashes
    // "Unable to load consent information" at every user. See this file's
    // header. A port that copies the null-check literally fails HERE and only
    // here.
    harness.pending();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    // Pin that a request is genuinely IN FLIGHT before asserting the absence.
    // Otherwise a screen that never fetched at all would satisfy "no error
    // while fetching" by never fetching. The harness records a request BEFORE
    // running the responder, so a never-settling one is still observable.
    await vi.waitFor(() => {
      expect(consentInfoCalls()).toHaveLength(1);
    });
    expect(page.getByTestId("consent-error").query()).toBeNull();
  });

  it("shows no consent prompt before the client is known", async () => {
    // The other half of the same contract: nothing is rendered in flight, so the
    // user cannot approve access for an application we have not identified yet.
    harness.pending();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await vi.waitFor(() => {
      expect(consentInfoCalls()).toHaveLength(1);
    });
    expect(page.getByTestId("consent-heading").query()).toBeNull();
    expect(page.getByTestId("consent-approve").query()).toBeNull();
    expect(page.getByTestId("consent-deny").query()).toBeNull();
  });

  it("fires the request exactly once", async () => {
    // The request is a side effect of mounting, not of rendering.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();

    expect(consentInfoCalls()).toHaveLength(1);
  });
});

describe("ConsentScreen — the consent prompt", () => {
  it("names the requesting application in the heading", async () => {
    // Oracle: `<h2>@_consentInfo.DisplayName is requesting access</h2>`.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect
      .element(page.getByTestId("consent-heading"))
      .toHaveTextContent(/Wallow Web is requesting access/u);
  });

  it("falls back to the client id when the client has no display name", async () => {
    // `displayName` is `null | string` on the generated `ConsentInfoResponse`.
    // The oracle interpolates it unguarded, so a null renders the sentence
    // " is requesting access" — a consent prompt that does not say WHO is
    // asking. The port names the client instead; consent to an unnamed party is
    // not consent.
    harness.resolveJson(consentInfo({ displayName: null }));

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-heading")).toHaveTextContent(CLIENT_ID);
    await expect
      .element(page.getByTestId("consent-heading"))
      .toHaveTextContent(/is requesting access/u);
  });

  it("lists every requested scope", async () => {
    // Oracle: `@foreach (ConsentScopeInfo scope in _consentInfo.RequestedScopes)
    // { <div>@scope.Name</div> }`.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-scopes")).toHaveTextContent("openid");
    await expect.element(page.getByTestId("consent-scopes")).toHaveTextContent("profile");
  });

  it("lists no scope the client did not request", async () => {
    // The scope list is the whole substance of the decision — it must be the
    // server's list, not a superset.
    harness.resolveJson(
      consentInfo({ requestedScopes: [{ name: "openid", description: "Sign you in" }] }),
    );

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-scopes")).toHaveTextContent("openid");
    await expect.element(page.getByTestId("consent-scopes")).not.toHaveTextContent("profile");
  });

  it("renders the prompt for a client requesting no scopes", async () => {
    // `RequestedScopes` is non-nullable but may be empty; the oracle's foreach
    // simply renders nothing. The prompt must still work rather than crash.
    harness.resolveJson(consentInfo({ requestedScopes: [] }));

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-scopes")).toBeInTheDocument();
    await expect.element(page.getByTestId("consent-approve")).toBeInTheDocument();
  });

  it("offers both an approve and a deny action", async () => {
    // Oracle: two BbButtons. Deny must always be present — a consent screen with
    // only an approve path is not a consent screen.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-approve")).toBeInTheDocument();
    await expect.element(page.getByTestId("consent-deny")).toBeInTheDocument();
  });

  it("shows no error alongside a loaded prompt", async () => {
    // Oracle's if/else — the error block and the prompt are mutually exclusive.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();

    expect(page.getByTestId("consent-error").query()).toBeNull();
  });

  it("drops the pre-registration placeholder marker", async () => {
    // Wallow-vec7.3.16 shipped `route-placeholder` as scaffolding; it must not
    // survive into the real screen.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();

    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });
});

describe("ConsentScreen — approve", () => {
  it("navigates to the consent-granted URL the builder returns", async () => {
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await user.click(page.getByTestId("consent-approve"));

    // A FULL navigation, not `router.navigate`: `/connect/authorize` is served
    // by the passthrough reverse proxy (src/lib/api-passthrough.ts), not by the client-side
    // route tree — the router has no route for it and would 404 in-app. The
    // assigned URL is read straight off the cancelled navigation, so this pins
    // the string the REAL builder produced rather than the arguments it got.
    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe(GRANTED_TARGET);
  });

  it("builds the URL same-origin, granting consent", async () => {
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await user.click(page.getByTestId("consent-approve"));

    // The origin divergence, pinned explicitly (see this file's header): THIS
    // origin, NOT the oracle's `ApiBaseUrl`. This app's own origin hosts
    // `/connect/**` via the passthrough proxy, and it has no browser-reachable
    // API origin to prepend even if it wanted one — so a relative hand-off that
    // the browser resolves against the page is exactly right.
    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(handoffUrl(handoff.urls).origin).toBe(globalThis.location.origin);
    expect(submitTarget(handoff.urls)).toBe(GRANTED_TARGET);
  });

  it("appends to a returnUrl that has no query string of its own", async () => {
    // Oracle: `separator = baseUrl.Contains('?') ? "&" : "?"`. Pinned through
    // the screen so a port that hand-rolls string concatenation instead of
    // calling the builder cannot pass by only ever being tested with a
    // `?`-bearing returnUrl.
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl="/connect/authorize" />);
    await user.click(page.getByTestId("consent-approve"));

    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe("/connect/authorize?consent_granted=true");
  });

  it("falls back to the root when the link carries no returnUrl", async () => {
    // Oracle: `string baseUrl = ReturnUrl ?? "/"`. Nullish ONLY — and an absent
    // returnUrl must NOT be treated as the unsafe-returnUrl case: there is
    // nothing hostile about a link that omits it, so the guard must not fire.
    // The builder maps `undefined` to the `/` fallback, producing
    // `/?consent_granted=true`.
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} />);
    await user.click(page.getByTestId("consent-approve"));

    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe("/?consent_granted=true");
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("does not deny while approving", async () => {
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await user.click(page.getByTestId("consent-approve"));

    // The two handlers differ by one boolean; a mis-wired button would be
    // invisible to a test that only checked that SOME navigation happened.
    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe(GRANTED_TARGET);
    expect(submitTarget(handoff.urls)).not.toContain("consent_denied");
  });
});

describe("ConsentScreen — deny", () => {
  it("navigates to the consent-denied URL the builder returns", async () => {
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await user.click(page.getByTestId("consent-deny"));

    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe(DENIED_TARGET);
  });

  it("builds the URL same-origin, refusing consent", async () => {
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await user.click(page.getByTestId("consent-deny"));

    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(handoffUrl(handoff.urls).origin).toBe(globalThis.location.origin);
    expect(submitTarget(handoff.urls)).toBe(DENIED_TARGET);
  });

  it("reports the denial to the authorize endpoint rather than staying put", async () => {
    // Oracle: Deny navigates, exactly as Approve does. A deny that silently did
    // nothing would strand the user on a dead consent screen and leave the
    // relying party's authorize request hanging — the denial has to be
    // DELIVERED. Observed via a real navigation being attempted (and cancelled
    // by the seam) to the deny-side URL.
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await user.click(page.getByTestId("consent-deny"));

    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe(DENIED_TARGET);
  });

  it("does not grant while denying", async () => {
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await user.click(page.getByTestId("consent-deny"));

    // The button-swap bug, from the side that matters: a Deny wired to
    // `granted: true` would authorize the client the user just refused.
    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe(DENIED_TARGET);
    expect(submitTarget(handoff.urls)).not.toContain("consent_granted");
  });
});

describe("ConsentScreen — the open-redirect guard", () => {
  const UNSAFE_RETURN_URLS: readonly string[] = [
    // Protocol-relative: looks relative, resolves off-origin. The guard's whole
    // reason to exist.
    "//evil.example/steal",
    // Absolute, off-origin.
    "https://evil.example/steal",
    // A scheme that executes rather than navigates. The `no-script-url` lint
    // exists to stop this string being USED as a URL; here it is the attack
    // being tested for, and rejecting it is the whole point of the case.
    // oxlint-disable-next-line no-script-url
    "javascript:alert(1)",
    // Present but blank — `IsNullOrWhiteSpace` in the C# validator, so NOT the
    // `ReturnUrl ?? "/"` nullish-fallback case. `""` is a supplied value that
    // fails the guard.
    "",
  ];

  for (const returnUrl of UNSAFE_RETURN_URLS) {
    it(`refuses to render a consent prompt for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // Refusing at CLICK time would be too late: the user would be asked to
      // authorize a request we had already decided was malformed.
      expect(page.getByTestId("consent-approve").query()).toBeNull();
      expect(page.getByTestId("consent-heading").query()).toBeNull();
    });

    it(`routes to the error page for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      // `Login.razor` L533-540's bail, and bd memory
      // `returnurl-guard-refuse-dont-sanitize`: REFUSE, do not silently fall
      // back to "/". `href` rather than `to`+`search` — see this file's header.
      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
      });
    });

    it(`never navigates to the unsafe returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      const handoff = captureHandoff();

      await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // The criterion itself: whatever else happens, the browser must not be
      // sent to any submit URL. The screen never assigned `location.href`, so
      // the navigation seam recorded nothing at all.
      expect(handoff.urls).toEqual([]);
    });

    it(`does not fetch consent info for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // The link is already known to be malformed, so there is nothing to ask
      // the server about. Bailing before the request also keeps the client's
      // display name and scope list from being disclosed to an attacker-crafted
      // link. `recordedCalls()` is every request that reached the network, so
      // this covers an ad-hoc lookup as well as the query-layer one.
      expect(recordedCalls()).toEqual([]);
    });
  }

  it("shows no consent error for an unsafe returnUrl", async () => {
    // The user is being sent to `/error`; flashing "Unable to load consent
    // information" on the way out would misreport an open-redirect attempt as a
    // transient server problem.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl="//evil.example" />);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });

    expect(page.getByTestId("consent-error").query()).toBeNull();
  });

  it("guards the returnUrl even when no client id is supplied", async () => {
    // The two refusal paths must not mask each other: a hostile returnUrl on a
    // link that also omits `client_id` is still a hostile returnUrl, and must
    // reach `/error` rather than being absorbed by the missing-client branch.
    await renderWithClient(<ConsentScreen returnUrl="//evil.example" />);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
  });

  it("lets a safe returnUrl through untouched", async () => {
    // The negative control: the guard must not be so eager that it breaks the
    // ordinary flow. A screen that routed EVERY returnUrl to `/error` would pass
    // every other test in this block.
    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-approve")).toBeInTheDocument();

    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});

describe("ConsentScreen — error state", () => {
  it("shows the error when no client id is supplied", async () => {
    // Oracle: `if (ClientId is not null) { … }` — no client id, no call, so
    // `_consentInfo` stays null and the error block renders.
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect
      .element(page.getByTestId("consent-error"))
      .toHaveTextContent(/unable to load consent information/iu);
  });

  it("does not call the endpoint when no client id is supplied", async () => {
    // A screen that "helpfully" sent `clientId: undefined` would 404 and blame
    // the server for the link's own defect.
    await renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();

    expect(harness.calls).toEqual([]);
  });

  it("treats an empty-string client id as missing", async () => {
    // `?client_id=&returnUrl=…` is a malformed link, not a client to look up.
    await renderWithClient(<ConsentScreen clientId="" returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();

    expect(harness.calls).toEqual([]);
  });

  it("shows the error when the consent-info request fails", async () => {
    // `AuthApiClient.GetConsentInfoAsync` returns null on any non-2xx and the
    // oracle renders the error block. Through this seam the same failure is a
    // rejection, because `unwrap()` throws — and here that throw is the REAL
    // one, produced by the real client from a real non-2xx response.
    harness.rejectJson({}, NOT_FOUND);

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect
      .element(page.getByTestId("consent-error"))
      .toHaveTextContent(/unable to load consent information/iu);
  });

  it("shows the same error for a server failure", async () => {
    // The oracle has ONE error message for every failure — no status narrowing,
    // so the WallowError code-loss gotcha costs this screen nothing.
    harness.rejectJson({}, SERVER_ERROR);

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect
      .element(page.getByTestId("consent-error"))
      .toHaveTextContent(/unable to load consent information/iu);
  });

  it("survives a rejection that is not WallowError-shaped at all", async () => {
    // A network failure has no `status`; it must land on the same error surface
    // rather than throwing inside the error branch. A transport that THROWS is
    // the faithful version of that: the generated client does not wrap its
    // `fetch` call, so the raw rejection reaches React Query un-shaped, exactly
    // as a real DNS/offline failure would.
    harness.respond(() => {
      throw new Error("network down");
    });

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();
  });

  it("offers no approve or deny action in the error state", async () => {
    // Oracle's if/else. This is the important half: with no consent info there
    // is no scope list, so an Approve button here would authorize an unknown
    // client for unknown scopes.
    harness.rejectJson({}, NOT_FOUND);

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();

    expect(page.getByTestId("consent-approve").query()).toBeNull();
    expect(page.getByTestId("consent-deny").query()).toBeNull();
    expect(page.getByTestId("consent-heading").query()).toBeNull();
    expect(page.getByTestId("consent-scopes").query()).toBeNull();
  });

  it("never leaks the raw rejection into the page", async () => {
    // `code: "UNKNOWN"` / `title: "Unknown error"` are seam artefacts, not
    // user-facing copy. The oracle shows one curated message. An empty error
    // body is what makes `toWallowError` fall back to exactly those two values,
    // so this is the strongest form of the leak test.
    harness.rejectJson({}, NOT_FOUND);

    await renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await expect.element(page.getByTestId("consent-error")).toBeInTheDocument();

    expect(document.body.textContent).not.toMatch(/unknown error|UNKNOWN/u);
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`: this route's component reads `client_id` and
 * `returnUrl` through `Route.useSearch()`, and every router hook dereferences a
 * `null` router outside a `RouterProvider` (`useRouter` only warns; `useMatch`
 * then throws on `router.stores`), so a bare render is unsatisfiable by any
 * correct implementation. Mirrors the harness `ResetPasswordForm.test.tsx`
 * established for the same reason.
 *
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`,
 * and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/consent", route: consentRoute }],
  });
}

describe("/consent route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path itself is the contract and is
    // not this task's to change (router.tsx is off-limits).
    const user = userEvent.setup();
    const handoff = captureHandoff();

    await renderRouteAt(
      `/consent?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}`,
    );

    await expect.element(page.getByTestId("consent-heading")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    // Both query parameters must actually reach the screen, not merely parse:
    // `client_id` threads as far as the request path...
    expect(consentInfoCalls()[0]?.path).toBe(CONSENT_INFO_PATH);

    // ...and `returnUrl` as far as the URL approve builds. A route that dropped
    // it would send the user to the "/" fallback instead.
    await user.click(page.getByTestId("consent-approve"));

    await vi.waitFor(() => {
      expect(handoff.urls).toHaveLength(1);
    });
    expect(submitTarget(handoff.urls)).toBe(GRANTED_TARGET);
  });

  it("reads returnUrl and client_id off the query string", () => {
    // The oracle's two `[SupplyParameterFromQuery]` properties. Note the wire
    // name is `client_id` (snake_case, per `[SupplyParameterFromQuery(Name =
    // "client_id")]`) — it is OpenIddict's parameter name and is not this
    // screen's to rename, even though the prop it feeds is `clientId`.
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch).toBeDefined();
    expect(validateSearch?.({ returnUrl: RETURN_URL, client_id: CLIENT_ID })).toEqual({
      returnUrl: RETURN_URL,
      client_id: CLIENT_ID,
    });
  });

  it("tolerates a query string with neither of them", () => {
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({})).toEqual({
      returnUrl: undefined,
      client_id: undefined,
    });
  });

  it("reads the space-delimited scope off the query string", () => {
    // The third parameter the authorize endpoint now sends (Wallow-dzt4). The
    // wire name is `scope` (singular), matching OAuth and what
    // `AuthorizationController` builds the consent redirect with; the value is
    // kept as the raw string here and split by the screen.
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({ returnUrl: RETURN_URL, client_id: CLIENT_ID, scope: SCOPE })).toEqual(
      { returnUrl: RETURN_URL, client_id: CLIENT_ID, scope: SCOPE },
    );
  });

  it("treats a non-string scope as absent", () => {
    // TanStack Router's default search parser JSON-parses every value BEFORE
    // `validateSearch` sees it (bd memory `tanstack-router-default-search-parser-
    // json-parses-values`), so `?scope=123` arrives as a NUMBER. Same rule the
    // other two parameters already follow: anything non-string is absent.
    const validateSearch = consentRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({ scope: 123 })).toEqual({
      returnUrl: undefined,
      client_id: undefined,
      scope: undefined,
    });
  });

  it("threads the scope from the query string all the way to the lookup", async () => {
    // The end of the chain this bead repairs: authorize redirect -> route search
    // -> screen -> consent-info request. Parsing `scope` without handing it down
    // would leave the consent list just as empty as before.
    await renderRouteAt(
      `/consent?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}` +
        `&scope=${encodeURIComponent(SCOPE)}`,
    );

    await vi.waitFor(() => {
      expect(consentInfoCalls()).toHaveLength(1);
    });

    expect(requestedScopesParameter()).toBe(SCOPE_LIST.join(" "));
  });
});
