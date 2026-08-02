import { createSdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { type AnyRedirect, isRedirect } from "@tanstack/react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route } from "./index";

/**
 * SSR safety for the public home route's `beforeLoad` gate.
 *
 * With the landing page disabled and no cached user, the gate must THROW a
 * TanStack `redirect({ href, reloadDocument })` to the BFF login rather than
 * call the SDK's browser-only `login()`, which assigns to the bare global
 * `location` and would surface as HTTP 500 under SSR. `@bc-solutions-coder/sdk`
 * is therefore NOT mocked; as a `.test.ts` this runs in the NODE project, under
 * the same no-global-`location` conditions as a full-page SSR render.
 */

/** The BFF login target the gate must send a forced-login visitor to. */
const EXPECTED_LOGIN_HREF: string = "/bff/login?returnTo=%2Fdashboard%2Fapps";

// Mutable branding stand-in so each test can flip `landingPage.enabled`. Mirrors
// the shape `routes/index.tsx` and `components/PublicLayout.tsx` read.
const branding = vi.hoisted(() => ({
  forkBranding: {
    appName: "Wallow",
    appIcon: "piggy-icon.svg",
    tagline: "Wallow in it",
    repositoryUrl: "https://github.com/bc-solutions-coder/wallow",
    docsUrl: "https://bc-solutions-coder.github.io/wallow/",
    landingPage: { enabled: true },
  },
  appIconUrl: "/piggy-icon.svg",
}));

// Partial override: every other branding export stays real, only the two values
// the gate reads are stood in.
vi.mock("@bc-solutions-coder/styles", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@bc-solutions-coder/styles")>()),
  ...branding,
}));

/** A real SDK over a fake transport, standing in for the request's instance. */
const sdk = createSdkHarness().sdk;

/**
 * Drive the route's `beforeLoad` with a minimal TanStack-shaped context whose
 * `queryClient.ensureQueryData` is a spy resolving the seeded user.
 */
async function runGate(user: unknown): Promise<unknown> {
  const ensureQueryData = vi.fn().mockResolvedValue(user);
  const beforeLoad = Route.options.beforeLoad as (opts: unknown) => Promise<unknown>;
  return beforeLoad({
    location: { pathname: "/", href: "/" },
    context: { queryClient: { ensureQueryData }, sdk },
  });
}

/**
 * Run the gate expecting it to throw, and hand back whatever it threw so callers
 * can assert on the redirect (or catch a browser-only `ReferenceError`).
 */
async function catchGate(user: unknown): Promise<unknown> {
  try {
    await runGate(user);
  } catch (error: unknown) {
    return error;
  }
  throw new Error("expected the home gate to throw a redirect, but it returned normally");
}

/**
 * Assert the caught value is a TanStack redirect, narrowing it so callers can
 * read `options.href` / `options.reloadDocument` without a cast.
 */
function assertRedirect(thrown: unknown): asserts thrown is AnyRedirect {
  expect(isRedirect(thrown)).toBe(true);
}

describe("routes/index (SSR-safe home gate)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    branding.forkBranding.landingPage.enabled = false;
  });

  it("throws a TanStack redirect to the BFF login when the landing page is disabled and no user is cached", async () => {
    const thrown: unknown = await catchGate(null);

    assertRedirect(thrown);
    expect(thrown.options.href).toBe(EXPECTED_LOGIN_HREF);
    expect(thrown.headers.get("Location")).toBe(EXPECTED_LOGIN_HREF);
  });

  it("marks the BFF login redirect as a full-document navigation", async () => {
    // `/bff/login` is a BFF endpoint, not a route in the TanStack tree. Without
    // `reloadDocument`, a client-side redirect with a RELATIVE href is committed
    // through the router (`buildAndCommitLocation`) and lands on a not-found
    // match instead of reaching the BFF.
    const thrown: unknown = await catchGate(null);

    assertRedirect(thrown);
    expect(thrown.options.reloadDocument).toBe(true);
  });

  it("never touches the browser-only location global when forcing a login (SSR safety)", async () => {
    // Precondition: this spec runs in the node project, i.e. under the same
    // no-global-`location` conditions as a full-page SSR render.
    expect("location" in globalThis).toBe(false);

    const thrown: unknown = await catchGate(null);

    // A ReferenceError here is the 500: the SSR request handler surfaces any
    // non-redirect throw from `beforeLoad` as an HTTP 500.
    expect(thrown).not.toBeInstanceOf(ReferenceError);
    expect(isRedirect(thrown)).toBe(true);
  });

  it("still shows the marketing page (no throw) when the landing page is enabled", async () => {
    branding.forkBranding.landingPage.enabled = true;

    await expect(runGate(null)).resolves.toBeUndefined();
  });

  it("still redirects an authenticated visitor to the dashboard", async () => {
    branding.forkBranding.landingPage.enabled = true;

    const thrown: unknown = await catchGate({ sub: "u1", email: "user@test.local" });

    assertRedirect(thrown);
    expect(thrown.options.to).toBe("/dashboard/apps");
  });
});
