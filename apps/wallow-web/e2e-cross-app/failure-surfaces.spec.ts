import { expect, type Page, type Route, test } from "@playwright/test";

import { signInAndLandOn } from "./sign-in";

/**
 * BACKEND + CROSS-APP dependent: the five failure surfaces the catalog names,
 * proven on a real signed-in session (the seeded admin from api/seed.json over
 * the three-origin stack; `./sign-in` describes the round trip).
 *
 * Each scenario starts from a REAL page and then injects ONE failure with
 * Playwright's request interception, at the browser's own `/api/...` call (the
 * same-origin BFF proxy the SDK is configured with), so what the screen shows
 * is the app's genuine resolution of that failure: the shipped copy, the
 * registry, the banner's actions, the toast, the form's field placement, the
 * router's not-found path. The injected bodies mirror what the API or the BFF
 * actually writes for that status (`application/problem+json` with a `code`),
 * because a body without one is an unrecognised response, not the failure
 * under test.
 *
 * Only the loader-404 scenario needs no interception: the API's own answer for
 * an unknown organization id drives the route's not-found path end to end.
 *
 * Nothing here mutates seeded state: the one mutation clicked (archive) never
 * leaves the browser, and the create form's POST is answered by the fixture.
 */

const BAD_REQUEST = 400;
const UNAUTHORIZED = 401;
const NOT_FOUND = 404;
const TOO_MANY_REQUESTS = 429;

/** The organizations list, from which one seeded org is picked. */
const ORGANIZATIONS_PAGE = "/dashboard/organizations";
/** A component-level read on the org detail page: NOT part of the loader, so it fails in place. */
const CLIENTS_READ = "**/api/v1/identity/organizations/*/clients";
/** A non-form mutation on the same page, left to the toast by the catalog. */
const ARCHIVE_MUTATION = "**/api/v1/identity/organizations/*/archive";
/** The create form's POST; the GET on the same path lists, so the handler checks the method. */
const ORGANIZATIONS_COLLECTION = "**/api/v1/identity/organizations";

/** An id no organization will ever carry, in the guid shape the route constraint accepts. */
const MISSING_ORGANIZATION_ID = "00000000-0000-0000-0000-000000000000";

const PROBLEM_MEDIA_TYPE = "application/problem+json";

/** Answer the intercepted request with a problem body, the way the API or the BFF would. */
function problem(
  route: Route,
  status: number,
  body: Record<string, unknown>,
  headers: Record<string, string> = {},
): Promise<void> {
  return route.fulfill({
    status,
    headers: { "content-type": PROBLEM_MEDIA_TYPE, ...headers },
    body: JSON.stringify({ type: "about:blank", status, ...body }),
  });
}

/**
 * Sign in, land on the organizations list, open the first seeded organization
 * and hand back its detail path. The detail route is where a component-level
 * read (the clients ledger) and a toast-owned mutation (archive) share a page,
 * so most scenarios below start from it.
 */
async function openFirstOrganization(page: Page): Promise<string> {
  await signInAndLandOn(page, ORGANIZATIONS_PAGE);
  await expect(page.getByTestId("dashboard-organizations")).toBeVisible({ timeout: 15_000 });

  await page.getByTestId("organization-item").first().click();
  await page.waitForURL((url) => url.pathname.startsWith(`${ORGANIZATIONS_PAGE}/`), {
    timeout: 15_000,
  });
  await expect(page.getByTestId("organization-detail-heading")).toBeVisible({ timeout: 15_000 });

  return new URL(page.url()).pathname;
}

test("network down: a read shows the banner with Try again, a mutation shows the toast", async ({
  page,
}) => {
  const detailPath = await openFirstOrganization(page);

  // The read: with the clients call unreachable, the ledger region is REPLACED
  // by the banner carrying the shipped network copy and a retry.
  await page.route(CLIENTS_READ, (route) => route.abort("connectionfailed"));
  await page.goto(detailPath);
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });

  const readBanner = page.getByTestId("organization-detail-clients-error");
  await expect(readBanner).toContainText(
    "Unable to reach the server. Check your connection and try again.",
    { timeout: 15_000 },
  );
  await expect(page.getByTestId("organization-detail-applications-heading")).toHaveCount(0);

  // The mutation: archive is left to the toast, so the same failure on its
  // call raises exactly one toast and no inline surface on the page.
  await page.route(ARCHIVE_MUTATION, (route) => route.abort("connectionfailed"));
  await page.getByTestId("organization-detail-archive").click();

  const toasts = page.locator("[data-sonner-toast]");
  await expect(toasts).toHaveCount(1, { timeout: 15_000 });
  await expect(toasts.first()).toContainText(
    "Unable to reach the server. Check your connection and try again.",
  );
  await expect(page.getByTestId("organization-detail-error")).toHaveCount(0);

  // Try again re-issues the read; with the network back it renders the ledgers.
  await page.unroute(CLIENTS_READ);
  await page.getByTestId("organization-detail-clients-error-retry").click();
  await expect(page.getByTestId("organization-detail-applications-heading")).toBeVisible({
    timeout: 15_000,
  });
  await expect(readBanner).toHaveCount(0);
});

test("a 429 shows how many seconds to wait, not the API's Retry-After sentence", async ({
  page,
}) => {
  const detailPath = await openFirstOrganization(page);

  // The API's rate limiter's exact answer: `RateLimit.Exceeded`, a developer
  // facing detail, and the wait in the `Retry-After` header.
  await page.route(CLIENTS_READ, (route) =>
    problem(
      route,
      TOO_MANY_REQUESTS,
      {
        title: "Too Many Requests",
        code: "RateLimit.Exceeded",
        detail:
          "Rate limit exceeded. Please retry after the duration indicated in the Retry-After header.",
      },
      { "retry-after": "30" },
    ),
  );
  await page.goto(detailPath);

  await expect(page.getByTestId("organization-detail-clients-error")).toContainText(
    "Too many requests. Please wait 30 seconds and try again.",
    { timeout: 15_000 },
  );
});

test("a validation failure shows field errors next to the input, not in the banner", async ({
  page,
}) => {
  await signInAndLandOn(page, ORGANIZATIONS_PAGE);
  await expect(page.getByTestId("dashboard-organizations")).toBeVisible({ timeout: 15_000 });

  // The client-side rule first: an empty name never leaves the browser.
  await page.getByTestId("organization-create-submit").click();
  await expect(page.getByTestId("organization-name-error")).toHaveText("Name is required");

  // Then the server's: a 400 validation problem keyed by property, the shape
  // `GlobalExceptionHandler` writes for a FluentValidation failure. The forms
  // path places each keyed message on its field and leaves the banner empty.
  const serverMessage = "Name must be 256 characters or fewer.";
  await page.route(ORGANIZATIONS_COLLECTION, (route) => {
    if (route.request().method() !== "POST") {
      return route.fallback();
    }
    return problem(route, BAD_REQUEST, {
      title: "One or more validation errors occurred.",
      code: "Validation.Failed",
      errors: { name: [serverMessage] },
    });
  });

  await page.getByTestId("organization-name").fill("E2E validation org");
  await page.getByTestId("organization-create-submit").click();

  await expect(page.getByTestId("organization-name-error")).toHaveText(serverMessage, {
    timeout: 15_000,
  });
  await expect(page.getByTestId("organization-create-error")).toHaveCount(0);
  // A form submission is the forms path's to show: never also a toast.
  await expect(page.locator("[data-sonner-toast]")).toHaveCount(0);
  // The typed name stays: nothing succeeded, nothing reset.
  await expect(page.getByTestId("organization-name")).toHaveValue("E2E validation org");
});

test("a 401 mid-session offers Sign in and comes back to the current path", async ({ page }) => {
  const detailPath = await openFirstOrganization(page);

  // What the BFF proxy answers once its session is gone.
  await page.route(CLIENTS_READ, (route) =>
    problem(route, UNAUTHORIZED, {
      title: "Not signed in",
      code: "Bff.SessionMissing",
      detail: "You are not signed in. Please sign in to continue.",
    }),
  );
  await page.goto(detailPath);

  // The banner, not a redirect: the page stays put and offers the action.
  const banner = page.getByTestId("organization-detail-clients-error");
  await expect(banner).toContainText("Your session has expired. Please sign in again.", {
    timeout: 15_000,
  });
  expect(new URL(page.url()).pathname).toBe(detailPath);

  const signIn = page.getByTestId("organization-detail-clients-error-sign-in");
  await expect(signIn).toHaveAttribute(
    "href",
    `/bff/login?returnTo=${encodeURIComponent(detailPath)}`,
  );

  // Taking the action runs the BFF login round trip and lands back HERE. The
  // issuer still holds this browser's sign-in, so the round trip is silent (the
  // upstream single-sign-on behaviour login-journey.spec.ts documents).
  await page.unroute(CLIENTS_READ);
  await signIn.click();
  await page.waitForURL((url) => url.pathname === detailPath, { timeout: 30_000 });
  await expect(page.locator("[data-app-ready='true']")).toBeAttached({ timeout: 20_000 });
  await expect(page.getByTestId("organization-detail-applications-heading")).toBeVisible({
    timeout: 15_000,
  });
  await expect(page.getByTestId("organization-detail-clients-error")).toHaveCount(0);
});

test("a loader 404 renders the not-found page", async ({ page }) => {
  await signInAndLandOn(page, ORGANIZATIONS_PAGE);
  await expect(page.getByTestId("dashboard-organizations")).toBeVisible({ timeout: 15_000 });

  // No interception: the API's real 404 for an unknown id reaches the route
  // loader, which converts it into the router's not-found path.
  const response = await page.goto(`${ORGANIZATIONS_PAGE}/${MISSING_ORGANIZATION_ID}`);

  await expect(page.getByTestId("root-not-found")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("organization-detail-heading")).toHaveCount(0);
  expect(response?.status()).toBe(NOT_FOUND);
});
