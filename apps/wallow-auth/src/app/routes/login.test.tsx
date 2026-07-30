import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { forkBranding } from "@bc-solutions-coder/styles";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../test/harness";
import { Route as loginRoute } from "./login";

/**
 * Route spec for `/login`'s PER-CLIENT BRANDING OVERLAY (Wallow-ffpq.2.5).
 *
 * The overlay was ported with the screens but never wired: `mergeClientBranding`
 * (packages/styles) and `AuthLayout`'s optional `branding` prop both exist, and
 * `routes/login.tsx` already reads `client_id` out of the query string — it just
 * hands `AuthLayout` nothing, so a tenant hitting `/login?client_id=acme` gets
 * default Wallow branding. This file pins the criterion: that link renders THAT
 * CLIENT's branding.
 *
 * ── WHY THE ROUTE AND NOT THE SCREEN ─────────────────────────────────────────
 *
 * `client_id` only exists once a URL is parsed by a router, and the branded
 * chrome is `AuthLayout` — which the ROUTE renders around `LoginScreen`, not
 * `LoginScreen` itself. So the whole criterion lives at the route seam, and a
 * bare render of a search-reading route component always dies on `router.stores`
 * outside a `RouterProvider` (bd memory
 * `wallow-auth-route-tests-never-bare-render-a`). Hence the real memory router
 * below, with a throwaway root — the app's real `__root.tsx` renders `<html>`.
 *
 * ── THE SEAM IS THE WIRE, NOT A STAND-IN (Wallow-pu6a.5.1) ───────────────────
 *
 * The branding read is the generated `clientBrandingGetBrandingOptions()` bound
 * to the request-scoped SDK, but these tests deliberately dictate no particular
 * call shape: whatever the screen reaches for ends at the same request, so the
 * seam is `@bc-solutions-coder/testing/sdk-harness` — the REAL SDK with only its
 * `fetch` faked. The branding fixture is delivered as a genuine 200 on
 * `GET /v1/identity/apps/{clientId}/branding`, and "did it ask, and for whom" is
 * read off the RECORDED REQUEST — which pins the clientId in the URL rather than
 * in a spy's argument list. This also retires the two module mocks the file used
 * to carry, both now forbidden by `src/sdk-test-seam.test.ts`.
 *
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface (Wallow-pu6a.5.5).
 *
 * ── THEME COLOURS ARE OUT OF SCOPE (recorded so the gap is not read as a bug) ─
 *
 * The `<style>` block carrying the CSS variables is emitted by `__root.tsx` from
 * the module-constant `forkResolvedBranding`, and the root route has no loader,
 * so per-client COLOURS need root-route wiring that this bead does not do. The
 * acceptance criterion is the identity layer — name, tagline, logo — and that is
 * what is asserted here. `themeJson` is therefore `null` in every fixture.
 */

const CLIENT_ID = "acme-web";
const CLIENT_NAME = "Acme";
const CLIENT_TAGLINE = "Acme things";
/** Already an absolute presigned S3 URL when the API hands it over. */
const CLIENT_LOGO = "https://cdn.test/acme.svg";

/** `ClientBrandingController.GetBranding` — the request under test. */
const BRANDING_ENDPOINT = `/v1/identity/apps/${CLIENT_ID}/branding`;

/**
 * The provider list the login screen also renders. Not under test — answered
 * with an empty list so it cannot leave an unrelated rejection racing these
 * assertions.
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
 * What the API really answers when a client has no branding row: the controller
 * returns a BARE `NotFound()` with no body at all. The SDK turns every non-2xx
 * into a rejection, so "this client has no branding" arrives at the screen as a
 * FAILED query rather than as `null`, and the screen has to absorb it. Sent as
 * the real thing now instead of a hand-fabricated error object — a fixture that
 * cannot drift from what the SDK does with a 404.
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
  harness = createAuthHarness();
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
    // The clientId is a PATH segment, so asserting the recorded path is the same
    // claim the old `toHaveBeenCalledWith(CLIENT_ID)` made — against the wire.
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
