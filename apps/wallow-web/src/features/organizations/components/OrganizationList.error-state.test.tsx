import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationList } from "./OrganizationList";

/**
 * The organizations list's query error surface.
 *
 * A failed BACKGROUND refetch is the half a bare `isError` check breaks: React
 * Query retains the last resolved data, so the rows stay and no banner appears
 * — the banner is for an error with NO data to fall back on. Error text comes
 * off a real RFC 7807 body over the wire, so `errorText()` runs its real path.
 */

/** An RFC 7807 body the SDK's error interceptor parses into an `ApiFailure`. */
const PROBLEM = {
  status: 500,
  code: "Server.Error",
  title: "Internal Server Error",
  detail: "Organizations are down.",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("OrganizationList — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the list query errors with nothing cached", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<OrganizationList />, { harness });

    await expect
      .element(page.getByTestId("organizations-error"))
      .toHaveTextContent("Organizations are down.");
  });

  it("does not show the empty state when the list query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<OrganizationList />, { harness });

    // Awaiting the error first is what makes the negative assertion meaningful:
    // it pins the render to the settled error state rather than the pending one.
    await expect.element(page.getByTestId("organizations-error")).toBeInTheDocument();
    expect(page.getByTestId("organizations-empty-state").elements()).toHaveLength(0);
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });

  it("keeps the loaded rows and shows no error when a background refetch fails", async () => {
    harness.resolveJson([{ id: "o1", name: "Acme", domain: null, memberCount: "3" }]);

    const { queryClient } = renderWithWallow(<OrganizationList />, { harness });
    await expect.element(page.getByTestId("organization-item").first()).toBeInTheDocument();

    harness.rejectJson(PROBLEM, 500);
    await queryClient.refetchQueries();

    await expect.element(page.getByTestId("organization-item").first()).toBeInTheDocument();
    expect(page.getByTestId("organizations-error").elements()).toHaveLength(0);
  });
});
