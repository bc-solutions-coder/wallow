import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { AppList } from "./AppList";

/**
 * The apps list page: its rows, its empty state, and its loading state.
 *
 * The network seam is the `fetch` of the request-scoped client `createSdkHarness()`
 * builds, so every state is driven from the transport rather than the cache: list
 * and empty via `harness.resolveJson`, loading by leaving the request
 * never-settling (`harness.pending()`).
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("AppList", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each seeded app as an app-item element", async () => {
    harness.resolveJson([
      {
        clientId: "c1",
        displayName: "Acme App",
        clientType: "public",
        redirectUris: [],
        createdAt: null,
      },
      {
        clientId: "c2",
        displayName: "Globex App",
        clientType: "confidential",
        redirectUris: ["https://globex.io/cb"],
        createdAt: "2026-07-01T00:00:00Z",
      },
    ]);

    renderWithWallow(<AppList />, { harness });

    await expect.element(page.getByTestId("app-item").first()).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("Acme App")).toBeInTheDocument();
    await expect.element(page.getByText("Globex App")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-item-name").first()).toHaveTextContent("Acme App");
    await expect.element(page.getByTestId("app-item-type").first()).toHaveTextContent("public");
  });

  it("renders the empty state and no rows when the app list is empty", async () => {
    harness.resolveJson([]);

    renderWithWallow(<AppList />, { harness });

    const empty = page.getByTestId("apps-empty-state");
    await expect.element(empty).toBeInTheDocument();
    await expect.element(empty).toHaveTextContent("No apps yet.");
    await expect
      .element(empty)
      .toHaveTextContent(
        "Nothing has been registered here. Get started by creating your first application.",
      );
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    // No cached data -> the query fires; the request never settles, so the
    // component stays in its loading state.
    harness.pending();

    renderWithWallow(<AppList />, { harness });

    await expect.element(page.getByTestId("apps-loading")).toHaveTextContent("Loading apps…");
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });
});
