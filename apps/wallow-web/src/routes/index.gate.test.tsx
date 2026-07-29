import type { ReactNode } from "react";
import { createSdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { type AnyRedirect, isRedirect } from "@tanstack/react-router";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { currentUserQuery } from "../lib/current-user";
import { Route } from "./index";

/**
 * Public-home reachability spec (Wallow-ffpq.3.6), rewired in Wallow-evd5.2.3 to
 * the cached current-user query. The home gate:
 *   - an AUTHENTICATED visitor is redirected to the dashboard
 *     (`/dashboard/apps`),
 *   - an unauthenticated visitor is shown the marketing page only when
 *     the landing-page flag is enabled,
 *   - otherwise they are sent to the BFF login (an OIDC challenge).
 *
 * The gate runs in the route's `beforeLoad`, reading the user through the
 * router-context QueryClient via
 * `context.queryClient.ensureQueryData(currentUserQuery(context.sdk.client))` — the
 * GENERATED current-user read bound to the request's own SDK instance
 * (Wallow-pu6a.5.5), NOT the retired `getWallowSdk().user.me()` facade. The landing-page flag
 * is read through a partially mocked `@bc-solutions-coder/styles`; the component
 * itself renders inside `PublicLayout`.
 */

const loginMock = vi.hoisted(() => vi.fn());
// Mutable branding stand-in so each test can flip `landingPage.enabled`.
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

// Partial override: every other branding export (theme rendering, asset URLs)
// stays real, only the two values the gate and PublicLayout read are stood in.
vi.mock("@bc-solutions-coder/styles", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@bc-solutions-coder/styles")>()),
  ...branding,
}));

// Spy on the SDK's `login` (a real browser nav in prod) while keeping every
// other export intact so `createFileRoute`/router wiring still resolves.
vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, login: loginMock };
});

// Stub TanStack `Link` (used by PublicLayout) to a plain anchor; keep the rest
// of react-router real so `createFileRoute` and `redirect` behave.
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Link: ({
      to,
      children,
      ...rest
    }: { to: string; children?: ReactNode } & Record<string, unknown>) => (
      <a href={to} {...rest}>
        {children}
      </a>
    ),
  };
});

/**
 * Drive the route's `beforeLoad` with a minimal TanStack-shaped context whose
 * `queryClient.ensureQueryData` is a spy resolving the seeded user. Returns the
 * spy so callers can assert delegation to the cached current-user query.
 */
function makeContext(user: unknown): {
  ensureQueryData: ReturnType<typeof vi.fn>;
  invoke: () => Promise<unknown>;
} {
  const ensureQueryData = vi.fn().mockResolvedValue(user);
  const beforeLoad = Route.options.beforeLoad as (opts: unknown) => Promise<unknown>;
  const invoke = (): Promise<unknown> =>
    beforeLoad({
      location: { pathname: "/", href: "/" },
      context: { queryClient: { ensureQueryData }, sdk },
    });
  return { ensureQueryData, invoke };
}

/** A real SDK over a fake transport, standing in for the request's instance. */
const sdk = createSdkHarness().sdk;

/** Run `beforeLoad` and return whatever it threw (a redirect), or `undefined`. */
async function captureThrow(user: unknown): Promise<{ to?: unknown } | undefined> {
  const { invoke } = makeContext(user);
  try {
    await invoke();
    return undefined;
  } catch (error) {
    return error as { to?: unknown };
  }
}

/**
 * Assert the caught value is a TanStack redirect, narrowing it so callers can
 * read `options.href` without a cast.
 */
function assertRedirect(thrown: unknown): asserts thrown is AnyRedirect {
  expect(isRedirect(thrown)).toBe(true);
}

describe("routes/index (public-home gate)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    branding.forkBranding.landingPage.enabled = true;
  });

  it("defines a beforeLoad gate", () => {
    expect(Route.options.beforeLoad).toBeDefined();
  });

  it("reads the current user via ensureQueryData(currentUserQuery(context.sdk.client))", async () => {
    const { ensureQueryData, invoke } = makeContext(null);

    await invoke();

    expect(ensureQueryData).toHaveBeenCalledTimes(1);
    const options = ensureQueryData.mock.calls[0]?.[0] as { queryKey?: unknown };
    expect(options.queryKey).toEqual(currentUserQuery(sdk.client).queryKey);
  });

  it("redirects an authenticated visitor to the dashboard", async () => {
    const thrown = await captureThrow({ sub: "u1", email: "user@test.local" });

    expect(thrown).toBeDefined();
    expect(String(thrown?.to ?? "")).toMatch(/^\/dashboard/u);
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("shows the page (no redirect, no login) for an unauthenticated visitor when the landing page is enabled", async () => {
    branding.forkBranding.landingPage.enabled = true;

    const thrown = await captureThrow(null);

    expect(thrown).toBeUndefined();
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("throws a BFF-login redirect (not a browser-only login() call) when the landing page is disabled", async () => {
    // The SSR half of this contract lives in `index.ssr-gate.test.ts` (node
    // project, real SDK). Here we only assert the client-side shape: the gate
    // must hand TanStack a redirect rather than reach for the SDK's browser-only
    // `login()`, which would 500 the same route under SSR (Wallow-fqw9).
    branding.forkBranding.landingPage.enabled = false;

    const thrown: unknown = await captureThrow(null);

    assertRedirect(thrown);
    expect(thrown.options.href).toBe("/bff/login?returnTo=%2Fdashboard%2Fapps");
    expect(loginMock).not.toHaveBeenCalled();
  });
});

describe("routes/index (public-home renders PublicLayout)", () => {
  it("renders the marketing page inside the PublicLayout chrome", async () => {
    const Home = Route.options.component!;
    render(<Home />);
    await expect.element(page.getByTestId("public-layout")).toBeInTheDocument();
  });
});
