import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, it, vi } from "vitest";

import {
  expectPageContainer,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";
import { Route } from "./$inquiryId";

/**
 * The inquiry-detail route shell: the column width the detail page sits in.
 *
 * The page reads `Route.useParams()`, which needs a mounted router; rather than
 * stand a whole `RouterProvider` up for two class assertions, the param hook is
 * stubbed on the route object itself with `vi.spyOn`.
 */

const INQUIRY = {
  id: "i1",
  name: "Ada Lovelace",
  email: "ada@example.com",
  company: "Analytical Engines",
  projectType: "web-app",
  status: "New",
  createdAt: "2026-07-15T00:00:00Z",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  return waitForTestId("dashboard-inquiry-detail");
}

describe("routes/dashboard/inquiries/$inquiryId (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    routeHarness(harness, {
      "GET /v1/inquiries/i1": INQUIRY,
      "GET /v1/inquiries/i1/comments": [],
    });
    vi.spyOn(Route, "useParams").mockReturnValue({ inquiryId: "i1" });
  });

  it("centers the detail page in the shared dashboard container", async () => {
    const root = await renderPage();

    expectPageContainer(root);
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
