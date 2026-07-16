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
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { Route as consentRoute } from "../../../routes/consent";
import { ConsentScreen } from "./ConsentScreen";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the DOM-matcher convention wallow-web's RTL tests
// established and wallow-auth copies.
expect.extend(matchers);

/**
 * Component spec for the Consent screen (Wallow-vec7.3.4), ported from the
 * Blazor oracle `api/src/Wallow.Auth/Components/Pages/Consent.razor`.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `consent-error`, `consent-heading`, `consent-scopes`, `consent-approve`,
 * `consent-deny`. The oracle has no loading testid — see "THE MISSING LOADING
 * STATE" below for why that is a finding, not an omission to paper over.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is the only permitted importer
 * of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-across-
 * graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies and
 * NEVER `vi.resetModules()`.
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
 * apps/wallow-auth's h3 server (`src/lib/auth-server.ts`) is a PASSTHROUGH
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
 * therefore pin what the SCREEN owes the builder (the arguments) and that it
 * navigates to whatever the builder returns, rather than re-deriving the
 * builder's string algebra here.
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
 * *while the request is still in flight*. So the Blazor screen flashes "Unable
 * to load consent information" at every user before its own fetch resolves. The
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

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  getConsentInfo: vi.fn(),
  buildConsentSubmitUrl: vi.fn(),
  isSafeReturnUrl: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: { getConsentInfo: mocks.getConsentInfo },
    oidc: {
      buildConsentSubmitUrl: mocks.buildConsentSubmitUrl,
      isSafeReturnUrl: mocks.isSafeReturnUrl,
    },
  }),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const CLIENT_ID = "wallow-web";
const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * The real `isSafeReturnUrl` rule (packages/sdk/src/auth-oidc.ts), restated here
 * because the screen reaches it through the mocked facade. Restating the rule
 * rather than stubbing `true`/`false` per test keeps these tests pinning the
 * SCREEN's use of the guard against the guard's actual semantics — a screen that
 * passed only because the stub said "safe" would be proving nothing.
 */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

/**
 * The real `buildConsentSubmitUrl` behaviour (packages/sdk/src/auth-oidc.ts),
 * restated for the same reason — in particular that a present-but-unsafe
 * returnUrl THROWS. Stubbing this to a fixed string would let a screen that
 * never guards its returnUrl pass the guard tests.
 *
 * The builder's own string algebra is pinned by its 67 tests in
 * `packages/sdk/src/auth-oidc.test.ts`; it is reproduced here only so this
 * screen meets a builder that behaves like the real one.
 */
function buildConsentSubmitUrlRule(
  origin: string,
  returnUrl: string | null | undefined,
  granted: boolean,
): string {
  let baseUrl: string = "/";

  if (returnUrl !== null && returnUrl !== undefined) {
    if (!isSafeReturnUrlRule(returnUrl)) {
      throw new TypeError(`unsafe return url: ${returnUrl}`);
    }
    baseUrl = returnUrl;
  }

  const separator: string = baseUrl.includes("?") ? "&" : "?";
  const parameter: string = granted ? "consent_granted=true" : "consent_denied=true";

  return `${origin.replace(/\/+$/u, "")}${baseUrl}${separator}${parameter}`;
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

/** A WallowError-shaped rejection, as the real facade's `unwrap()` throws. */
function wallowErrorShaped(status: number): Error & { status: number; code: string } {
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

/**
 * Replace `window.location` with a plain settable object so the screen's full
 * navigation is observable. jsdom refuses `vi.spyOn(window.location, "assign")`
 * ("Cannot redefine property"), but `location` itself is a configurable
 * accessor, so `vi.stubGlobal` swaps it wholesale — and `globalThis === window`
 * under jsdom, so the screen's `window.location.href = …` writes here.
 */
function stubLocation(): { href: string } {
  const location = { href: "" };
  vi.stubGlobal("location", location);
  return location;
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  mocks.buildConsentSubmitUrl.mockImplementation(buildConsentSubmitUrlRule);
  mocks.getConsentInfo.mockResolvedValue(consentInfo());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("ConsentScreen — loading", () => {
  it("requests the consent info for the client in the query string", () => {
    mocks.getConsentInfo.mockReturnValue(new Promise(() => {}));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    // Oracle: `GetConsentInfoAsync(ClientId, Array.Empty<string>())`. Asserted
    // on the first argument alone: the facade treats an omitted `scopes` and an
    // empty array identically (auth-client.ts:185-195, `scopes?.length ? … : …`),
    // so pinning `toHaveBeenCalledWith(CLIENT_ID)` would fail an implementation
    // that faithfully passed the oracle's `Array.Empty<string>()`.
    expect(mocks.getConsentInfo.mock.calls[0]?.[0]).toBe(CLIENT_ID);
  });

  it("requests no scopes, as the oracle does", () => {
    mocks.getConsentInfo.mockReturnValue(new Promise(() => {}));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    // `Array.Empty<string>()` — the screen must not invent a scope list. The
    // scopes being consented to come back FROM this call, they are not an input
    // to it.
    //
    // The `toHaveBeenCalled` precondition is load-bearing: without it,
    // `calls[0]?.[1]` is `undefined` when the screen made NO call at all, and
    // "no scopes were passed" would pass vacuously.
    expect(mocks.getConsentInfo).toHaveBeenCalled();

    const scopes: unknown = mocks.getConsentInfo.mock.calls[0]?.[1];

    expect(scopes === undefined || (Array.isArray(scopes) && scopes.length === 0)).toBe(true);
  });

  it("shows no error while the request is still in flight", () => {
    // The oracle's wart, deliberately not ported: it renders on `_consentInfo is
    // null`, which is also true before the fetch resolves, so it flashes
    // "Unable to load consent information" at every user. See this file's
    // header. A port that copies the null-check literally fails HERE and only
    // here.
    mocks.getConsentInfo.mockReturnValue(new Promise(() => {}));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    // Pin that a request is genuinely IN FLIGHT before asserting the absence.
    // Otherwise a screen that never fetched at all would satisfy "no error
    // while fetching" by never fetching.
    expect(mocks.getConsentInfo).toHaveBeenCalled();
    expect(screen.queryByTestId("consent-error")).toBeNull();
  });

  it("shows no consent prompt before the client is known", () => {
    // The other half of the same contract: nothing is rendered in flight, so the
    // user cannot approve access for an application we have not identified yet.
    mocks.getConsentInfo.mockReturnValue(new Promise(() => {}));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    expect(mocks.getConsentInfo).toHaveBeenCalled();
    expect(screen.queryByTestId("consent-heading")).toBeNull();
    expect(screen.queryByTestId("consent-approve")).toBeNull();
    expect(screen.queryByTestId("consent-deny")).toBeNull();
  });

  it("fires the request exactly once", async () => {
    // The request is a side effect of mounting, not of rendering.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-heading");

    expect(mocks.getConsentInfo).toHaveBeenCalledTimes(1);
  });
});

describe("ConsentScreen — the consent prompt", () => {
  it("names the requesting application in the heading", async () => {
    // Oracle: `<h2>@_consentInfo.DisplayName is requesting access</h2>`.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    const heading: HTMLElement = await screen.findByTestId("consent-heading");

    expect(heading).toHaveTextContent(/Wallow Web is requesting access/u);
  });

  it("falls back to the client id when the client has no display name", async () => {
    // `displayName` is `null | string` on the generated `ConsentInfoResponse`.
    // The oracle interpolates it unguarded, so a null renders the sentence
    // " is requesting access" — a consent prompt that does not say WHO is
    // asking. The port names the client instead; consent to an unnamed party is
    // not consent.
    mocks.getConsentInfo.mockResolvedValue(consentInfo({ displayName: null }));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    const heading: HTMLElement = await screen.findByTestId("consent-heading");

    expect(heading).toHaveTextContent(CLIENT_ID);
    expect(heading).toHaveTextContent(/is requesting access/u);
  });

  it("lists every requested scope", async () => {
    // Oracle: `@foreach (ConsentScopeInfo scope in _consentInfo.RequestedScopes)
    // { <div>@scope.Name</div> }`.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    const scopes: HTMLElement = await screen.findByTestId("consent-scopes");

    expect(scopes).toHaveTextContent("openid");
    expect(scopes).toHaveTextContent("profile");
  });

  it("lists no scope the client did not request", async () => {
    // The scope list is the whole substance of the decision — it must be the
    // server's list, not a superset.
    mocks.getConsentInfo.mockResolvedValue(
      consentInfo({ requestedScopes: [{ name: "openid", description: "Sign you in" }] }),
    );

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    const scopes: HTMLElement = await screen.findByTestId("consent-scopes");

    expect(scopes).toHaveTextContent("openid");
    expect(scopes).not.toHaveTextContent("profile");
  });

  it("renders the prompt for a client requesting no scopes", async () => {
    // `RequestedScopes` is non-nullable but may be empty; the oracle's foreach
    // simply renders nothing. The prompt must still work rather than crash.
    mocks.getConsentInfo.mockResolvedValue(consentInfo({ requestedScopes: [] }));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    expect(await screen.findByTestId("consent-scopes")).toBeInTheDocument();
    expect(screen.getByTestId("consent-approve")).toBeInTheDocument();
  });

  it("offers both an approve and a deny action", async () => {
    // Oracle: two BbButtons. Deny must always be present — a consent screen with
    // only an approve path is not a consent screen.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-heading");

    expect(screen.getByTestId("consent-approve")).toBeInTheDocument();
    expect(screen.getByTestId("consent-deny")).toBeInTheDocument();
  });

  it("shows no error alongside a loaded prompt", async () => {
    // Oracle's if/else — the error block and the prompt are mutually exclusive.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-heading");

    expect(screen.queryByTestId("consent-error")).toBeNull();
  });

  it("drops the pre-registration placeholder marker", async () => {
    // Wallow-vec7.3.16 shipped `route-placeholder` as scaffolding; it must not
    // survive into the real screen.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-heading");

    expect(screen.queryByTestId("route-placeholder")).toBeNull();
  });
});

describe("ConsentScreen — approve", () => {
  it("navigates to the consent-granted URL the builder returns", async () => {
    const location = stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await userEvent.click(await screen.findByTestId("consent-approve"));

    // A FULL navigation, not `router.navigate`: `/connect/authorize` is served
    // by the h3 reverse proxy (src/lib/auth-server.ts), not by the client-side
    // route tree — the router has no route for it and would 404 in-app.
    expect(location.href).toBe(`${RETURN_URL}&consent_granted=true`);
  });

  it("builds the URL same-origin, granting consent", async () => {
    stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await userEvent.click(await screen.findByTestId("consent-approve"));

    // The origin divergence, pinned explicitly (see this file's header): `""`,
    // NOT the oracle's `ApiBaseUrl`. This app's own origin hosts
    // `/connect/**` via the passthrough proxy, and it has no browser-reachable
    // API origin to prepend even if it wanted one.
    expect(mocks.buildConsentSubmitUrl).toHaveBeenCalledWith("", RETURN_URL, true);
  });

  it("appends to a returnUrl that has no query string of its own", async () => {
    // Oracle: `separator = baseUrl.Contains('?') ? "&" : "?"`. Pinned through
    // the screen so a port that hand-rolls string concatenation instead of
    // calling the builder cannot pass by only ever being tested with a
    // `?`-bearing returnUrl.
    const location = stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl="/connect/authorize" />);
    await userEvent.click(await screen.findByTestId("consent-approve"));

    expect(location.href).toBe("/connect/authorize?consent_granted=true");
  });

  it("falls back to the root when the link carries no returnUrl", async () => {
    // Oracle: `string baseUrl = ReturnUrl ?? "/"`. Nullish ONLY — and an absent
    // returnUrl must NOT be treated as the unsafe-returnUrl case: there is
    // nothing hostile about a link that omits it, so the guard must not fire.
    const location = stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} />);
    await userEvent.click(await screen.findByTestId("consent-approve"));

    expect(location.href).toBe("/?consent_granted=true");
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("does not deny while approving", async () => {
    stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await userEvent.click(await screen.findByTestId("consent-approve"));

    // The two handlers differ by one boolean; a mis-wired button would be
    // invisible to a test that only checked that SOME navigation happened.
    expect(mocks.buildConsentSubmitUrl).not.toHaveBeenCalledWith("", RETURN_URL, false);
  });
});

describe("ConsentScreen — deny", () => {
  it("navigates to the consent-denied URL the builder returns", async () => {
    const location = stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await userEvent.click(await screen.findByTestId("consent-deny"));

    expect(location.href).toBe(`${RETURN_URL}&consent_denied=true`);
  });

  it("builds the URL same-origin, refusing consent", async () => {
    stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await userEvent.click(await screen.findByTestId("consent-deny"));

    expect(mocks.buildConsentSubmitUrl).toHaveBeenCalledWith("", RETURN_URL, false);
  });

  it("reports the denial to the authorize endpoint rather than staying put", async () => {
    // Oracle: Deny navigates, exactly as Approve does. A deny that silently did
    // nothing would strand the user on a dead consent screen and leave the
    // relying party's authorize request hanging — the denial has to be
    // DELIVERED.
    const location = stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await userEvent.click(await screen.findByTestId("consent-deny"));

    expect(location.href).not.toBe("");
    expect(location.href).toContain("consent_denied=true");
  });

  it("does not grant while denying", async () => {
    const stubbed = stubLocation();

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);
    await userEvent.click(await screen.findByTestId("consent-deny"));

    // The button-swap bug, from the side that matters: a Deny wired to
    // `granted: true` would authorize the client the user just refused.
    expect(mocks.buildConsentSubmitUrl).not.toHaveBeenCalledWith("", RETURN_URL, true);
    expect(stubbed.href).not.toContain("consent_granted");
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
      renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // Refusing at CLICK time would be too late: the user would be asked to
      // authorize a request we had already decided was malformed.
      expect(screen.queryByTestId("consent-approve")).toBeNull();
      expect(screen.queryByTestId("consent-heading")).toBeNull();
    });

    it(`routes to the error page for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      // `Login.razor` L533-540's bail, and bd memory
      // `returnurl-guard-refuse-dont-sanitize`: REFUSE, do not silently fall
      // back to "/". `href` rather than `to`+`search` — see this file's header.
      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
      });
    });

    it(`never navigates to the unsafe returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      const stubbed = stubLocation();

      renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // The criterion itself: whatever else happens, the browser must not be
      // sent to the attacker's URL.
      expect(stubbed.href).toBe("");
    });

    it(`does not fetch consent info for returnUrl ${JSON.stringify(returnUrl)}`, async () => {
      renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={returnUrl} />);

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalled();
      });

      // The link is already known to be malformed, so there is nothing to ask
      // the server about. Bailing before the request also keeps the client's
      // display name and scope list from being disclosed to an attacker-crafted
      // link.
      expect(mocks.getConsentInfo).not.toHaveBeenCalled();
    });
  }

  it("shows no consent error for an unsafe returnUrl", async () => {
    // The user is being sent to `/error`; flashing "Unable to load consent
    // information" on the way out would misreport an open-redirect attempt as a
    // transient server problem.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl="//evil.example" />);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });

    expect(screen.queryByTestId("consent-error")).toBeNull();
  });

  it("guards the returnUrl even when no client id is supplied", async () => {
    // The two refusal paths must not mask each other: a hostile returnUrl on a
    // link that also omits `client_id` is still a hostile returnUrl, and must
    // reach `/error` rather than being absorbed by the missing-client branch.
    renderWithClient(<ConsentScreen returnUrl="//evil.example" />);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
  });

  it("lets a safe returnUrl through untouched", async () => {
    // The negative control: the guard must not be so eager that it breaks the
    // ordinary flow. A screen that routed EVERY returnUrl to `/error` would pass
    // every other test in this block.
    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-approve");

    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});

describe("ConsentScreen — error state", () => {
  it("shows the error when no client id is supplied", async () => {
    // Oracle: `if (ClientId is not null) { … }` — no client id, no call, so
    // `_consentInfo` stays null and the error block renders.
    renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    const error: HTMLElement = await screen.findByTestId("consent-error");

    expect(error).toHaveTextContent(/unable to load consent information/iu);
  });

  it("does not call the endpoint when no client id is supplied", async () => {
    // A screen that "helpfully" sent `clientId: undefined` would 404 and blame
    // the server for the link's own defect.
    renderWithClient(<ConsentScreen returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-error");

    expect(mocks.getConsentInfo).not.toHaveBeenCalled();
  });

  it("treats an empty-string client id as missing", async () => {
    // `?client_id=&returnUrl=…` is a malformed link, not a client to look up.
    renderWithClient(<ConsentScreen clientId="" returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-error");

    expect(mocks.getConsentInfo).not.toHaveBeenCalled();
  });

  it("shows the error when the consent-info request fails", async () => {
    // `AuthApiClient.GetConsentInfoAsync` returns null on any non-2xx and the
    // oracle renders the error block. Through this seam the same failure is a
    // rejection, because `unwrap()` throws.
    mocks.getConsentInfo.mockRejectedValue(wallowErrorShaped(404));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    const error: HTMLElement = await screen.findByTestId("consent-error");

    expect(error).toHaveTextContent(/unable to load consent information/iu);
  });

  it("shows the same error for a server failure", async () => {
    // The oracle has ONE error message for every failure — no status narrowing,
    // so the WallowError code-loss gotcha costs this screen nothing.
    mocks.getConsentInfo.mockRejectedValue(wallowErrorShaped(500));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    expect(await screen.findByTestId("consent-error")).toHaveTextContent(
      /unable to load consent information/iu,
    );
  });

  it("survives a rejection that is not WallowError-shaped at all", async () => {
    // A network failure has no `status`; it must land on the same error surface
    // rather than throwing inside the error branch.
    mocks.getConsentInfo.mockRejectedValue(new Error("network down"));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    expect(await screen.findByTestId("consent-error")).toBeInTheDocument();
  });

  it("offers no approve or deny action in the error state", async () => {
    // Oracle's if/else. This is the important half: with no consent info there
    // is no scope list, so an Approve button here would authorize an unknown
    // client for unknown scopes.
    mocks.getConsentInfo.mockRejectedValue(wallowErrorShaped(404));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-error");

    expect(screen.queryByTestId("consent-approve")).toBeNull();
    expect(screen.queryByTestId("consent-deny")).toBeNull();
    expect(screen.queryByTestId("consent-heading")).toBeNull();
    expect(screen.queryByTestId("consent-scopes")).toBeNull();
  });

  it("never leaks the raw rejection into the page", async () => {
    // `code: "UNKNOWN"` / `title: "Unknown error"` are seam artefacts, not
    // user-facing copy. The oracle shows one curated message.
    mocks.getConsentInfo.mockRejectedValue(wallowErrorShaped(404));

    renderWithClient(<ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} />);

    await screen.findByTestId("consent-error");

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
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    consentRoute.update({ id: "/consent", path: "/consent", getParentRoute: () => rootRoute }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/consent route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path itself is the contract and is
    // not this task's to change (router.tsx is off-limits).
    const user = userEvent.setup();
    const location = stubLocation();

    renderRouteAt(`/consent?client_id=${CLIENT_ID}&returnUrl=${encodeURIComponent(RETURN_URL)}`);

    expect(await screen.findByTestId("consent-heading")).toBeInTheDocument();
    expect(screen.queryByTestId("route-placeholder")).toBeNull();
    // Both query parameters must actually reach the screen, not merely parse:
    // `client_id` threads as far as the request...
    expect(mocks.getConsentInfo.mock.calls[0]?.[0]).toBe(CLIENT_ID);

    // ...and `returnUrl` as far as the URL approve navigates to. A route that
    // dropped it would send the user to the "/" fallback instead.
    await user.click(screen.getByTestId("consent-approve"));

    expect(location.href).toBe(`${RETURN_URL}&consent_granted=true`);
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
});
