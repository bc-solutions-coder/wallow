import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "../../../test/invalidation";
import { organizationsGetAllQueryKey } from "../api";
import { CreateOrganizationForm } from "./CreateOrganizationForm";

/**
 * Component spec for the CANONICAL create-form (Wallow-8w1h.4.3). This is the
 * TanStack Form + mutation template Phases 4-6 copy, so it is the spec of
 * record for that shape.
 *
 * The form builds its mutation from the GENERATED `organizationsCreateMutation({
 * client })` re-exported by api.ts (Wallow-pu6a.5.5), so the network seam is the
 * SDK instance the render puts on the router context, backed by
 * `createSdkHarness()`. The create request is asserted via the recorded outgoing
 * request (`harness.last`); the success sweep is checked by running the filter
 * the mutation passed `invalidateQueries` against the real
 * `organizationsGetAllQueryKey()` (`expectSwept`); a server ProblemDetails is
 * driven with `harness.rejectJson`.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-name` (input,
 * bead-mandated), `organization-create-submit` (submit button),
 * `organization-name-error` (required-field validation message),
 * `organization-create-error` (server RFC 7807 ProblemDetails surface).
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("CreateOrganizationForm", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the name input and submit button", async () => {
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await expect.element(page.getByTestId("organization-name")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-create-submit")).toBeInTheDocument();
  });

  it("submits, POSTing { name, domain: null } to the organizations endpoint", async () => {
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
      expect(harness.last?.path).toBe("/api/v1/identity/organizations");
      expect(harness.last?.body).toEqual({ name: "Acme", domain: null });
    });
  });

  it("sweeps the organization list query after a successful create", async () => {
    const { queryClient } = renderWithWallow(<CreateOrganizationForm />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expectSwept(invalidateSpy, organizationsGetAllQueryKey());
  });

  it("resets the name field after a successful create", async () => {
    renderWithWallow(<CreateOrganizationForm />, { harness });

    const input = page.getByTestId("organization-name");
    await userEvent.type(input, "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });
    await expect.element(input).toHaveValue("");
  });

  it("blocks submit and shows a required error when the name is empty", async () => {
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect.element(page.getByTestId("organization-name-error")).toBeInTheDocument();
    expect(harness.calls).toHaveLength(0);
  });

  it("renders the ProblemDetails message when the create fails", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/409",
        title: "Conflict",
        status: "409",
        detail: "An organization with that name already exists.",
      },
      409,
    );

    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect
      .element(page.getByTestId("organization-create-error"))
      .toHaveTextContent("An organization with that name already exists.");
  });
});
