import { expect, type APIRequestContext, type Page, test } from "@playwright/test";

import { waitForEmailBody } from "./mailpit";
import { generateTotp } from "./totp";

/**
 * MFA TOTP lifecycle, end to end — enrollment then challenge. BACKEND-DEPENDENT
 * and MAILPIT-DEPENDENT: the flow crosses the h3 proxy into Wallow.Api
 * (`v1/identity/mfa/enroll/totp`, `.../enroll/confirm`, `.../auth/mfa/verify`) and
 * reads the account-verification email out of Mailpit. Needs the live seeded stack
 * + Mailpit (scripts/e2e.sh boots both). A failure here is a bug to file.
 *
 * ── WHY A THROWAWAY USER, AND WHY THE TWO TESTS ARE SERIAL ───────────────────
 *
 * There is no seeded MFA-enabled account (api/seed.json has none), and enabling
 * MFA on the shared seeded admin would break every sibling spec that signs that
 * admin in with a password — a plain login would start returning mfaRequired. So
 * each run registers a FRESH `@e2e.local` user, confirms its email via the Mailpit
 * link (login requires a confirmed email), enrolls MFA on it, and only then can
 * exercise the challenge. Enrollment is a prerequisite for the challenge, so the
 * two tests run `describe.serial` and share the enrolled user + its TOTP secret:
 * the challenge cannot be reached without first standing an enrolled user up.
 *
 * ── HOW /mfa/enroll GETS A SESSION ───────────────────────────────────────────
 *
 * `enroll/totp` resolves the user from the API auth cookie (or an MfaPartial
 * cookie). A bare `/login` never sets that cookie — it only renders the signed-in
 * banner. Signing in with `returnUrl=/mfa/enroll` instead drives the
 * exchange-ticket path: the login mints a ticket, the app redirects through
 * `exchange-ticket` (which `SignInAsync`s the full auth cookie, same origin), and
 * `Url.IsLocalUrl("/mfa/enroll")` lets it land the browser on the enroll screen
 * already carrying the cookie the enroll call needs.
 *
 * ── THE ASSERTIONS ───────────────────────────────────────────────────────────
 *
 * Per Wallow-xzha.4.2 the QR itself is not asserted (it is drawn by qrcode.react;
 * a stable oracle it is not) — the manual-entry secret (`mfa-enroll-secret`) and
 * the post-confirm backup codes (`mfa-enroll-backup-codes`) are the app-level
 * signals that enrollment succeeded. The challenge asserts `mfa-challenge-success`,
 * the screen's own verified state, not a URL. The TOTP codes are computed locally
 * from the enrollment secret with the server's exact parameters (see totp.ts).
 */
const MFA_PASSWORD = "E2eMfa123!";

/**
 * The enrolled user and its base32 secret, shared across the serial describe: the
 * enroll test stands them up, the challenge test reuses them.
 */
let mfaEmail = "";
let mfaSecret = "";

/** Pull the `/verify-email/confirm?token=…&email=…` query out of the confirmation email. */
function extractVerifyQuery(emailHtml: string): string {
  const decoded: string = emailHtml.replaceAll("&amp;", "&");
  const match: RegExpMatchArray | null = decoded.match(
    /\/verify-email\/confirm\?(?<query>token=[^"'<\s]+)/u,
  );

  if (match?.groups?.query === undefined) {
    throw new Error("verification email did not contain a verify-email/confirm link");
  }

  return match.groups.query;
}

/**
 * Register a fresh user through the signup screen and confirm its email via the
 * Mailpit-delivered link, leaving an account that can sign in (login requires a
 * confirmed email).
 */
async function registerAndConfirm(
  page: Page,
  request: APIRequestContext,
  email: string,
): Promise<void> {
  await page.goto("/register");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("register-email").fill(email);
  await page.getByTestId("register-password").fill(MFA_PASSWORD);
  await page.getByTestId("register-confirm-password").fill(MFA_PASSWORD);
  await page.getByTestId("register-terms").check();
  await page.getByTestId("register-privacy").check();
  await page.getByTestId("register-submit").click();

  await expect(page.getByTestId("verify-email-heading")).toBeVisible({ timeout: 15_000 });

  const emailHtml: string = await waitForEmailBody(request, {
    to: email,
    subject: "Verify your email address",
  });
  const query: string = extractVerifyQuery(emailHtml);

  await page.goto(`/verify-email/confirm?${query}`);
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();
  await expect(page.getByTestId("verify-email-confirm-success")).toBeVisible({ timeout: 15_000 });
}

test.describe.serial("mfa totp lifecycle", () => {
  test("totp enrollment issues backup codes", async ({ page, request }) => {
    mfaEmail = `e2e-mfa-${Date.now()}@e2e.local`;
    await registerAndConfirm(page, request, mfaEmail);

    // Sign in with a returnUrl so the exchange-ticket path sets a full auth cookie
    // and lands the browser on the enroll screen (see the session note above).
    await page.goto(`/login?returnUrl=${encodeURIComponent("/mfa/enroll")}`);
    await expect(page.locator("[data-app-ready='true']")).toBeAttached();
    await page.getByTestId("login-email").fill(mfaEmail);
    await page.getByTestId("login-password").fill(MFA_PASSWORD);
    await page.getByTestId("login-submit").click();

    await expect(page.locator("[data-app-ready='true']")).toBeAttached();
    await expect(page.getByTestId("mfa-enroll-secret")).toBeVisible({ timeout: 15_000 });

    mfaSecret = ((await page.getByTestId("mfa-enroll-secret").textContent()) ?? "").trim();
    expect(mfaSecret).not.toBe("");

    await page.getByTestId("mfa-enroll-code").fill(generateTotp(mfaSecret));
    await page.getByTestId("mfa-enroll-submit").click();

    await expect(page.getByTestId("mfa-enroll-backup-codes")).toBeVisible({ timeout: 15_000 });
  });

  test("totp challenge verifies after an mfa-required login", async ({ page }) => {
    expect(mfaSecret).not.toBe("");

    // A fresh context with no cookies: the enrolled user now hits the MFA gate, so
    // a password login returns mfaRequired and the app hands off to /mfa/challenge.
    await page.goto("/login");
    await expect(page.locator("[data-app-ready='true']")).toBeAttached();
    await page.getByTestId("login-email").fill(mfaEmail);
    await page.getByTestId("login-password").fill(MFA_PASSWORD);
    await page.getByTestId("login-submit").click();

    await expect(page.getByTestId("mfa-challenge-code")).toBeVisible({ timeout: 15_000 });
    await page.getByTestId("mfa-challenge-code").fill(generateTotp(mfaSecret));
    await page.getByTestId("mfa-challenge-submit").click();

    await expect(page.getByTestId("mfa-challenge-success")).toBeVisible({ timeout: 15_000 });
  });
});
