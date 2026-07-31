import { describe, expect, it } from "vitest";

import { Route as indexRoute } from "./index";

/**
 * The Home route (`/`): a `beforeLoad` redirect to `/login`, with no markup of
 * its own.
 */
describe("/ (Home) route", () => {
  it("redirects to /login instead of rendering a page", () => {
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

    // `redirect()` throws a carrier whose payload sits under `options`, and the
    // target is spelled `href`, not `to`: `/login` resolves as a raw location
    // rather than against the typed route tree.
    const redirect = thrown as { options?: { href?: string; statusCode?: number } };

    expect(redirect.options?.href).toBe("/login");
  });

  it("redirects with a 307, preserving the request method", () => {
    // The default would be a 301 — permanently cached by the browser, which
    // would make `/` a one-way door if this app ever grew a real landing page.
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
    // A component-level `<Navigate>` would 200 with markup and bounce
    // client-side; `beforeLoad` makes SSR emit `Location: /login` on a 3xx.
    expect(indexRoute.options.component).toBeUndefined();
  });
});
