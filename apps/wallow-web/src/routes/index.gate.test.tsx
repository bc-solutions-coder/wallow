import type { ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route } from "./index";

/**
 * Public-home reachability spec (Wallow-ffpq.3.6) — the home gate:
 *   - an AUTHENTICATED visitor is redirected to the dashboard
 *     (`/dashboard/apps`),
 *   - an unauthenticated visitor is shown the marketing page only when
 *     the landing-page flag is enabled,
 *   - otherwise they are sent to the BFF login (an OIDC challenge).
 *
 * The gate runs in the route's `beforeLoad`, reading the user through the mocked
 * `getWallowSdk().user.me()` facade (like `routes/dashboard/route.test.tsx`) and
 * the landing-page flag through the mocked `../../lib/branding` shim. The
 * component itself renders inside `PublicLayout`.
 */

const meMock = vi.hoisted(() => vi.fn());
const loginMock = vi.hoisted(() => vi.fn());
// Mutable branding stand-in so each test can flip `landingPage.enabled`.
const branding = vi.hoisted(() => ({
  forkBranding: {
    appName: "Wallow",
    appIcon: "piggy-icon.svg",
    tagline: "Wallow in it",
    landingPage: { enabled: true },
  },
  appIconUrl: "/piggy-icon.svg",
}));

vi.mock("../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({ user: { me: meMock } }),
}));

vi.mock("../lib/branding", () => branding);

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

/** Invoke the route's `beforeLoad` with a minimal TanStack-shaped context. */
async function runBeforeLoad(): Promise<void> {
  const beforeLoad = Route.options.beforeLoad as (opts: unknown) => Promise<unknown>;
  await beforeLoad({ location: { pathname: "/", href: "/" }, context: {} });
}

/** Run `beforeLoad` and return whatever it threw (a redirect), or `undefined`. */
async function captureThrow(): Promise<{ to?: unknown } | undefined> {
  try {
    await runBeforeLoad();
    return undefined;
  } catch (error) {
    return error as { to?: unknown };
  }
}

describe("routes/index (public-home gate)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    branding.forkBranding.landingPage.enabled = true;
  });

  it("defines a beforeLoad gate", () => {
    expect(Route.options.beforeLoad).toBeDefined();
  });

  it("redirects an authenticated visitor to the dashboard", async () => {
    meMock.mockResolvedValue({ sub: "u1", email: "user@test.local" });

    const thrown = await captureThrow();

    expect(thrown).toBeDefined();
    expect(String(thrown?.to ?? "")).toMatch(/^\/dashboard/u);
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("shows the page (no redirect, no login) for an unauthenticated visitor when the landing page is enabled", async () => {
    meMock.mockResolvedValue(null);
    branding.forkBranding.landingPage.enabled = true;

    const thrown = await captureThrow();

    expect(thrown).toBeUndefined();
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("sends an unauthenticated visitor to the BFF login when the landing page is disabled", async () => {
    meMock.mockResolvedValue(null);
    branding.forkBranding.landingPage.enabled = false;

    await captureThrow();

    expect(loginMock).toHaveBeenCalled();
  });
});

describe("routes/index (public-home renders PublicLayout)", () => {
  it("renders the marketing page inside the PublicLayout chrome", async () => {
    const Home = Route.options.component!;
    render(<Home />);
    await expect.element(page.getByTestId("public-layout")).toBeInTheDocument();
  });
});
