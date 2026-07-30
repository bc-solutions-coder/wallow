import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { AppList } from "./AppList";

/**
 * Query error-state spec for the apps list (Wallow-lrlm.4.2). `AppList` copies
 * the canonical `OrganizationList` shape, including its gap: `data ?? []` turns
 * a failed `appsGetUserAppsOptions()` read into the "No apps yet." card. Same
 * fix, same shape as `OrganizationList.error-state.test.tsx` — the error branch
 * fires only when there is no cached data, and its sentence comes from
 * `errorText()`.
 */

/** An RFC 7807 body the SDK's error interceptor brands as a `WallowError`. */
const PROBLEM = { status: 500, title: "Internal Server Error", detail: "Apps are unavailable." };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("AppList — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the apps query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<AppList />, { harness });

    await expect.element(page.getByTestId("apps-error")).toHaveTextContent("Apps are unavailable.");
  });

  it("does not show the empty state when the apps query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<AppList />, { harness });

    await expect.element(page.getByTestId("apps-error")).toBeInTheDocument();
    expect(page.getByTestId("apps-empty-state").elements()).toHaveLength(0);
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });
});
