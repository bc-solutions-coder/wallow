import { expect, test } from "@playwright/test";

/**
 * Route-reachability gate: every route listed here must render through a real
 * browser and reach hydration (`data-app-ready="true"`, stamped by
 * src/components/ready-indicator.tsx once the client bundle hydrates the SSR'd
 * document). It asserts reachability, not flow correctness, and takes NO backend
 * dependency — mirroring apps/wallow-auth/e2e/routes.spec.ts.
 *
 * Only `/bff-demo` qualifies today because it is the one public route with no
 * `beforeLoad` gate: it SSRs a 200 and hydrates with the API absent (its own
 * `getUser()` call runs client-side and merely logs when the API is down, which
 * does not block the readiness marker).
 *
 * Deliberately excluded until their own beads land:
 *   - `/` — its `beforeLoad` runs `getUser()` during SSR against a relative
 *     `/bff/user`, which 500s regardless of backend (a separate SSR defect).
 *   - `/dashboard/**` — auth-gated: `beforeLoad` drives a real BFF login
 *     navigation (`/bff/login` -> OIDC) when unauthenticated, needing the API.
 *     That cross-app login journey is covered by the backend-dependent suite
 *     (Wallow-xzha.4.3), not this render-only gate.
 */
const routes: string[] = ["/bff-demo"];

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
