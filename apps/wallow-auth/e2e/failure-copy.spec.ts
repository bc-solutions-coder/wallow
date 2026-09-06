import { expect, type APIRequestContext, test } from "@playwright/test";

import { E2E_PASSWORD, registerAndConfirm, uniqueEmail } from "./register";

/**
 * The two refusals whose copy the login screen cannot get from its own tables:
 * the account lockout (423 `Auth.LockedOut`, worded from the catalog's `detail`)
 * and the passwordless throttle (429 `RateLimit.Exceeded`, worded from the
 * `Retry-After` the API sends). BACKEND-DEPENDENT and MAILPIT-DEPENDENT: both
 * cross the passthrough proxy into Wallow.Api, and the lockout needs a throwaway
 * user (register.ts) because locking the seeded admin would fail every sibling
 * spec that signs it in. Needs the live stack + Mailpit (scripts/e2e.sh boots
 * both). A failure here is a bug to file.
 *
 * The refusals are PROVOKED over the API and ASSERTED through the UI: the
 * spending of attempts is not what is under test, the sentence the screen shows
 * once they are spent is. Each loop stops at the first refused status rather
 * than counting to a configured threshold, so a changed lockout or throttle
 * setting moves the loop, not the assertion. The throttle is per address, so a
 * fresh unknown address is enough — the send answers 200 for unknown users by
 * design (anti-enumeration), and the throttle is counted before that lookup.
 */

const LOCKED = 423;
const TOO_MANY_REQUESTS = 429;

/** Enough attempts to cross any sane threshold; the loop returns at the first refusal. */
const MAX_ATTEMPTS = 20;

/** Post wrong passwords until the API answers 423 (ASP.NET Identity's lockout). */
async function exhaustLoginAttempts(request: APIRequestContext, email: string): Promise<void> {
  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt += 1) {
    // Sequential on purpose: the loop stops at the FIRST refusal, and attempts in
    // flight together would race the threshold and over-spend it.
    // eslint-disable-next-line no-await-in-loop
    const response = await request.post("/v1/identity/auth/login", {
      data: { email, password: "not-the-password", rememberMe: false },
    });

    if (response.status() === LOCKED) {
      return;
    }
  }

  throw new Error(`login never locked out after ${MAX_ATTEMPTS} failed attempts`);
}

/** Post OTP sends until the API answers 429 (the passwordless per-address throttle). */
async function exhaustOtpSends(request: APIRequestContext, email: string): Promise<void> {
  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt += 1) {
    // Sequential for the same reason as the login loop above.
    // eslint-disable-next-line no-await-in-loop
    const response = await request.post("/v1/identity/auth/passwordless/otp", {
      data: { email },
    });

    if (response.status() === TOO_MANY_REQUESTS) {
      return;
    }
  }

  throw new Error(`otp send never throttled after ${MAX_ATTEMPTS} sends`);
}

test("a locked-out account reads the catalog's lockout sentence", async ({ page, request }) => {
  const email: string = uniqueEmail("e2e-lockout");
  await registerAndConfirm(page, request, email);
  await exhaustLoginAttempts(request, email);

  // The RIGHT password now: a lockout refuses the account, not the credential,
  // and "incorrect password" here would send the user round a loop that only
  // extends the lock.
  await page.goto("/login");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();
  await page.getByTestId("login-email").fill(email);
  await page.getByTestId("login-password").fill(E2E_PASSWORD);
  await page.getByTestId("login-submit").click();

  const error = page.getByTestId("login-error");
  await expect(error).toContainText("This account is locked. Try again later.", {
    timeout: 15_000,
  });
  await expect(page.getByTestId("login-signed-in")).toHaveCount(0);
});

test("a throttled otp send reads the wait from Retry-After", async ({ page, request }) => {
  const email: string = uniqueEmail("e2e-throttle");
  await exhaustOtpSends(request, email);

  await page.goto("/login");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();
  await page.getByTestId("login-tab-otp").click();
  await page.getByTestId("login-otp-email").fill(email);
  await page.getByTestId("login-otp-send-submit").click();

  // The number is the window's remaining seconds, so only its presence is pinned.
  const error = page.getByTestId("login-error");
  await expect(error).toContainText(
    /Too many requests\. Please wait \d+ seconds? and try again\./u,
    {
      timeout: 15_000,
    },
  );
  // The email form stays up: the throttle is a refusal to SEND, not a sent code.
  await expect(page.getByTestId("login-otp-sent")).toHaveCount(0);
});
