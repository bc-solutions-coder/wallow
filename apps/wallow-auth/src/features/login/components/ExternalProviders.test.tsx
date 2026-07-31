import { isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import {
  createTestQueryClient,
  renderWithWallow,
} from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { QueryClient } from "@bc-solutions-coder/query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as loginRoute } from "@app/routes/login";
import { accountGetExternalProvidersQueryKey } from "../api";
import { ExternalProviders } from "./ExternalProviders";

/**
 * The login screen's external-provider list.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so assertions read the
 * recorded request, not a spy.
 *
 * The challenge link must NOT consult `isSafeReturnUrl`: the server re-validates
 * against an allow-list of ABSOLUTE urls that the relative-only guard rejects,
 * so wiring it in refuses real traffic rather than hardening anything.
 */

/** The returnUrl a DIRECT sign-in carries: relative by construction. */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/**
 * The returnUrl the EXTERNAL-LOGIN path really carries: ABSOLUTE and
 * origin-allow-listed, the only shape the server can accept. `isSafeReturnUrl`
 * returns FALSE for it — which is precisely why it must not be consulted.
 */
const ABSOLUTE_RETURN_URL = "https://app.wallow.test/connect/authorize?client_id=web";

/** The challenge endpoint, same-origin — no API origin prepended. */
const EXTERNAL_LOGIN_PATH = "/v1/identity/auth/external-login";

/** An API origin the challenge link must never name. */
const API_ORIGIN = "localhost:5001";

/** The endpoint's body: a list of provider display names. */
const PROVIDERS = ["Google", "Microsoft"];

/** Where the provider list comes from. */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

/** Reached only by the route-level tests below. */
const LOGIN_ENDPOINT = "/v1/identity/auth/login";

const NOT_FOUND_STATUS = 404;

let harness: SdkHarness;

/**
 * How the fake transport answers the provider-list GET. Reprogrammed per test —
 * the dispatcher installed in `beforeEach` reads it on every call, so a test can
 * change the endpoint's behaviour without re-stating the other endpoints.
 */
let providersReply: () => Response | Promise<Response>;

/** The provider-list body every subsequent request answers with, at 200. */
function respondWithProviders(body: unknown): void {
  providersReply = () => Response.json(body);
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement, queryClient: QueryClient = createTestQueryClient()) {
  return renderWithWallow(ui, { harness, queryClient });
}

/**
 * The query key the list fetches under, asked of the generating factory rather
 * than spelled as a literal: the key carries the client's `baseUrl`, so a
 * drifted literal makes `getQueryState` return `undefined` rather than fail,
 * quietly turning {@link settleProviders} into a no-op wait.
 */
function providersQueryKey() {
  return accountGetExternalProvidersQueryKey({ client: harness.client });
}

/** Every recorded request to the provider-list endpoint, in order. */
function providerCalls() {
  return harness.calls.filter((call) => call.path === PROVIDERS_ENDPOINT);
}

/**
 * Wait until the provider query has actually SETTLED. Every "renders nothing"
 * test needs this: waiting on the REQUEST instead resolves the instant the query
 * function is invoked, BEFORE the response is parsed, so the DOM assertion runs
 * against a still-pending render and is true of every implementation.
 */
async function settleProviders(client: QueryClient): Promise<void> {
  await vi.waitFor(() => {
    expect(client.getQueryState(providersQueryKey())?.status).not.toBe("pending");
  });
}

/**
 * `"returnUrl" in props` rather than `??`: the no-returnUrl branch and the `""`
 * branch are both under test, and a `??` default would silently substitute for
 * an explicit `undefined`.
 */
function renderProviders(props: { returnUrl?: string } = {}): QueryClient {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;
  const client: QueryClient = createTestQueryClient();

  renderWithClient(<ExternalProviders returnUrl={returnUrl} />, client);

  return client;
}

/** The rendered challenge link for a provider, once the query has settled. */
async function providerLink(testid: string): Promise<HTMLElement> {
  const locator = page.getByTestId(testid);

  await expect.element(locator).toBeInTheDocument();

  return locator.element() as HTMLElement;
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
  harness = createAuthHarness();
  respondWithProviders(PROVIDERS);
  harness.respond((call) => {
    if (call.path === PROVIDERS_ENDPOINT) {
      return providersReply();
    }

    // No signInTicket and no returnUrl is the shell's `signed-in` disposition —
    // the one state that retires the provider list.
    if (call.path === LOGIN_ENDPOINT) {
      return Response.json({ succeeded: true });
    }

    // The route-level tests carry `client_id=web`, so `/login` also asks for that
    // client's branding overlay. A bare 404 is "no branding configured".
    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

describe("ExternalProviders — the list", () => {
  it("renders one challenge link per provider the API reports", async () => {
    renderProviders();

    await expect.element(page.getByTestId("login-external-google")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-external-microsoft")).toBeInTheDocument();
  });

  it("announces each challenge link as a link, not a button", async () => {
    // These carry the catalog Button's recipe through `render={<a href/>}`, and
    // Base UI stamps `role="button"` on every non-native element it substitutes:
    // a challenge link would drop out of a screen reader's links list while its
    // href still offered open-in-new-tab (WCAG 2.2 SC 4.1.2).
    renderProviders();

    const google: HTMLElement = await providerLink("login-external-google");

    expect(page.getByRole("link", { name: "Google" }).query()).toBe(google);
    expect(page.getByRole("button", { name: "Google" }).query()).toBeNull();
  });

  it("labels each link with the provider's display name", async () => {
    renderProviders();

    await expect.element(page.getByTestId("login-external-google")).toHaveTextContent("Google");
    await expect
      .element(page.getByTestId("login-external-microsoft"))
      .toHaveTextContent("Microsoft");
  });

  it("renders the oracle's separator copy above the list", async () => {
    renderProviders();

    await expect.element(page.getByTestId("login-external-google")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("login-external-providers"))
      .toHaveTextContent("Or continue with");
  });

  it("kebab-cases a multi-word provider name into its testid", async () => {
    // Display names are prose ("Microsoft Entra ID"), so the testid cannot be
    // the raw name.
    respondWithProviders(["Microsoft Entra ID"]);
    renderProviders();

    await expect
      .element(page.getByTestId("login-external-microsoft-entra-id"))
      .toHaveTextContent("Microsoft Entra ID");
  });

  it("asks the API for the provider list exactly once", async () => {
    renderProviders();

    await expect.element(page.getByTestId("login-external-google")).toBeInTheDocument();
    expect(providerCalls()).toHaveLength(1);
    expect(providerCalls()[0]?.method).toBe("GET");
  });

  it("renders nothing at all when no providers are configured", async () => {
    // A bare "Or continue with" separator over an empty grid is worse than no
    // section at all.
    respondWithProviders([]);
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(page.getByTestId("login-external-providers").query()).toBeNull();
  });

  it("renders nothing while the provider list is still in flight", async () => {
    // This one must NOT use `settleProviders` — the response never arrives, which
    // is the whole point. The assertion is paired with the query being genuinely
    // PENDING so it cannot silently become a copy of the "no providers" test.
    providersReply = () => new Promise<Response>(() => {});
    const client: QueryClient = renderProviders();

    await vi.waitFor(() => {
      expect(providerCalls()).toHaveLength(1);
    });
    expect(client.getQueryState(providersQueryKey())?.status).toBe("pending");
    expect(page.getByTestId("login-external-providers").query()).toBeNull();
  });

  it("renders nothing, and does not throw, when the provider call fails", async () => {
    // Password sign-in is still usable without the social buttons, so a fetch
    // failure degrades to the empty rendering rather than taking the screen down.
    providersReply = async () => await Promise.reject(new TypeError("Failed to fetch"));
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(page.getByTestId("login-external-providers").query()).toBeNull();
  });

  it("renders nothing when the body is not a list at all", async () => {
    // `getExternalProviders` is typed `Promise<unknown>`, so the screen owns the
    // narrowing at its boundary: no cast, structural check, fail closed.
    respondWithProviders({ providers: PROVIDERS });
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(page.getByTestId("login-external-providers").query()).toBeNull();
  });

  it("refuses a list that is not entirely non-empty strings", async () => {
    // Fail-closed on the WHOLE body rather than filtering the good entries out
    // of a bad one: half-trusting it puts a `String(null)` link on screen.
    respondWithProviders(["Google", null, ""]);
    const client: QueryClient = renderProviders();

    await settleProviders(client);
    expect(page.getByTestId("login-external-providers").query()).toBeNull();
  });
});

describe("ExternalProviders — the challenge URL", () => {
  it("points at this origin's external-login endpoint, never the API origin", async () => {
    // BOTH directions: the path must be right AND no API origin may be
    // prepended. A cross-origin top-level GET drops the SameSite cookies the
    // whole handshake rides on.
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
    expect(providerParamOf(await providerLink("login-external-microsoft"))).toBe("Microsoft");
  });

  it("carries the returnUrl the OIDC flow handed the screen", async () => {
    renderProviders();

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(RETURN_URL);
  });

  it("keeps an absolute, allow-listed returnUrl instead of refusing it", async () => {
    // THE REAL-TRAFFIC POLE. The server accepts ONLY absolute URLs, so this is
    // the shape it can actually honour; wiring `isSafeReturnUrl` in here would
    // strip the cargo and kill external sign-in outright.
    renderProviders({ returnUrl: ABSOLUTE_RETURN_URL });

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(ABSOLUTE_RETURN_URL);
  });

  it("never consults the relative-only returnUrl guard", async () => {
    // The DEFERRAL POLE. `isSafeReturnUrl` is the real pure function, not a spy,
    // so "never consulted" is observed at its EFFECT: its genuine verdict here is
    // FALSE (asserted first, so the test cannot pass on a value it would have
    // waved through) and the link is still built with the value intact.
    expect(isSafeReturnUrl(ABSOLUTE_RETURN_URL)).toBe(false);
    renderProviders({ returnUrl: ABSOLUTE_RETURN_URL });

    const link: HTMLElement = await providerLink("login-external-google");

    expect((link.getAttribute("href") ?? "").startsWith(`${EXTERNAL_LOGIN_PATH}?`)).toBe(true);
    expect(returnUrlParamOf(link)).toBe(ABSOLUTE_RETURN_URL);
  });

  it("encodes the returnUrl as a single query value", async () => {
    // What a DEFERRED guard still owes. ASP.NET binds a duplicated `[FromQuery]`
    // as "a,b", so raw cargo carrying `&provider=` could silently change which
    // identity provider the user is challenged against.
    const hostile = "/connect/authorize?a=1&provider=evil-idp&returnUrl=https://evil.example.com";
    renderProviders({ returnUrl: hostile });

    const link: HTMLElement = await providerLink("login-external-google");

    expect(returnUrlParamOf(link)).toBe(hostile);
    expect(providerParamOf(link)).toBe("Google");
  });

  it("encodes the provider name as a single query value", async () => {
    // The provider name is escaped too: it is API-supplied prose, not a URL token.
    respondWithProviders(["Ac&me returnUrl=https://evil.example.com"]);
    renderProviders();

    const link: HTMLElement = await providerLink(
      "login-external-ac-me-returnurl-https-evil-example-com",
    );

    expect(providerParamOf(link)).toBe("Ac&me returnUrl=https://evil.example.com");
    expect(returnUrlParamOf(link)).toBe(RETURN_URL);
  });

  it("falls back to the current page URL when the link carried no returnUrl", async () => {
    // A user who reached /login directly must land back where they started. The
    // fallback is `globalThis.location.href`, which under real Chromium is the
    // live value the component reads — so it is asserted against, not stubbed.
    const currentUrl: string = globalThis.location.href;
    renderProviders({ returnUrl: undefined });

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(currentUrl);
  });

  it("falls back to the current page URL for an empty returnUrl", async () => {
    // `""` is not nullish, so a bare `??` would pass it through and the server
    // would bounce the user to /error. It takes the `undefined` fallback instead.
    const currentUrl: string = globalThis.location.href;
    renderProviders({ returnUrl: "" });

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(currentUrl);
  });

  it("preserves client_id inside the returnUrl rather than as a parameter of its own", async () => {
    // There is no top-level `client_id` at this seam: the endpoint binds only
    // `provider` and `returnUrl`. The client_id rides INSIDE returnUrl, which is
    // the `/connect/authorize?client_id=…` request the challenge resumes.
    renderProviders();

    const link: HTMLElement = await providerLink("login-external-google");

    expect(new URL(link.getAttribute("href") ?? "", "http://x").searchParams.get("client_id")).toBe(
      null,
    );
    expect(returnUrlParamOf(link)).toContain("client_id=web");
  });
});

/** The provider list is a SECTION rendered next to `TabPanel`, not a tab panel. */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
  });
}

describe("/login route — external providers", () => {
  it("renders the provider list on the login screen", async () => {
    renderRouteAt(`/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=web`);

    await expect.element(page.getByTestId("login-external-google")).toBeInTheDocument();
  });

  it("threads returnUrl out of the query string into the challenge link", async () => {
    renderRouteAt(`/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=web`);

    expect(returnUrlParamOf(await providerLink("login-external-google"))).toBe(RETURN_URL);
  });

  it("offers the providers on every tab, not just the password one", async () => {
    // "Or continue with" is an alternative to all three tabs, so it sits outside
    // the tab chain.
    const user = userEvent.setup();
    renderRouteAt("/login");

    await expect.element(page.getByTestId("login-external-google")).toBeInTheDocument();
    await user.click(page.getByTestId("login-tab-otp"));

    await expect.element(page.getByTestId("login-external-google")).toBeInTheDocument();
  });

  it("retires the provider list once the user is signed in", async () => {
    // Offering "or continue with Google" under a "you are now signed in" alert
    // invites the user to start over. Asserted present BEFORE the sign-in too:
    // "is absent" alone passes for a component that renders nothing at all.
    const user = userEvent.setup();
    renderRouteAt("/login");

    await expect.element(page.getByTestId("login-external-google")).toBeInTheDocument();

    await user.type(page.getByTestId("login-email"), "user@example.com");
    await user.type(page.getByTestId("login-password"), "Sup3rSecret!");
    await user.click(page.getByTestId("login-submit"));

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(page.getByTestId("login-external-google").query()).toBeNull();
    expect(page.getByTestId("login-external-providers").query()).toBeNull();
  });
});
