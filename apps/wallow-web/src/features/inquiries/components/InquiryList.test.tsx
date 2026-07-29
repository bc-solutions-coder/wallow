import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { InquiryList } from "./InquiryList";

/**
 * Component spec for the inquiries list page (Wallow-8w1h.7.2), mirroring
 * OrganizationList.test.tsx. Data flows through the generated
 * `inquiriesGetAllOptions()`, so the network seam is the `fetch` of the
 * request-scoped client this spec's own `createSdkHarness()` builds
 * (Wallow-pu6a.5.5 — there is no shared module-global client left to install a
 * mock onto). Every state is driven from the transport rather than the cache:
 * list and empty via `harness.resolveJson`, loading by leaving the request
 * never-settling (`harness.pending()`).
 *
 * DIVERGENCE reconciliation (see bead 7.2 note): task 7 said to "copy the C# E2E
 * InquiryPage page object's testids", but InquiryPage.cs only carries the public
 * SUBMIT-FORM testids — there is NO admin list UI or list-row testid to
 * mirror. So this list follows the Organizations `{page}-{element}` convention
 * per the bead's own acceptance: page root `dashboard-inquiries`, per-row
 * `inquiry-item`, plus `inquiry-item-status` for the acceptance's "showing status
 * per inquiry" requirement, `inquiries-empty-state`, and `inquiries-loading`.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("InquiryList", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each seeded inquiry as an inquiry-item element", async () => {
    harness.resolveJson([
      {
        id: "i1",
        name: "Ada Lovelace",
        email: "ada@example.com",
        company: null,
        projectType: "web-app",
        status: "New",
        createdAt: "2026-07-15T00:00:00Z",
      },
      {
        id: "i2",
        name: "Grace Hopper",
        email: "grace@example.com",
        company: "Navy",
        projectType: "consulting",
        status: "Contacted",
        createdAt: "2026-07-14T00:00:00Z",
      },
    ]);

    renderWithWallow(<InquiryList />, { harness });

    await expect.element(page.getByTestId("inquiry-item").first()).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("Ada Lovelace")).toBeInTheDocument();
    await expect.element(page.getByText("Grace Hopper")).toBeInTheDocument();
  });

  it("shows the status for each inquiry", async () => {
    harness.resolveJson([
      {
        id: "i1",
        name: "Ada Lovelace",
        email: "ada@example.com",
        company: null,
        projectType: "web-app",
        status: "New",
        createdAt: "2026-07-15T00:00:00Z",
      },
      {
        id: "i2",
        name: "Grace Hopper",
        email: "grace@example.com",
        company: "Navy",
        projectType: "consulting",
        status: "Contacted",
        createdAt: "2026-07-14T00:00:00Z",
      },
    ]);

    renderWithWallow(<InquiryList />, { harness });

    await expect.element(page.getByTestId("inquiry-item-status").first()).toBeInTheDocument();
    const statuses = page.getByTestId("inquiry-item-status").elements();
    expect(statuses).toHaveLength(2);
    expect(statuses.map((el) => el.textContent)).toEqual(["New", "Contacted"]);
  });

  it("renders the empty state and no rows when the inquiry list is empty", async () => {
    harness.resolveJson([]);

    renderWithWallow(<InquiryList />, { harness });

    await expect.element(page.getByTestId("inquiries-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    // No cached data -> the query fires; the request never settles, so the
    // component stays in its loading state.
    harness.pending();

    renderWithWallow(<InquiryList />, { harness });

    await expect.element(page.getByTestId("inquiries-loading")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });
});
