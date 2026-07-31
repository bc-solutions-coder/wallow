import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { InquiryList } from "./InquiryList";

/**
 * Behaviour spec for the inquiries list page: the rows, the per-row status, the
 * empty state and the loading state.
 *
 * Every state is driven from the transport rather than the cache — list and
 * empty via `harness.resolveJson`, loading by leaving the request never-settling
 * (`harness.pending()`).
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

    // The company line is optional: Ada has none, so it stays absent rather than empty.
    const companies = page.getByTestId("inquiry-item-company").elements();
    expect(companies).toHaveLength(1);
    expect(companies[0]?.textContent).toBe("Navy");
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
    expect(page.getByTestId("inquiries-empty-state-icon").element().textContent).toBe("🐷");
    expect(page.getByTestId("inquiries-empty-state-message").element().textContent).toBe(
      "No inquiries yet.",
    );
    expect(page.getByTestId("inquiries-empty-state-description").element().textContent).toBe(
      "Nothing has arrived here. New inquiries show up as soon as one is submitted.",
    );
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    harness.pending();

    renderWithWallow(<InquiryList />, { harness });

    await expect.element(page.getByTestId("inquiries-loading")).toBeInTheDocument();
    expect(page.getByTestId("inquiries-loading").element().textContent).toBe("Loading inquiries…");
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });
});
