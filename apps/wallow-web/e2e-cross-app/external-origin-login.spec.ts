import { expect, type Page, test } from "@playwright/test";

import { ADMIN_EMAIL, fillLoginForm } from "./sign-in";

/** What the external origin's own `/bff/user` answers once its BFF session is gone. */
const UNAUTHENTICATED_STATUS = 401;
/** What `POST /contact` answers when the service-account inquiry landed. */
const CONTACT_OK_STATUS = 200;
/** How long to let any stray redirect fire before asserting the error screen is terminal. */
const ERROR_PAGE_SETTLE_MS = 1000;

/**
 * `bff-example` (docker/docker-compose.test.yml) is the external relying-party example,
 * `apps/minimal-app`: its own image, its own origin, authenticating as the seeded THIRD-PARTY
 * `bff-example-client` (api/seed.json) — a genuinely separate site built on the published SDK
 * alone. Its host port defaults to 3003 (`ports: ["127.0.0.1:${E2E_BFF_PORT:-3003}:3010"]`);
 * `scripts/e2e.sh` allocates a per-run port and passes it as `E2E_BFF_EXAMPLE_URL`
 * (Wallow-joo0). There is no equivalent under `pnpm backend` (Aspire has no bff-example
 * service), so unlike login-journey.spec.ts this suite needs the containerised stack
 * specifically.
 */
const BFF_EXAMPLE_ORIGIN = process.env.E2E_BFF_EXAMPLE_URL ?? "http://localhost:3003";

/**
 * BACKEND + CROSS-APP dependent (issue #151 — the acceptance journey for the external-RP
 * spec #131). Three serial stages over the full containerised stack (`./scripts/e2e.sh`):
 *
 *   1. Anonymous contact: `POST /contact` with no cookies reaches the API as the seeded
 *      service account (`sa-wallow-nightly-sync`, client-credentials, `inquiries.write`).
 *   2. Sign-in from the external origin through wallow-auth's branded consent, then a typed
 *      API call through the example's `/api` proxy, then back-channel logout: signing out at
 *      the PLATFORM origin (wallow-web) revokes the external origin's session server-to-server
 *      with its front-channel iframe deliberately blocked.
 *   3. Suspension: an org admin suspends `bff-example-client` through the organization
 *      surface, and a fresh login attempt at the external origin dead-ends on wallow-auth's
 *      error screen — no redirect back to the out-of-service client.
 *
 * SERIAL is load-bearing: the suspension stage poisons every earlier one, so the stages run
 * in this order in one worker (`test.describe.serial`).
 *
 * WHY THIS IS A DIFFERENT FLOW FROM login-journey.spec.ts, NOT A DUPLICATE: `wallow-web-client`
 * is seeded first-party (`"firstParty": true` in `api/seed.json`, which registers it with
 * implicit consent), so its authorize round trip never renders consent. `bff-example-client`
 * is a third-party client bound to the Wallow organization, so the API routes it through
 * wallow-auth's interactive consent screen with real, seeded scope descriptions.
 */
/**
 * Drive the external origin's sign-in end to end: minimal-app's `/bff/login` with
 * `returnTo=/` (the home page carries the `bff-*` testids), wallow-auth's login form, the
 * third-party consent screen, and the code round trip back to the example's own callback.
 */
async function signInAtExternalOrigin(page: Page): Promise<void> {
  await page.goto(`${BFF_EXAMPLE_ORIGIN}/bff/login?returnTo=${encodeURIComponent("/")}`);

  // Cross-origin redirect lands on wallow-auth's login screen.
  await fillLoginForm(page);

  // bff-example-client is a real third-party client: the API sends the browser to wallow-auth's
  // /consent instead of straight back to bff-example's callback.
  await page.waitForURL((url) => url.pathname === "/consent", { timeout: 20_000 });
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await expect(page.getByTestId("consent-heading")).toBeVisible();

  // The transaction-scoped client context: the screen is branded as the
  // requesting third-party client — its seeded displayName headlines, attributed to the
  // organization that owns it — resolved from the pending authorize request's returnUrl,
  // never from an anonymous per-client branding read.
  await expect(page.getByTestId("auth-header-name")).toHaveText("BFF Example");
  await expect(page.getByTestId("auth-header-organization")).toHaveText("by Wallow");
  await expect(page).toHaveTitle("Sign in · BFF Example");

  // The fork footer stays the PLATFORM's even inside a third-party authorize
  // transaction: client branding skins the header, never the attribution.
  await expect(page.getByTestId("fork-attribution")).toContainText("A Wallow App");

  // Real, seeded scope descriptions (api/seed.json apiScopes, synced by
  // OpenIddictScopeSyncService), not null placeholders.
  const scopes = page.getByTestId("consent-scopes");
  await expect(scopes).toContainText("Confirm your identity to this application");
  await expect(scopes).toContainText("Your name and profile details");
  await expect(scopes).toContainText("Access to read inquiries and inquiry data");

  await page.getByTestId("consent-approve").click();

  // Consent grant -> code -> token -> bff-example's own callback -> returnTo (/).
  await page.waitForURL((url) => url.origin === BFF_EXAMPLE_ORIGIN && url.pathname === "/", {
    timeout: 30_000,
  });
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
}

test.describe.serial("external relying-party acceptance journey", () => {
  test("an anonymous contact message reaches the API through the service account", async ({
    request,
  }) => {
    // The plain request fixture carries no cookies: nobody is signed in anywhere. The
    // example's own server route submits the inquiry as `sa-wallow-nightly-sync`
    // (client-credentials), the only way an anonymous action can reach the platform.
    const response = await request.post(`${BFF_EXAMPLE_ORIGIN}/contact`, {
      data: {
        name: "E2E visitor",
        email: "visitor@example.com",
        message: "hello from the three-origin acceptance suite",
      },
    });

    expect(response.status()).toBe(CONTACT_OK_STATUS);
    const body = (await response.json()) as Record<string, unknown>;
    expect(body.id).toBeTruthy();
    expect(body.status).toBe("received");
  });

  test("external sign-in, typed API call, and back-channel logout from the platform origin", async ({
    page,
  }) => {
    await signInAtExternalOrigin(page);

    await expect(page.getByTestId("bff-user-status")).toHaveText("authenticated", {
      timeout: 15_000,
    });
    await expect(page.getByTestId("bff-user-email")).toHaveText(ADMIN_EMAIL);

    // The typed API call: the page's own button drives the generated
    // `usersGetCurrentUser` through the example's `/api` proxy, which attaches the
    // session's bearer server-side.
    await page.getByTestId("bff-call-api").click();
    await expect(page.getByTestId("bff-api-result")).toContainText(ADMIN_EMAIL, {
      timeout: 15_000,
    });

    // userinfo/id-token claims carry org_id/org_name: the SDK's mapClaims reads them into the
    // session as organizationId/organizationName, which the external origin's own /bff/user
    // view exposes. A bound client's session always names its organization.
    const bffUser = await page.request.get(`${BFF_EXAMPLE_ORIGIN}/bff/user`);
    expect(bffUser.ok()).toBe(true);
    const bffUserBody = (await bffUser.json()) as Record<string, unknown>;
    expect(bffUserBody.organizationId).toBeTruthy();
    expect(bffUserBody.organizationName).toBeTruthy();

    // ---- Back-channel logout, proven from the platform origin ----
    // Same browser, same OP session: entering wallow-web replays the authorize round trip
    // silently (first-party client, no consent) and lands authenticated on the dashboard.
    // The target is /dashboard/my-organizations DELIBERATELY: it needs no organization
    // context, and a silent re-login carries no organization hint — if another journey has
    // given the admin a second membership, a context-needing page (like
    // /dashboard/organizations) 403s server-side. Any dashboard page carries the logout
    // control, which is all this hop is for.
    await page.goto(`/bff/login?returnTo=${encodeURIComponent("/dashboard/my-organizations")}`);
    await expect(page.getByTestId("dashboard-my-organizations")).toBeVisible({ timeout: 30_000 });

    // Disable the example's FRONT-channel receiver for this browser: with the logged-out
    // page's iframe blocked, only server-to-server back-channel delivery
    // (Clients__1__BackchannelLogoutUri -> the example's /bff/backchannel-logout, sessions
    // in Valkey) can revoke the external origin's session — which is exactly what this
    // stage exists to prove.
    await page.route(`${BFF_EXAMPLE_ORIGIN}/bff/frontchannel-logout*`, (route) => route.abort());

    // The external session is alive going into the logout, so the 401 below is the
    // back-channel delivery speaking, not leftover state.
    const beforeLogout = await page.request.get(`${BFF_EXAMPLE_ORIGIN}/bff/user`);
    expect(beforeLogout.ok()).toBe(true);

    // TEST-STACK QUIRK: every origin here shares the cookie host "localhost", so
    // wallow-web's pages can see bff-example's non-HttpOnly `-csrf` double-submit cookie
    // (`wallow_bff_example` from BFF_APP_ID in docker-compose.test.yml). The browser SDK
    // matches the CSRF cookie by that suffix, and on plain HTTP (no `__Host-` tiebreaker)
    // the foreign cookie can win, making wallow-web's logout POST carry the wrong token
    // and 403. A real deployment separates the hosts; model that by dropping the foreign
    // cookie — the external origin only needs it for state-changing BROWSER calls, and
    // everything this stage still does against bff-example is a GET.
    await page.context().clearCookies({ name: "wallow_bff_example-csrf" });

    await page.getByTestId("dashboard-nav").getByTestId("dashboard-logout-link").click();
    await page.waitForURL((url) => !url.pathname.startsWith("/dashboard"), { timeout: 30_000 });

    // Delivery is asynchronous server-to-server work, so poll rather than assert once.
    await expect
      .poll(
        async () => {
          const response = await page.request.get(`${BFF_EXAMPLE_ORIGIN}/bff/user`);
          return response.status();
        },
        { timeout: 20_000 },
      )
      .toBe(UNAUTHENTICATED_STATUS);
  });

  test("suspending the client at the organization surface stops new external logins", async ({
    page,
  }) => {
    // A fresh context (fresh cookie jar), so this is a full login at wallow-web.
    await page.goto(`/bff/login?returnTo=${encodeURIComponent("/dashboard/organizations")}`);
    await fillLoginForm(page);
    await expect(page.getByTestId("dashboard-organizations")).toBeVisible({ timeout: 30_000 });

    // The org-surface suspension, exactly as the dashboard's own mutations issue it:
    // through wallow-web's /api proxy with the session's CSRF token. /bff/user hands the
    // browser both the admin's organizationId (the Wallow org owns bff-example-client) and
    // the double-submit token the proxy requires on a state-changing request.
    const bffUser = await page.request.get("/bff/user");
    expect(bffUser.ok()).toBe(true);
    const { organizationId, csrfToken } = (await bffUser.json()) as {
      organizationId?: string;
      csrfToken?: string;
    };
    expect(organizationId).toBeTruthy();
    expect(csrfToken).toBeTruthy();

    const suspend = await page.request.post(
      `/api/v1/identity/organizations/${organizationId}/clients/bff-example-client/suspend`,
      { headers: { "x-csrf-token": csrfToken ?? "" } },
    );
    expect(suspend.ok(), `suspend answered ${suspend.status()}: ${await suspend.text()}`).toBe(
      true,
    );

    // A fresh login attempt at the external origin dead-ends on wallow-auth's error screen:
    // the authorize endpoint refuses the client BEFORE anyone signs in, on the auth host —
    // a client out of service gets no traffic back, so the browser never returns to
    // bff-example.
    await page.goto(`${BFF_EXAMPLE_ORIGIN}/bff/login?returnTo=${encodeURIComponent("/")}`);
    await page.waitForURL((url) => url.pathname === "/error", { timeout: 20_000 });
    expect(new URL(page.url()).origin).not.toBe(BFF_EXAMPLE_ORIGIN);
    expect(new URL(page.url()).searchParams.get("reason")).toBe("client_suspended");

    await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
    await expect(page.getByTestId("error-message")).toContainText("suspended by its organization");

    // No redirect back to the relying party: the error screen is terminal. Give any
    // pending navigation a beat, then confirm the browser is still on the error page.
    await page.waitForTimeout(ERROR_PAGE_SETTLE_MS);
    expect(new URL(page.url()).pathname).toBe("/error");
  });
});
