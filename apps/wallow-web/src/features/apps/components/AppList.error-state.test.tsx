import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { AppList } from "./AppList";

/**
 * The apps list's query error state.
 *
 * The list reads `data ?? []`, so a failed read must not fall through to the
 * "No apps yet." card. The error branch fires only when there is no cached data.
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
