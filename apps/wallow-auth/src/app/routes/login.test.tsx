import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { forkBranding } from "@bc-solutions-coder/styles";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as loginRoute } from "./login";

/**
 * `/login`'s per-client branding overlay: `?client_id=acme` renders that
 * client's identity — name, tagline, logo — in place of the fork's.
 *
 * Driven through the ROUTE, not `LoginScreen`: `client_id` exists only once a
 * router has parsed the URL, and a bare render of a search-reading route
 * component dies on `router.stores` outside a `RouterProvider`.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so "did it ask, and for
 * whom" is read off the recorded request path.
 */

const CLIENT_ID = "acme-web";
const CLIENT_NAME = "Acme";
const CLIENT_TAGLINE = "Acme things";
/** Already an absolute presigned S3 URL when the API hands it over. */
const CLIENT_LOGO = "https://cdn.test/acme.svg";

/** `ClientBrandingController.GetBranding` — the request under test. */
const BRANDING_ENDPOINT = `/v1/identity/apps/${CLIENT_ID}/branding`;

/**
 * The provider list the login screen also renders, answered with an empty list
 * so it cannot leave an unrelated rejection racing these assertions.
 */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

const NOT_FOUND_STATUS = 404;

let harness: SdkHarness;

/** How the fake transport answers the branding GET; reprogrammed per test. */
let brandingReply: () => Response | Promise<Response>;

/** A `ClientBrandingDto` as `GET /v1/identity/apps/{clientId}/branding` returns it. */
function clientBranding(overrides: Record<string, unknown> = {}) {
  return {
    clientId: CLIENT_ID,
    displayName: CLIENT_NAME,
    tagline: CLIENT_TAGLINE,
    logoUrl: CLIENT_LOGO,
    themeJson: null,
    ...overrides,
  };
}

/**
 * What the API answers when a client has no branding row: a BARE `NotFound()`
 * with no body. The SDK turns every non-2xx into a rejection, so "this client
 * has no branding" reaches the screen as a FAILED query, not as `null`.
 */
function noBrandingRow(): Response {
  return new Response(null, { status: NOT_FOUND_STATUS });
}

/** Every recorded request that asked any client for its branding. */
function brandingCalls() {
  return harness.calls.filter((call) => call.path.endsWith("/branding"));
}

function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
  });
}

/** The layout's `<h1>` — the branded heading, which carries no testid today. */
function headingText(): string | null {
  return page.getByRole("heading", { level: 1 }).element().textContent;
}

/**
 * Settle the form first, so a fork-fallback assertion cannot pass merely by
 * running before the branding query has had a chance to change anything.
 */
async function formRendered(): Promise<void> {
  await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
}

beforeEach(() => {
  harness = createPassthroughHarness();
  brandingReply = () => Response.json(clientBranding());
  harness.respond((call) => {
    if (call.path === BRANDING_ENDPOINT) {
      return brandingReply();
    }

    if (call.path === PROVIDERS_ENDPOINT) {
      return Response.json([]);
    }

    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

describe("/login per-client branding", () => {
  it("looks up branding for the client_id the link carries", async () => {
    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(brandingCalls()).toHaveLength(1);
    });
    expect(brandingCalls()[0]?.path).toBe(BRANDING_ENDPOINT);
    expect(brandingCalls()[0]?.method).toBe("GET");
  });

  it("headlines the client's name in place of the fork's", async () => {
    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(headingText()).toBe(CLIENT_NAME);
    });
  });

  it("shows the client's tagline and retires the fork's", async () => {
    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await expect.element(page.getByText(CLIENT_TAGLINE)).toBeInTheDocument();
    expect(page.getByText(forkBranding.tagline).query()).toBeNull();
  });

  it("renders the client's logo above the form", async () => {
    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(page.getByRole("img", { name: CLIENT_NAME }).element().getAttribute("src")).toBe(
        CLIENT_LOGO,
      );
    });
  });

  it("still attributes the fork on a client-branded page", async () => {
    // The footer is what tells a user on an "Acme" login page that Wallow serves
    // it — the overlay must reach the heading and stop there.
    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(headingText()).toBe(CLIENT_NAME);
    });
    expect(page.getByText(/App$/u).getByText(forkBranding.appName).element()).toBeDefined();
  });

  it("keeps the fork's branding when the link identifies no client", async () => {
    // `/` redirects to a bare `/login`, and the OIDC hand-off is not the only way
    // in — with no client to overlay there is nothing to fetch.
    renderRouteAt("/login");

    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
    expect(brandingCalls()).toHaveLength(0);
  });

  it("falls back to the fork's branding when the client has no branding row", async () => {
    // A bare 404 is the API's "no branding configured", not an error the person
    // signing in can act on: the form must stay usable and unmarked.
    brandingReply = noBrandingRow;

    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(brandingCalls()).toHaveLength(1);
    });
    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("shows the fork's branding, and a usable form, while the client's is in flight", async () => {
    // Branding is chrome. It must never gate the form behind a spinner, so the
    // pending state renders exactly what "no client" renders.
    brandingReply = () => new Promise<Response>(() => {});

    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(brandingCalls()).toHaveLength(1);
    });
    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
  });
});
