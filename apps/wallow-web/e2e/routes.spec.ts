import { expect, test } from "@playwright/test";

/**
 * Route-reachability gate: every route listed here must render through a real
 * browser and reach hydration (`data-app-ready="true"`, stamped by
 * src/components/ready-indicator.tsx once the client bundle hydrates the SSR'd
 * document). It asserts reachability, not flow correctness, and takes NO backend
 * dependency — mirroring apps/wallow-auth/e2e/routes.spec.ts.
 *
 * Both routes here render for an anonymous visitor with the API absent:
 *   - `/` — the marketing landing. Its `beforeLoad` resolves `getUser()` during
 *     SSR against the app's OWN h3 `/bff/user` bridge, which 401s off the absent
 *     session cookie without ever reaching the issuer, so the gate reads "not
 *     signed in" and falls through to `LandingPage`. That bridge only loads when
 *     the BFF env is present, which is why playwright.config.ts supplies the
 *     Aspire dev values to its `webServer` — without them the module throws at
 *     import and every `/bff/*` request 500s.
 *   - `/bff-demo` — no `beforeLoad` gate at all; its `getUser()` call runs
 *     client-side and merely logs when the API is down, which does not block the
 *     readiness marker.
 *
 * `/dashboard/**` stays excluded: it is auth-gated, so `beforeLoad` drives a real
 * BFF login navigation (`/bff/login` -> OIDC) when unauthenticated, needing the
 * API. That cross-app login journey is covered by the backend-dependent suite
 * (Wallow-xzha.4.3), not this render-only gate.
 */
const routes: string[] = ["/", "/bff-demo"];

const FIRST_ERROR_STATUS = 400;

for (const route of routes) {
  test(`renders ${route}`, async ({ page }) => {
    const response = await page.goto(route);
    expect(response, `no response for ${route}`).not.toBeNull();
    expect(response!.status(), `${route} returned ${response!.status()}`).toBeLessThan(
      FIRST_ERROR_STATUS,
    );
    await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 15_000 });
  });
}
