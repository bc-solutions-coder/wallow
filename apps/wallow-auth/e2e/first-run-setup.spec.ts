import { expect, test, type APIResponse } from "@playwright/test";

/**
 * First-run setup journey. REQUIRES the backend — and, unlike every other
 * backend spec, a backend that has NO administrator yet: scripts/e2e.sh seeds
 * its stack with the admin bootstrap disabled precisely so this journey creates
 * admin@wallow.dev through the /setup page. It runs alone in the `first-run`
 * Playwright project, which the main project depends on: the account created
 * here is the one the rest of the suite signs in as.
 *
 * Against an already-provisioned backend (a local dev stack) the probe below
 * skips the journey instead of failing it. Inside scripts/e2e.sh — recognised
 * by the empty E2E_SEED_ADMIN_EMAIL it exports — a skip would silently drop
 * this coverage, so there an unavailable or already-completed setup is a
 * failure.
 */

const ADMIN_EMAIL = process.env.E2E_USER ?? "admin@wallow.dev";
const ADMIN_PASSWORD = process.env.E2E_PASSWORD ?? "Admin123!";
// NOT the seeded organization's name: setup creates a brand-new organization,
// and the seeded one already owns that name's unique slug.
const ORGANIZATION_NAME = "Wallow E2E";

const SLOW_BACKEND_TIMEOUT_MS = 15_000;

test("first run: login funnels to setup, setup creates the admin, the admin signs in", async ({
  page,
  request,
}) => {
  const probe: APIResponse | null = await request
    .get("/v1/identity/setup/status")
    .catch(() => null);
  const setupRequired: boolean =
    probe !== null &&
    probe.ok() &&
    ((await probe.json()) as { setupRequired?: boolean }).setupRequired === true;

  if (process.env.E2E_SEED_ADMIN_EMAIL === "") {
    expect(setupRequired, "the stack was seeded without an admin, yet setup is not open").toBe(
      true,
    );
  } else {
    test.skip(!setupRequired, "setup already completed — this backend provisioned its admin");
  }

  // While no administrator exists, /login's beforeLoad forwards every visitor
  // to /setup — the redirect is the app-level signal, seen as the setup screen.
  await page.goto("/login");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();
  await expect(page.getByTestId("setup-heading")).toBeVisible();

  await page.getByTestId("setup-email").fill(ADMIN_EMAIL);
  await page.getByTestId("setup-password").fill(ADMIN_PASSWORD);
  await page.getByTestId("setup-confirm-password").fill(ADMIN_PASSWORD);
  await page.getByTestId("setup-first-name").fill("Admin");
  await page.getByTestId("setup-last-name").fill("User");
  await page.getByTestId("setup-organization-name").fill(ORGANIZATION_NAME);
  await page.getByTestId("setup-submit").click();

  await expect(page.getByTestId("setup-success-heading")).toBeVisible({
    timeout: SLOW_BACKEND_TIMEOUT_MS,
  });

  // The success card's link is a full document navigation on purpose, so the
  // login gate re-reads setup status — now complete — and stays on /login.
  await page.getByTestId("setup-signin-link").click();
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();
  await page.getByTestId("login-email").fill(ADMIN_EMAIL);
  await page.getByTestId("login-password").fill(ADMIN_PASSWORD);
  await page.getByTestId("login-submit").click();
  await expect(page.getByTestId("login-signed-in")).toBeVisible({
    timeout: SLOW_BACKEND_TIMEOUT_MS,
  });
});
