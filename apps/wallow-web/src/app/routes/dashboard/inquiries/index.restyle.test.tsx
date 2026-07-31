import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

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
 * Page chrome for the inquiries index: the shared container, the header row, and
 * the order of list and create form.
 *
 * This page has no call-to-action, and `PageHeader` renders no actions slot when
 * it is given none. Vertical rhythm between heading, list and create card comes
 * from `space-y-8` on the shell; the width comes from the shared `PAGE_CONTAINER`.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Render the route page and resolve its settled root element.
 *
 * The root and the heading commit on the first paint, but `InquiryList` renders
 * `inquiries-loading` until the harness answers `GET /v1/inquiries` — so gating
 * on the root alone leaves every list assertion racing that response (it loses
 * on a loaded CI runner). Gate on the list too: it is the last thing this page
 * paints, so the specs below can read the DOM synchronously.
 */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  const root = await waitForTestId("dashboard-inquiries");
  await waitForTestId("inquiries-table");
  return root;
}

describe("routes/dashboard/inquiries (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    routeHarness(harness, {
      "GET /v1/inquiries": [
        {
          id: "i1",
          name: "Ada Lovelace",
          email: "ada@example.com",
          company: null,
          projectType: "web-app",
          status: "New",
          createdAt: "2026-07-15T00:00:00Z",
        },
      ],
    });
  });

  it("centers the page body in the shared dashboard container", async () => {
    const root = await renderPage();

    expectPageContainer(root);
    // The shared width does not carry the page's own vertical rhythm.
    expectClasses(root, "space-y-8");
  });

  it("titles the page with an h1 reading Inquiries", async () => {
    await renderPage();

    const heading = byTestId("inquiries-header-title");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Inquiries");
    expectClasses(heading, "text-3xl font-bold tracking-tight text-foreground");
  });

  it("lays the heading out in the page header row", async () => {
    await renderPage();

    const header = byTestId("inquiries-header");
    expectClasses(header, "flex items-start justify-between gap-4 mb-8");
    expect(header.contains(byTestId("inquiries-header-title"))).toBe(true);
  });

  it("renders the heading above the inquiry list", async () => {
    await renderPage();

    expectPrecedes(byTestId("inquiries-header-title"), byTestId("inquiries-table"));
  });

  it("keeps the create form below the list", async () => {
    await renderPage();

    expectPrecedes(byTestId("inquiries-table"), byTestId("inquiry-create-form"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
