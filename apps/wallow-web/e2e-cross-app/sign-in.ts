import { expect, type Page } from "@playwright/test";

/** The seeded admin (api/seed.json), the same credentials as apps/wallow-auth/e2e/login.spec.ts. */
export const ADMIN_EMAIL: string = process.env.E2E_USER ?? "admin@wallow.dev";
const ADMIN_PASSWORD: string = process.env.E2E_PASSWORD ?? "Admin123!";

/**
 * Fill and submit wallow-auth's login form once the auth app has hydrated (its
 * own readiness marker, per .claude/rules/E2E.md). The caller decides where the
 * round trip should land.
 */
export async function fillLoginForm(page: Page): Promise<void> {
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await page.getByTestId("login-email").fill(ADMIN_EMAIL);
  await page.getByTestId("login-password").fill(ADMIN_PASSWORD);
  await page.getByTestId("login-submit").click();
}

/**
 * Drive the whole cross-app login round trip and leave the browser on `returnTo`
 * with an authenticated wallow-web BFF session. Every journey in this suite
 * starts here, so the journey itself is described once:
 *
 *   1. Enter at the home page's "Get Started" target. We navigate to that exact
 *      href rather than clicking through `/`, because `/`'s `beforeLoad` runs
 *      server-side; the href IS the home page's Get Started contract.
 *   2. The redirects deposit the browser on wallow-auth's login screen. Wait for
 *      the auth app to hydrate before touching the form (its own readiness
 *      marker, per .claude/rules/E2E.md).
 *   3. Sign in with the seeded admin (api/seed.json), same credentials as
 *      apps/wallow-auth/e2e/login.spec.ts.
 *   4. The OIDC round trip returns to wallow-web on the original `returnTo`,
 *      which we then wait to hydrate.
 *
 * `/dashboard`'s `beforeLoad` auth gate only lets a child route render once the
 * SSR `getUser()` resolved a signed-in user, so a caller that sees its route's
 * own testid has proof of an authenticated session.
 */
export async function signInAndLandOn(page: Page, returnTo: string): Promise<void> {
  await page.goto(`/bff/login?returnTo=${encodeURIComponent(returnTo)}`);
  await fillLoginForm(page);
  await page.waitForURL((url) => url.pathname === returnTo, { timeout: 30_000 });
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
}
