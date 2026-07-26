import { expect, test } from "@playwright/test";

/**
 * Logout flow. The `signed_out=true` landing's "Return to application" link is
 * BACKEND-DEPENDENT: it renders only after `GET /v1/identity/auth/redirect-uri/
 * validate` (through the h3 proxy into Wallow.Api) confirms the
 * `post_logout_redirect_uri` is on a registered client's allow-list. Like
 * login.spec.ts this needs the live seeded stack (scripts/e2e.sh); a failure is
 * a bug to file, not necessarily a regression in this app.
 *
 * Every assertion is an app-level signal via data-testid — the presence or
 * absence of `logout-return-link`, and the confirm/landing heading text — never
 * an incidental URL side effect. `logout-confirm-heading` is deliberately shared
 * across both phases (the oracle's choice), so the phase is told apart by copy.
 *
 * The allowed origin is `http://localhost:5051` — the API's own configured
 * `AuthUrl` in docker-compose.test.yml, which OpenIddictRedirectUriValidator adds
 * to the allow-list unconditionally, so this holds regardless of which OIDC
 * clients the seeder registered.
 */
const ALLOWED_REDIRECT_URI = "http://localhost:5051/after-logout";
const DISALLOWED_REDIRECT_URI = "https://evil.example.com/";

test("confirm phase offers the sign-out handoff to /connect/logout", async ({ page }) => {
  await page.goto("/logout");
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await expect(page.getByTestId("logout-confirm-heading")).toHaveText("Sign out");

  const confirmButton = page.getByTestId("logout-confirm-button");
  await expect(confirmButton).toBeVisible();
  // The end-session handoff is a real same-origin navigation to the OpenIddict
  // endpoint the h3 proxy serves, not an in-app route.
  await expect(confirmButton).toHaveAttribute("href", /\/connect\/logout/u);
});

test("signed-out landing shows the return link for an allow-listed redirect uri", async ({
  page,
}) => {
  await page.goto(
    `/logout?signed_out=true&post_logout_redirect_uri=${encodeURIComponent(ALLOWED_REDIRECT_URI)}`,
  );
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  await expect(page.getByTestId("logout-confirm-heading")).toHaveText("Signed out");

  // The link is gated on the server's allow-list answer; an allowed origin flips
  // it visible.
  const returnLink = page.getByTestId("logout-return-link");
  await expect(returnLink).toBeVisible({ timeout: 15_000 });
  await expect(returnLink).toHaveAttribute("href", ALLOWED_REDIRECT_URI);
});

test("signed-out landing withholds the return link for a rejected redirect uri", async ({
  page,
}) => {
  await page.goto(
    `/logout?signed_out=true&post_logout_redirect_uri=${encodeURIComponent(DISALLOWED_REDIRECT_URI)}`,
  );
  await expect(page.locator("[data-app-ready='true']")).toBeAttached();

  // The landing still confirms the sign-out — the user IS signed out — but the
  // link stays absent because the server refused the origin (fail-closed). The
  // allow-listed case above proves the backend is genuinely discriminating and
  // this absence is not just an unreachable API.
  await expect(page.getByTestId("logout-confirm-heading")).toHaveText("Signed out");
  await expect(page.getByTestId("logout-return-link")).toHaveCount(0);
});
