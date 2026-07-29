import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { createSdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { type AnyRedirect, isRedirect } from "@tanstack/react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { currentUserQuery } from "../../lib/current-user";
import { getRouter } from "../../router";
import { Route } from "./route";

/**
 * Auth-gate spec for the `/dashboard` layout route (Wallow-8w1h.8.1), rewired in
 * Wallow-evd5.2.3 to the cached current-user query and made SSR-safe in
 * Wallow-zyxe.
 *
 * The gate must read the current user through the router-context QueryClient via
 * `context.queryClient.ensureQueryData(currentUserQuery(context.sdk.client))` — the
 * GENERATED current-user read bound to the request's own SDK instance
 * (Wallow-pu6a.5.5), NOT a module-global client and not the retired
 * `getWallowSdk().user.me()` facade. When the cached user
 * resolves `null` it must **throw** a TanStack `redirect({ href, reloadDocument })`
 * to the BFF login rather than calling the SDK's browser-only `login()` — that
 * helper assigns to the bare global `location`, which does not exist under Node,
 * so a full-page SSR load of `/dashboard/**` returned HTTP 500 instead of a
 * redirect. When a user IS present the gate lets the navigation through and
 * exposes `isAdmin` derived from the user's roles claim.
 *
 * This file deliberately does NOT mock `@bc-solutions-coder/sdk`: the previous
 * `login` spy masked exactly the defect above. It runs in the vitest NODE project
 * (see `vitest.config.ts` → `nodeTsxSpecs`), so there is no global `location` and
 * any browser-only navigation reached from `beforeLoad` surfaces as a thrown
 * `ReferenceError` — the same failure the SSR handler turns into a 500.
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

  it("no longer imports the retired getWallowSdk facade", () => {
    const source: string = readFileSync(
      fileURLToPath(new URL("./route.tsx", import.meta.url)),
      "utf8",
    );

    expect(source).not.toMatch(/getWallowSdk|lib\/wallow-sdk/u);
    expect(source).toMatch(/currentUserQuery/u);
    expect(source).toMatch(/ensureQueryData/u);
  });

  it("does not import the SDK's browser-only login helper", () => {
    // `login()` assigns to the bare global `location`, so it must not be
    // reachable from a hook that also runs server-side. The gate redirects
    // through TanStack's `redirect()` instead, which works on SSR and CSR alike.
    const source: string = readFileSync(
      fileURLToPath(new URL("./route.tsx", import.meta.url)),
      "utf8",
    );
    const importLines: string = (source.match(/^import .*$/gmu) ?? []).join("\n");

    expect(importLines).not.toMatch(/\blogin\b/u);
    expect(importLines).toMatch(/\bredirect\b/u);
  });
});

/**
 * Router-registration spec: `src/router.tsx` must register the `/dashboard`
 * layout route AND reparent the existing dashboard verticals under it (instead
 * of directly under the root), so they render inside the shell's `<Outlet/>`.
 */
describe("routes/dashboard/route (router registration)", () => {
  it("registers the /dashboard layout route in the router tree", () => {
    const router = getRouter();
    expect(Object.keys(router.routesById)).toContain("/dashboard");
  });

  it("reparents the dashboard children under the /dashboard layout route", () => {
    const router = getRouter();
    // The file-based codegen registers the index route with a trailing slash
    // (`/dashboard/organizations/`); the old manual reparenting used the bare
    // `/dashboard/organizations`. The parent is still the `/dashboard` shell.
    const child = router.routesById["/dashboard/organizations/"];
    expect(child?.parentRoute?.id).toBe("/dashboard");
  });
});
