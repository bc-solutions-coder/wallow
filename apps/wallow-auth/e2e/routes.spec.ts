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
];

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
