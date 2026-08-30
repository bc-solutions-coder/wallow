import { expect, type Page, test } from "@playwright/test";

/** What `/bff/user` answers once the BFF session is gone. */
const UNAUTHENTICATED_STATUS = 401;

/**
 * BACKEND + CROSS-APP dependent (Wallow-xzha.4.3). This is NOT a render-only
 * reachability gate — it exercises the complete wallow-web -> wallow-auth ->
 * wallow-web login round trip and therefore needs the full three-origin stack up
 * and cross-wired (docker/docker-compose.test.yml, or `pnpm backend`), plus the
 * seeded admin from api/seed.json. Run it with the dedicated config:
 *   `pnpm --filter ./apps/wallow-web test:e2e:cross-app`
 * (set `E2E_BASE_URL=http://localhost:5053` against the compose stack -- :5053 is
 * that stack's classic default; `./scripts/e2e.sh` substitutes a per-run port
 * instead, Wallow-joo0). A failure
 * here can be a real cross-app regression, not necessarily a fault in this spec.
 *
 * Two tests share that stack: the login round trip itself, and (Wallow-vufu.1.3)
 * an authenticated mutation plus logout performed on the session it establishes.
 *
 * The journey traced in the routing audit:
 *   1. wallow-web's home "Get Started" link targets
 *      `/bff/login?returnTo=/dashboard/my-organizations` (src/routes/index.tsx). Both tests
 *      enter through that endpoint rather than clicking through `/`, because
 *      `/`'s `beforeLoad` shares the SSR defect noted below; the href IS the home
 *      page's Get Started contract.
 *   2. The BFF `/bff/login` builds PKCE+state+nonce and 302s into the OIDC
 *      authorize endpoint, which (unauthenticated) redirects to wallow-auth's
 *      `/login` — a DIFFERENT origin the browser follows.
 *   3. Password login succeeds; the same-origin exchange-ticket proxy sets the
 *      API auth cookie, the flow re-enters authorize (now authenticated), an OIDC
 *      code is issued, wallow-web's `/bff/callback` exchanges it for tokens, and
 *      the browser lands back on the original `returnTo` (`/dashboard/my-organizations`) with
 *      an authenticated wallow-web BFF session.
 *
 * ASSERTING THE AUTHENTICATED STATE via `dashboard-my-organizations`:
 *   The final signal is the dashboard's own `data-testid="dashboard-my-organizations"`,
 *   rendered by the authenticated `/dashboard/my-organizations` route. Reaching it through the
 *   real redirect (a full-page load) exercises the SSR fix from Wallow-cqoa: the
 *   `/dashboard` route's `beforeLoad` and the page's loader both run server-side, so
 *   `getWallowSdk()` now points the BFF client at the request's absolute origin
 *   and forwards the session cookie during SSR (Node's fetch has no cookie jar and
 *   cannot parse a relative URL). Before that fix the dashboard rendered an error
 *   boundary ("Failed to parse URL from /bff/user"); it now hydrates the signed-in
 *   organizations page. This is the strengthened assertion the earlier `/bff-demo`
 *   `bff-user-status` stand-in was a placeholder for.
 */
/**
 * Drive the whole cross-app login round trip and leave the browser on `returnTo`
 * with an authenticated wallow-web BFF session. Both tests below start here, so
 * the journey itself is described once:
 *
 *   1. Enter at the home page's "Get Started" target. We navigate to that exact
 *      href rather than clicking through `/`, because `/`'s `beforeLoad` shares
 *      the SSR defect noted above; the href IS the home page's Get Started
 *      contract.
 *   2. The redirects deposit the browser on wallow-auth's login screen. Wait for
 *      the auth app to hydrate before touching the form (its own readiness
 *      marker, per .claude/rules/E2E.md).
 *   3. Sign in with the seeded admin (api/seed.json), same credentials as
 *      apps/wallow-auth/e2e/login.spec.ts.
 *   4. The OIDC round trip returns to wallow-web on the original `returnTo`,
 *      which we then wait to hydrate.
 *
 * `/dashboard`'s `beforeLoad` auth gate only lets a child route render once the
 * SSR `getUser()` resolved a signed-in user, so a caller that sees its route's
 * own testid has proof of an authenticated session.
 */
async function signInAndLandOn(page: Page, returnTo: string): Promise<void> {
  await page.goto(`/bff/login?returnTo=${encodeURIComponent(returnTo)}`);

  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });

  await page.getByTestId("login-email").fill(process.env.E2E_USER ?? "admin@wallow.dev");
  await page.getByTestId("login-password").fill(process.env.E2E_PASSWORD ?? "Admin123!");
  await page.getByTestId("login-submit").click();

  await page.waitForURL((url) => url.pathname === returnTo, { timeout: 30_000 });
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
}

test("cross-app login journey establishes an authenticated wallow-web session", async ({
  page,
}) => {
  await signInAndLandOn(page, "/dashboard/my-organizations");

  // The `dashboard-my-organizations` signal described in the header block above.
  await expect(page.getByTestId("dashboard-my-organizations")).toBeVisible({ timeout: 15_000 });
});

/**
 * The authenticated-MUTATION half of the journey (Wallow-vufu.1.3). Signing in
 * proves the session cookie exists; it does NOT prove the browser can spend that
 * session on a state-changing request. Nothing in the suite covered that, which
 * is why the CSRF regression in Wallow-vufu.1.1 shipped undetected: the SDK's
 * shared interceptor stamped `x-csrf-token` only when `setCsrfToken()` had been
 * called, and the only remaining caller was `/bff-demo`. Every real dashboard
 * mutation therefore sent no header and the BFF answered 403 CSRF_INVALID, with
 * the login journey above still perfectly green.
 *
 * ASSERTING THE MUTATION SUCCEEDED via the form's own reset:
 *   `CreateOrganizationForm` calls `form.reset()` from the mutation's per-call
 *   `onSuccess`, so the `organization-name` field going empty is an app-level
 *   signal that the POST actually succeeded — not an incidental side effect. On
 *   the 403 the field keeps the typed name and `organization-create-error`
 *   appears instead, so this assertion fails exactly when the regression is
 *   present (verified by stashing the Wallow-vufu.1.1 fix and re-running).
 *   We deliberately do NOT assert the new organization appears in
 *   `organizations-table`: the create returns 201 with a real id, but the list
 *   query still omits it (an org-membership/list-scoping gap recorded on
 *   Wallow-vufu.1.2), which is unrelated to CSRF and would make this spec fail
 *   for the wrong reason.
 */
test("an authenticated mutation and logout complete on a real BFF session", async ({ page }) => {
  await signInAndLandOn(page, "/dashboard/organizations");
  await expect(page.getByTestId("dashboard-organizations")).toBeVisible({ timeout: 15_000 });

  // 1. The mutation: a real POST through the BFF, which the API rejects unless
  //    the SDK interceptor resolved a CSRF token for it.
  await page.getByTestId("organization-name").fill(`E2E CSRF Org ${Date.now()}`);
  await page.getByTestId("organization-create-submit").click();

  await expect(page.getByTestId("organization-name")).toHaveValue("", { timeout: 15_000 });
  await expect(page.getByTestId("organization-create-error")).toHaveCount(0);

  // 2. Logout. Scoped through `dashboard-nav` because the same control also
  //    lives in the mobile drawer, and the rail is the one this viewport renders.
  await page.getByTestId("dashboard-nav").getByTestId("dashboard-logout-link").click();
  await page.waitForURL((url) => !url.pathname.startsWith("/dashboard"), { timeout: 30_000 });
  await expect(page.getByTestId("dashboard-logout-error")).toHaveCount(0);

  // 3. The signed-out state, asserted as app-level signals rather than the
  //    landing URL: the public landing page renders and the authenticated shell
  //    is gone from the tree...
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await expect(page.getByTestId("home-heading")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("dashboard-nav")).toHaveCount(0);

  // ...and the BFF itself no longer recognises the browser. `page.request` shares
  // the page's cookie jar, so a 401 here is the destroyed session speaking, which
  // is the signal that survives the caveat below.
  //
  // CAVEAT — why we do NOT re-enter `/dashboard/**` to prove sign-out: `/bff/logout`
  // ends the wallow-web BFF session, but the API's own auth cookie on the issuer
  // origin outlives it. Navigating back to a gated route therefore replays the OIDC
  // authorize round trip, gets a code issued WITHOUT a login prompt, and silently
  // lands on the dashboard again — observed on this stack. That is upstream
  // single-sign-out behaviour, not a failure of the logout under test here.
  const bffUser = await page.request.get("/bff/user");
  expect(bffUser.status()).toBe(UNAUTHENTICATED_STATUS);
});
