/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OrganizationList } from "./OrganizationList";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly. This is the DOM-matcher convention for wallow-web's RTL
// tests (Phases 4-6 copy it).
expect.extend(matchers);

/**
 * Component spec for the CANONICAL list-page (Wallow-8w1h.4.2). The
 * `getWallowSdk()` facade is mocked so the list query is inert; list/empty
 * states are driven by seeding the `['orgs']` cache with `setQueryData`, and the
 * loading state by leaving the query to hit a never-resolving facade call.
 *
 * These are the FIRST React Testing Library tests in wallow-web, so this file
 * establishes the jsdom pragma + `@testing-library/jest-dom` import convention
 * (no global setupFiles is wired). Testids follow `{page}-{element}` kebab-case;
 * per-row testid is `organization-item` (the bead spec overrides the Blazor
 * oracle's `organizations-row`).
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  create: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/organizations; `../../../lib/wallow-sdk` from here).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: { list: mocks.list, get: mocks.get, create: mocks.create },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("OrganizationList", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders each seeded org as an organization-item element", async () => {
    const client = newClient();
    client.setQueryData(
      ["orgs"],
      [
        { id: "o1", name: "Acme", domain: null, memberCount: "3" },
        { id: "o2", name: "Globex", domain: "globex.io", memberCount: "1" },
      ],
    );

    renderWithClient(client, <OrganizationList />);

    const items = await screen.findAllByTestId("organization-item");
    expect(items).toHaveLength(2);
    expect(screen.getByText("Acme")).toBeInTheDocument();
    expect(screen.getByText("Globex")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when the org list is empty", async () => {
    const client = newClient();
    client.setQueryData(["orgs"], []);

    renderWithClient(client, <OrganizationList />);

    expect(await screen.findByTestId("organizations-empty-state")).toBeInTheDocument();
    expect(screen.queryAllByTestId("organization-item")).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", () => {
    const client = newClient();
    // No cached data -> the query fires; the facade never resolves, so the
    // component stays in its loading state.
    mocks.list.mockReturnValue(new Promise<never>(() => {}));

    renderWithClient(client, <OrganizationList />);

    expect(screen.getByTestId("organizations-loading")).toBeInTheDocument();
    expect(screen.queryAllByTestId("organization-item")).toHaveLength(0);
  });
});
