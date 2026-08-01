import { expect, test } from "@playwright/test";

/**
 * Route-reachability gate: every screen the app claims to serve must render
 * through a real browser and reach hydration (`data-app-ready="true"`, stamped
 * by src/components/ready-indicator.tsx). This is the render-only deletion gate
 * from docs/plans/2026-07-17-auth-cutover-reset.md re-proven continuously —
 * it asserts reachability, not flow correctness.
 */
const routes: string[] = [
  "/",
  "/login",
  "/register",
  "/forgot-password",
  "/reset-password",
  "/verify-email",
  "/verify-email/confirm",
  "/mfa/challenge",
  "/mfa/enroll",
  "/consent",
  "/logout",
  "/invitation",
  "/accept-terms",
  "/privacy",
  "/terms",
  "/error",
  "/access-request",
];

const FIRST_ERROR_STATUS = 400;

/**
 * A path no route claims. Needs no backend — the SSR host answers it entirely
 * from the router's own not-found state.
 */
const UNMATCHED_ROUTE = "/no-such-auth-page";
const NOT_FOUND_STATUS = 404;

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

/**
 * The other half of the reachability gate (Wallow-ffpq.2.7): the paths the app
 * does NOT serve must say so properly — a real 404 status carrying the app's own
 * not-found screen, not a 200, and not the framework's bare "Not Found" text.
 * Backend-free, like the loop above.
 */
test(`404s on ${UNMATCHED_ROUTE} with the not-found screen`, async ({ page }) => {
  const response = await page.goto(UNMATCHED_ROUTE);

  expect(response, `no response for ${UNMATCHED_ROUTE}`).not.toBeNull();
  expect(response!.status()).toBe(NOT_FOUND_STATUS);
  await expect(page.getByTestId("not-found-heading")).toBeVisible();
  await expect(page.getByTestId("not-found-login-link")).toHaveAttribute("href", "/login");
  // The 404 is a page of this app like any other, so it hydrates like any other.
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 15_000 });
});
