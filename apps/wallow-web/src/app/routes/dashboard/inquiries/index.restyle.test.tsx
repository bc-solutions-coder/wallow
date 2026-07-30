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
  waitForTestId,
} from "../../../test/style-contract";
import { Route } from "./index";

/**
 * Restyle spec for the inquiries index page (Wallow-urec.4.2), following the
 * `.4.1` apps worked example. It asserts only the page chrome the restyle adds;
 * the route's behaviour (loader, `dashboard-inquiries` root, the inline
 * `inquiry-create-form`) stays pinned by the sibling `index.test.tsx`, which the
 * restyle must not edit.
 *
 * This page is the one list page in Phase 4 with NO call-to-action link, so it
 * takes `register.tsx`'s heading treatment (a bare `h1`) rather than `.4.1`'s
 * `flex items-center justify-between` header row — a flex row holding a single
 * child would be chrome with nothing to align. Vertical rhythm between the
 * heading, the list, and the create card comes from `space-y-8` on the shell.
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

  it("centers the page body in the dashboard shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-5xl mx-auto space-y-8");
  });

  it("titles the page with an h1 reading Inquiries", async () => {
    await renderPage();

    const heading = byTestId("inquiries-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Inquiries");
    expectClasses(heading, "text-3xl font-bold text-foreground");
  });

  it("renders the heading above the inquiry list", async () => {
    await renderPage();

    expectPrecedes(byTestId("inquiries-heading"), byTestId("inquiries-table"));
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
