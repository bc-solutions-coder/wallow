import { expect, type Page, test } from "@playwright/test";

/**
 * Forgot-password flow, end to end. BACKEND-DEPENDENT: submit posts
 * `v1/identity/auth/forgot-password` through the h3 proxy into Wallow.Api. Needs
 * the live seeded stack (scripts/e2e.sh boots it). A failure here is a bug to
 * file — the flow crosses the proxy into Wallow.Api.
 *
 * ANTI-ENUMERATION is the whole point of this screen: the endpoint returns 200
 * regardless of whether the address exists, and the UI renders the SAME
 * `forgot-password-success` confirmation either way. So both tests assert the
 * identical confirmation renders, and that NO `forgot-password-error` testid
 * appears — its absence is deliberate (ForgotPasswordForm.tsx has no error
 * surface, by design), and asserting the absence pins that the branches stay
 * byte-identical to anyone diffing the page.
 */
const ADMIN_EMAIL = process.env.E2E_USER ?? "admin@wallow.dev";

async function submitForgotPassword(page: Page, email: string): Promise<void> {
  await page.goto("/forgot-password");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("forgot-password-email").fill(email);
  await page.getByTestId("forgot-password-submit").click();

  await expect(page.getByTestId("forgot-password-success")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("forgot-password-error")).toHaveCount(0);
}

test("forgot-password for a real account shows the anti-enumeration confirmation", async ({
  page,
}) => {
  await submitForgotPassword(page, ADMIN_EMAIL);
});

test("forgot-password for an unknown account shows the identical confirmation", async ({
  page,
}) => {
  await submitForgotPassword(page, `e2e-nobody-${Date.now()}@e2e.local`);
});
