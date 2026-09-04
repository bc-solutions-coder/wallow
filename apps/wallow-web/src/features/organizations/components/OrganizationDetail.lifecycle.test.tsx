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
import { organizationClientsListQueryKey } from "../api";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * A client's lifecycle from the org page: the suspend / reinstate toggle
 * follows the row's status and re-reads the ledger, and the delete dialog
 * only arms once the client id has been typed back. Real SDK over a faked
 * fetch.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "1" };

const members = [
  { id: "u1", email: "ada@acme.io", firstName: "Ada", lastName: "L", enabled: true, roles: [] },
];

const application = {
  clientId: "app-acme-portal",
  name: "Portal",
  kind: "application",
  status: "active",
  redirectUris: ["https://portal.acme.io/callback"],
  postLogoutRedirectUris: [],
  scopes: ["openid"],
  createdByUserId: "u1",
  createdAt: "2026-08-30T00:00:00Z",
  lastRotatedByUserId: null,
  lastRotatedAt: null,
};

const suspended = { ...application, status: "suspended" };

const ROW = "organization-detail-application";
const CLIENT_PATH = "/api/v1/identity/organizations/o1/clients/app-acme-portal";

let harness: SdkHarness;

function seed(clients: readonly unknown[], answers: Record<string, unknown> = {}): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": members,
      "GET /v1/identity/organizations/o1/clients": clients,
      "GET /v1/identity/scopes": [],
      ...answers,
    },
    { fallback: [] },
  );
}

async function openDeleteDialog(): Promise<void> {
  await expect.element(page.getByTestId(`${ROW}-item`)).toBeInTheDocument();
  await userEvent.click(page.getByTestId(`${ROW}-delete`));
  await expect.element(page.getByTestId(`${ROW}-delete-popup`)).toBeInTheDocument();
}

describe("OrganizationDetail client lifecycle", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("suspends an active client and re-reads the ledger", async () => {
    seed([application], {
      "POST /v1/identity/organizations/o1/clients/app-acme-portal/suspend": suspended,
    });

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await expect.element(page.getByTestId(`${ROW}-item`)).toHaveTextContent("active");

    await userEvent.click(page.getByTestId(`${ROW}-suspend`));

    await expectSwept(invalidateSpy, organizationClientsListQueryKey({ path: { orgId: "o1" } }));
    const post = harness.calls.find(
      (call) => call.method === "POST" && call.path === `${CLIENT_PATH}/suspend`,
    );
    expect(post).toBeDefined();
  });

  it("offers Reinstate on a suspended client and posts to reinstate", async () => {
    seed([suspended], {
      "POST /v1/identity/organizations/o1/clients/app-acme-portal/reinstate": application,
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect.element(page.getByTestId(`${ROW}-item`)).toHaveTextContent("suspended");
    await expect.element(page.getByTestId(`${ROW}-suspend`)).not.toBeInTheDocument();

    await userEvent.click(page.getByTestId(`${ROW}-reinstate`));

    await expect
      .poll(() =>
        harness.calls.some(
          (call) => call.method === "POST" && call.path === `${CLIENT_PATH}/reinstate`,
        ),
      )
      .toBe(true);
  });

  it("shows why a suspension was refused", async () => {
    seed([application], {
      "POST /v1/identity/organizations/o1/clients/app-acme-portal/suspend": failsWith(
        {
          code: "Identity.ClientAlreadySuspended",
          detail: "The client is already suspended.",
          status: 422,
        },
        422,
      ),
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await expect.element(page.getByTestId(`${ROW}-item`)).toBeInTheDocument();
    await userEvent.click(page.getByTestId(`${ROW}-suspend`));

    await expect
      .element(page.getByTestId(`${ROW}-lifecycle-error`))
      .toHaveTextContent("The client is already suspended.");
  });

  it("only arms the delete once the client id has been typed back", async () => {
    seed([application], {
      "DELETE /v1/identity/organizations/o1/clients/app-acme-portal": null,
    });

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    await openDeleteDialog();

    const confirm = page.getByTestId(`${ROW}-delete-confirm`);
    await expect.element(confirm).toBeDisabled();
    await userEvent.fill(page.getByTestId(`${ROW}-delete-input`), "app-acme");
    await expect.element(confirm).toBeDisabled();
    await userEvent.fill(page.getByTestId(`${ROW}-delete-input`), "app-acme-portal");
    await expect.element(confirm).toBeEnabled();

    await userEvent.click(confirm);

    await expect.element(page.getByTestId(`${ROW}-delete-popup`)).not.toBeInTheDocument();
    await expectSwept(invalidateSpy, organizationClientsListQueryKey({ path: { orgId: "o1" } }));
    const call = harness.calls.find((c) => c.method === "DELETE" && c.path === CLIENT_PATH);
    expect(call).toBeDefined();
  });

  it("keeps the delete dialog open and shows the error when deletion fails", async () => {
    seed([application], {
      "DELETE /v1/identity/organizations/o1/clients/app-acme-portal": failsWith(
        {
          code: "Identity.ClientDeleteRefused",
          detail: "Deletion is not allowed right now.",
          status: 409,
        },
        409,
      ),
    });

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openDeleteDialog();
    await userEvent.fill(page.getByTestId(`${ROW}-delete-input`), "app-acme-portal");
    await userEvent.click(page.getByTestId(`${ROW}-delete-confirm`));

    await expect
      .element(page.getByTestId(`${ROW}-delete-error`))
      .toHaveTextContent("Deletion is not allowed right now.");
    await expect.element(page.getByTestId(`${ROW}-delete-popup`)).toBeInTheDocument();
  });

  it("cancelling the delete forgets what was typed", async () => {
    seed([application]);

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openDeleteDialog();
    await userEvent.fill(page.getByTestId(`${ROW}-delete-input`), "app-acme-portal");
    await userEvent.click(page.getByTestId(`${ROW}-delete-cancel`));
    await expect.element(page.getByTestId(`${ROW}-delete-popup`)).not.toBeInTheDocument();

    await openDeleteDialog();

    await expect.element(page.getByTestId(`${ROW}-delete-input`)).toHaveValue("");
    await expect.element(page.getByTestId(`${ROW}-delete-confirm`)).toBeDisabled();
  });
});
