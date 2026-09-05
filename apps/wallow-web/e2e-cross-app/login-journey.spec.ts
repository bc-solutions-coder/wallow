import { expect, test } from "@playwright/test";

import { signInAndLandOn } from "./sign-in";

/** What `/bff/user` answers once the BFF session is gone. */
const UNAUTHENTICATED_STATUS = 401;

/**
 * BACKEND + CROSS-APP dependent: the complete wallow-web -> wallow-auth ->
 * wallow-web login round trip over the three-origin stack (docker-compose.test.yml
 * or `pnpm backend`) and the seeded admin from api/seed.json. Run it with
 * `pnpm --filter ./apps/wallow-web test:e2e:cross-app`. A failure here can be a
 * real cross-app regression, not necessarily a fault in this spec. Two tests
 * share the stack: the round trip itself, and an authenticated mutation plus
 * logout on the session it establishes. Both start from `./sign-in`'s
 * `signInAndLandOn`, which describes the journey step by step.
 */
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
 * mutation therefore sent no header and the BFF answered a 403 CSRF rejection, with
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
