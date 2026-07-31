import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import { page } from "vitest/browser";

import {
  byTestId,
  expectClasses,
  expectPageContainer,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";
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

/**
 * Render the route page and resolve its settled root element.
 *
 * Gated on the list as well as the root: the root paints immediately, but the
 * list only replaces its loading state once the harness answers, and reading
 * `organizations-table` synchronously after the root would race that response.
 */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  const root = await waitForTestId("dashboard-organizations");
  await waitForTestId("organizations-table");
  return root;
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

  it("centers the page body in the shared dashboard container", async () => {
    const root = await renderPage();

    expectPageContainer(root);
  });

  it("titles the page with an h1 reading Organizations", async () => {
    await renderPage();

    // Wallow-lrlm.5.1: `PageHeader`'s derived testid, `Text`'s title scale.
    const heading = byTestId("organizations-header-title");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Organizations");
    expectClasses(heading, "text-3xl font-bold tracking-tight text-foreground");
  });

  it("lays the heading out in the page header row", async () => {
    await renderPage();

    const header = byTestId("organizations-header");
    expectClasses(header, "flex items-start justify-between gap-4 mb-8");
    expect(header.contains(byTestId("organizations-header-title"))).toBe(true);
  });

  it("renders no actions slot on a page with no page-level action", async () => {
    await renderPage();

    // This page's create form mounts inline below the list, so there is no CTA
    // to put beside the title — and `PageHeader` renders the slot only when it
    // is given one, rather than leaving an empty flex child in the row.
    expect(page.getByTestId("organizations-header-actions").elements()).toHaveLength(0);
  });

  it("renders the header row above the organization list", async () => {
    await renderPage();

    expectPrecedes(byTestId("organizations-header-title"), byTestId("organizations-table"));
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
