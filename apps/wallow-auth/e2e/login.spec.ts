import { expect, test } from "@playwright/test";

/**
 * Password-login smoke test. Unlike routes.spec.ts this REQUIRES the backend
 * (`pnpm backend` + seeded admin from api/seed.json). A failure here is a bug
 * to file, not necessarily a regression in this app — the login flow crosses
 * the h3 proxy into Wallow.Api.
 *
 * A direct /login visit carries no OIDC returnUrl, so the screen deliberately
 * stays on /login and renders the signed-in state rather than navigating —
 * `login-signed-in` is the authenticated signal, not a URL change.
 */
test("password login reaches an authenticated state", async ({ page }) => {
  await page.goto("/login");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();
  await page.getByTestId("login-email").fill(process.env.E2E_USER ?? "admin@wallow.dev");
  await page.getByTestId("login-password").fill(process.env.E2E_PASSWORD ?? "Admin123!");
  await page.getByTestId("login-submit").click();
  await expect(page.getByTestId("login-signed-in")).toBeVisible({ timeout: 15_000 });
});
