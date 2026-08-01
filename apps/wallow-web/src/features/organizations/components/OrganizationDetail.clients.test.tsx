import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept, sweeps } from "@bc-solutions-coder/testing/invalidation";
import { clientsGetByTenantQueryKey, organizationsGetMembersQueryKey } from "../api";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail bound-clients table and register-client form: both reachable
 * once the org loads, and the sweep a successful registration issues.
 *
 * Runs the real SDK over a faked fetch (`createSdkHarness`) mounted on the
 * router context. The org / members / clients reads are in flight together, so
 * `routeHarness` answers each by URL rather than in call order.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** What the register POST answers with on the success path. */
const registered = {
  id: "c1",
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  name: "Dashboard",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function seedLoadedOrg(): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
      "POST /v1/identity/clients": registered,
    },
    { fallback: [] },
  );
}

describe("OrganizationDetail bound clients + register-client", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders the bound-clients table once the org loads", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-detail-clients-table")).toBeInTheDocument();
  });

  it("renders the register-client form fields reachable from the org detail page", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-display-name"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-client-type"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-submit"))
      .toBeInTheDocument();
  });

  it("sweeps the bound-clients query after a successful registration", async () => {
    // The row a registration creates only appears if the list this section
    // reads is invalidated; without this case the sweep can be deleted and the
    // suite stays green.
    seedLoadedOrg();

    const { queryClient } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    // The controls arrive with the org read, so the form cannot be driven at
    // first paint. A non-empty display name is what gets past the zod schema.
    await expect
      .element(page.getByTestId("organization-detail-register-display-name"))
      .toBeInTheDocument();
    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    // Asserted by BEHAVIOUR, not identity: generated keys are flat
    // (`[{ _id, baseUrl, tags, ...args }]`), so `queriesForOperation(...)` hands
    // back an opaque predicate — run it against the real key instead.
    await expectSwept(invalidateSpy, clientsGetByTenantQueryKey({ path: { tenantId: "o1" } }));

    // And it is that operation the sweep names, not a blanket pass over the
    // cache: the members list on the same screen is left alone.
    const membersKey = organizationsGetMembersQueryKey({ path: { id: "o1" } });
    expect(invalidateSpy.mock.calls.some((call) => sweeps(call[0], membersKey))).toBe(false);
  });
});
