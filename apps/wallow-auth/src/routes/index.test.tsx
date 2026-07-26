import { describe, expect, it } from "vitest";

import { Route as indexRoute } from "./index";

/**
 * Characterization spec for the Home route (`/`) — a redirect to `/login`.
 *
 * ── WHY THIS FILE EXISTS AND WHY IT IS ALREADY GREEN ─────────────────────────
 *
 * Home is one of the five screens in this bead's acceptance criteria, but unlike
 * the other four it needs no porting: `routes/index.tsx` was implemented back in
 * Wallow-vec7.1.4 and Wallow-vec7.3.16 explicitly marks it DO-NOT-TOUCH. It had
 * no test, though, so the acceptance criterion rested on nothing executable.
 *
 * These tests were therefore GREEN the moment they were written — they are
 * deliberately not part of this bead's RED count. They pin existing behaviour
 * this bead is accountable for rather than driving new code, and they touch only
 * this new file; `index.tsx` is not modified.
 */
describe("/ (Home) route", () => {
  it("redirects to /login instead of rendering a page", () => {
    // The oracle has no markup at all — `/` exists only to bounce the user to
    // the login screen.
    const beforeLoad = indexRoute.options.beforeLoad as () => void;

    expect(beforeLoad).toBeDefined();
    expect(() => {
      beforeLoad();
    }).toThrow();
  });

  it("names /login as the redirect target", () => {
    const beforeLoad = indexRoute.options.beforeLoad as () => void;

    let thrown: unknown;
    try {
      beforeLoad();
    } catch (error: unknown) {
      thrown = error;
    }

    // `redirect()` throws a carrier whose payload sits under `options` — the
    // shape was read off the thrown value rather than assumed.
    //
    // `href`, not `to`: `/login` is resolved as a raw location rather than
    // against the typed route tree (bd memory `tanstack-router-redirect-to-an-
    // unregistered-route-use-href-not-to`).
    const redirect = thrown as { options?: { href?: string; statusCode?: number } };

    expect(redirect.options?.href).toBe("/login");
  });

  it("redirects with a 307, preserving the request method", () => {
    // The 307 Wallow-vec7.3.16 boot-verified. Pinned because the default would
    // be a 301 — permanently cached by the browser, which would make `/` a
    // one-way door if this app ever grew a real landing page.
    const beforeLoad = indexRoute.options.beforeLoad as () => void;

    let thrown: unknown;
    try {
      beforeLoad();
    } catch (error: unknown) {
      thrown = error;
    }

    const redirect = thrown as { options?: { statusCode?: number } };

    expect(redirect.options?.statusCode).toBe(307);
  });

  it("redirects from beforeLoad so the server emits a real HTTP redirect", () => {
    // The redirect is issued in `beforeLoad` rather than by a component-level
    // `<Navigate>` precisely so SSR returns a 3xx with `Location: /login` — a
    // component-level redirect would 200 with markup and bounce client-side.
    expect(indexRoute.options.component).toBeUndefined();
  });
});
