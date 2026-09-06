import {
  createSdkHarness,
  failsWith,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { organizationsGetAllQueryKey } from "../api";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Deleting the organization from its detail page: the confirm stays dead until
 * the organization's exact name has been typed back, a refusal keeps the dialog
 * open with the API's own words, and success sweeps the Organizations tag and
 * leaves for the list — the page this screen renders no longer exists. The
 * control itself only renders for a user holding OrganizationsDelete (or a
 * global admin). Real SDK over a faked fetch; the memory router is real, so
 * the departure is asserted on `router.state.location.pathname`.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "1" };

const members = [
  { id: "u1", email: "ada@acme.io", firstName: "Ada", lastName: "L", enabled: true, roles: [] },
];

const orgAdmin = {
  id: "u1",
  email: "ada@acme.io",
  firstName: "Ada",
  lastName: "L",
  roles: ["admin"],
  permissions: ["OrganizationsDelete"],
  isGlobalAdmin: false,
};
const plainMember = { ...orgAdmin, roles: [], permissions: [] };

const NAME = "organization-detail";
const DELETE_PATH = "/api/v1/identity/organizations/o1";

let harness: SdkHarness;

function seed(answers: Record<string, unknown> = {}): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/users/me": orgAdmin,
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": members,
      "GET /v1/identity/organizations/o1/clients": [],
      ...answers,
    },
    { fallback: [] },
  );
}

async function openDeleteDialog(): Promise<void> {
  await expect.element(page.getByTestId(`${NAME}-heading`)).toHaveTextContent("Acme");
  await userEvent.click(page.getByTestId(`${NAME}-delete`));
  await expect.element(page.getByTestId(`${NAME}-delete-popup`)).toBeInTheDocument();
}

describe("OrganizationDetail deletion", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("shows no delete control to a user without the OrganizationsDelete permission", async () => {
    seed({ "GET /v1/identity/users/me": plainMember });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId(`${NAME}-heading`)).toHaveTextContent("Acme");
    expect(page.getByTestId(`${NAME}-delete`).query()).toBeNull();
  });

  it("only arms the confirm once the organization's exact name has been typed", async () => {
    seed();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openDeleteDialog();

    const confirm = page.getByTestId(`${NAME}-delete-confirm`);
    await expect.element(confirm).toBeDisabled();
    await userEvent.fill(page.getByTestId(`${NAME}-delete-input`), "acme");
    await expect.element(confirm).toBeDisabled();
    await userEvent.fill(page.getByTestId(`${NAME}-delete-input`), "Acme");
    await expect.element(confirm).toBeEnabled();
  });

  it("deletes with the typed name, sweeps Organizations and leaves for the list", async () => {
    seed({ "DELETE /v1/identity/organizations/o1": null });

    const { queryClient, router } = renderWithWallow(<OrganizationDetail orgId="o1" />, {
      harness,
    });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await openDeleteDialog();
    await userEvent.fill(page.getByTestId(`${NAME}-delete-input`), "Acme");
    await userEvent.click(page.getByTestId(`${NAME}-delete-confirm`));

    await expect.element(page.getByTestId(`${NAME}-delete-popup`)).not.toBeInTheDocument();
    await expectSwept(invalidateSpy, organizationsGetAllQueryKey());
    const call = harness.calls.find((c) => c.method === "DELETE" && c.path === DELETE_PATH);
    expect(call?.body).toEqual({ confirmName: "Acme" });
    await expect.poll(() => router.state.location.pathname).toBe("/dashboard/organizations");
  });

  it("keeps the dialog open and shows why the deletion was refused", async () => {
    seed({
      "DELETE /v1/identity/organizations/o1": failsWith(
        {
          code: "Identity.OrganizationSuspendedByPlatform",
          detail: "The organization is suspended by the platform.",
          status: 422,
        },
        422,
      ),
    });

    const { router } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openDeleteDialog();
    await userEvent.fill(page.getByTestId(`${NAME}-delete-input`), "Acme");
    await userEvent.click(page.getByTestId(`${NAME}-delete-confirm`));

    await expect
      .element(page.getByTestId(`${NAME}-delete-error`))
      .toHaveTextContent("The organization is suspended by the platform.");
    await expect.element(page.getByTestId(`${NAME}-delete-popup`)).toBeInTheDocument();
    expect(router.state.location.pathname).not.toBe("/dashboard/organizations");
  });

  it("cancelling forgets what was typed", async () => {
    seed();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openDeleteDialog();
    await userEvent.fill(page.getByTestId(`${NAME}-delete-input`), "Acme");
    await userEvent.click(page.getByTestId(`${NAME}-delete-cancel`));
    await expect.element(page.getByTestId(`${NAME}-delete-popup`)).not.toBeInTheDocument();

    await openDeleteDialog();

    await expect.element(page.getByTestId(`${NAME}-delete-input`)).toHaveValue("");
    await expect.element(page.getByTestId(`${NAME}-delete-confirm`)).toBeDisabled();
  });
});
