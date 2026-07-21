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
 * ASSERTING THE AUTHENTICATED STATE — why not `dashboard-apps` directly:
 *   The ideal final signal is the dashboard's own `data-testid="dashboard-apps"`.
 *   It is currently unreachable through the real redirect (a full-page load) for a
 *   reason UNRELATED to this journey: the SDK's `getUser()` fetches the RELATIVE
 *   URL `/bff/user` (packages/sdk/src/auth.ts), which Node's fetch cannot parse
 *   during SSR. The `/dashboard` route's `beforeLoad` calls `getUser()`
 *   server-side, so the authenticated dashboard renders an error boundary
 *   ("Failed to parse URL from /bff/user") instead of hydrating — a pre-existing
 *   wallow-web SSR defect (Wallow-cqoa). Until it lands, we assert the SAME
 *   authenticated-wallow-web-session fact via `/bff-demo`, whose `getUser()` runs
 *   client-side (SSR-safe) and stamps `bff-user-status="authenticated"` +
 *   `bff-user-email`. Swap the two assertions below for `dashboard-apps` once
 *   Wallow-cqoa is fixed.
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

  // 5. Confirm the round trip produced an AUTHENTICATED wallow-web session, read
  //    from wallow-web's own client-rendered signed-in signal (see header note
  //    on why this stands in for `dashboard-apps` today).
  await page.goto("/bff-demo");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await expect(page.getByTestId("bff-user-status")).toHaveText("authenticated", {
    timeout: 15_000,
  });
  await expect(page.getByTestId("bff-user-email")).toHaveText(
    process.env.E2E_USER ?? "admin@wallow.dev",
  );
});
