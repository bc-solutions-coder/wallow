import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import {
  byTestId,
  expectClasses,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
} from "@shared/testing/style-contract";
import { Route } from "./index";

/**
 * Restyle spec for the apps index page (Wallow-urec.4.1) — the WORKED EXAMPLE
 * for Phase 4. It asserts only the page chrome the restyle adds; the route's
 * behaviour (loader, `dashboard-apps` root, `apps-register-link` href) stays
 * pinned by the sibling `index.test.tsx`, which the restyle must not edit.
 *
 * The page renders with a seeded, non-empty `['apps']` cache so the whole page —
 * header row plus populated list — is on screen for the token-color scan.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Render the route page and resolve its settled root element.
 *
 * Gated on the list as well as the root: the root paints immediately, but the
 * list only replaces its loading state once the harness answers, and reading
 * `apps-table` synchronously after the root would race that response.
 */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  const root = await waitForTestId("dashboard-apps");
  await waitForTestId("apps-table");
  return root;
}

describe("routes/dashboard/apps (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    routeHarness(harness, {
      "GET /v1/identity/apps": [
        {
          clientId: "c1",
          displayName: "Acme App",
          clientType: "public",
          redirectUris: [],
          createdAt: null,
        },
      ],
    });
  });

  it("centers the page body in the dashboard shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-5xl mx-auto");
  });

  it("titles the page with an h1 reading My Apps", async () => {
    await renderPage();

    const heading = byTestId("apps-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("My Apps");
    expectClasses(heading, "text-3xl font-bold text-foreground");
  });

  it("lays the heading and the register CTA out in one header row", async () => {
    await renderPage();

    const headerRow = parentOf(byTestId("apps-heading"));
    expectClasses(headerRow, "flex items-center justify-between mb-8");
    expect(headerRow.contains(byTestId("apps-register-link"))).toBe(true);
  });

  // Wallow-lrlm.4.3 widened this case. The pill's own utilities — token colours,
  // weight, padding, radius, type scale — are UNCHANGED and still pinned here;
  // what moved is the interaction treatment, which now comes from the catalog
  // `buttonRecipe` (F3.T1) instead of being hand-written: `hover:opacity-90`
  // became the recipe's `hover:bg-primary/90`, `transition-colors` became
  // `motion-safe:transition-colors`, and a focus-visible ring the hand-rolled
  // pill never had arrived with it. The behavioural half — anchor, href, router
  // click, no `role="button"` — lives in the sibling `index.navigation.test.tsx`.
  it("styles the register link as the gold pill CTA", async () => {
    await renderPage();

    const link = byTestId("apps-register-link");
    expectClasses(
      link,
      "bg-primary text-primary-foreground font-medium px-6 py-2.5 rounded-full no-underline text-sm hover:bg-primary/90 motion-safe:transition-colors focus-visible:ring-2 focus-visible:ring-ring",
    );
    // Regression guard: the pill is still the same link, with the same words.
    expect(link.getAttribute("href")).toBe("/dashboard/apps/register");
    expect(link.textContent?.trim()).toBe("Register New App");
  });

  it("renders the header row above the app list", async () => {
    await renderPage();

    expectPrecedes(byTestId("apps-heading"), byTestId("apps-table"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
