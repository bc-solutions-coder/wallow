import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { CreateOrganizationForm } from "./CreateOrganizationForm";

/**
 * Component spec for the CANONICAL create-form (Wallow-8w1h.4.3). This is the
 * TanStack Form + mutation template Phases 4-6 copy, so it is the spec of
 * record for that shape.
 *
 * The `getWallowSdk()` facade is mocked so the create call is a spy; the form
 * builds its mutation from `createOrganizationMutation(queryClient)` (the api.ts
 * factory), so invalidation of `['orgs']` on success is observed by spying on
 * the live client's `invalidateQueries`.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-name` (input,
 * bead-mandated), `organization-create-submit` (submit button),
 * `organization-name-error` (required-field validation message),
 * `organization-create-error` (server RFC 7807 ProblemDetails surface).
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  create: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/organizations; `../../../lib/wallow-sdk` from this test file).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: { list: mocks.list, get: mocks.get, create: mocks.create },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("CreateOrganizationForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the name input and submit button", async () => {
    renderWithClient(newClient(), <CreateOrganizationForm />);

    await expect.element(page.getByTestId("organization-name")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-create-submit")).toBeInTheDocument();
  });

  it("submits, calling the create facade with { name, domain: null }", async () => {
    mocks.create.mockResolvedValue({ id: "new", name: "Acme", domain: null, memberCount: "0" });

    renderWithClient(newClient(), <CreateOrganizationForm />);

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(mocks.create).toHaveBeenCalledTimes(1);
    });
    expect(mocks.create).toHaveBeenCalledWith({ name: "Acme", domain: null });
  });

  it("invalidates the ['orgs'] list query after a successful create", async () => {
    const client = newClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.create.mockResolvedValue({ id: "new", name: "Acme", domain: null, memberCount: "0" });

    renderWithClient(client, <CreateOrganizationForm />);

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["orgs"] });
    });
  });

  it("resets the name field after a successful create", async () => {
    mocks.create.mockResolvedValue({ id: "new", name: "Acme", domain: null, memberCount: "0" });

    renderWithClient(newClient(), <CreateOrganizationForm />);

    const input = page.getByTestId("organization-name");
    await userEvent.type(input, "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(mocks.create).toHaveBeenCalledTimes(1);
    });
    await expect.element(input).toHaveValue("");
  });

  it("blocks submit and shows a required error when the name is empty", async () => {
    renderWithClient(newClient(), <CreateOrganizationForm />);

    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect.element(page.getByTestId("organization-name-error")).toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it("renders the ProblemDetails message when the create fails", async () => {
    mocks.create.mockRejectedValue({
      type: "https://httpstatuses.io/409",
      title: "Conflict",
      status: "409",
      detail: "An organization with that name already exists.",
    });

    renderWithClient(newClient(), <CreateOrganizationForm />);

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect
      .element(page.getByTestId("organization-create-error"))
      .toHaveTextContent("An organization with that name already exists.");
  });
});
