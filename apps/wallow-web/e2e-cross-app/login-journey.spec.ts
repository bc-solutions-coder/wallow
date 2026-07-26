import { expect, test } from "@playwright/test";

/**
 * BACKEND + CROSS-APP dependent (Wallow-xzha.4.3). This is NOT a render-only
 * reachability gate — it exercises the complete wallow-web -> wallow-auth ->
 * wallow-web login round trip and therefore needs the full three-origin stack up
 * and cross-wired (docker/docker-compose.test.yml, or `pnpm backend`), plus the
 * seeded admin from api/seed.json. Run it with the dedicated config:
 *   `pnpm --filter ./apps/wallow-web test:e2e:cross-app`
 * (set `E2E_BASE_URL=http://localhost:5053` against the compose stack). A failure
 * here can be a real cross-app regression, not necessarily a fault in this spec.
 *
 * The journey traced in the routing audit:
 *   1. wallow-web's home "Get Started" link targets
 *      `/bff/login?returnTo=/dashboard/apps` (src/routes/index.tsx). We navigate
 *      to that exact href rather than clicking through `/`, because `/`'s
 *      `beforeLoad` shares the SSR defect noted below; the href IS the home
 *      page's Get Started contract.
 *   2. The BFF `/bff/login` builds PKCE+state+nonce and 302s into the OIDC
 *      authorize endpoint, which (unauthenticated) redirects to wallow-auth's
 *      `/login` — a DIFFERENT origin the browser follows.
 *   3. Password login succeeds; the same-origin exchange-ticket proxy sets the
 *      API auth cookie, the flow re-enters authorize (now authenticated), an OIDC
 *      code is issued, wallow-web's `/bff/callback` exchanges it for tokens, and
 *      the browser lands back on the original `returnTo` (`/dashboard/apps`) with
 *      an authenticated wallow-web BFF session.
 *
 * ASSERTING THE AUTHENTICATED STATE via `dashboard-apps`:
 *   The final signal is the dashboard's own `data-testid="dashboard-apps"`,
 *   rendered by the authenticated `/dashboard/apps` route. Reaching it through the
 *   real redirect (a full-page load) exercises the SSR fix from Wallow-cqoa: the
 *   `/dashboard` route's `beforeLoad` and the apps loader both run server-side, so
 *   `getWallowSdk()` now points the BFF client at the request's absolute origin
 *   and forwards the session cookie during SSR (Node's fetch has no cookie jar and
 *   cannot parse a relative URL). Before that fix the dashboard rendered an error
 *   boundary ("Failed to parse URL from /bff/user"); it now hydrates the signed-in
 *   apps list. This is the strengthened assertion the earlier `/bff-demo`
 *   `bff-user-status` stand-in was a placeholder for.
 */
test("cross-app login journey establishes an authenticated wallow-web session", async ({
  page,
}) => {
  // 1. Enter the journey at the home page's "Get Started" target.
  await page.goto("/bff/login?returnTo=/dashboard/apps");

  // 2. The redirects deposit the browser on wallow-auth's login screen. Wait for
  //    the auth app to hydrate before touching the form (its own readiness
  //    marker, per .claude/rules/E2E.md).
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });

  // 3. Sign in with the seeded admin (api/seed.json), same credentials as
  //    apps/wallow-auth/e2e/login.spec.ts.
  await page.getByTestId("login-email").fill(process.env.E2E_USER ?? "admin@wallow.dev");
  await page.getByTestId("login-password").fill(process.env.E2E_PASSWORD ?? "Admin123!");
  await page.getByTestId("login-submit").click();

  // 4. The OIDC round trip returns to wallow-web on the original `returnTo`.
  await page.waitForURL((url) => url.pathname === "/dashboard/apps", { timeout: 30_000 });

  // 5. Confirm the round trip produced an AUTHENTICATED wallow-web session by
  //    asserting the authenticated dashboard itself renders: wait for wallow-web
  //    to hydrate, then for the `/dashboard/apps` route's own signal. Its
  //    `beforeLoad` auth gate only lets this route render when the SSR `getUser()`
  //    resolved the signed-in user (Wallow-cqoa), so `dashboard-apps` being
  //    attached is the direct proof of an authenticated session.
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await expect(page.getByTestId("dashboard-apps")).toBeVisible({ timeout: 15_000 });
});
