import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationList } from "./OrganizationList";

/**
 * Component spec for the CANONICAL list-page (Wallow-8w1h.4.2). Data flows
 * through the generated `organizationsGetAllOptions()`, so the network seam is
 * the `fetch` of the request-scoped client this spec's own `createSdkHarness()`
 * builds (Wallow-pu6a.5.5 — there is no shared module-global client left to
 * install a mock onto). Every state is driven from the transport rather than
 * the cache: list and empty via `harness.resolveJson`, loading by leaving the
 * request never-settling (`harness.pending()`).
 *
 * Runs under the browser-mode project (real Chromium via `vitest-browser-react`;
 * Wallow-xzha.3.2), so there is no jsdom pragma and no `@testing-library/*`.
 * Testids follow `{page}-{element}` kebab-case; per-row testid is
 * `organization-item` (the bead spec deliberately uses `organization-item`, not
 * `organizations-row`).
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
  });

  it("renders the empty state and no rows when the org list is empty", async () => {
    harness.resolveJson([]);

    renderWithWallow(<OrganizationList />, { harness });

    await expect.element(page.getByTestId("organizations-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    // No cached data -> the query fires; the request never settles, so the
    // component stays in its loading state.
    harness.pending();

    renderWithWallow(<OrganizationList />, { harness });

    await expect.element(page.getByTestId("organizations-loading")).toBeInTheDocument();
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });
});
