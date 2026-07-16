import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../router";
import { Route } from "./route";

/**
 * Auth-gate spec for the `/dashboard` layout route (Wallow-8w1h.8.1).
 *
 * The gate must read the current user via the mocked facade
 * (`getWallowSdk().user.me()`) and, when it resolves `null`, redirect to the BFF
 * login by calling the SDK's `login(currentPath)`. When a user is present it
 * must allow the navigation through (never call `login`).
 */

// Hoisted spies shared between the mock factories and the test bodies.
const meMock = vi.hoisted(() => vi.fn());
const loginMock = vi.hoisted(() => vi.fn());

// Mock the facade: `beforeLoad` asks `getWallowSdk().user.me()` for the user.
vi.mock("../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({ user: { me: meMock } }),
}));

// Spy on the SDK's `login` (a real browser nav in prod) without loading the
// real navigation. Keep every other SDK export intact so the rest of the router
// graph (built by `createRouter`) still resolves.
vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, login: loginMock };
});

/** Invoke the route's `beforeLoad` with a minimal TanStack-shaped context. */
async function runGate(): Promise<void> {
  const beforeLoad = Route.options.beforeLoad as (opts: unknown) => Promise<unknown>;
  await beforeLoad({
    location: { pathname: "/dashboard/organizations", href: "/dashboard/organizations" },
    context: {},
  });
}

describe("routes/dashboard/route (auth gate)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a layout route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("defines a beforeLoad auth gate", () => {
    expect(Route.options.beforeLoad).toBeDefined();
  });

  it("redirects to login when unauthenticated (user.me resolves null)", async () => {
    meMock.mockResolvedValue(null);
    await runGate();
    expect(loginMock).toHaveBeenCalled();
  });

  it("allows through when authenticated (does not call login)", async () => {
    meMock.mockResolvedValue({ sub: "u1", email: "user@test.local" });
    await runGate();
    expect(loginMock).not.toHaveBeenCalled();
  });
});

/**
 * Router-registration spec: `src/router.tsx` must register the `/dashboard`
 * layout route AND reparent the existing dashboard verticals under it (instead
 * of directly under the root), so they render inside the shell's `<Outlet/>`.
 */
describe("routes/dashboard/route (router registration)", () => {
  it("registers the /dashboard layout route in the router tree", () => {
    const router = createRouter();
    expect(Object.keys(router.routesById)).toContain("/dashboard");
  });

  it("reparents the dashboard children under the /dashboard layout route", () => {
    const router = createRouter();
    const child = (router.routesById as Record<string, { parentRoute?: { id?: string } }>)[
      "/dashboard/organizations"
    ];
    expect(child?.parentRoute?.id).toBe("/dashboard");
  });
});
