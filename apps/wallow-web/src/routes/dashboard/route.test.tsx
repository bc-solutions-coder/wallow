import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { userQueries } from "@bc-solutions-coder/sdk/query";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../router";
import { Route } from "./route";

/**
 * Auth-gate spec for the `/dashboard` layout route (Wallow-8w1h.8.1), rewired in
 * Wallow-evd5.2.3 to the cached current-user query.
 *
 * The gate must read the current user through the router-context QueryClient via
 * `context.queryClient.ensureQueryData(userQueries.currentUser())` (SDK query
 * layer), NOT the retired `getWallowSdk().user.me()` facade. When the cached user
 * resolves `null` it redirects to the BFF login by calling the SDK's
 * `login(currentPath)` and returns `{ isAdmin: false }`; when a user is present it
 * lets the navigation through (never calls `login`) and exposes `isAdmin` derived
 * from the user's roles claim.
 */

// Spy on the SDK's `login` (a real browser nav in prod) without loading the real
// navigation. Keep every other SDK export intact so the rest of the router graph
// (built by `createRouter`) still resolves.
const loginMock = vi.hoisted(() => vi.fn());

vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, login: loginMock };
});

/**
 * Drive the route's `beforeLoad` with a minimal TanStack-shaped context whose
 * `queryClient.ensureQueryData` is a spy resolving the seeded user. Returns the
 * spy and the gate's result so callers can assert both delegation and outcome.
 */
async function runGate(
  user: unknown,
): Promise<{ ensureQueryData: ReturnType<typeof vi.fn>; result: unknown }> {
  const ensureQueryData = vi.fn().mockResolvedValue(user);
  const beforeLoad = Route.options.beforeLoad as (opts: unknown) => Promise<unknown>;
  const result = await beforeLoad({
    location: { pathname: "/dashboard/organizations", href: "/dashboard/organizations" },
    context: { queryClient: { ensureQueryData } },
  });
  return { ensureQueryData, result };
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

  it("reads the current user via context.queryClient.ensureQueryData(userQueries.currentUser())", async () => {
    const { ensureQueryData } = await runGate({ sub: "u1", roles: ["admin"] });

    expect(ensureQueryData).toHaveBeenCalledTimes(1);
    const options = ensureQueryData.mock.calls[0]?.[0] as { queryKey?: unknown };
    expect(options.queryKey).toEqual(userQueries.currentUser().queryKey);
  });

  it("redirects to login and returns isAdmin:false when the cached user is null", async () => {
    const { result } = await runGate(null);

    expect(loginMock).toHaveBeenCalled();
    expect(result).toEqual({ isAdmin: false });
  });

  it("allows through and exposes isAdmin:true for an admin user", async () => {
    const { result } = await runGate({ sub: "u1", roles: ["admin"] });

    expect(loginMock).not.toHaveBeenCalled();
    expect(result).toEqual({ isAdmin: true });
  });

  it("allows through and exposes isAdmin:false for a non-admin user", async () => {
    const { result } = await runGate({ sub: "u1", roles: ["member"] });

    expect(loginMock).not.toHaveBeenCalled();
    expect(result).toEqual({ isAdmin: false });
  });

  it("no longer imports the retired getWallowSdk facade", () => {
    const source: string = readFileSync(
      fileURLToPath(new URL("./route.tsx", import.meta.url)),
      "utf8",
    );

    expect(source).not.toMatch(/getWallowSdk|lib\/wallow-sdk/u);
    expect(source).toMatch(/userQueries/u);
    expect(source).toMatch(/ensureQueryData/u);
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
      // The file-based codegen registers the index route with a trailing slash
      // (`/dashboard/organizations/`); the old manual reparenting used the bare
      // `/dashboard/organizations`. The parent is still the `/dashboard` shell.
      "/dashboard/organizations/"
    ];
    expect(child?.parentRoute?.id).toBe("/dashboard");
  });
});
