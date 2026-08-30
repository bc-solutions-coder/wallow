import { expect, type Page, test } from "@playwright/test";

/** What the external origin's own `/bff/user` answers once its BFF session is gone. */
const UNAUTHENTICATED_STATUS = 401;

/**
 * `bff-example` (docker/docker-compose.test.yml) hosts wallow-web's own image/route surface
 * but authenticates as the seeded THIRD-PARTY `bff-example-client` (api/seed.json) rather than
 * `wallow-web-client` -- it stands in for a genuinely separate site built on the same SDK, per
 * design doc Sec 14. Its host port defaults to 3003
 * (`ports: ["${E2E_BFF_PORT:-3003}:3000"]`); `scripts/e2e.sh` allocates a per-run
 * port and passes it as `E2E_BFF_EXAMPLE_URL` (Wallow-joo0). There is no
 * equivalent under `pnpm backend` (Aspire has no bff-example service), so unlike
 * login-journey.spec.ts this spec needs the containerised stack specifically.
 */
const BFF_EXAMPLE_ORIGIN = process.env.E2E_BFF_EXAMPLE_URL ?? "http://localhost:3003";

/**
 * BACKEND + CROSS-APP dependent (Wallow-yp3e.4, plan 5.5.6 -- "sign in with Wallow from
 * another site"). Exercises the complete bff-example -> wallow-auth (login + consent) ->
 * bff-example round trip for the external-origin reference client, needing the full stack
 * cross-wired exactly like login-journey.spec.ts: `./scripts/e2e.sh`, or
 * `E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app`
 * (both :5053 and bff-example's :3003 are classic defaults; `scripts/e2e.sh` substitutes
 * per-run ports and threads them through `E2E_BASE_URL`/`E2E_BFF_EXAMPLE_URL` instead,
 * independently of each other -- Wallow-joo0). Plus the seeded admin from `api/seed.json`.
 *
 * WHY THIS IS A DIFFERENT FLOW FROM login-journey.spec.ts, NOT A DUPLICATE: `wallow-web-client`
 * is seeded first-party (`"firstParty": true` in `api/seed.json`, which registers it with
 * implicit consent), so its authorize round trip never renders consent. `bff-example-client`
 * is a third-party client bound to the Wallow organization, so the API routes it through
 * wallow-auth's interactive consent screen --
 * the leg this bead exists to prove renders real, seeded scope descriptions rather than the
 * null placeholders design doc Sec 14.3 documented before plan Sec 5.5.1 seeded
 * `OpenIddictScopes` rows.
 *
 * bff-demo.tsx's own "Sign in" button hardcodes `login("/")` (returnTo="/"), which would land
 * the round trip on bff-example's home page rather than back on `/bff-demo`. This spec instead
 * navigates straight to `/bff/login?returnTo=`, the exact endpoint `login()` itself redirects
 * to, so the round trip returns to `/bff-demo` where the demo's own
 * `bff-user-status`/`bff-user-email` testids give the authenticated signal directly.
 */
async function signInAtExternalOrigin(page: Page): Promise<void> {
  await page.goto(`${BFF_EXAMPLE_ORIGIN}/bff/login?returnTo=${encodeURIComponent("/bff-demo")}`);

  // Cross-origin redirect lands on wallow-auth's login screen.
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await page.getByTestId("login-email").fill(process.env.E2E_USER ?? "admin@wallow.dev");
  await page.getByTestId("login-password").fill(process.env.E2E_PASSWORD ?? "Admin123!");
  await page.getByTestId("login-submit").click();

  // bff-example-client is a real third-party client: the API sends the browser to wallow-auth's
  // /consent instead of straight back to bff-example's callback.
  await page.waitForURL((url) => url.pathname === "/consent", { timeout: 20_000 });
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await expect(page.getByTestId("consent-heading")).toBeVisible();

  // Real, seeded scope descriptions (api/seed.json apiScopes, synced by
  // OpenIddictScopeSyncService), not the null placeholders plan Sec 5.5.1/14.3 fixed.
  const scopes = page.getByTestId("consent-scopes");
  await expect(scopes).toContainText("Confirm your identity to this application");
  await expect(scopes).toContainText("Your name and profile details");
  await expect(scopes).toContainText("Access to read inquiries and inquiry data");

  await page.getByTestId("consent-approve").click();

  // Consent grant -> code -> token -> bff-example's own callback -> returnTo (/bff-demo).
  await page.waitForURL(
    (url) => url.origin === BFF_EXAMPLE_ORIGIN && url.pathname === "/bff-demo",
    { timeout: 30_000 },
  );
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
}

test("sign-in with Wallow from an external origin completes end to end", async ({ page }) => {
  await signInAtExternalOrigin(page);

  await expect(page.getByTestId("bff-user-status")).toHaveText("authenticated", {
    timeout: 15_000,
  });
  await expect(page.getByTestId("bff-user-email")).toHaveText(
    process.env.E2E_USER ?? "admin@wallow.dev",
  );

  // userinfo/id-token claims carry org_id/org_name: the SDK's mapClaims reads them into the
  // session as organizationId/organizationName, which the external origin's own /bff/user
  // view exposes. A bound client's session always names its organization.
  const bffUser = await page.request.get(`${BFF_EXAMPLE_ORIGIN}/bff/user`);
  expect(bffUser.ok()).toBe(true);
  const bffUserBody = (await bffUser.json()) as Record<string, unknown>;
  expect(bffUserBody.organizationId).toBeTruthy();
  expect(bffUserBody.organizationName).toBeTruthy();

  // Logout ends the session at the external origin -- the "clears the local session at the
  // other relying party" leg of the acceptance criteria. Proven the same way
  // login-journey.spec.ts proves its own logout: page.request shares the browser's cookie
  // jar, so a 401 from the external origin's own /bff/user is the destroyed session speaking.
  //
  // The pathname check is load-bearing: the click starts an async POST /bff/logout, and only
  // after that resolves does the SDK navigate -- to "/", because the opaque manual-redirect
  // response hides the Location header (packages/sdk/src/auth.ts endSession). An origin-only
  // predicate matches the /bff-demo URL the page is ALREADY on, so waitForURL resolves
  // immediately and the /bff/user check below races the in-flight logout POST -- 200 instead
  // of 401 under CI load. Waiting for "/" orders the check after the session is destroyed.
  await page.getByTestId("bff-logout").click();
  await page.waitForURL((url) => url.origin === BFF_EXAMPLE_ORIGIN && url.pathname === "/", {
    timeout: 20_000,
  });

  const bffUserAfterLogout = await page.request.get(`${BFF_EXAMPLE_ORIGIN}/bff/user`);
  expect(bffUserAfterLogout.status()).toBe(UNAUTHENTICATED_STATUS);
});
