import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "../../../test/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import {
  byTestId,
  expectClasses,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
} from "../../../test/style-contract";
import { Route } from "./index";

/**
 * Restyle spec for the organizations index page (Wallow-urec.4.3), following the
 * worked example in `routes/dashboard/apps/index.restyle.test.tsx`. It asserts
 * only the page chrome the restyle adds; the route's behaviour (loader,
 * `dashboard-organizations` root, the inline `organization-create-form`) stays
 * pinned by the sibling `index.test.tsx`, which the restyle must not edit.
 *
 * Unlike the apps page, this page has NO create-page CTA to put in the header
 * row: `/dashboard/organizations/create` does not exist in the React app (the
 * create form mounts inline below the list), and Phase 4 is styling-only, so the
 * Blazor original's "Create Organization" link is deliberately NOT ported. The
 * header row therefore holds the h1 alone.
 *
 * The page renders with a seeded, non-empty `['orgs']` cache so the whole page —
 * header row, populated list, create form — is on screen for the token scan.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  return waitForTestId("dashboard-organizations");
}

describe("routes/dashboard/organizations (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    routeHarness(harness, {
      "GET /v1/identity/organizations": [
        { id: "o1", name: "Acme", domain: "acme.io", memberCount: "3" },
      ],
    });
  });

  it("centers the page body in the dashboard shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-5xl mx-auto");
  });

  it("titles the page with an h1 reading Organizations", async () => {
    await renderPage();

    const heading = byTestId("organizations-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Organizations");
    expectClasses(heading, "text-3xl font-bold text-foreground");
  });

  it("lays the heading out in the page header row", async () => {
    await renderPage();

    const headerRow = parentOf(byTestId("organizations-heading"));
    expectClasses(headerRow, "flex items-center justify-between mb-8");
  });

  it("renders the header row above the organization list", async () => {
    await renderPage();

    expectPrecedes(byTestId("organizations-heading"), byTestId("organizations-table"));
  });

  it("keeps the create form mounted below the list", async () => {
    await renderPage();

    // Regression guard: the restyle reorders nothing — the inline create form
    // (Wallow-ffpq.3.5) still trails the list on this page.
    expectPrecedes(byTestId("organizations-table"), byTestId("organization-create-form"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
