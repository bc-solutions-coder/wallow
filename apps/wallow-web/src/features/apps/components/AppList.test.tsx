import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { AppList } from "./AppList";

/**
 * Component spec for the Apps list page (Wallow-8w1h.5.2), mirroring
 * OrganizationList.test.tsx. Data flows through the SDK query layer
 * (`appsQueries.list()`), so the network seam is the shared SDK client's
 * `fetch`, overridden per test via `installSdkClientMock` (Wallow-evd5.2.6 — the
 * retired `getWallowSdk()` facade is no longer in the path). List/empty states
 * are driven by seeding the `['apps']` cache with `setQueryData`
 * (`staleTime: Infinity` keeps the seed from refetching), and the loading state
 * by leaving the query to hit a never-settling request (`sdk.pending()`).
 *
 * Testids follow `{page}-{element}` kebab-case: per-row
 * `app-item` (deliberately `app-item`, not `apps-row`), empty state
 * `apps-empty-state`, loading `apps-loading`.
 */

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("AppList", () => {
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("renders each seeded app as an app-item element", async () => {
    const client = newClient();
    client.setQueryData(
      ["apps"],
      [
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
      ],
    );

    renderWithClient(client, <AppList />);

    await expect.element(page.getByTestId("app-item").first()).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("Acme App")).toBeInTheDocument();
    await expect.element(page.getByText("Globex App")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when the app list is empty", async () => {
    const client = newClient();
    client.setQueryData(["apps"], []);

    renderWithClient(client, <AppList />);

    await expect.element(page.getByTestId("apps-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    const client = newClient();
    // No cached data -> the query fires; the request never settles, so the
    // component stays in its loading state.
    sdk.pending();

    renderWithClient(client, <AppList />);

    await expect.element(page.getByTestId("apps-loading")).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });
});
