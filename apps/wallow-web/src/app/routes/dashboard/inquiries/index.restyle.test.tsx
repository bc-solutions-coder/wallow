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
 * Restyle spec for the inquiries index page (Wallow-urec.4.2), following the
 * `.4.1` apps worked example. It asserts only the page chrome the restyle adds;
 * the route's behaviour (loader, `dashboard-inquiries` root, the inline
 * `inquiry-create-form`) stays pinned by the sibling `index.test.tsx`, which the
 * restyle must not edit.
 *
 * This page has NO call-to-action link. Under Wallow-lrlm.5.1 that no longer
 * means a different heading treatment: every dashboard page opens with the same
 * `PageHeader`, which simply renders no actions slot when it is given none — so
 * "one page, one header component" costs nothing here. Vertical rhythm between
 * the heading, the list, and the create card still comes from `space-y-8` on the
 * shell; the width comes from the shared `PAGE_CONTAINER`.
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
 * paints, so once it is in the document the whole page is settled and the specs
 * below can read the DOM synchronously.
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
    // Regression guard: the width moved to the shared rule, the page's own
    // vertical rhythm between heading, list, and create card did not.
    expectClasses(root, "space-y-8");
  });

  it("titles the page with an h1 reading Inquiries", async () => {
    await renderPage();

    // Wallow-lrlm.5.1: `PageHeader`'s derived testid, `Text`'s title scale.
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

    // Regression guard: the restyle reorders nothing — list first, create second.
    expectPrecedes(byTestId("inquiries-table"), byTestId("inquiry-create-form"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
