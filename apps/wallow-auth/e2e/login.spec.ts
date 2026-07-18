import { expect, test } from "@playwright/test";

/**
 * Password-login smoke test. Unlike routes.spec.ts this REQUIRES the backend
 * (`pnpm backend` + seeded admin from api/seed.json). A failure here is a bug
 * to file, not necessarily a regression in this app — the login flow crosses
 * the h3 proxy into Wallow.Api.
 */
test("password login reaches an authenticated state", async ({ page }) => {
  await page.goto("/login");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();
  await page.getByTestId("login-email").fill(process.env.E2E_USER ?? "admin@wallow.dev");
  await page.getByTestId("login-password").fill(process.env.E2E_PASSWORD ?? "Admin123!");
  await page.getByTestId("login-submit").click();
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/u, { timeout: 15_000 });
});
