import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { forkBranding } from "@bc-solutions-coder/styles";
import type { AnyRoute } from "@tanstack/react-router";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { appIconUrl } from "@shared/lib/branding";

import { Route as rootRoute } from "./__root";
import { Route as acceptTermsRoute } from "./accept-terms";
import { Route as consentRoute } from "./consent";
import { Route as errorRoute } from "./error";
import { Route as forgotPasswordRoute } from "./forgot-password";
import { Route as invitationRoute } from "./invitation";
import { Route as mfaChallengeRoute } from "./mfa/challenge";
import { Route as mfaEnrollRoute } from "./mfa/enroll";
import { Route as privacyRoute } from "./privacy";
import { Route as registerRoute } from "./register";
import { Route as resetPasswordRoute } from "./reset-password";
import { Route as setupRoute } from "./setup";
import { Route as verifyEmailConfirmRoute } from "./verify-email/confirm";
import { Route as verifyEmailRoute } from "./verify-email/index";

/**
 * The transaction-branding sweep: every in-transaction screen wears the
 * requesting client's chrome — over the fork's own footer — under the app
 * root's own loader, the fork-only screens never do even handed a
 * transaction-shaped URL, and the document head titles the tab per arm while
 * the favicon stays the fork's.
 *
 * `/login` has its own richer spec (`login.test.tsx`); this file proves the
 * WIRING is uniform across the rest, one mounted route at a time.
 */

const CLIENT_NAME = "Acme";
const ORGANIZATION = "Acme Corp";
const RETURN_URL = "/connect/authorize?client_id=acme-web&scope=openid";
const CONTEXT_ENDPOINT = "/v1/identity/auth/authorize-context";
const NOT_FOUND_STATUS = 404;

/** The in-transaction screens beyond `/login`, each with its mount path. */
const BRANDED_SCREENS: readonly { readonly path: string; readonly route: AnyRoute }[] = [
  { path: "/register", route: registerRoute },
  { path: "/consent", route: consentRoute },
  { path: "/accept-terms", route: acceptTermsRoute },
  { path: "/mfa/challenge", route: mfaChallengeRoute },
  { path: "/mfa/enroll", route: mfaEnrollRoute },
  { path: "/verify-email/", route: verifyEmailRoute },
  { path: "/forgot-password", route: forgotPasswordRoute },
];

/** Fork-only screens: a crafted transaction-shaped link must change nothing. */
const FORK_SCREENS: readonly { readonly path: string; readonly route: AnyRoute }[] = [
  { path: "/error", route: errorRoute },
  { path: "/reset-password", route: resetPasswordRoute },
  { path: "/verify-email/confirm", route: verifyEmailConfirmRoute },
  { path: "/invitation", route: invitationRoute },
  { path: "/setup", route: setupRoute },
  { path: "/privacy", route: privacyRoute },
];

/** An `AuthorizeContextResponse` for a third-party client, overridable. */
function authorizeContext(overrides: Record<string, unknown> = {}) {
  return {
    clientId: "acme-web",
    displayName: CLIENT_NAME,
    tagline: "Acme things",
    logoUrl: null,
    themeJson: null,
    organizationName: ORGANIZATION,
    firstParty: false,
    scopes: [{ name: "openid", description: null }],
    ...overrides,
  };
}

let harness: SdkHarness;

/** Every recorded request that asked for the transaction's client context. */
function contextCalls() {
  return harness.calls.filter((call) => call.path === CONTEXT_ENDPOINT);
}

beforeEach(() => {
  harness = createPassthroughHarness();
  harness.respond((call) => {
    if (call.path === CONTEXT_ENDPOINT) {
      return Response.json(authorizeContext());
    }

    // Whatever else a screen asks for on mount is not this spec's subject; a
    // bare 404 leaves each in its unbranded/error arm without crashing it.
    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

function renderRouteAt(url: string, mount: { readonly path: string; readonly route: AnyRoute }) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [mount],
    // The REAL root route's loader/deps, so the resolution under test is the
    // registered one, not a copy this spec could let drift.
    rootOptions: {
      loaderDeps: rootRoute.options.loaderDeps,
      loader: rootRoute.options.loader,
    },
  });
}

describe("every in-transaction screen wears the client's branding", () => {
  for (const screen of BRANDED_SCREENS) {
    it(`brands ${screen.path} for the requesting client`, async () => {
      renderRouteAt(`${screen.path}?returnUrl=${encodeURIComponent(RETURN_URL)}`, screen);

      await expect.element(page.getByTestId("auth-header-name")).toHaveTextContent(CLIENT_NAME);
      await expect
        .element(page.getByTestId("auth-header-organization"))
        .toHaveTextContent(`by ${ORGANIZATION}`);

      // The footer never takes the client's branding: it is what tells the
      // user on an "Acme" page that the fork serves it.
      expect(page.getByText(/App$/u).getByText(forkBranding.appName).element()).toBeDefined();
    });
  }
});

describe("the fork-only screens never brand", () => {
  for (const screen of FORK_SCREENS) {
    it(`keeps the fork's chrome on ${screen.path} and asks for no context`, async () => {
      // The same transaction-shaped URL that brands the screens above: on these
      // paths the root loader's gate answers null before any request goes out.
      renderRouteAt(`${screen.path}?returnUrl=${encodeURIComponent(RETURN_URL)}`, screen);

      await expect
        .element(page.getByTestId("auth-header-name"))
        .toHaveTextContent(forkBranding.appName);
      expect(page.getByTestId("auth-header-organization").query()).toBeNull();
      expect(contextCalls()).toHaveLength(0);
    });
  }
});

describe("the document head", () => {
  /** The root route's `head()` output for a given loader answer. */
  async function headFor(authorizeContextAnswer: unknown) {
    return await rootRoute.options.head?.({
      loaderData: { authorizeContext: authorizeContextAnswer },
    } as never);
  }

  it("titles a third-party transaction for the client being signed in to", async () => {
    const head = await headFor(authorizeContext());

    expect(head?.meta).toContainEqual({ title: `Sign in · ${CLIENT_NAME}` });
  });

  it("keeps the fork's name outside a transaction", async () => {
    const head = await headFor(null);

    expect(head?.meta).toContainEqual({ title: forkBranding.appName });
  });

  it("keeps the fork's name for a first-party client", async () => {
    const head = await headFor(authorizeContext({ firstParty: true }));

    expect(head?.meta).toContainEqual({ title: forkBranding.appName });
  });

  it("keeps the fork's favicon even on a client-branded transaction", async () => {
    // The address-bar identity stays the fork's: a client-supplied icon there
    // would be impersonation surface.
    const head = await headFor(authorizeContext());

    expect(head?.links).toEqual([{ rel: "icon", href: appIconUrl }]);
  });
});
