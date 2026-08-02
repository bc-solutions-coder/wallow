import {
  createSdkHarness,
  failsWith,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { chooseOption } from "@bc-solutions-coder/testing/catalog-select";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { organizationsGetMembersQueryKey } from "../api";
import { MemberRoles } from "./MemberRoles";

/**
 * Member role management: roster + roles, assign/remove mutations, and the
 * own-org gate.
 *
 * The backend's `UsersController.AssignRole`/`RemoveRole` write to the
 * caller's AMBIENT tenant regardless of `orgId`, so `organizationsGetAll` (the
 * only read the backend scopes to the caller's own memberships) decides
 * whether any mutation control renders at all — that gate, not the roster
 * itself, is what most of this file exercises.
 *
 * `organization-member-role-role-name` is the SAME testid on every row's
 * assign form, so any test that drives it via `chooseOption` uses a
 * single-member fixture.
 */

// The caller's own org, per `organizationsGetAllOptions` — "o2" (a different
// id) is the FOREIGN org the gate test views without belonging to.
const ownOrg = { id: "o1", name: "Acme", domain: null, memberCount: 2 };

const twoMembers = [
  {
    id: "u1",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "L",
    enabled: true,
    roles: ["user"],
  },
  {
    id: "u2",
    email: "bob@acme.io",
    firstName: "Bob",
    lastName: "R",
    enabled: true,
    roles: [],
  },
];

const oneMember = [twoMembers[0]];

const roleCatalog = [{ name: "user" }, { name: "admin" }];

let harness: SdkHarness;

describe("MemberRoles", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each active member with their role names", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o1/members": twoMembers,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
    });

    renderWithWallow(<MemberRoles orgId="o1" />, { harness });

    await expect.element(page.getByTestId("member-roles-table")).toBeInTheDocument();
    expect(page.getByTestId("member-role-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("ada@acme.io")).toBeInTheDocument();
    await expect.element(page.getByText("bob@acme.io")).toBeInTheDocument();
    await expect.element(page.getByText("user")).toBeInTheDocument();
  });

  it("assigns a role: POSTs to the users roles endpoint with the chosen role name", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o1/members": oneMember,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
      "POST /v1/identity/users/u1/roles": {},
    });

    renderWithWallow(<MemberRoles orgId="o1" />, { harness });

    await expect.element(page.getByTestId("member-role-item")).toBeInTheDocument();
    await chooseOption("organization-member-role-role-name", "admin");
    await userEvent.click(page.getByTestId("organization-member-role-submit"));

    await vi.waitFor(() => {
      const call = harness.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/users/u1/roles",
      );
      expect(call).toBeDefined();
      expect(call?.body).toEqual({ roleName: "admin" });
    });
  });

  it("sweeps the members query after a successful assign", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o1/members": oneMember,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
      "POST /v1/identity/users/u1/roles": {},
    });

    const { queryClient } = renderWithWallow(<MemberRoles orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await expect.element(page.getByTestId("member-role-item")).toBeInTheDocument();
    await chooseOption("organization-member-role-role-name", "admin");
    await userEvent.click(page.getByTestId("organization-member-role-submit"));

    await expectSwept(invalidateSpy, organizationsGetMembersQueryKey({ path: { id: "o1" } }));
  });

  it("removes a role: DELETEs the user's role endpoint", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o1/members": oneMember,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
      "DELETE /v1/identity/users/u1/roles/user": {},
    });

    renderWithWallow(<MemberRoles orgId="o1" />, { harness });

    const removeButtons = page.getByTestId("member-role-remove");
    await expect.element(removeButtons.first()).toBeInTheDocument();
    await userEvent.click(removeButtons.first());

    await vi.waitFor(() => {
      const call = harness.calls.find(
        (c) => c.method === "DELETE" && c.path === "/api/v1/identity/users/u1/roles/user",
      );
      expect(call).toBeDefined();
    });
  });

  it("sweeps the members query after a successful remove", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o1/members": oneMember,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
      "DELETE /v1/identity/users/u1/roles/user": {},
    });

    const { queryClient } = renderWithWallow(<MemberRoles orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.click(page.getByTestId("member-role-remove").first());

    await expectSwept(invalidateSpy, organizationsGetMembersQueryKey({ path: { id: "o1" } }));
  });

  it("renders a 422 assign failure and keeps the roster mounted", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o1/members": oneMember,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
      "POST /v1/identity/users/u1/roles": failsWith(
        {
          title: "Unprocessable Entity",
          detail: "That role does not exist.",
          extensions: { code: "Identity.RoleNotFound" },
        },
        422,
      ),
    });

    renderWithWallow(<MemberRoles orgId="o1" />, { harness });

    await expect.element(page.getByTestId("member-role-item")).toBeInTheDocument();
    await chooseOption("organization-member-role-role-name", "admin");
    await userEvent.click(page.getByTestId("organization-member-role-submit"));

    await expect
      .element(page.getByTestId("organization-member-role-error"))
      .toHaveTextContent("That role does not exist.");
    expect(page.getByTestId("member-role-item").elements()).toHaveLength(1);
  });

  it("gates every mutation control on the caller's own organizations: no controls, no request, for a foreign org", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o2/members": twoMembers,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
    });

    renderWithWallow(<MemberRoles orgId="o2" />, { harness });

    await expect.element(page.getByTestId("member-roles-foreign-org")).toBeInTheDocument();
    await expect.element(page.getByTestId("member-role-item").first()).toBeInTheDocument();
    expect(page.getByTestId("member-role-remove").elements()).toHaveLength(0);
    expect(page.getByTestId("organization-member-role-role-name").elements()).toHaveLength(0);

    const mutationCall = harness.calls.find((c) => c.method === "POST" || c.method === "DELETE");
    expect(mutationCall).toBeUndefined();
  });

  it("renders no foreign-org note and full mutation controls for the caller's own org", async () => {
    routeHarness(harness, {
      "GET /v1/identity/organizations/o1/members": oneMember,
      "GET /v1/identity/organizations": [ownOrg],
      "GET /v1/identity/roles": roleCatalog,
    });

    renderWithWallow(<MemberRoles orgId="o1" />, { harness });

    await expect.element(page.getByTestId("member-role-item")).toBeInTheDocument();
    expect(page.getByTestId("member-roles-foreign-org").elements()).toHaveLength(0);
    await expect
      .element(page.getByTestId("organization-member-role-role-name"))
      .toBeInTheDocument();
  });
});
