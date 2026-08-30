import { describe, expect, it, type Mock, vi } from "vitest";

import type { WallowUser } from "./auth";
import * as browserEntry from "./index";
import { type LoginRedirectOptions, loginRedirect, requireAuth } from "./route-context";

/**
 * Spec (Wallow-pu6a.5.6): the SSR-safe auth guard.
 *
 * This runs in the SDK's node environment, i.e. under the same conditions as a
 * full-page SSR render: no `location`, no `document`. That is deliberate — the
 * defect these helpers exist to prevent (Wallow-zyxe) was a guard that
 * navigated by assigning to the global `location`, which turned a gated SSR
 * load into an HTTP 500 instead of a redirect. Any helper here that reached for
 * a browser global would blow up in this file rather than in production.
 *
 * The pinned properties:
 *
 *   (a) `loginRedirect` is pure — it BUILDS a target and navigates nothing;
 *   (b) the target is an `href` with `reloadDocument`, never a `to`: `/bff/login`
 *       is a BFF endpoint outside the route tree, so a `to` (or an href without
 *       `reloadDocument`) is committed through the client router and lands on a
 *       not-found match instead of reaching the BFF;
 *   (c) `requireAuth` narrows the user away from null on the happy path and
 *       THROWS the injected router redirect otherwise — a returned redirect
 *       would let the guarded route render;
 *   (d) `redirect` is injected, so the SDK takes no dependency on
 *       `@tanstack/react-router` and the guard is testable without a router.
 */

/** A distinguishable stand-in for the object a router's `redirect()` returns. */
const REDIRECT_SENTINEL = { __redirect: true } as const;

/** A spy shaped like the router's `redirect()`, returning {@link REDIRECT_SENTINEL}. */
type RedirectSpy = Mock<(options: LoginRedirectOptions) => typeof REDIRECT_SENTINEL>;

function redirectSpy(): RedirectSpy {
  return vi.fn((_options: LoginRedirectOptions) => REDIRECT_SENTINEL);
}

/**
 * Run `act` expecting it to throw, and hand back what it threw.
 *
 * The guard throws a plain redirect object rather than an `Error`, which
 * `expect().toThrow()` matchers are not built to identify — the assertions want
 * identity against {@link REDIRECT_SENTINEL}, not message matching.
 */
function catchThrown(act: () => unknown): unknown {
  try {
    act();
  } catch (error: unknown) {
    return error;
  }
  throw new Error("expected the auth guard to throw a redirect, but it returned normally");
}

/** Build a user claim bag; `sub` is the only claim the SDK's type requires. */
function user(claims: Record<string, unknown> = {}): WallowUser {
  return { sub: "user-1", ...claims };
}

describe("loginRedirect", () => {
  it("targets the BFF login endpoint with an encoded returnTo", () => {
    expect(loginRedirect("/dashboard/organizations")).toEqual({
      href: "/bff/login?returnTo=%2Fdashboard%2Forganizations",
      reloadDocument: true,
    });
  });

  it("encodes a returnTo that carries its own query string", () => {
    // Left raw, the `?`/`&` would be read as extra params on /bff/login.
    expect(loginRedirect("/dashboard/apps?tab=keys&page=2").href).toBe(
      "/bff/login?returnTo=%2Fdashboard%2Fapps%3Ftab%3Dkeys%26page%3D2",
    );
  });

  it("defaults to the app root when returnTo is absent or blank", () => {
    expect(loginRedirect().href).toBe("/bff/login?returnTo=%2F");
    expect(loginRedirect("").href).toBe("/bff/login?returnTo=%2F");
    expect(loginRedirect("   ").href).toBe("/bff/login?returnTo=%2F");
  });

  it("carries an organization hint for the silent re-authorize", () => {
    // The organization picker is a link built from this: the BFF forwards the
    // hint to the authorize request, and the IdP switches the session's org.
    expect(loginRedirect("/dashboard", { organization: "o2" }).href).toBe(
      "/bff/login?returnTo=%2Fdashboard&organization=o2",
    );
    expect(loginRedirect("/dashboard", { organization: "a b&c" }).href).toBe(
      "/bff/login?returnTo=%2Fdashboard&organization=a%20b%26c",
    );
  });

  it("omits the organization parameter when no hint is given", () => {
    expect(loginRedirect("/dashboard", {}).href).toBe("/bff/login?returnTo=%2Fdashboard");
    expect(loginRedirect("/dashboard", { organization: "  " }).href).toBe(
      "/bff/login?returnTo=%2Fdashboard",
    );
  });

  it("always marks the redirect as a full-document navigation", () => {
    // Baked in rather than left to each call site: forgetting `reloadDocument`
    // is the same not-found bug in a quieter form.
    expect(loginRedirect("/dashboard").reloadDocument).toBe(true);
  });

  it("never builds a router `to` target", () => {
    expect(loginRedirect("/dashboard")).not.toHaveProperty("to");
  });

  it("is pure — it navigates nothing and reads no browser global", () => {
    // Precondition: this spec runs under SSR-like conditions.
    expect("location" in globalThis).toBe(false);
    expect("document" in globalThis).toBe(false);

    expect(() => loginRedirect("/dashboard")).not.toThrow();
  });
});

describe("requireAuth", () => {
  it("returns the authenticated user unchanged", () => {
    const authenticated: WallowUser = user({ roles: ["admin"] });
    const redirect: RedirectSpy = redirectSpy();

    expect(requireAuth({ user: authenticated, returnTo: "/dashboard", redirect })).toBe(
      authenticated,
    );
  });

  it("does not redirect when there is a user", () => {
    const redirect: RedirectSpy = redirectSpy();

    requireAuth({ user: user(), returnTo: "/dashboard", redirect });

    expect(redirect).not.toHaveBeenCalled();
  });

  it.each<readonly [string, null | undefined]>([
    ["null", null],
    ["undefined", undefined],
  ])("throws the injected redirect when the user is %s", (_label, absent) => {
    const redirect: RedirectSpy = redirectSpy();

    // Throwing, not returning: a returned redirect would let the guarded route
    // render for an anonymous visitor.
    const thrown: unknown = catchThrown(() =>
      requireAuth({ user: absent, returnTo: "/dashboard/organizations", redirect }),
    );

    expect(thrown).toBe(REDIRECT_SENTINEL);
  });

  it("hands redirect exactly what loginRedirect built, once", () => {
    const redirect: RedirectSpy = redirectSpy();

    catchThrown(() => requireAuth({ user: null, returnTo: "/dashboard/organizations", redirect }));

    expect(redirect).toHaveBeenCalledTimes(1);
    expect(redirect).toHaveBeenCalledWith(loginRedirect("/dashboard/organizations"));
  });

  it("never asks for a router `to` target", () => {
    const redirect: RedirectSpy = redirectSpy();

    catchThrown(() => requireAuth({ user: null, returnTo: "/dashboard", redirect }));

    const options = redirect.mock.calls[0]?.[0] as LoginRedirectOptions;
    expect(options.href).toBe("/bff/login?returnTo=%2Fdashboard");
    expect(options.reloadDocument).toBe(true);
    expect(options).not.toHaveProperty("to");
  });

  it("defaults the returnTo to the app root", () => {
    const redirect: RedirectSpy = redirectSpy();

    catchThrown(() => requireAuth({ user: null, redirect }));

    expect(redirect).toHaveBeenCalledWith(loginRedirect());
  });

  it("never touches a browser global on either path (SSR safety)", () => {
    // The whole point: a guard runs during SSR too, where `location` does not
    // exist. Reaching for it surfaces as a ReferenceError, not a redirect.
    expect("location" in globalThis).toBe(false);
    const redirect: RedirectSpy = redirectSpy();

    expect(() => requireAuth({ user: user(), redirect })).not.toThrow();
    expect(catchThrown(() => requireAuth({ user: null, redirect }))).toBe(REDIRECT_SENTINEL);
  });
});

describe("browser entry surface", () => {
  it.each(["loginRedirect", "requireAuth"])("exports %s from the package root", (name: string) => {
    // The AC is "exported from the SDK browser entry": apps import these from
    // `@bc-solutions-coder/sdk`, never from a deep internal path.
    expect(Object.keys(browserEntry)).toContain(name);
    expect((browserEntry as unknown as Record<string, unknown>)[name]).toBeDefined();
  });
});
