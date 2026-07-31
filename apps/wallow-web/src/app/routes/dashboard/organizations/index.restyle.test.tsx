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
 * Page chrome for the organizations index: the shared container, the header row,
 * and the order of list and create form.
 *
 * Renders with a non-empty list so the whole page — header row, populated list,
 * create form — is on screen for the token-colour scan.
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

    // The create form mounts inline below the list, so there is no CTA to put
    // beside the title — and `PageHeader` renders the slot only when it is given
    // one, rather than leaving an empty flex child in the row.
    expect(page.getByTestId("organizations-header-actions").elements()).toHaveLength(0);
  });

  it("renders the header row above the organization list", async () => {
    await renderPage();

    expectPrecedes(byTestId("organizations-header-title"), byTestId("organizations-table"));
  });

  it("keeps the create form mounted below the list", async () => {
    await renderPage();

    expectPrecedes(byTestId("organizations-table"), byTestId("organization-create-form"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
