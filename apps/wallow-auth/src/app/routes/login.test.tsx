import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { forkBranding } from "@bc-solutions-coder/styles";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as rootRoute } from "./__root";
import { Route as loginRoute } from "./login";

/**
 * `/login`'s client-branding overlay: a login inside an authorize transaction
 * wears the requesting client's identity — name, tagline, logo, organization
 * attribution — in place of the fork's.
 *
 * Driven through the ROUTE under the app root's own loader options, so the
 * layout-level resolution the app ships runs for real: the context is fetched
 * once at the root, keyed by the transaction's returnUrl — never by the bare
 * `client_id` — and the screen only reads that answer back.
 */

const CLIENT_NAME = "Acme";
const CLIENT_TAGLINE = "Acme things";
/** Already an absolute presigned S3 URL when the API hands it over. */
const CLIENT_LOGO = "https://cdn.test/acme.svg";
const ORGANIZATION = "Acme Corp";

const RETURN_URL = "/connect/authorize?client_id=acme-web&scope=openid%20profile";
/** A login sitting inside the pending authorize transaction. */
const TRANSACTION_LOGIN = `/login?returnUrl=${encodeURIComponent(RETURN_URL)}`;

const CONTEXT_ENDPOINT = "/v1/identity/auth/authorize-context";

/**
 * The provider list the login screen also renders, answered with an empty list
 * so it cannot leave an unrelated rejection racing these assertions.
 */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

const NOT_FOUND_STATUS = 404;

let harness: SdkHarness;

/** How the fake transport answers the context GET; reprogrammed per test. */
let contextReply: () => Response | Promise<Response>;

/** An `AuthorizeContextResponse` for a third-party client, overridable. */
function authorizeContext(overrides: Record<string, unknown> = {}) {
  return {
    clientId: "acme-web",
    displayName: CLIENT_NAME,
    tagline: CLIENT_TAGLINE,
    logoUrl: CLIENT_LOGO,
    themeJson: null,
    organizationName: ORGANIZATION,
    firstParty: false,
    scopes: [{ name: "openid", description: null }],
    ...overrides,
  };
}

/** Every recorded request that asked for the transaction's client context. */
function contextCalls() {
  return harness.calls.filter((call) => call.path === CONTEXT_ENDPOINT);
}

function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
    // The REAL root route's loader/deps, so the context resolution under test
    // is the registered one, not a copy this spec could let drift.
    rootOptions: {
      loaderDeps: rootRoute.options.loaderDeps,
      loader: rootRoute.options.loader,
    },
  });
}

/** The layout's `<h1>` — the branded heading. */
function headingText(): string | null {
  return page.getByRole("heading", { level: 1 }).element().textContent;
}

/**
 * Settle the form first, so a fork-fallback assertion cannot pass merely by
 * running before the router has finished loading the screen.
 */
async function formRendered(): Promise<void> {
  await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
}

beforeEach(() => {
  harness = createPassthroughHarness();
  contextReply = () => Response.json(authorizeContext());
  harness.respond((call) => {
    if (call.path === CONTEXT_ENDPOINT) {
      return contextReply();
    }

    if (call.path === PROVIDERS_ENDPOINT) {
      return Response.json([]);
    }

    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

describe("/login inside an authorize transaction", () => {
  it("resolves the context for the transaction the link carries", async () => {
    renderRouteAt(TRANSACTION_LOGIN);

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });
    const url = new URL(contextCalls()[0]?.url ?? "");
    expect(url.searchParams.get("returnUrl")).toBe(RETURN_URL);
    expect(contextCalls()[0]?.method).toBe("GET");
  });

  it("headlines the client's name in place of the fork's", async () => {
    renderRouteAt(TRANSACTION_LOGIN);

    await vi.waitFor(() => {
      expect(headingText()).toBe(CLIENT_NAME);
    });
  });

  it("shows the client's tagline and retires the fork's", async () => {
    renderRouteAt(TRANSACTION_LOGIN);

    await expect.element(page.getByText(CLIENT_TAGLINE)).toBeInTheDocument();
    expect(page.getByText(forkBranding.tagline).query()).toBeNull();
  });

  it("renders the client's logo above the form", async () => {
    renderRouteAt(TRANSACTION_LOGIN);

    await vi.waitFor(() => {
      expect(page.getByRole("img", { name: CLIENT_NAME }).element().getAttribute("src")).toBe(
        CLIENT_LOGO,
      );
    });
  });

  it("attributes the requesting organization beneath the header", async () => {
    renderRouteAt(TRANSACTION_LOGIN);

    await expect
      .element(page.getByTestId("auth-header-organization"))
      .toHaveTextContent(`by ${ORGANIZATION}`);
  });

  it("still attributes the fork on a client-branded page", async () => {
    // The footer is what tells a user on an "Acme" login page that Wallow serves
    // it — the overlay must reach the heading and stop there.
    renderRouteAt(TRANSACTION_LOGIN);

    await vi.waitFor(() => {
      expect(headingText()).toBe(CLIENT_NAME);
    });
    expect(page.getByText(/App$/u).getByText(forkBranding.appName).element()).toBeDefined();
  });
});

describe("/login outside a resolvable transaction", () => {
  it("keeps the fork's branding when the link carries no returnUrl", async () => {
    // `/` redirects to a bare `/login`, and the OIDC hand-off is not the only
    // way in — with no transaction to resolve there is nothing to fetch.
    renderRouteAt("/login");

    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
    expect(contextCalls()).toHaveLength(0);
  });

  it("asks nothing when the returnUrl is not a pending authorize URL", async () => {
    // Locally safe, but no transaction can live there — the endpoint would
    // refuse it, so the loader does not ask.
    renderRouteAt(`/login?returnUrl=${encodeURIComponent("/dashboard")}`);

    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
    expect(contextCalls()).toHaveLength(0);
  });

  it("falls back to the fork when the transaction cannot be resolved", async () => {
    // The endpoint 404s an expired transaction and an unknown client alike —
    // fork chrome, a usable form, and nothing marked as an error.
    contextReply = () => new Response(null, { status: NOT_FOUND_STATUS });

    renderRouteAt(TRANSACTION_LOGIN);

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });
    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("keeps the fork's chrome for a first-party client", async () => {
    // The fork's own apps ARE the fork: no overlay, and no "by <organization>"
    // line — attribution exists to flag a third party.
    contextReply = () =>
      Response.json(authorizeContext({ firstParty: true, organizationName: "Wallow" }));

    renderRouteAt(TRANSACTION_LOGIN);

    await vi.waitFor(() => {
      expect(contextCalls()).toHaveLength(1);
    });
    await formRendered();

    expect(headingText()).toBe(forkBranding.appName);
    expect(page.getByTestId("auth-header-organization").query()).toBeNull();
  });
});
