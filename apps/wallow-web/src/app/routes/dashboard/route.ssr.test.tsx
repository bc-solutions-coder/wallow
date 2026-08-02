import { currentUserQuery } from "@bc-solutions-coder/auth";
import { createSdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { type AnyRedirect, isRedirect } from "@tanstack/react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./route";

/**
 * The `/dashboard` auth gate: it reads the current user through the
 * router-context QueryClient (the SHARED `currentUserQuery` from
 * `@bc-solutions-coder/auth`, or the `ensureCurrentUser` primer that composes
 * the same pair), throws a redirect to the BFF login when that user is null, and
 * exposes `isAdmin` when one is present.
 *
 * `@bc-solutions-coder/sdk` is deliberately NOT mocked, and this runs in the
 * vitest NODE project: with no global `location`, a browser-only navigation from
 * `beforeLoad` surfaces as the `ReferenceError` SSR turns into a 500.
 */

/** The BFF login target the gate must send an unauthenticated visitor to. */
const EXPECTED_LOGIN_HREF: string = "/bff/login?returnTo=%2Fdashboard%2Forganizations";

/** A real SDK over a fake transport, standing in for the request's instance. */
const sdk = createSdkHarness().sdk;

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
    context: { queryClient: { ensureQueryData }, sdk },
  });
  return { ensureQueryData, result };
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
  throw new Error("expected the auth gate to throw a redirect, but it returned normally");
}

/**
 * Assert the caught value is a TanStack redirect, narrowing it so callers can
 * read `options.href` / `options.reloadDocument` without a cast.
 */
function assertRedirect(thrown: unknown): asserts thrown is AnyRedirect {
  expect(isRedirect(thrown)).toBe(true);
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

  it("reads the current user via ensureQueryData(currentUserQuery(context.sdk.client))", async () => {
    const { ensureQueryData } = await runGate({ sub: "u1", roles: ["admin"] });

    expect(ensureQueryData).toHaveBeenCalledTimes(1);
    const options = ensureQueryData.mock.calls[0]?.[0] as { queryKey?: unknown };
    expect(options.queryKey).toEqual(currentUserQuery(sdk.client).queryKey);
  });

  it("throws a TanStack redirect to the BFF login when the cached user is null", async () => {
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

  it("never touches the browser-only location global when unauthenticated (SSR safety)", async () => {
    // Precondition: this spec runs in the node project, i.e. under the same
    // no-global-`location` conditions as a full-page SSR render.
    expect("location" in globalThis).toBe(false);

    const thrown: unknown = await catchGate(null);

    // A ReferenceError here is the 500: the SSR request handler surfaces any
    // non-redirect throw from `beforeLoad` as an HTTP 500.
    expect(thrown).not.toBeInstanceOf(ReferenceError);
    expect(isRedirect(thrown)).toBe(true);
  });

  it("allows through and exposes isAdmin:true for an admin user", async () => {
    const { result } = await runGate({ sub: "u1", roles: ["admin"] });

    expect(result).toEqual({ isAdmin: true });
  });

  it("allows through and exposes isAdmin:false for a non-admin user", async () => {
    const { result } = await runGate({ sub: "u1", roles: ["member"] });

    expect(result).toEqual({ isAdmin: false });
  });
});

/** The dashboard verticals must sit under `/dashboard` to render in its `<Outlet/>`. */
describe("routes/dashboard/route (router registration)", () => {
  it("registers the /dashboard layout route in the router tree", () => {
    const router = getRouter();
    expect(Object.keys(router.routesById)).toContain("/dashboard");
  });

  it("reparents the dashboard children under the /dashboard layout route", () => {
    const router = getRouter();
    // The file-based codegen registers the index route WITH a trailing slash.
    const child = router.routesById["/dashboard/organizations/"];
    expect(child?.parentRoute?.id).toBe("/dashboard");
  });
});
