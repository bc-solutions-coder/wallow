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
import { ExternalProviders } from "./ExternalProviders";

expect.extend(matchers);

/**
 * Component spec for the Login screen's EXTERNAL PROVIDER list
 * (Wallow-vec7.3.14 / 2.8d), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Login.razor` (the `_externalProviders`
 * block, L189-209 + `GetExternalLoginUrl` L312-317).
 *
 * This is the fourth bead of a five-bead chain over one screen. `.3.11` fixed the
 * structure and this spec honours it: the provider list is NOT a login panel (it
 * implements no `LoginPanelProps`, owns no mutation and reports nothing up), it
 * is a SECTION the shell renders next to `TabPanel`, gated on `signedIn` exactly
 * as the oracle gates it inside the `else` of `if (_signedIn)`.
 *
 * Testids: the oracle tags NOTHING here — bd memory `wallow-auth-blazor-oracle-
 * testid-gaps-api-src` records the gap and mandates `{page}-{element}` names, so
 * these are INVENTED per the scout's mandate:
 *
 *     login-external-providers      the container (oracle: the whole `@if` block)
 *     login-external-{provider}     one per challenge link (kebab-cased name)
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4/.3.6/.3.11) ─────────
 *
 * The oracle builds `{ApiBaseUrl}/v1/identity/auth/external-login?…`. That prepend
 * is NOT ported. It matters MORE here than anywhere else in the chain: this link
 * starts the OIDC challenge, and the whole external-login handshake rides
 * SameSite cookies that a cross-origin top-level GET would drop. This app's h3
 * server mounts `/v1/**` and `/connect/**` at the ROOT (`src/lib/auth-server.ts`),
 * so the same-origin path IS the endpoint. `pointsAtThisOrigin` pins it in both
 * directions — the path must be right AND the API origin must not appear.
 *
 * ── NO `isSafeReturnUrl` GUARD (deferred — and this is the load-bearing call) ──
 *
 * bd memory `guard-where-the-client-picks-the-destination-defer`: guard where the
 * CLIENT picks the destination, defer where the SERVER does. Here the client
 * picks a same-origin CONSTANT path (`/v1/identity/auth/external-login`) and
 * `returnUrl` is inert query CARGO. `AccountController.ExternalLogin`
 * (api/.../Controllers/AccountController.cs:242-265) re-validates it FAIL-CLOSED
 * before doing anything at all:
 *
 *     if (string.IsNullOrEmpty(returnUrl) || !await redirectUriValidator.IsAllowedAsync(returnUrl))
 *         return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
 *
 * and `ExternalLoginCallback` (:274) validates it a second time. The open-redirect
 * decision is the API's, made server-side, against the OpenIddict allow-list.
 *
 * Wiring `isSafeReturnUrl` in here would not harden anything — it would BREAK
 * everything. `IsAllowedAsync` (OpenIddictRedirectUriValidator.cs:24) accepts ONLY
 * `Uri.TryCreate(…, UriKind.Absolute, …)` values; `isSafeReturnUrl`
 * (packages/sdk/src/auth-oidc.ts:55) accepts ONLY `startsWith('/') &&
 * !startsWith('//')`. The accept-sets are provably DISJOINT, so the guard would
 * refuse every value the server can actually honour. That is not a hypothetical:
 * `.3.6` shipped exactly that mistake on the MFA hand-off and dead-ended 100% of
 * external-login users at `/error` until `.3.17` fixed it.
 *
 * So this spec pins the deferral from BOTH poles, because a guard tested only
 * against attacks cannot tell "correct" from "refuses everything":
 *
 *   REAL-TRAFFIC POLE  `keepsAnAbsoluteReturnUrl` — the ABSOLUTE, allow-listed
 *     shape must still produce a working link. This is the .3.17 regression test.
 *   DEFERRAL POLE      `neverConsultsTheRelativeOnlyGuard` — the guard must not be
 *     consulted at all; if a later edit wires it in, this fails before it ships.
 *
 * What a deferred guard DOES owe is INJECTION, and that is pinned too
 * (`encodesTheReturnUrlAsASingleQueryValue`): ASP.NET binds a duplicated
 * `[FromQuery]` value as `"a,b"`, so unencoded cargo carrying `&provider=` could
 * change which identity provider the user is challenged against.
 */

const mocks = vi.hoisted(() => ({
  getExternalProviders: vi.fn(),
  isSafeReturnUrl: vi.fn(),
  // The shell + password panel reach for these when the route-level tests below
  // render the whole screen. Owned by `.3.11`; mocked here only to host it.
  login: vi.fn(),
  buildExchangeTicketUrl: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      getExternalProviders: mocks.getExternalProviders,
      login: mocks.login,
    },
    oidc: {
      isSafeReturnUrl: mocks.isSafeReturnUrl,
      buildExchangeTicketUrl: mocks.buildExchangeTicketUrl,
    },
  }),
}));

/**
 * The returnUrl `/connect/authorize` sends a DIRECT sign-in (relative by
 * construction: `AuthorizationController.cs:53` + `Url.IsLocalUrl` at :62).
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/**
 * The returnUrl the EXTERNAL-LOGIN path really carries: ABSOLUTE and
 * origin-allow-listed, the only shape `IsAllowedAsync` can accept. `isSafeReturnUrl`
 * returns FALSE for this — which is precisely why it must not be consulted.
 */
const ABSOLUTE_RETURN_URL = "https://app.wallow.test/connect/authorize?client_id=web";

/** The current page URL, the oracle's `Navigation.Uri` fallback (absolute). */
const CURRENT_URL = "http://localhost:5002/login?returnUrl=%2Fconnect%2Fauthorize";

/** The endpoint the oracle's `GetExternalLoginUrl` targets, sans `ApiBaseUrl`. */
const EXTERNAL_LOGIN_PATH = "/v1/identity/auth/external-login";

/** The origin the oracle prepends and this port does not. */
const API_ORIGIN = "localhost:5001";

/** The oracle's `GetExternalProvidersAsync` body: `Ok(List<string>)` of display names. */
const PROVIDERS = ["Google", "Microsoft"];

/** The real `isSafeReturnUrl` rule, mirrored (screens may not import the SDK). */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement, client: QueryClient = newClient()) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/** The query key `ExternalProviders` fetches under. */
const PROVIDERS_QUERY_KEY = ["external-providers"];

/**
 * Wait until the provider query has actually SETTLED.
 *
 * Every "renders nothing" test below needs this, and the obvious alternative is a
 * trap that this spec fell into once already: `await waitFor(() =>
 * expect(getExternalProviders).toHaveBeenCalled())` resolves the instant the query
 * function is INVOKED — synchronously, on mount — which is BEFORE the promise
 * resolves and the component re-renders. The DOM assertion then runs against a
 * still-pending render, so "nothing is on screen" is true of every possible
 * implementation and the test pins nothing. A mutant that half-trusted a malformed
 * provider list survived precisely that hole. Waiting on the query's own state is
 * the only honest signal that the narrowing has had its chance to run.
 */
async function settleProviders(client: QueryClient): Promise<void> {
  await waitFor(() => {
    expect(client.getQueryState(PROVIDERS_QUERY_KEY)?.status).not.toBe("pending");
  });
}

/**
 * `"returnUrl" in props` rather than `??`: the no-returnUrl branch (the oracle's
 * `Navigation.Uri` fallback) and the `""` branch are both under test, and a `??`
 * default would silently substitute for an explicit `undefined` — bd memory
 * `red-phase-render-helpers-must-distinguish-explicit-undefined`.
 */
function renderProviders(props: { returnUrl?: string } = {}): QueryClient {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;
  const client: QueryClient = newClient();

  renderWithClient(<ExternalProviders returnUrl={returnUrl} />, client);

  return client;
}

/** jsdom refuses to redefine `location`; `location` itself is a configurable accessor. */
function stubLocation(href: string): void {
  vi.stubGlobal("location", { href });
}

/** The rendered challenge link for a provider, once the query has settled. */
async function providerLink(testid: string): Promise<HTMLElement> {
  return await screen.findByTestId(testid);
}

/** The `returnUrl` value the link actually carries, decoded by a real URL parser. */
function returnUrlParamOf(link: HTMLElement): string | null {
  const href: string = link.getAttribute("href") ?? "";

  return new URL(href, "http://localhost:5002").searchParams.get("returnUrl");
}

/** The `provider` value the link actually carries, decoded by a real URL parser. */
function providerParamOf(link: HTMLElement): string | null {
  const href: string = link.getAttribute("href") ?? "";

  return new URL(href, "http://localhost:5002").searchParams.get("provider");
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  mocks.getExternalProviders.mockResolvedValue(PROVIDERS);
  // No signInTicket and no returnUrl is the shell's `signed-in` disposition
  // (`auth-result.ts:285`) — the one state that retires the provider list.
  mocks.login.mockResolvedValue({ succeeded: true });
  stubLocation(CURRENT_URL);
});

describe("ExternalProviders — the list", () => {
  it("renders one challenge link per provider the API reports", async () => {
    renderProviders();

    expect(await providerLink("login-external-google")).toBeInTheDocument();
    expect(screen.getByTestId("login-external-microsoft")).toBeInTheDocument();
  });

  it("labels each link with the provider's display name", async () => {
    renderProviders();

    expect(await providerLink("login-external-google")).toHaveTextContent("Google");
    expect(screen.getByTestId("login-external-microsoft")).toHaveTextContent("Microsoft");
  });

  it("renders the oracle's separator copy above the list", async () => {
    renderProviders();

    await providerLink("login-external-google");
    expect(screen.getByTestId("login-external-providers")).toHaveTextContent("Or continue with");
  });

  it("kebab-cases a multi-word provider name into its testid", async () => {
    // `GetExternalProviders` returns `s.DisplayName ?? s.Name` — display names are
    // prose ("Microsoft Entra ID"), so the testid cannot be the raw name.
    mocks.getExternalProviders.mockResolvedValue(["Microsoft Entra ID"]);
    renderProviders();

    expect(await providerLink("login-external-microsoft-entra-id")).toHaveTextContent(
      "Microsoft Entra ID",
    );
  });

  it("asks the API for the provider list exactly once", async () => {
    renderProviders();

    await providerLink("login-external-google");
    expect(mocks.getExternalProviders).toHaveBeenCalledTimes(1);
  });

  it("renders nothing at all when no providers are configured", async () => {
    // The oracle's `@if (_externalProviders.Count > 0)` — a bare "Or continue
    // with" separator over an empty grid is worse than no section.
    mocks.getExternalProviders.mockResolvedValue([]);
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(screen.queryByTestId("login-external-providers")).toBeNull();
  });

  it("renders nothing while the provider list is still in flight", async () => {
    // This one must NOT use `settleProviders` — the promise never resolves, which
    // is the whole point. It pins that the section waits for real data rather than
    // flashing an empty "Or continue with" separator on first paint. Its assertion
    // is deliberately paired with the query being genuinely PENDING, so it cannot
    // silently become a second copy of the "no providers" test.
    mocks.getExternalProviders.mockReturnValue(new Promise(() => {}));
    const client: QueryClient = renderProviders();

    await waitFor(() => {
      expect(mocks.getExternalProviders).toHaveBeenCalled();
    });
    expect(client.getQueryState(PROVIDERS_QUERY_KEY)?.status).toBe("pending");
    expect(screen.queryByTestId("login-external-providers")).toBeNull();
  });

  it("renders nothing, and does not throw, when the provider call fails", async () => {
    // The oracle awaits this in `OnInitializedAsync` with no try/catch, so a
    // failure takes the whole page down. Not ported: password sign-in is still
    // perfectly usable without the social buttons, so this degrades to the
    // Count == 0 rendering rather than destroying the screen around it.
    mocks.getExternalProviders.mockRejectedValue(new TypeError("Failed to fetch"));
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(screen.queryByTestId("login-external-providers")).toBeNull();
  });

  it("renders nothing when the body is not a list at all", async () => {
    // `getExternalProviders` is typed `Promise<unknown>` — the screen owns the
    // narrowing at its boundary (bd memory `untyped-sdk-response-fail-closed-
    // pattern-wallow-auth`). No cast, structural check, fail closed.
    mocks.getExternalProviders.mockResolvedValue({ providers: PROVIDERS });
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(screen.queryByTestId("login-external-providers")).toBeNull();
  });

  it("refuses a list that is not entirely non-empty strings", async () => {
    // Fail-closed on the WHOLE body rather than filtering the good entries out of
    // a bad one: a list this shape means the endpoint is not what we think it is,
    // and half-trusting it would put a link built from `String(null)` on screen.
    mocks.getExternalProviders.mockResolvedValue(["Google", null, ""]);
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(screen.queryByTestId("login-external-providers")).toBeNull();
  });
});

describe("ExternalProviders — the challenge URL", () => {
  it("points at this origin's external-login endpoint, never the API origin", async () => {
    // BOTH directions, per the .3.4/.3.11 origin pin: the path must be right AND
    // the oracle's `ApiBaseUrl` prepend must be absent. A cross-origin top-level
    // GET here drops the SameSite cookies the whole handshake rides on.
    renderProviders();

    const link: HTMLElement = await providerLink("login-external-google");
    const href: string = link.getAttribute("href") ?? "";

    expect(href.startsWith(`${EXTERNAL_LOGIN_PATH}?`)).toBe(true);
    expect(href).not.toContain(API_ORIGIN);
    expect(href).not.toContain("http://");
  });

  it("names the provider it challenges", async () => {
    renderProviders();

    expect(providerParamOf(await providerLink("login-external-google"))).toBe("Google");
    expect(providerParamOf(screen.getByTestId("login-external-microsoft"))).toBe("Microsoft");
  });

  it("carries the returnUrl the OIDC flow handed the screen", async () => {
    renderProviders();

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(RETURN_URL);
  });

  it("keeps an absolute, allow-listed returnUrl instead of refusing it", async () => {
    // THE REAL-TRAFFIC POLE, and the `.3.17` regression test. `IsAllowedAsync`
    // accepts ONLY absolute URLs, so this is the shape the server can actually
    // honour. If a future edit wires `isSafeReturnUrl` in here, this link stops
    // existing (or loses its cargo) and 100% of external sign-in dies — an outage
    // wearing a security feature's clothes.
    renderProviders({ returnUrl: ABSOLUTE_RETURN_URL });

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(ABSOLUTE_RETURN_URL);
  });

  it("never consults the relative-only returnUrl guard", async () => {
    // The DEFERRAL POLE. The destination is a same-origin constant path and the
    // server re-validates fail-closed (AccountController.cs:257), so the guard has
    // no business here — and its accept-set is disjoint from what arrives.
    renderProviders({ returnUrl: ABSOLUTE_RETURN_URL });

    await providerLink("login-external-google");
    expect(mocks.isSafeReturnUrl).not.toHaveBeenCalled();
  });

  it("encodes the returnUrl as a single query value", async () => {
    // The injection guard a DEFERRED open-redirect guard still owes. ASP.NET binds
    // a duplicated `[FromQuery]` as "a,b", so raw cargo carrying `&provider=` could
    // silently change which identity provider the user is challenged against.
    const hostile = "/connect/authorize?a=1&provider=evil-idp&returnUrl=https://evil.example.com";
    renderProviders({ returnUrl: hostile });

    const link: HTMLElement = await providerLink("login-external-google");

    expect(returnUrlParamOf(link)).toBe(hostile);
    expect(providerParamOf(link)).toBe("Google");
  });

  it("encodes the provider name as a single query value", async () => {
    // The oracle escapes only the returnUrl (`GetExternalLoginUrl` L315-316). The
    // provider name is escaped too: it is API-supplied prose, not a URL token.
    mocks.getExternalProviders.mockResolvedValue(["Ac&me returnUrl=https://evil.example.com"]);
    renderProviders();

    const link: HTMLElement = await providerLink(
      "login-external-ac-me-returnurl-https-evil-example-com",
    );

    expect(providerParamOf(link)).toBe("Ac&me returnUrl=https://evil.example.com");
    expect(returnUrlParamOf(link)).toBe(RETURN_URL);
  });

  it("falls back to the current page URL when the link carried no returnUrl", async () => {
    // The oracle's `ReturnUrl ?? currentUrl` (`Navigation.Uri`, L314): a user who
    // reached /login directly and signs in with Google must land back where they
    // started, not at a dead end.
    renderProviders({ returnUrl: undefined });

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(CURRENT_URL);
  });

  it("falls back to the current page URL for an empty returnUrl", async () => {
    // The oracle's `??` passes `""` THROUGH (it is not null), and the server then
    // bounces the user to /error via its `IsNullOrEmpty` arm. That is a dead link
    // rendered on purpose; the fallback that already exists for `undefined` serves
    // the identical user with an identical intent, so `""` takes it too. Disclosed
    // as a deliberate divergence on the bead.
    renderProviders({ returnUrl: "" });

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(CURRENT_URL);
  });

  it("preserves client_id inside the returnUrl rather than as a parameter of its own", async () => {
    // DIVERGENCE FROM THE BEAD'S ACCEPTANCE TEXT, disclosed on the bead. The
    // acceptance asks that each link preserve `returnUrl`/`client_id`. There is no
    // `client_id` to preserve at this seam: `ExternalLogin([FromQuery] string
    // provider, [FromQuery] string returnUrl)` (AccountController.cs:242) binds no
    // such parameter, and the oracle's `GetExternalLoginUrl` sends none. The
    // client_id IS preserved — it rides INSIDE returnUrl, which is the
    // `/connect/authorize?client_id=…` request the challenge resumes. Adding a
    // top-level `client_id=` would be inventing cargo the server discards.
    renderProviders();

    const link: HTMLElement = await providerLink("login-external-google");

    expect(new URL(link.getAttribute("href") ?? "", "http://x").searchParams.get("client_id")).toBe(
      null,
    );
    expect(returnUrlParamOf(link)).toContain("client_id=web");
  });
});

/**
 * The shell integration `.3.11` mandated: the provider list is a SECTION rendered
 * next to `TabPanel`, gated on `signedIn` — not a tab panel.
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

describe("/login route — external providers", () => {
  it("renders the provider list on the login screen", async () => {
    renderRouteAt(`/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=web`);

    expect(await providerLink("login-external-google")).toBeInTheDocument();
  });

  it("threads returnUrl out of the query string into the challenge link", async () => {
    renderRouteAt(`/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=web`);

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(RETURN_URL);
  });

  it("offers the providers on every tab, not just the password one", async () => {
    // The oracle's `@if (_externalProviders.Count > 0)` sits OUTSIDE the tab
    // `else if` chain — "Or continue with" is an alternative to all three tabs.
    const user = userEvent.setup();
    renderRouteAt("/login");

    await providerLink("login-external-google");
    await user.click(screen.getByTestId("login-tab-otp"));

    expect(screen.getByTestId("login-external-google")).toBeInTheDocument();
  });

  it("retires the provider list once the user is signed in", async () => {
    // The oracle nests the whole `@if (_externalProviders.Count > 0)` block inside
    // the `else` of `if (_signedIn)` — offering "or continue with Google" under a
    // "you are now signed in" alert invites the user to start over.
    //
    // Asserted present BEFORE the sign-in, not just absent after: an "is absent"
    // assertion alone passes for a component that renders nothing at all, which is
    // exactly what the scaffold does. This must fail red for the right reason.
    const user = userEvent.setup();
    renderRouteAt("/login");

    expect(await providerLink("login-external-google")).toBeInTheDocument();

    await user.type(await screen.findByTestId("login-email"), "user@example.com");
    await user.type(screen.getByTestId("login-password"), "Sup3rSecret!");
    await user.click(screen.getByTestId("login-submit"));

    expect(await screen.findByTestId("login-signed-in")).toBeInTheDocument();
    expect(screen.queryByTestId("login-external-google")).toBeNull();
    expect(screen.queryByTestId("login-external-providers")).toBeNull();
  });
});
