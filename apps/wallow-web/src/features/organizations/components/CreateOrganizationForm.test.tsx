import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { CreateOrganizationForm } from "./CreateOrganizationForm";

/**
 * Component spec for the CANONICAL create-form (Wallow-8w1h.4.3). This is the
 * TanStack Form + mutation template Phases 4-6 copy, so it is the spec of
 * record for that shape.
 *
 * The form builds its mutation from `createOrganizationMutation(queryClient)`
 * (the SDK query factory re-exported by api.ts), so the network seam is the
 * shared SDK client's `fetch`, overridden per test via `installSdkClientMock`
 * (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade is no longer in the
 * path). The create request is asserted via the recorded outgoing request
 * (`sdk.last`); invalidation of `['orgs']` on success is observed by spying on
 * the live client's `invalidateQueries`; a server ProblemDetails is driven with
 * `sdk.rejectJson`.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-name` (input,
 * bead-mandated), `organization-create-submit` (submit button),
 * `organization-name-error` (required-field validation message),
 * `organization-create-error` (server RFC 7807 ProblemDetails surface).
 */

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("CreateOrganizationForm", () => {
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("renders the name input and submit button", async () => {
    renderWithClient(newClient(), <CreateOrganizationForm />);

    await expect.element(page.getByTestId("organization-name")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-create-submit")).toBeInTheDocument();
  });

  it("submits, POSTing { name, domain: null } to the organizations endpoint", async () => {
    renderWithClient(newClient(), <CreateOrganizationForm />);

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(sdk.last?.method).toBe("POST");
      expect(sdk.last?.path).toBe("/api/v1/identity/organizations");
      expect(sdk.last?.body).toEqual({ name: "Acme", domain: null });
    });
  });

  it("invalidates the ['orgs'] list query after a successful create", async () => {
    const client = newClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");

    renderWithClient(client, <CreateOrganizationForm />);

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["orgs"] });
    });
  });

  it("resets the name field after a successful create", async () => {
    renderWithClient(newClient(), <CreateOrganizationForm />);

    const input = page.getByTestId("organization-name");
    await userEvent.type(input, "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(sdk.last?.method).toBe("POST");
    });
    await expect.element(input).toHaveValue("");
  });

  it("blocks submit and shows a required error when the name is empty", async () => {
    renderWithClient(newClient(), <CreateOrganizationForm />);

    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect.element(page.getByTestId("organization-name-error")).toBeInTheDocument();
    expect(sdk.fetchMock).not.toHaveBeenCalled();
  });

  it("renders the ProblemDetails message when the create fails", async () => {
    sdk.rejectJson(
      {
        type: "https://httpstatuses.io/409",
        title: "Conflict",
        status: "409",
        detail: "An organization with that name already exists.",
      },
      409,
    );

    renderWithClient(newClient(), <CreateOrganizationForm />);

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect
      .element(page.getByTestId("organization-create-error"))
      .toHaveTextContent("An organization with that name already exists.");
  });
});
