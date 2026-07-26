import { expect, test } from "@playwright/test";

/**
 * Signup (register) flow, end to end. BACKEND-DEPENDENT: register posts
 * `v1/identity/auth/register` through the h3 proxy into Wallow.Api, which creates
 * the account and emails a verification link. Needs the live seeded stack
 * (scripts/e2e.sh boots it). A failure here is a bug to file, not necessarily a
 * regression in this app — the flow crosses the proxy into Wallow.Api.
 *
 * The happy-path signal is the app-level hop to the /verify-email notice
 * (`verify-email-heading`), which RegisterForm navigates to on a 200; the
 * error-path signal is the `register-error` banner. Neither is a bare URL change:
 * the notice heading and the error banner are the screen's own rendered state.
 *
 * The email uses a unique local part per run so the happy-path account is always
 * fresh; the `@e2e.local` domain matches no seeded organisation, so the pre-submit
 * domain lookup 404s and the org-match interstitial never intercepts the submit.
 * The email_taken case targets the seeded admin (api/seed.json), the one address
 * guaranteed to already exist.
 */
const ADMIN_EMAIL = process.env.E2E_USER ?? "admin@wallow.dev";
const SIGNUP_PASSWORD = "E2eSignup123!";

test("register happy path reaches the verify-email notice", async ({ page }) => {
  await page.goto("/register");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  const email = `e2e-signup-${Date.now()}@e2e.local`;

  await page.getByTestId("register-email").fill(email);
  await page.getByTestId("register-password").fill(SIGNUP_PASSWORD);
  await page.getByTestId("register-confirm-password").fill(SIGNUP_PASSWORD);
  await page.getByTestId("register-terms").check();
  await page.getByTestId("register-privacy").check();
  await page.getByTestId("register-submit").click();

  await expect(page.getByTestId("verify-email-heading")).toBeVisible({ timeout: 15_000 });
});

test("register with an existing email surfaces the email_taken error", async ({ page }) => {
  await page.goto("/register");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("register-email").fill(ADMIN_EMAIL);
  await page.getByTestId("register-password").fill(SIGNUP_PASSWORD);
  await page.getByTestId("register-confirm-password").fill(SIGNUP_PASSWORD);
  await page.getByTestId("register-terms").check();
  await page.getByTestId("register-privacy").check();
  await page.getByTestId("register-submit").click();

  await expect(page.getByTestId("register-error")).toBeVisible({ timeout: 15_000 });
});
