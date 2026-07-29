import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { AppList } from "./AppList";

/**
 * Component spec for the Apps list page (Wallow-8w1h.5.2), mirroring
 * OrganizationList.test.tsx. Data flows through the generated
 * `appsGetUserAppsOptions()`, so the network seam is the `fetch` of the
 * request-scoped client this spec's own `createSdkHarness()` builds
 * (Wallow-pu6a.5.5 — there is no shared module-global client left to install a
 * mock onto). Every state is driven from the transport rather than the cache:
 * list and empty via `harness.resolveJson`, loading by leaving the request
 * never-settling (`harness.pending()`).
 *
 * Testids follow `{page}-{element}` kebab-case: per-row
 * `app-item` (deliberately `app-item`, not `apps-row`), empty state
 * `apps-empty-state`, loading `apps-loading`.
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
  });

  it("renders the empty state and no rows when the app list is empty", async () => {
    harness.resolveJson([]);

    renderWithWallow(<AppList />, { harness });

    await expect.element(page.getByTestId("apps-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    // No cached data -> the query fires; the request never settles, so the
    // component stays in its loading state.
    harness.pending();

    renderWithWallow(<AppList />, { harness });

    await expect.element(page.getByTestId("apps-loading")).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });
});
