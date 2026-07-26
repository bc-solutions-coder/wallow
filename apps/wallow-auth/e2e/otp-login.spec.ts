import { expect, test } from "@playwright/test";

import { waitForEmailBody } from "./mailpit";

/**
 * OTP login tab, end to end — the passwordless SIGN-IN tab on /login, NOT MFA.
 * BACKEND-DEPENDENT and MAILPIT-DEPENDENT: send posts `v1/identity/auth/
 * passwordless/otp` through the h3 proxy into Wallow.Api, which emails a 6-digit
 * code via SMTP to Mailpit; verify posts `.../passwordless/otp/verify`. Needs the
 * live seeded stack + Mailpit (scripts/e2e.sh boots both). A failure is a bug to
 * file.
 *
 * The signed-in signal is the app-level `login-signed-in` banner, not a URL
 * change — a bare /login carries no OIDC returnUrl, so a successful verify lands
 * on the authenticated state in place (same disposition login.spec.ts relies on).
 *
 * Verify MUST target the seeded admin (api/seed.json): the send endpoint returns
 * success even for unknown addresses (anti-enumeration), but only a real user's
 * code redeems into a session.
 */
const ADMIN_EMAIL = process.env.E2E_USER ?? "admin@wallow.dev";

/** Pull the 6-digit passcode out of the emailed code block. */
function extractOtpCode(emailHtml: string): string {
  // The code sits in its own centred, wide-letter-spaced paragraph in the
  // "otpcode" template — target that element so the many other numbers in the
  // email (font sizes, ports, padding) cannot be mistaken for the code.
  const match: RegExpMatchArray | null = emailHtml.match(
    /letter-spacing:\s*8px;">\s*(?<code>\d{6})\s*</u,
  );

  if (match?.groups?.code === undefined) {
    throw new Error("otp email did not contain a 6-digit code");
  }

  return match.groups.code;
}

test("otp send then verify reaches an authenticated state", async ({ page, request }) => {
  await page.goto("/login");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("login-tab-otp").click();
  await page.getByTestId("login-otp-email").fill(ADMIN_EMAIL);
  await page.getByTestId("login-otp-send-submit").click();

  // The email form flips to the code form once the send lands.
  await expect(page.getByTestId("login-otp-sent")).toBeVisible({ timeout: 15_000 });

  const emailHtml: string = await waitForEmailBody(request, {
    to: ADMIN_EMAIL,
    subject: "Your Login Code",
  });
  const code: string = extractOtpCode(emailHtml);

  await page.getByTestId("login-otp-code").fill(code);
  await page.getByTestId("login-otp-verify-submit").click();

  await expect(page.getByTestId("login-signed-in")).toBeVisible({ timeout: 15_000 });
});
