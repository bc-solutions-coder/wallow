import { expect, type APIRequestContext, type Page } from "@playwright/test";

import { waitForEmailBody } from "./mailpit";

/**
 * Stands up a throwaway, sign-in-capable user for the backend-dependent specs
 * that must not touch the seeded admin (mfa.spec.ts, failure-copy.spec.ts): a
 * lockout or an MFA enrolment on the shared admin would break every sibling
 * spec that signs it in with a password. NOT a spec file — its name is outside
 * Playwright's `*.spec.ts` glob, so the runner never treats it as a test.
 *
 * MAILPIT-DEPENDENT: login requires a confirmed email, and the confirmation link
 * is read back out of Mailpit (see mailpit.ts for which inbox that is).
 */

/** The password every throwaway user is registered with. */
export const E2E_PASSWORD = "E2eMfa123!";

/** Wide enough that two specs registering in the same millisecond never collide. */
const EMAIL_NONCE_SPACE = 1_000_000;

/** A fresh `@e2e.local` address so parallel specs and reruns never share a user. */
export function uniqueEmail(prefix: string): string {
  const nonce: number = Math.floor(Math.random() * EMAIL_NONCE_SPACE);
  return `${prefix}-${Date.now()}-${nonce}@e2e.local`;
}

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
 * Mailpit-delivered link, leaving an account that can sign in with `E2E_PASSWORD`.
 */
export async function registerAndConfirm(
  page: Page,
  request: APIRequestContext,
  email: string,
): Promise<void> {
  await page.goto("/register");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("register-email").fill(email);
  await page.getByTestId("register-password").fill(E2E_PASSWORD);
  await page.getByTestId("register-confirm-password").fill(E2E_PASSWORD);
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
