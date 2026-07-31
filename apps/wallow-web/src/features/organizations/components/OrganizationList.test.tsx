import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationList } from "./OrganizationList";

/**
 * The organizations list page: rows, empty state and loading state.
 *
 * Runs the real SDK over a faked fetch (`createSdkHarness`) mounted on the
 * router context. Every state is driven from the transport rather than the
 * cache: list and empty via `harness.resolveJson`, loading by leaving the
 * request never-settling (`harness.pending()`).
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("OrganizationList", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each seeded org as an organization-item element", async () => {
    harness.resolveJson([
      { id: "o1", name: "Acme", domain: null, memberCount: "3" },
      { id: "o2", name: "Globex", domain: "globex.io", memberCount: "1" },
    ]);

    renderWithWallow(<OrganizationList />, { harness });

    await expect.element(page.getByTestId("organization-item").first()).toBeInTheDocument();
    expect(page.getByTestId("organization-item").elements()).toHaveLength(2);
    // Exact match: `getByText` is substring-by-default in the browser provider,
    // so "Globex" would otherwise also match the "globex.io" domain cell.
    await expect.element(page.getByText("Acme", { exact: true })).toBeInTheDocument();
    await expect.element(page.getByText("Globex", { exact: true })).toBeInTheDocument();

    const [acme, globex] = page.getByTestId("organization-item").elements();
    await expect
      .element(page.getByTestId("organization-item-members").first())
      .toHaveTextContent("3");

    // The domain cell is rendered only for an org that has one.
    expect(acme?.querySelector('[data-testid="organization-item-domain"]')).toBeNull();
    expect(globex?.querySelector('[data-testid="organization-item-domain"]')?.textContent).toBe(
      "globex.io",
    );
  });

  it("renders the empty state and no rows when the org list is empty", async () => {
    harness.resolveJson([]);

    renderWithWallow(<OrganizationList />, { harness });

    const empty = page.getByTestId("organizations-empty-state");
    await expect.element(empty).toHaveTextContent("No organizations yet.");
    await expect
      .element(empty)
      .toHaveTextContent(
        "Nothing belongs here yet. Get started by creating your first organization.",
      );
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    harness.pending();

    renderWithWallow(<OrganizationList />, { harness });

    await expect
      .element(page.getByTestId("organizations-loading"))
      .toHaveTextContent("Loading organizations…");
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });
});
