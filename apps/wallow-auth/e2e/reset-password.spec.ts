import { expect, test } from "@playwright/test";

import { waitForEmailBody } from "./mailpit";

/**
 * Reset-password flow, end to end. BACKEND-DEPENDENT and MAILPIT-DEPENDENT: the
 * request half posts `v1/identity/auth/forgot-password` (which emails a reset
 * link via SMTP to Mailpit), and the reset half posts
 * `v1/identity/auth/reset-password` with the real token pulled from that email.
 * Needs the live seeded stack + Mailpit (scripts/e2e.sh boots both). A failure
 * here is a bug to file — the flow crosses the h3 proxy into Wallow.Api.
 *
 * The reset targets the seeded admin (api/seed.json) because forgot-password only
 * emails a link for an account that actually exists (anti-enumeration returns 200
 * either way, but sends nothing for an unknown address). The new password is set
 * to the SAME value the admin already has, so this spec does not mutate the shared
 * seeded credential the sibling password/OTP/magic-link specs sign in with — it
 * proves the round trip without polluting the fixture.
 *
 * The success signal is the login-page banner from Wallow-xzha.1.2:
 * ResetPasswordForm navigates to `/login?message=password_reset` on a completed
 * reset, and the login screen renders `login-password-reset-notice` for exactly
 * that token. Asserting the banner (an app-level rendered signal) is what proves
 * the reset succeeded, not the bare URL.
 */
const ADMIN_EMAIL = process.env.E2E_USER ?? "admin@wallow.dev";
const ADMIN_PASSWORD = process.env.E2E_PASSWORD ?? "Admin123!";

/**
 * Pull the `/reset-password?token=…&email=…` query string out of the emailed
 * reset link. The template HTML-encodes the href (`&amp;`), so entities are
 * decoded first; the token/email stay URL-encoded, which is exactly what the app
 * route re-decodes on navigation.
 */
function extractResetQuery(emailHtml: string): string {
  const decoded: string = emailHtml.replaceAll("&amp;", "&");
  const match: RegExpMatchArray | null = decoded.match(
    /\/reset-password\?(?<query>token=[^"'<\s]+)/u,
  );

  if (match?.groups?.query === undefined) {
    throw new Error("reset email did not contain a reset-password link");
  }

  return match.groups.query;
}

test("forgot then reset with the emailed token shows the login success banner", async ({
  page,
  request,
}) => {
  await page.goto("/forgot-password");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("forgot-password-email").fill(ADMIN_EMAIL);
  await page.getByTestId("forgot-password-submit").click();
  await expect(page.getByTestId("forgot-password-success")).toBeVisible({ timeout: 15_000 });

  const emailHtml: string = await waitForEmailBody(request, {
    to: ADMIN_EMAIL,
    subject: "Password Reset Request",
  });
  const query: string = extractResetQuery(emailHtml);

  await page.goto(`/reset-password?${query}`);
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("reset-password-new-password").fill(ADMIN_PASSWORD);
  await page.getByTestId("reset-password-confirm").fill(ADMIN_PASSWORD);
  await page.getByTestId("reset-password-submit").click();

  await expect(page.getByTestId("login-password-reset-notice")).toBeVisible({ timeout: 15_000 });
});
