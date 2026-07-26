import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { InquiryList } from "./InquiryList";

/**
 * Component spec for the inquiries list page (Wallow-8w1h.7.2), mirroring
 * OrganizationList.test.tsx. Data flows through the SDK query layer
 * (`inquiriesQueries.list()`), so the network seam is the shared SDK client's
 * `fetch`, overridden per test via `installSdkClientMock` (Wallow-evd5.2.6 — the
 * retired `getWallowSdk()` facade is no longer in the path). List/empty states
 * are driven by seeding the `['inquiries']` cache with `setQueryData`
 * (`staleTime: Infinity` keeps the seed from refetching), and the loading state
 * by leaving the query to hit a never-settling request (`sdk.pending()`).
 *
 * DIVERGENCE reconciliation (see bead 7.2 note): task 7 said to "copy the C# E2E
 * InquiryPage page object's testids", but InquiryPage.cs only carries the public
 * SUBMIT-FORM testids — there is NO admin list UI or list-row testid to
 * mirror. So this list follows the Organizations `{page}-{element}` convention
 * per the bead's own acceptance: page root `dashboard-inquiries`, per-row
 * `inquiry-item`, plus `inquiry-item-status` for the acceptance's "showing status
 * per inquiry" requirement, `inquiries-empty-state`, and `inquiries-loading`.
 */

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("InquiryList", () => {
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("renders each seeded inquiry as an inquiry-item element", async () => {
    const client = newClient();
    client.setQueryData(
      ["inquiries"],
      [
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
      ],
    );

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiry-item").first()).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("Ada Lovelace")).toBeInTheDocument();
    await expect.element(page.getByText("Grace Hopper")).toBeInTheDocument();
  });

  it("shows the status for each inquiry", async () => {
    const client = newClient();
    client.setQueryData(
      ["inquiries"],
      [
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
      ],
    );

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiry-item-status").first()).toBeInTheDocument();
    const statuses = page.getByTestId("inquiry-item-status").elements();
    expect(statuses).toHaveLength(2);
    expect(statuses.map((el) => el.textContent)).toEqual(["New", "Contacted"]);
  });

  it("renders the empty state and no rows when the inquiry list is empty", async () => {
    const client = newClient();
    client.setQueryData(["inquiries"], []);

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiries-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    const client = newClient();
    // No cached data -> the query fires; the request never settles, so the
    // component stays in its loading state.
    sdk.pending();

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiries-loading")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });
});
