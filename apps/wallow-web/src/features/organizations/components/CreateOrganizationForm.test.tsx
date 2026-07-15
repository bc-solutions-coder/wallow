/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { CreateOrganizationForm } from "./CreateOrganizationForm";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the DOM-matcher convention for wallow-web's RTL tests
// (established by OrganizationList.test.tsx; Phases 4-6 copy it).
expect.extend(matchers);

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

  it("renders the name input and submit button", () => {
    renderWithClient(newClient(), <CreateOrganizationForm />);

    expect(screen.getByTestId("organization-name")).toBeInTheDocument();
    expect(screen.getByTestId("organization-create-submit")).toBeInTheDocument();
  });

  it("submits, calling the create facade with { name, domain: null }", async () => {
    const user = userEvent.setup();
    mocks.create.mockResolvedValue({ id: "new", name: "Acme", domain: null, memberCount: "0" });

    renderWithClient(newClient(), <CreateOrganizationForm />);

    await user.type(screen.getByTestId("organization-name"), "Acme");
    await user.click(screen.getByTestId("organization-create-submit"));

    await waitFor(() => {
      expect(mocks.create).toHaveBeenCalledTimes(1);
    });
    expect(mocks.create).toHaveBeenCalledWith({ name: "Acme", domain: null });
  });

  it("invalidates the ['orgs'] list query after a successful create", async () => {
    const user = userEvent.setup();
    const client = newClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.create.mockResolvedValue({ id: "new", name: "Acme", domain: null, memberCount: "0" });

    renderWithClient(client, <CreateOrganizationForm />);

    await user.type(screen.getByTestId("organization-name"), "Acme");
    await user.click(screen.getByTestId("organization-create-submit"));

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["orgs"] });
    });
  });

  it("resets the name field after a successful create", async () => {
    const user = userEvent.setup();
    mocks.create.mockResolvedValue({ id: "new", name: "Acme", domain: null, memberCount: "0" });

    renderWithClient(newClient(), <CreateOrganizationForm />);

    const input = screen.getByTestId("organization-name") as HTMLInputElement;
    await user.type(input, "Acme");
    await user.click(screen.getByTestId("organization-create-submit"));

    await waitFor(() => {
      expect(mocks.create).toHaveBeenCalledTimes(1);
    });
    await waitFor(() => {
      expect(input.value).toBe("");
    });
  });

  it("blocks submit and shows a required error when the name is empty", async () => {
    const user = userEvent.setup();

    renderWithClient(newClient(), <CreateOrganizationForm />);

    await user.click(screen.getByTestId("organization-create-submit"));

    expect(await screen.findByTestId("organization-name-error")).toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it("renders the ProblemDetails message when the create fails", async () => {
    const user = userEvent.setup();
    mocks.create.mockRejectedValue({
      type: "https://httpstatuses.io/409",
      title: "Conflict",
      status: "409",
      detail: "An organization with that name already exists.",
    });

    renderWithClient(newClient(), <CreateOrganizationForm />);

    await user.type(screen.getByTestId("organization-name"), "Acme");
    await user.click(screen.getByTestId("organization-create-submit"));

    const error = await screen.findByTestId("organization-create-error");
    expect(error).toHaveTextContent("An organization with that name already exists.");
  });
});
