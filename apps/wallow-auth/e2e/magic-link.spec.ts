import { expect, test } from "@playwright/test";

import { waitForEmailBody } from "./mailpit";

/**
 * Magic-link login, end to end. BACKEND-DEPENDENT and MAILPIT-DEPENDENT: the send
 * half posts `v1/identity/auth/passwordless/magic-link` through the h3 proxy into
 * Wallow.Api, which emails the link via SMTP to Mailpit; the verify half redeems
 * the emailed token on load at `/login?magicLinkToken=…`. Needs the live seeded
 * stack + Mailpit (scripts/e2e.sh boots both). A failure is a bug to file.
 *
 * The signed-in signal is the app-level `login-signed-in` banner, not a URL
 * change — a bare /login carries no OIDC returnUrl, so a successful verify lands
 * on the authenticated state in place (same disposition login.spec.ts relies on).
 *
 * Verify MUST target the seeded admin (api/seed.json): the send endpoint returns
 * success even for unknown addresses (anti-enumeration), but only a real user's
 * token redeems into a session.
 *
 * Wallow-gfph fixed the underlying defect: PasswordlessService now signs the token
 * through Data Protection's shared, Redis-persisted key ring instead of an extracted
 * per-instance HMAC key, so a token minted in the send request validates in the later
 * verify request. (OTP was always unaffected — it compares the stored code directly
 * with no signature.)
 */
const ADMIN_EMAIL = process.env.E2E_USER ?? "admin@wallow.dev";

/** Pull the `magicLinkToken` value out of the emailed sign-in link. */
function extractMagicLinkToken(emailHtml: string): string {
  // The template HTML-encodes the href, but with no returnUrl/client_id cargo the
  // token is the whole query, so no `&amp;` sits inside it. Stop at the first
  // delimiter regardless, to stay correct if cargo is ever added.
  const match: RegExpMatchArray | null = emailHtml.match(/magicLinkToken=(?<token>[^&"'<\s]+)/u);

  if (match?.groups?.token === undefined) {
    throw new Error("magic-link email did not contain a magicLinkToken");
  }

  return match.groups.token;
}

test("magic-link send then verify reaches an authenticated state", async ({ page, request }) => {
  await page.goto("/login");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await page.getByTestId("login-tab-magic-link").click();
  await page.getByTestId("login-magic-link-email").fill(ADMIN_EMAIL);
  await page.getByTestId("login-magic-link-submit").click();

  // The form is replaced by the anti-enumeration confirmation once the send lands.
  await expect(page.getByTestId("login-magic-link-sent")).toBeVisible({ timeout: 15_000 });

  const emailHtml: string = await waitForEmailBody(request, {
    to: ADMIN_EMAIL,
    subject: "Your Magic Link",
  });
  const token: string = extractMagicLinkToken(emailHtml);

  // The emailed link points at the API's configured AuthUrl origin; drive the
  // token through the app under test instead, which redeems it on load.
  await page.goto(`/login?magicLinkToken=${token}`);
  await expect(page.getByTestId("login-signed-in")).toBeVisible({ timeout: 15_000 });
});
