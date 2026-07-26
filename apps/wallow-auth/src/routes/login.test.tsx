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

import { forkBranding } from "../lib/branding";
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
 * ── THE TWO SEAMS ARE BOTH MOCKED, ON PURPOSE ────────────────────────────────
 *
 * `AuthClient.getClientBranding(clientId)` already exists and is reachable two
 * ways: through the SDK query layer (`authQueries.clientBranding`, the seam
 * `ExternalProviders` uses) or through the app's facade
 * (`getWallowAuthSdk().auth`). Both are routed to ONE hoisted spy so these tests
 * pin the RENDERED RESULT rather than dictating which path the fetch takes —
 * only `queryFn` is swapped, so the real `queryKey` (and therefore the real
 * `queryKeys.auth.clientBranding` factory) still governs the cache. Per bd memory
 * `vitest-resetmodules-breaks-instanceof-across-graphs`, plain `vi.mock`
 * factories + `vi.hoisted` spies, never `vi.resetModules()`.
 *
 * ── THEME COLOURS ARE OUT OF SCOPE (recorded so the gap is not read as a bug) ─
 *
 * The `<style>` block carrying the CSS variables is emitted by `__root.tsx` from
 * the module-constant `forkResolvedBranding`, and the root route has no loader,
 * so per-client COLOURS need root-route wiring that this bead does not do. The
 * acceptance criterion is the identity layer — name, tagline, logo — and that is
 * what is asserted here. `themeJson` is therefore `null` in every fixture.
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  getClientBranding: vi.fn(),
  getExternalProviders: vi.fn(),
}));

vi.mock("../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: { getClientBranding: mocks.getClientBranding },
    oidc: {},
  }),
}));

vi.mock("@bc-solutions-coder/sdk/query", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk/query")>();
  return {
    ...actual,
    authQueries: {
      ...actual.authQueries,
      clientBranding: (clientId: string) => ({
        ...actual.authQueries.clientBranding(clientId),
        queryFn: async (): Promise<unknown> => await mocks.getClientBranding(clientId),
      }),
      // Not under test — stubbed so the provider list cannot reach the network
      // and leave an unrelated rejection racing these assertions.
      externalProviders: () => ({
        ...actual.authQueries.externalProviders(),
        queryFn: async (): Promise<unknown> => await mocks.getExternalProviders(),
      }),
    },
  };
});

const CLIENT_ID = "acme-web";
const CLIENT_NAME = "Acme";
const CLIENT_TAGLINE = "Acme things";
/** Already an absolute presigned S3 URL when the API hands it over. */
const CLIENT_LOGO = "https://cdn.test/acme.svg";

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
 * What the facade really throws when a client has no branding row: the
 * controller answers a BARE `NotFound()` (no body), and `unwrap()` turns every
 * non-2xx into a `WallowError` — so "this client has no branding" arrives as a
 * REJECTION, not as `null`, and the screen has to absorb it.
 */
function notFound(): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status: 404,
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

function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    loginRoute.update({
      id: "/login",
      path: "/login",
      getParentRoute: () => rootRoute,
    } as any),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
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
  vi.clearAllMocks();
  mocks.getExternalProviders.mockResolvedValue([]);
  mocks.getClientBranding.mockResolvedValue(clientBranding());
});

describe("/login per-client branding", () => {
  it("looks up branding for the client_id the link carries", async () => {
    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(mocks.getClientBranding).toHaveBeenCalledWith(CLIENT_ID);
    });
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
    expect(mocks.getClientBranding).not.toHaveBeenCalled();
  });

  it("falls back to the fork's branding when the client has no branding row", async () => {
    // A bare 404 is the API's "no branding configured", not an error the person
    // signing in can act on: the form must stay usable and unmarked.
    mocks.getClientBranding.mockRejectedValue(notFound());

    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(mocks.getClientBranding).toHaveBeenCalledWith(CLIENT_ID);
    });
    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("shows the fork's branding, and a usable form, while the client's is in flight", async () => {
    // Branding is chrome. It must never gate the form behind a spinner, so the
    // pending state renders exactly what "no client" renders.
    mocks.getClientBranding.mockReturnValue(new Promise(() => {}));

    renderRouteAt(`/login?client_id=${CLIENT_ID}`);

    await vi.waitFor(() => {
      expect(mocks.getClientBranding).toHaveBeenCalledWith(CLIENT_ID);
    });
    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
  });
});
